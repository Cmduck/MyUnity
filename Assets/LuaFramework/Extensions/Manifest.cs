using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


namespace Native
{

    using DownloadUnits = System.Collections.Generic.Dictionary<string, DownloadUnit>;
    //! Asset object
    using Asset = ManifestAsset;

   [Flags]
   public enum DataType : short
   {
      Normal = 0,
      GZip = 1,
      Archive = 2,
      XXTea = 4,
      Unknown = 8
   };

    public struct DownloadUnit
    {
        public string srcUrl;
        public string storagePath;
        public string customId;
        public float size;
    };

    public struct ManifestAsset
    {
        public string md5;
        public string path;
        public short type;
        public int size;
        public int downloadState;
    };

    public struct ManifestInfo
    {
        public string packageUrl;
        public string remoteManifestUrl;
        public string remoteVersionUrl;
        public string version;
        public string md5;
        public bool updating;
        public Dictionary<string, Asset> assets;
    }

    public delegate int OnVersionCompare(in string versionA, in string versionB);

    public class Manifest
    {

        int cmpVersion(in string v1, in string v2)
        {
            string[] octV1 = v1.Split('.');
            string[] octV2 = v2.Split('.');

            if (octV1.Length != 3 || octV2.Length != 3)
            {
                return 1;
            }

            int ret = 0;
            for (var i = 0; i < 3; i++)
            {
                if (octV1[i] == octV2[i])
                {
                    continue;
                }
                ret = (octV1[i].CompareTo(octV2[i]) >= 0) ? 1 : -1;
                break;
            }

            return ret;
        }

        //! The type of difference
        public enum DiffType : byte
        {
            ADDED,
            DELETED,
            MODIFIED
        };

        public enum DownloadState
        {
            UNSTARTED,
            DOWNLOADING,
            SUCCESSED,
            UNMARKED
        };

        //! Object indicate the difference between two Assets
        public struct AssetDiff
        {
            public Asset asset;
            public DiffType type;
        };

        //! Indicate whether the version informations have been fully loaded
        private bool _versionLoaded;

        //! Indicate whether the manifest have been fully loaded
        private bool _loaded;

        //! Indicate whether the manifest is updating and can be resumed in the future
        private bool _updating;

        //! The local manifest root
        private string _manifestRoot;

        //! The remote package url
        private string _packageUrl;

        //! The remote path of manifest file
        private string _remoteManifestUrl;

        //! The remote path of version file [Optional]
        private string _remoteVersionUrl;

        //! The version of local manifest
        private string _version;

        //! Full assets list
        private Dictionary<string, Asset> _assets = new Dictionary<string, Asset>();

        //! All search paths
        private List<string> _searchPaths;

        private ManifestInfo _manifestInfo;

        /// <summary>
        /// Check whether the version informations have been fully loaded
        /// </summary>
        /// <returns></returns>
        public bool isVersionLoaded() 
        {
            return _versionLoaded;
        }

        /// <summary>
        /// Check whether the manifest have been fully loaded
        /// </summary>
        /// <returns></returns>
        public bool isLoaded()
        {
            return _loaded;
        }

        public void setPackageUrl(in string url)
        {
            _packageUrl = url;
        }

        /// <summary>
        /// Gets remote package url.
        /// </summary>
        /// <returns></returns>
        public ref readonly string getPackageUrl()
        {
            return ref _packageUrl;
        }

        /// <summary>
        /// Gets remote manifest file url.
        /// </summary>
        /// <returns></returns>
        public ref readonly string getManifestFileUrl()
        {
            return ref _remoteManifestUrl;
        }

        /// <summary>
        /// Gets remote version file url.
        /// </summary>
        /// <returns></returns>
        public ref readonly string getVersionFileUrl()
        {
            return ref _remoteVersionUrl;
        }

        /// <summary>
        /// Gets manifest version.
        /// </summary>
        /// <returns></returns>
        public ref readonly string getVersion()
        {
            return ref _version;
        }

        /// <summary>
        /// Get the search paths list related to the Manifest.
        /// </summary>
        /// <returns></returns>
        public List<string> getSearchPaths()
        {
            return null;
        }

        /// <summary>
        /// Get the manifest root path, normally it should also be the local storage path.
        /// </summary>
        /// <returns></returns>
        public ref readonly string getManifestRoot()  
        {
           return ref _manifestRoot;
        }

        /// <summary>
        /// Constructor for Manifest class, create manifest by parsing a json file
        /// </summary>
        /// <param name="manifestUrl">manifestUrl Url of the local manifest</param>
        public Manifest(in string manifestUrl = "")
        {
            _versionLoaded = false;
            _loaded = false;
            _updating = false;

            if (!string.IsNullOrEmpty(manifestUrl))
            {
                parseFile(manifestUrl);
            }
        }

