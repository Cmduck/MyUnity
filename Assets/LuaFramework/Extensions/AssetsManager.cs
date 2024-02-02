using Extension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Native
{
    using DownloadUnits = System.Collections.Generic.Dictionary<string, DownloadUnit>;
    using Asset = ManifestAsset;

    public class AssetsManager
    {
        //! Update states
        public enum State
        {
            UNINITED,
            UNCHECKED,
            PREDOWNLOAD_VERSION,
            DOWNLOADING_VERSION,
            VERSION_LOADED,
            PREDOWNLOAD_MANIFEST,
            DOWNLOADING_MANIFEST,
            MANIFEST_LOADED,
            NEED_UPDATE,
            READY_TO_UPDATE,
            UPDATING,
            UNZIPPING,
            UP_TO_DATE,
            FAIL_TO_UPDATE
        }

        struct AsyncData
        {
            public string customId;
            public string zipFile;
            public bool succeed;
        }

        //! Whether user have requested to update
        enum UpdateEntry : byte
        {
            NONE,
            CHECK_UPDATE,
            DO_UPDATE
        };

        public static readonly string VERSION_FILENAME = "version.manifest";
        public static readonly string TEMP_MANIFEST_FILENAME = "project.manifest.temp";
        public static readonly string TEMP_PACKAGE_SUFFIX = "_temp";
        public static readonly string MANIFEST_FILENAME = "project.manifest";
        public static readonly float SAVE_POINT_INTERVAL = 0.1F;

        public static readonly string VERSION_ID = "@version";
        public static readonly string MANIFEST_ID = "@manifest";

        //public delegate int VersionCompareHandle(in string versionA, in string versionB);
        public delegate bool VerifyCallback(in string path, ManifestAsset asset);
        public delegate void EventCallback(in EventAssetsManager e);


        //! The event of the current AssetsManager in event dispatcher
        private string _eventName;

        //! State of update
        private State _updateState = State.UNINITED;

        //Downloader
        private Downloader _downloader;

        private Dictionary<string, Asset> _assets;

        //! The path to store successfully downloaded version.
        private string _storagePath;

        //! The path to store downloading version.
        private string _tempStoragePath;

        //! The local path of cached temporary version file
        private string _tempVersionPath;

        //! The local path of cached manifest file
        private string _cacheManifestPath;

        //! The local path of cached temporary manifest file
        private string _tempManifestPath;

        //! The path of local manifest file
        private string _manifestUrl;

        //! Local manifest
        private Manifest _localManifest = null;

        //! Local temporary manifest for download resuming
        private Manifest _tempManifest = null;

        //! Remote manifest
        private Manifest _remoteManifest = null;

        UpdateEntry _updateEntry = UpdateEntry.NONE;

        //! All assets unit to download
        DownloadUnits _downloadUnits;

        //! All failed units
        DownloadUnits _failedUnits;

        //! Download queue
        List<string> _queue;

        bool _downloadResumed = false;

        //! Max concurrent task count for downloading
        int _maxConcurrentTask = 32;

        //! Current concurrent task count
        int _currConcurrentTask = 0;

        //! Download percent
        float _percent = 0F;

        //! Download percent by file
        float _percentByFile = 0F;

        //! Indicate whether the total size should be enabled
        bool _totalEnabled = false;

        //! Indicate the number of file whose total size have been collected
        int _sizeCollected = 0;

        //! Total file size need to be downloaded (sum of all files)
        double _totalSize = 0F;

        //! Total downloaded file size (sum of all downloaded files)
        double _totalDownloaded = 0F;

        //! Downloaded size for each file
        Dictionary<string, double> _downloadedSize;

        //! Total number of assets to download
        int _totalToDownload = 0;
        //! Total number of assets still waiting to be downloaded
        int _totalWaitToDownload = 0;
        //! Next target percent for saving the manifest file
        float _nextSavePoint = 0F;

        //! Handle function to compare versions between different manifests
        OnVersionCompare _versionCompareHandle = null;

        //! Callback function to verify the downloaded assets
        VerifyCallback _verifyCallback = null;

        //! Callback function to dispatch events
        EventCallback _eventCallback = null;

        //! Marker for whether the assets manager is inited
        bool _inited = false;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="manifestUrl">The url for the local manifest file</param>
        /// <param name="storagePath">The storage path for downloaded assets</param>
        /// @warning   The cached manifest in your storage path have higher priority and will be searched first,
        /// only if it doesn't exist, AssetsManager will use the given manifestUrl.
        public AssetsManager(in string manifestUrl, in string storagePath)
        {
            init(manifestUrl, storagePath);
        }

        public AssetsManager(in string manifestUrl, in string storagePath, OnVersionCompare handle)
        {
            _versionCompareHandle = handle;
            init(manifestUrl, storagePath);
        }

        ~AssetsManager()
        {
            _downloader.onTaskError = null;
            _downloader.onFileTaskSuccess = null;
            _downloader.onTaskProgress = null;

            _localManifest = null;
            // _tempManifest could share a ptr with _remoteManifest or _localManifest
            if (_tempManifest != _localManifest && _tempManifest != _remoteManifest)
            {
                _tempManifest = null;
            }

            _remoteManifest = null;
        }

        /// <summary>
        /// Check out if there is a new version of manifest.You may use this method before updating, 
        /// then let user determine whether he wants to update resources.
        /// </summary>
        public void checkUpdate()
        {
            if (_updateEntry != UpdateEntry.NONE)
            {
                Debug.LogError("AssetsManager::checkUpdate, updateEntry isn't NONE");
                return;
            }

            if (!_inited)
            {
                Debug.LogError("AssetsManager : Manifests uninited.");
                dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_NO_LOCAL_MANIFEST);
            }

            if (!_localManifest.isLoaded())
            {
                Debug.LogError("AssetsManager : No local manifest file found error.");
                dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_NO_LOCAL_MANIFEST);
                return;
            }

            _updateEntry = UpdateEntry.CHECK_UPDATE;

            switch (_updateState)
            {
                case State.FAIL_TO_UPDATE:
                    {
                        _updateState = State.UNCHECKED;
                        downloadVersion();
                    }
                    break;
                case State.UNCHECKED:
                case State.PREDOWNLOAD_VERSION:
                    {
                        downloadVersion();
                    }
                    break;
                case State.UP_TO_DATE:
                    {
                        dispatchUpdateEvent(EventAssetsManager.EventCode.ALREADY_UP_TO_DATE);
                    }
                    break;
                case State.NEED_UPDATE:
                    {
                        dispatchUpdateEvent(EventAssetsManager.EventCode.NEW_VERSION_FOUND);
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Prepare the update process, this will cleanup download process flags, fill up download units with temporary manifest or remote manifest
        /// </summary>
        public void prepareUpdate()
        {
            if (_updateState != State.NEED_UPDATE)
            {
                return;
            }

            // Clean up before update
            _failedUnits.Clear();
            _downloadUnits.Clear();
            _totalWaitToDownload = _totalToDownload = 0;
            _nextSavePoint = 0;
            _percent = _percentByFile = 0F;
            _sizeCollected = 0;
            _totalDownloaded = _totalSize = 0.0;
            _downloadResumed = false;
            _downloadedSize.Clear();
            _totalEnabled = false;

            string localPackageUrl = _localManifest.getPackageUrl();

            // Temporary manifest exists, previously updating and equals to the remote version, resuming previous download
            if (_tempManifest != null && _tempManifest.isLoaded() && _tempManifest.isUpdating() && _tempManifest.versionEquals(_remoteManifest))
            {
                _tempManifest.setPackageUrl(localPackageUrl);
                _tempManifest.saveToFile(_tempManifestPath);
                _tempManifest.genResumeAssetsList(_downloadUnits);
                _totalWaitToDownload = _totalToDownload = _downloadUnits.Count;
                _downloadResumed = true;

                // Collect total size
                foreach(var iter in _downloadUnits)
                {
                    if (iter.Value.size > 0)
                    {
                        _totalSize += iter.Value.size;
                    }
                }
            }
            else
            {
                // Temporary manifest exists, but can't be parsed or version doesn't equals remote manifest (out of date)
                if (_tempManifest != null)
                {
                    // Remove all temp files
                    Directory.Delete(_tempStoragePath, true);
                    _tempManifest = null;
                    _remoteManifest.setPackageUrl(localPackageUrl);
                    // Recreate temp storage path and save remote manifest
                    _remoteManifest.saveToFile(_tempManifestPath);
                }

                // Temporary manifest will be used to register the download states of each asset,
                // in this case, it equals remote manifest.
                _tempManifest = _remoteManifest;

                // Check difference between local manifest and remote manifest
                Dictionary<string, Manifest.AssetDiff > diffMap = _localManifest.genDiff(_remoteManifest);
                if (diffMap.Count == 0)
                {
                    updateSucceed();
                    return;
                } 
                
                // Generate download units for all assets that need to be updated or added
                string packageUrl = _remoteManifest.getPackageUrl();
                // Preprocessing local files in previous version and creating download folders
                foreach(var it in diffMap)
                {
                    Manifest.AssetDiff diff = it.Value;
                    if (diff.type != Manifest.DiffType.DELETED)
                    {
                        string path = diff.asset.path;
                        DownloadUnit unit;
                        unit.customId = it.Key;
                        unit.srcUrl = packageUrl + path + "?md5=" + diff.asset.md5;
                        unit.storagePath = _tempStoragePath + path;
                        unit.size = diff.asset.size;
                        _downloadUnits.Add(unit.customId, unit);
                        _tempManifest.setAssetDownloadState(it.Key, Manifest.DownloadState.UNSTARTED);
                        _totalSize += unit.size;
                    }
                }
                // Start updating the temp manifest
                _tempManifest.setUpdating(true);
                // Save current download manifest information for resuming
                _tempManifest.saveToFile(_tempManifestPath);

                _totalWaitToDownload = _totalToDownload = _downloadUnits.Count;
            }
            _updateState = State.READY_TO_UPDATE;
        }

        /// <summary>
        /// Update with the current local manifest.
        /// </summary>
        public void update()
        {
            if (_updateEntry != UpdateEntry.NONE)
            {
                Debug.LogError("AssetsManager::checkUpdate, updateEntry isn't NONE");
                return;
            }

            if (!_inited)
            {
                Debug.LogError("AssetsManager : Manifests uninited.");
                dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_NO_LOCAL_MANIFEST);
            }

            if (!_localManifest.isLoaded())
            {
                Debug.LogError("AssetsManager : No local manifest file found error.");
                dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_NO_LOCAL_MANIFEST);
                return;
            }

            _updateEntry = UpdateEntry.DO_UPDATE;

            switch (_updateState)
            {
                case State.UNCHECKED:
                    {
                        _updateState = State.PREDOWNLOAD_VERSION;
                        downloadVersion();
                    }
                    break;
                case State.PREDOWNLOAD_VERSION:
                    {
                        downloadVersion();
                    }
                    break;
                case State.VERSION_LOADED:
                    {
                        parseVersion();
                    }
                    break;
                case State.PREDOWNLOAD_MANIFEST:
                    {
                        downloadManifest();
                    }
                    break;
                case State.MANIFEST_LOADED:
                    {
                        parseManifest();
                    }
                    break;
                case State.FAIL_TO_UPDATE:
                case State.READY_TO_UPDATE:
                case State.NEED_UPDATE:
                    {
                        // Manifest not loaded yet
                        if (!_remoteManifest.isLoaded())
                        {
                            _updateState = State.PREDOWNLOAD_MANIFEST;
                            downloadManifest();
                        }
                        else if (_updateEntry == UpdateEntry.DO_UPDATE)
                        {
                            startUpdate();
                        }
                    }
                    break;
                case State.UP_TO_DATE:
                case State.UPDATING:
                case State.UNZIPPING:
                    {
                        _updateEntry = UpdateEntry.NONE;
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Reupdate all failed assets under the current AssetsManager context
        /// </summary>
        public void downloadFailedAssets()
        {
            Debug.Log($"AssetsManagerEx : Start update {_failedUnits.Count} failed assets.");
            updateAssets(_failedUnits);
        }

        /// <summary>
        /// Gets the current update state.
        /// </summary>
        /// <returns></returns>
        public State getState()
        {
            return _updateState;
        }

        /// <summary>
        /// Gets storage path.
        /// </summary>
        /// <returns></returns>
        public ref readonly string getStoragePath()
        {
            return ref _storagePath;
        }

        /// <summary>
        /// Function for retrieving the local manifest object
        /// </summary>
        /// <returns></returns>
        public ref readonly Manifest getLocalManifest()
        {
            return ref _localManifest;
        }

        /// <summary>
        /// Load a local manifest from url.
        /// You can only manually load local manifest when the update state is UNCHECKED, it will fail once the update process is began.
        /// This API will do the following things:
        /// 1. Reset storage path
        /// 2. Set local storage
        /// 3. Search for cached manifest and compare with the local manifest
        /// 4. Init temporary manifest and remote manifest
        /// If successfully load the given local manifest and inited other manifests, it will return true, otherwise it will return false
        /// </summary>
        /// <param name="manifestUrl">The local manifest url</param>
        /// <returns></returns>
        public bool loadLocalManifest(in string manifestUrl)
        {
            if (string.IsNullOrEmpty(manifestUrl))
            {
                return false;
            }

            if (_updateState > State.UNINITED)
            {
                return false;
            }
            _manifestUrl = manifestUrl;
            // Init and load local manifest
            _localManifest = new Manifest();
            if (_localManifest == null)
            {
                return false;
            }

            Manifest cachedManifest = null;
            // Find the cached manifest file
            if (File.Exists(_cacheManifestPath))
            {
                cachedManifest = new Manifest();
                if (cachedManifest != null)
                {
                    cachedManifest.parseFile(_cacheManifestPath);
                    if (!cachedManifest.isLoaded())
                    {
                        File.Delete(_cacheManifestPath);
                        cachedManifest = null;
                    }
                }
            }

            // Ensure no search path of cached manifest is used to load this manifest
            //List<string> searchPaths = _fileUtils->getSearchPaths();
            //if (cachedManifest)
            //{
            //    std::vector < std::string> cacheSearchPaths = cachedManifest->getSearchPaths();
            //    std::vector < std::string> trimmedPaths = searchPaths;
            //    for (const auto &path : cacheSearchPaths) {
            //        const auto pos = std::find(trimmedPaths.begin(), trimmedPaths.end(), path);
            //        if (pos != trimmedPaths.end())
            //        {
            //            trimmedPaths.erase(pos);
            //        }
            //    }
            //    _fileUtils->setSearchPaths(trimmedPaths);
            //}

            // Load local manifest in app package
            _localManifest.parseFile(_manifestUrl);

            return true;
        }

        /// <summary>
        /// Load a local manifest from url.
        /// </summary>
        /// <param name="manifestUrl">The local manifest object to be set</param>
        /// <param name="storagePath">The local storage path</param>
        /// <returns></returns>
        public bool loadLocalManifest(in string manifestUrl, in string storagePath)
        {
            return true;
        }

        /// <summary>
        /// retrieving the remote manifest object
        /// </summary>
        /// <returns></returns>
        public Manifest getRemoteManifest()
        {
            return _remoteManifest;
        }

        /// <summary>
        /// Load a custom remote manifest object, the manifest must be loaded already.
        /// You can only manually load remote manifest when the update state is UNCHECKED and local manifest is already inited, it will fail once the update process is began.
        /// </summary>
        /// <param name="remoteManifest">The remote manifest object to be set</param>
        /// <returns></returns>
        public bool loadRemoteManifest(Manifest remoteManifest)
        {
            return true;
        }

        /// <summary>
        /// Gets whether the current download is resuming previous unfinished job, this will only be available after READY_TO_UPDATE state, 
        /// under unknown states it will return false by default.
        /// </summary>
        /// <returns></returns>
        public bool isResuming()
        {
            return _downloadResumed;
        }

        /// <summary>
        /// Gets the total byte size to be downloaded of the update, this will only be available after READY_TO_UPDATE state, under unknown states it will return 0 by default.
        /// </summary>
        /// <returns></returns>
        public double getTotalBytes()
        {
            return _totalSize;
        }

        /// <summary>
        /// Gets the current downloaded byte size of the update, this will only be available after READY_TO_UPDATE state, under unknown states it will return 0 by default.
        /// </summary>
        /// <returns></returns>
        public double getDownloadedBytes()
        {
            return _totalDownloaded;
        }

        /// <summary>
        /// Gets the total files count to be downloaded of the update, this will only be available after READY_TO_UPDATE state, 
        /// under unknown states it will return 0 by default.
        /// </summary>
        /// <returns></returns>
        public int getTotalFiles()
        {
            return _totalToDownload;
        }

        /// <summary>
        /// Gets the current downloaded files count of the update, this will only be available after READY_TO_UPDATE state, 
        /// under unknown states it will return 0 by default.
        /// </summary>
        /// <returns></returns>
        public int getDownloadedFiles()
        {
            return _totalToDownload - _totalWaitToDownload;
        }


        /// <summary>
        /// Function for retrieving the max concurrent task count
        /// </summary>
        /// <returns></returns>
        public int getMaxConcurrentTask()
        {
            return _maxConcurrentTask;
        }


        /// <summary>
        /// Function for setting the max concurrent task count
        /// </summary>
        /// <param name="max"></param>
        public void setMaxConcurrentTask(int max)
        {
            _maxConcurrentTask = max;
        }

        /// <summary>
        /// Set the handle function for comparing manifests versions
        /// </summary>
        /// <param name="handle">The compare function</param>
        public void setVersionCompareHandle(in OnVersionCompare handle)
        {
            _versionCompareHandle = handle;
        }

        /// <summary>
        /// Set the verification function for checking whether downloaded asset is correct, e.g. using md5 verification
        /// </summary>
        /// <param name="callback">The verify callback function</param>
        public void setVerifyCallback(in VerifyCallback callback)
        {
            _verifyCallback = callback;
        }

        /// <summary>
        /// Set the event callback for receiving update process events
        /// </summary>
        /// <param name="callback">The event callback function</param>
        public void setEventCallback(in EventCallback callback)
        {
            _eventCallback = callback;
        }

        private void init(in string manifestUrl, in string storagePath)
        {
            _eventName = "__AssetsManager__";

            var hints = new DownloaderHints();

            _downloader = new Downloader(hints);

            _downloader.onTaskError = (in DownloadTask task, int errorCode, int errorCodeInternal, in string errorStr) =>
            {
                onError(task, errorCode, errorCodeInternal, errorStr);
            };

            _downloader.onTaskProgress = (in DownloadTask task, uint bytesReceived, uint totalBytesReceived, uint totalBytesExpected) =>
            {
                onProgress(totalBytesExpected, totalBytesReceived, task.requestURL, task.identifier);
            };

            _downloader.onFileTaskSuccess = (in DownloadTask task) =>
            {
                onSuccess(task.requestURL, task.storagePath, task.identifier);
            };

            setStoragePath(storagePath);

            _tempVersionPath = _tempStoragePath + VERSION_FILENAME;
            _cacheManifestPath = _storagePath + MANIFEST_FILENAME;
            _tempManifestPath = _tempStoragePath + TEMP_MANIFEST_FILENAME;

            if (!string.IsNullOrEmpty(manifestUrl))
            {
                loadLocalManifest(manifestUrl);
            }
        }

        private static string basename(in string path)
        {
            return Path.GetDirectoryName(path);
        }

        private string get(in string key)
        {
            Asset asset;
            if (_assets.TryGetValue(key, out asset))
            {
                return _storagePath + asset.path;
            }

            return "";
        }

        private void initManifests()
        {
            _inited = true;
            // Init and load temporary manifest
            _tempManifest = new Manifest();
            if (_tempManifest != null)
            {
                _tempManifest.parseFile(_tempManifestPath);
                // Previous update is interrupted
                if (File.Exists(_tempManifestPath))
                {
                    // Manifest parse failed, remove all temp files
                    if (!_tempManifest.isLoaded())
                    {
                        Directory.Delete(_tempStoragePath);
                        _tempManifest = null;
                    }
                }
            }
            else
            {
                _inited = false;
            }

            // Init remote manifest for future usage
            _remoteManifest = new Manifest();
            if (_remoteManifest == null)
            {
                _inited = false;
            }

            if (!_inited)
            {
                _localManifest = null;
                _tempManifest = null;
                _remoteManifest = null;
            }
        }

        private void prepareLocalManifest()
        {
            // An alias to assets
            _assets = _localManifest.getAssets();

            // Add search paths
            _localManifest.prependSearchPaths();
        }

        private void setStoragePath(in string storagePath)
        {
            _storagePath = storagePath;

            adjustPath(ref _storagePath);

            Directory.CreateDirectory(_storagePath);
            _tempStoragePath = _storagePath;
            _tempStoragePath.Insert(_storagePath.Length - 1, TEMP_PACKAGE_SUFFIX);
            Directory.CreateDirectory(_tempStoragePath);
        }

        private static void adjustPath(ref string path)
        {
            if (!string.IsNullOrEmpty(path) && path[path.Length - 1] != '/')
            {
                path += "/";
            }
        }

        private void dispatchUpdateEvent(EventAssetsManager.EventCode code, in string assetId = "", in string message = "", int errCode = 0, int errInternel = 0)
        {
            switch (code)
            {
                case EventAssetsManager.EventCode.ERROR_UPDATING:
                case EventAssetsManager.EventCode.ERROR_PARSE_MANIFEST:
                case EventAssetsManager.EventCode.ERROR_NO_LOCAL_MANIFEST:
                case EventAssetsManager.EventCode.ERROR_DECOMPRESS:
                case EventAssetsManager.EventCode.ERROR_DOWNLOAD_MANIFEST:
                case EventAssetsManager.EventCode.UPDATE_FAILED:
                case EventAssetsManager.EventCode.UPDATE_FINISHED:
                case EventAssetsManager.EventCode.ALREADY_UP_TO_DATE:
                    _updateEntry = UpdateEntry.NONE;
                    break;
                case EventAssetsManager.EventCode.UPDATE_PROGRESSION:
                    break;
                case EventAssetsManager.EventCode.ASSET_UPDATED:
                    break;
                case EventAssetsManager.EventCode.NEW_VERSION_FOUND:
                    if (_updateEntry == UpdateEntry.CHECK_UPDATE)
                    {
                        _updateEntry = UpdateEntry.NONE;
                    }
                    break;
                default:
                    break;
            }

            if (_eventCallback != null)
            {
                var e = new EventAssetsManager(_eventName, this, code, assetId, message, errCode, errInternel);

                _eventCallback(e);
            }
        }

        private void downloadVersion()
        {
            if (_updateState > State.PREDOWNLOAD_VERSION)
            {
                return;
            }

            string versionUrl = _localManifest.getVersionFileUrl();

            if (!string.IsNullOrEmpty(versionUrl))
            {
                _updateState = State.DOWNLOADING_VERSION;
                // Download version file asynchronously
                _downloader.createDownloadTask(versionUrl, _tempVersionPath, VERSION_ID);
            }
            // No version file found
            else
            {
                Debug.Log("AssetsManagerEx : No version file found, step skipped");
                _updateState = State.PREDOWNLOAD_MANIFEST;
                downloadManifest();
            }
        }

        private void parseVersion()
        {
            if (_updateState != State.VERSION_LOADED)
            {
                return;
            }

            _remoteManifest.parseVersion(_tempVersionPath);

            if (!_remoteManifest.isVersionLoaded())
            {
                Debug.Log("AssetsManagerEx : Fail to parse version file, step skipped");
                _updateState = State.PREDOWNLOAD_MANIFEST;
                downloadManifest();
            }
            else
            {
                if (_localManifest.versionGreaterOrEquals(_remoteManifest, _versionCompareHandle))
                {
                    _updateState = State.UP_TO_DATE;
                    Directory.Delete(_tempVersionPath, true);
                    dispatchUpdateEvent(EventAssetsManager.EventCode.ALREADY_UP_TO_DATE);
                }
                else
                {
                    _updateState = State.PREDOWNLOAD_MANIFEST;
                    downloadManifest();
                }
            }
        }

        private void downloadManifest()
        {
            if (_updateState != State.PREDOWNLOAD_MANIFEST)
            {
                return;
            }

            string manifestUrl = _localManifest.getManifestFileUrl();

            if (!string.IsNullOrEmpty(manifestUrl))
            {
                _updateState = State.DOWNLOADING_MANIFEST;
                // Download version file asynchronously
                _downloader.createDownloadTask(manifestUrl, _tempManifestPath, MANIFEST_ID);
            }
            // No manifest file found
            else
            {
                Debug.Log("AssetsManagerEx : No manifest file found, check update failed\n");
                dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_DOWNLOAD_MANIFEST);
                _updateState = State.UNCHECKED;
            }
        }

        private void parseManifest()
        {
            if (_updateState != State.MANIFEST_LOADED)
            {
                return;
            }

            _remoteManifest.parseFile(_tempManifestPath);

            if (!_remoteManifest.isLoaded())
            {
                Debug.Log("AssetsManagerEx : Error parsing manifest file, " + _tempManifestPath);
                dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_PARSE_MANIFEST);
                _updateState = State.UNCHECKED;
            }
            else
            {
                if (_localManifest.versionGreaterOrEquals(_remoteManifest, _versionCompareHandle))
                {
                    _updateState = State.UP_TO_DATE;
                    Directory.Delete(_tempVersionPath, true);
                    dispatchUpdateEvent(EventAssetsManager.EventCode.ALREADY_UP_TO_DATE);
                }
                else
                {
                    _updateState = State.NEED_UPDATE;

                    if (_updateEntry == UpdateEntry.DO_UPDATE)
                    {
                        startUpdate();
                    }
                    else if (_updateEntry == UpdateEntry.CHECK_UPDATE)
                    {
                        prepareUpdate();
                    }

                    dispatchUpdateEvent(EventAssetsManager.EventCode.NEW_VERSION_FOUND);
                }
            }
        }

        private void startUpdate()
        {
            if (_updateState == State.NEED_UPDATE)
            {
                prepareUpdate();
            }
            if (_updateState == State.READY_TO_UPDATE)
            {
                _totalSize = 0;
                _updateState = State.UPDATING;
                string msg;
                if (_downloadResumed)
                {
                    msg = string.Format("Resuming from previous unfinished update, %d files remains to be finished.", _totalToDownload);
                }
                else
                {
                    msg = string.Format("Start to update %d files from remote package.", _totalToDownload);
                }
                dispatchUpdateEvent(EventAssetsManager.EventCode.UPDATE_PROGRESSION, "", msg);
                batchDownload();
            }
        }

        private void updateSucceed()
        {
            // Set temp manifest's updating
            if (_tempManifest != null)
            {
                _tempManifest.setUpdating(false);
            }

            // Every thing is correctly downloaded, do the following
            // 1. rename temporary manifest to valid manifest
            if (File.Exists(_tempManifestPath))
            {
                File.Move(_tempStoragePath + TEMP_MANIFEST_FILENAME, _tempStoragePath + MANIFEST_FILENAME);
            }

            // 2. Get the delete files
            Dictionary<string, Manifest.AssetDiff > diffMap = _localManifest.genDiff(_remoteManifest);

            // 3. merge temporary storage path to storage path so that temporary version turns to cached version
            if (Directory.Exists(_tempStoragePath))
            {
                // Merging all files in temp storage path to storage path
                //List<string> files;
                string[] files = Directory.GetFiles(_tempStoragePath);;
                int baseOffset = _tempStoragePath.Length;
                string relativePath;
                string dstPath;
                foreach (var file in files)
                {
                    relativePath = file.Substring(baseOffset);
                    dstPath = _storagePath + relativePath;

                    // Create directory
                    if (relativePath[relativePath.Length-1] == '/')
                    {
                        Directory.CreateDirectory(dstPath);
                    }
                    // Copy file
                    else
                    {
                        if (File.Exists(dstPath))
                        {
                            File.Delete(dstPath);
                        }
                        File.Move(file, dstPath);
                    }

                    // Remove from delete list for safe, although this is not the case in general.
                    if (diffMap.ContainsKey(relativePath))
                    {
                        diffMap.Remove(relativePath);
                    }
                }

                // Preprocessing local files in previous version and creating download folders
                foreach (var it in diffMap)
                {
                    Manifest.AssetDiff diff = it.Value;
                    if (diff.type == Manifest.DiffType.DELETED)
                    {
                        // TODO(kenshin): Do this when download finish, it don’t matter delete or not.
                        string exsitedPath = _storagePath + diff.asset.path;
                        File.Delete(exsitedPath);
                    }
                }
            }

            // 4. swap the localManifest
            _localManifest = null;
            _localManifest = _remoteManifest;
            _localManifest.setManifestRoot(_storagePath);
            _remoteManifest = null;
            // 5. make local manifest take effect
            prepareLocalManifest();
            // 6. Set update state
            _updateState = State.UP_TO_DATE;
            // 7. Notify finished event
            dispatchUpdateEvent(EventAssetsManager.EventCode.UPDATE_FINISHED);
            // 8. Remove temp storage path
            Directory.Delete(_tempStoragePath);
        }

        private bool decompress(in string filename)
        {
            GZipHelper.uncompress(filename, filename);
            return true;
        }

        private void decompressDownloadedZip(in string customId, in string storagePath)
        {
            var asyncData = new AsyncData();
            asyncData.customId = customId;
            asyncData.zipFile = storagePath;
            asyncData.succeed = false;

            IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf(asyncData));

            Action<IntPtr> decompressFinished = (IntPtr param) =>
            {
                AsyncData dataInner = (AsyncData)Marshal.PtrToStructure(intPtr, typeof(AsyncData));

                if (dataInner.succeed)
                {
                    fileSuccess(dataInner.customId, dataInner.zipFile);
                }
                else
                {
                    string errorMsg = "Unable to decompress file " + dataInner.zipFile;
                    // Ensure zip file deletion (if decompress failure cause task thread exit abnormally)
                    File.Delete(dataInner.zipFile);
                    dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_DECOMPRESS, "", errorMsg);
                    fileError(dataInner.customId, errorMsg);
                }

                Marshal.FreeHGlobal(intPtr);
            };

            AsyncTaskPool.Instance.enqueue(AsyncTaskPool.TaskType.TASK_OTHER, decompressFinished, intPtr, () =>
            {
                // Decompress all zip files
                if (decompress(asyncData.zipFile))
                {
                    asyncData.succeed = true;
                }
                File.Delete(asyncData.zipFile);
            });
        }

        /// <summary>
        /// Update a list of assets under the current AssetsManager context
        /// </summary>
        /// <param name="assets"></param>
        private void updateAssets(in DownloadUnits assets)
        {
            if (!_inited)
            {
                Debug.Log("AssetsManagerEx : Manifests uninited.\n");
                dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_NO_LOCAL_MANIFEST);
                return;
            }

            if (_updateState != State.UPDATING && _localManifest.isLoaded() && _remoteManifest.isLoaded())
            {
                _updateState = State.UPDATING;
                _downloadUnits.Clear();
                _downloadedSize.Clear();
                _percent = _percentByFile = 0F;
                _sizeCollected = 0;
                _totalDownloaded = _totalSize = 0.0;
                _totalWaitToDownload = _totalToDownload = assets.Count;
                _nextSavePoint = 0;
                _totalEnabled = false;
                if (_totalToDownload > 0)
                {
                    _downloadUnits = assets;
                    batchDownload();
                }
                else if (_totalToDownload == 0)
                {
                    onDownloadUnitsFinished();
                }
            }
        }

        /// <summary>
        /// Retrieve all failed assets during the last update
        /// </summary>
        /// <returns></returns>
        private ref readonly DownloadUnits getFailedAssets()
        {
            return ref _failedUnits;
        }

        /// <summary>
        /// Function for destroying the downloaded version file and manifest file N/A
        /// </summary>
        private void destroyDownloadedVersion()
        {
            Directory.Delete(_storagePath);
            Directory.Delete(_tempStoragePath);
        }

        /// <summary>
        /// Download items in queue with max concurrency setting
        /// </summary>
        private void queueDowload()
        {
            if (_totalWaitToDownload == 0)
            {
                onDownloadUnitsFinished();
                return;
            }

            while (_currConcurrentTask < _maxConcurrentTask && _queue.Count > 0)
            {
                string key = _queue[_queue.Count - 1];
                _queue.RemoveAt(_queue.Count - 1);

                _currConcurrentTask++;
                DownloadUnit unit = _downloadUnits[key];
                Directory.CreateDirectory(basename(unit.storagePath));
                _downloader.createDownloadTask(unit.srcUrl, unit.storagePath, unit.customId);

                _tempManifest.setAssetDownloadState(key, Manifest.DownloadState.DOWNLOADING);
            }
            if (_percentByFile / 100 > _nextSavePoint)
            {
                // Save current download manifest information for resuming
                _tempManifest.saveToFile(_tempManifestPath);
                _nextSavePoint += SAVE_POINT_INTERVAL;
            }
        }

        private void fileError(in string identifier, in string errorStr, int errorCode = 0, int errorCodeInternal = 0)
        {
            // Found unit and add it to failed units
            if (_downloadUnits.ContainsKey(identifier))
            {
                _totalWaitToDownload--;

                DownloadUnit unit;
                _downloadUnits.TryGetValue(identifier, out unit);
                _failedUnits.Add(unit.customId, unit);
            }

            dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_UPDATING, identifier, errorStr, errorCode, errorCodeInternal);
            _tempManifest.setAssetDownloadState(identifier, Manifest.DownloadState.UNSTARTED);

            _currConcurrentTask = Math.Max(0, _currConcurrentTask - 1);
            queueDowload();
        }

        private void fileSuccess(in string customId, in string storagePath)
        {
            // Set download state to SUCCESSED
            _tempManifest.setAssetDownloadState(customId, Manifest.DownloadState.SUCCESSED);

            // Found unit and delete it
            if (_failedUnits.ContainsKey(customId))
            {
                // Remove from failed units list
                _failedUnits.Remove(customId);
            }

            if (_downloadUnits.ContainsKey(customId))
            {
                // Reduce count only when unit found in _downloadUnits
                _totalWaitToDownload--;

                _percentByFile = 100 * (_totalToDownload - _totalWaitToDownload) / (_totalToDownload);
                // Notify progression event
                dispatchUpdateEvent(EventAssetsManager.EventCode.UPDATE_PROGRESSION, "");
            }

            // Notify asset updated event
            dispatchUpdateEvent(EventAssetsManager.EventCode.ASSET_UPDATED, customId);

            _currConcurrentTask = Math.Max(0, _currConcurrentTask - 1);
            queueDowload();
        }

        /// <summary>
        /// Call back function for error handling,the error will then be reported to user's listener registed in addUpdateEventListener
        /// </summary>
        /// <param name="task"></param>
        /// <param name="errorCode"></param>
        /// <param name="errorCodeInternal"></param>
        /// <param name="errorStr"></param>
        private void onError(in DownloadTask task, int errorCode, int errorCodeInternal, in string errorStr)
        {
            // Skip version error occurred
            if (task.identifier == VERSION_ID)
            {
                Debug.Log("AssetsManagerEx : Fail to download version file, step skipped\n");
                _updateState = State.PREDOWNLOAD_MANIFEST;
                downloadManifest();
            }
            else if (task.identifier == MANIFEST_ID)
            {
                dispatchUpdateEvent(EventAssetsManager.EventCode.ERROR_DOWNLOAD_MANIFEST, task.identifier, errorStr, errorCode, errorCodeInternal);
                _updateState = State.FAIL_TO_UPDATE;
            }
            else
            {
                fileError(task.identifier, errorStr, errorCode, errorCodeInternal);
            }
        }

        /// <summary>
        /// Call back function for recording downloading percent of the current asset,
        /// the progression will then be reported to user's listener registed in addUpdateProgressEventListener
        /// </summary>
        /// <param name="total">Total size to download for this asset</param>
        /// <param name="downloaded">Total size already downloaded for this asset</param>
        /// <param name="url">The url of this asset</param>
        /// <param name="customId">The key of this asset</param>
        private void onProgress(double total, double downloaded, in string url, in string customId)
        {
            if (customId == VERSION_ID || customId == MANIFEST_ID)
            {
                _percent = (float)(100 * downloaded / total);
                // Notify progression event
                dispatchUpdateEvent(EventAssetsManager.EventCode.UPDATE_PROGRESSION, customId);
                return;
            } // Calculate total downloaded
            bool found = false;
            _totalDownloaded = 0;
            foreach (var it in _downloadedSize)
            {
                var size = it.Value;
                if (it.Key == customId)
                {
                    size = downloaded;
                    found = true;
                }
                _totalDownloaded += size;
            }
            // Collect information if not registed
            if (!found)
            {
                // Set download state to DOWNLOADING, this will run only once in the download process
                _tempManifest.setAssetDownloadState(customId, Manifest.DownloadState.DOWNLOADING);
                // Register the download size information
                _downloadedSize.Remove(customId);
                _downloadedSize.Add(customId, downloaded);
                // Check download unit size existance, if not exist collect size in total size
                if (_downloadUnits[customId].size == 0)
                {
                    _totalSize += total;
                    _sizeCollected++;
                    // All collected, enable total size
                    if (_sizeCollected == _totalToDownload)
                    {
                        _totalEnabled = true;
                    }
                }
            }

            if (_totalEnabled && _updateState == State.UPDATING)
            {
                var currentPercent = (100 * _totalDownloaded / _totalSize);
                // Notify at integer level change
                if ((int)currentPercent != (int)_percent)
                {
                    _percent = (float)currentPercent;
                    // Notify progression event
                    dispatchUpdateEvent(EventAssetsManager.EventCode.UPDATE_PROGRESSION, customId);
                }
            }
        }

        /// <summary>
        /// Call back function for success of the current asset
        /// the success event will then be send to user's listener registed in addUpdateEventListener
        /// </summary>
        /// <param name="srcUrl">The url of this asset</param>
        /// <param name="storagePath">The storage path of this asset</param>
        /// <param name="customId">The key of this asset</param>
        private void onSuccess(in string srcUrl, in string storagePath, in string customId)
        {
            if (customId == VERSION_ID)
            {
                _updateState = State.VERSION_LOADED;
                parseVersion();
            }
            else if (customId == MANIFEST_ID)
            {
                _updateState = State.MANIFEST_LOADED;
                parseManifest();
            }
            else
            {
                bool ok = true;
                var assets = _remoteManifest.getAssets();


                Asset asset;
                bool bFound = assets.TryGetValue(customId, out asset);

                if (bFound)
                {
                    if (_verifyCallback != null)
                    {
                        ok = _verifyCallback(storagePath, asset);
                    }
                }

                if (ok)
                {
                    bool zip = bFound && asset.type == (short)DataType.GZip;
                    if (zip)
                    {
                        decompressDownloadedZip(customId, storagePath);
                    }
                    else
                    {
                        fileSuccess(customId, storagePath);
                    }
                }
                else
                {
                    fileError(customId, "Asset file verification failed after downloaded");
                }
            }
        }

        private void batchDownload()
        {
            _queue.Clear();
            foreach (var iter in _downloadUnits) {
                DownloadUnit unit = iter.Value;
                if (unit.size > 0)
                {
                    _totalSize += unit.size;
                    _sizeCollected++;
                }

                _queue.Add(iter.Key);
            }
            // All collected, enable total size
            if (_sizeCollected == _totalToDownload)
            {
                _totalEnabled = true;
            }

            queueDowload();
        }

        /// <summary>
        /// Called when one DownloadUnits finished
        /// </summary>
        private void onDownloadUnitsFinished()
        {
            // Always save current download manifest information for resuming
            _tempManifest.saveToFile(_tempManifestPath);

            // Finished with error check
            if (_failedUnits.Count > 0)
            {
                _updateState = State.FAIL_TO_UPDATE;
                dispatchUpdateEvent(EventAssetsManager.EventCode.UPDATE_FAILED);
            }
            else if (_updateState == State.UPDATING)
            {
                updateSucceed();
            }
        }
    }
}