        /// <summary>
        /// Constructor for Manifest class, create manifest by parsing a json string
        /// </summary>
        /// <param name="content">Json string content</param>
        /// <param name="manifestRoot">The root path of the manifest file (It should be local path, so that we can find assets path relative to the root path)</param>
        public Manifest(in string content, in string manifestRoot)
        {
            _versionLoaded = false;
            _loaded = false;
            _updating = false;

            if (!string.IsNullOrEmpty(content))
            {
                parseJSONString(content, manifestRoot);
            }
        }

        /// <summary>
        /// Parse the manifest file information into this manifest
        /// </summary>
        /// <param name="manifestUrl">Url of the local manifest</param>
        public void parseFile(in string manifestUrl)
        {
            loadJson(manifestUrl);

            // Register the local manifest root
            int found = manifestUrl.LastIndexOf("/\\");
            if (found != -1) {
                _manifestRoot = manifestUrl.Substring(0, found + 1);
            }
            loadManifest(_manifestInfo);
        }

        /// <summary>
        /// Parse the manifest from json string into this manifest
        /// </summary>
        /// <param name="content">Json string content</param>
        /// <param name="manifestRoot">The root path of the manifest file (It should be local path, so that we can find assets path relative to the root path)</param>
        public void parseJSONString(in string content, in string manifestRoot)
        {
            loadJsonFromString(content);
            // Register the local manifest root
            _manifestRoot = manifestRoot;
            loadManifest(_manifestInfo);
        }

        /// <summary>
        /// Get whether the manifest is being updating
        /// </summary>
        /// <returns>Updating or not</returns>
        public bool isUpdating()  
        {
            return _updating;
        }

        /// <summary>
        /// Set whether the manifest is being updating
        /// </summary>
        /// <param name="updating">Updating or not</param>
        public void setUpdating(bool updating)
        {
            if (_loaded)
            {
                _manifestInfo.updating = updating;

                _updating = updating;
            }
        }


        /// <summary>
        /// Load the json file into local json object
        /// </summary>
        /// <param name="url">Url of the json file</param>
        void loadJson(in string url)
        {
            clear();

            if (File.Exists(url))
            {
                string content = File.ReadAllText(url);

                if (string.IsNullOrEmpty(content))
                {
                    Debug.LogError("Fail to retrieve local file content:" + url);
                }
                else
                {
                    loadJsonFromString(content);
                }
            }
        }

        /// <summary>
        /// Load the json from a string into local json object
        /// </summary>
        /// <param name="content">The json content string</param>
        void loadJsonFromString(in string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                Debug.LogError("Fail to parse empty json content.");
            }
            else
            {
                try
                {
                    _manifestInfo = LitJson.JsonMapper.ToObject<ManifestInfo>(content);
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                }
                
            }
        }

        /// <summary>
        /// Parse the version file information into this manifest
        /// </summary>
        /// <param name="versionUrl">Url of the local version file</param>
        public void parseVersion(in string versionUrl)
        {
            loadJson(versionUrl);
            loadVersion(_manifestInfo);
        }

        /// <summary>
        /// Check whether the version of this manifest equals to another.
        /// </summary>
        /// <param name="b">The other manifest</param>
        /// <returns>Equal or not</returns>
        public bool versionEquals(Manifest b)
        {
            // Check manifest version
            if (_version != b.getVersion())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check whether the version of this manifest is greater or equals than another.
        /// </summary>
        /// <param name="b">The other manifest</param>
        /// <param name="handle">Customized comparasion handle function</param>
        /// <returns>Greater or not</returns>
        public bool versionGreaterOrEquals(Manifest b, OnVersionCompare handle)
        {
            string bVersion = b.getVersion();

            bool greater = false;
            if (handle != null)
            {
                greater = handle(_version, bVersion) >= 0;
            }
            else
            {
                greater = cmpVersion(_version, bVersion) >= 0;
            }

            return greater;
        }

        /// <summary>
        /// Check whether the version of this manifest is greater or equals than another.
        /// </summary>
        /// <param name="b">The other manifest</param>
        /// <param name="handle">Customized comparasion handle function</param>
        /// <returns></returns>
        public bool versionGreater(in Manifest b, OnVersionCompare handle)
        {
            string bVersion = b.getVersion();

            bool greater = false;
            if (handle != null)
            {
                greater = handle(_version, bVersion) > 0;
            }
            else
            {
                greater = cmpVersion(_version, bVersion) > 0;
            }

            return greater;
        }

        /// <summary>
        /// Generate difference between this Manifest and another.
        /// </summary>
        /// <param name="b">The other manifest</param>
        /// <returns></returns>
        public Dictionary<string, AssetDiff> genDiff(in Manifest b)
        {
            Dictionary<string, AssetDiff> diffMap = new Dictionary<string, AssetDiff>();
            Dictionary<string, Asset> bAssets = b.getAssets();

            string key;
            Asset valueA;
            Asset valueB;

            foreach (KeyValuePair<string, Asset> entry in _assets)
            {
                Console.WriteLine($"{entry.Key}: {entry.Value}");
                key = entry.Key;
                valueA = entry.Value;

                // Deleted
                if (!bAssets.ContainsKey(key))
                {
                    AssetDiff diff;
                    diff.asset = valueA;
                    diff.type = DiffType.DELETED;
                    diffMap.Add(key, diff);
                    continue;
                }


                // Modified
                bAssets.TryGetValue(key, out valueB);

                if (valueA.md5 != valueB.md5)
                {
                    AssetDiff diff;
                    diff.asset = valueB;
                    diff.type = DiffType.MODIFIED;
                    diffMap.Add(key, diff);
                }
            }

            foreach(KeyValuePair<string, Asset> entry in bAssets)
            {
                key = entry.Key;
                valueB = entry.Value;

                // Added
                if (!_assets.ContainsKey(key))
                {
                    AssetDiff diff;
                    diff.asset = valueB;
                    diff.type = DiffType.ADDED;
                    diffMap.Add(key, diff);
                }
            }

            return diffMap;
        }

        /// <summary>
        /// Generate resuming download assets list
        /// </summary>
        /// <param name="units">The download units reference to be modified by the generation result</param>
        public void genResumeAssetsList(DownloadUnits units)
        {
            foreach(KeyValuePair<string, Asset> entry in _assets)
            {
                Asset asset = entry.Value;
                if (asset.downloadState != (int)DownloadState.SUCCESSED && asset.downloadState != (int)DownloadState.UNMARKED)
                {
                    DownloadUnit unit;
                    unit.customId = entry.Key;
                    unit.srcUrl = _packageUrl + asset.path;
                    unit.storagePath = _manifestRoot + asset.path;
                    unit.size = asset.size;
                    units.Add(unit.customId, unit);
                }
            }
        }

        /// <summary>
        /// Prepend all search paths to the FileUtils.
        /// </summary>
        public void prependSearchPaths()
        {

        }

        public void loadVersion(in ManifestInfo json)
        {
            _remoteManifestUrl = json.remoteManifestUrl;
            _remoteVersionUrl = json.remoteVersionUrl;
            _version = json.version;
            _updating = json.updating;

            _versionLoaded = true;
        }

        public void loadManifest(in ManifestInfo json)
        {
            loadVersion(json);

            _packageUrl = json.packageUrl;
            if (!string.IsNullOrEmpty(_packageUrl) && !_packageUrl.EndsWith('/'))
            {
                _packageUrl += "/";
            }

            foreach(KeyValuePair<string, Asset> entry in json.assets)
            {
                _assets.Add(entry.Key, entry.Value);
            }
        }

        public void saveToFile(in string filepath)
        {
            var folder = Path.GetDirectoryName(filepath);

            if (!Directory.Exists(folder))
            {
                Debug.Log($"不存在{folder},创建它");
                Directory.CreateDirectory(folder);
            }

            using (StreamWriter outputFile = new StreamWriter(filepath))
            {
                outputFile.Write(LitJson.JsonMapper.ToJson(_manifestInfo));
            }
        }

        static Asset parseAsset(in string path, in LitJson.JsonData json)
        {
            return new Asset();
        }

        public void clear()
        {
            if (_versionLoaded || _loaded)
            {
                _remoteManifestUrl = "";
                _remoteVersionUrl = "";
                _version = "";
                _versionLoaded = false;
            }

            if (_loaded)
            {
                _assets.Clear();
                _searchPaths.Clear();
                _loaded = false;
            }
        }

        /// <summary>
        /// Gets assets.
        /// </summary>
        /// <returns></returns>
        public ref readonly Dictionary<string, Asset> getAssets()
        {
            return ref _assets;
        }

        /// <summary>
        /// Set the download state for an asset
        /// </summary>
        /// <param name="key">Key of the asset to set</param>
        /// <param name="state">The current download state of the asset</param>
        public void setAssetDownloadState(in string key, in DownloadState state)
        {
            if (_assets.ContainsKey(key))
            {
                Asset asset;
                _assets.TryGetValue(key, out asset);

                asset.downloadState = (int)state;
            }
        }

        public void setManifestRoot(in string root)
        {
            _manifestRoot = root;
        }
    }
}
