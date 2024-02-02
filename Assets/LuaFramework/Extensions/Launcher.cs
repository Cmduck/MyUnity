using Extension;
using LuaInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using static Extension.AsyncTaskPool;
using UnzipUnits = System.Collections.Generic.Dictionary<string, Launcher.UnzipUnit>;


/// <summary>
/// 应用启动器(解压AssetBundles, 拷贝代码资源)
/// </summary>
public class Launcher
{
    struct AsyncData
    {
        public string customId;
        public string zipFile;
        public bool succeed;
    }

    // 解压单元
    public struct UnzipUnit
    {
        public string srcUrl;
        public string storagePath;
        public string customId;
        public short type;
    }


    private static Launcher s_Instance = null;

    [NoToLua]
    public static Dictionary<string, ManifestInfo> moduleManfiests = new();

    private Downloader _downloader = null;
    
    private UnzipUnits _unzipUnits = new();

    
    private int _totalFiles = 0;
    private int _downloadedFiles = 0;

    private int _count = 0;

    private Action<int, int> _luaProgress;
    private Action<int> _luaComplete;

    public static Launcher Instance { 
        get 
        { 
            if (s_Instance == null)
            {
                s_Instance = new Launcher();
            }
            return s_Instance; 
        } 
    }

    private Launcher()
    {
        var hints = new DownloaderHints();
        hints.countOfMaxProcessingTasks = 20;
        hints.tempFileNameSuffix = "";
        hints.timeoutInSeconds = 45;
        hints.allowResume = false;

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
    }

    ~Launcher() { }

    private void onError(in DownloadTask task, int errorCode, int errorCodeInternal, in string errorStr)
    {
        Debug.LogError($"[Launcher] onError() - task:{task.identifier} errorCode:{errorCode} errorCodeInternal:{errorCodeInternal} errorStr:{errorStr}");
    }

    private void onProgress(double total, double downloaded, in string url, in string customId)
    {
        Debug.Log($"[Launcher] onProgress() - task:{customId} url:{url} total:{total} downloaded:{downloaded}");
    }

    private void onFileSuccess(in string customId, in string storagePath)
    {
        Debug.Log($"{customId} 拷贝完成 => " + storagePath);
        ++_downloadedFiles;

        _luaProgress(_downloadedFiles, _totalFiles);

        if (_downloadedFiles == _totalFiles)
        {
            _luaComplete(0);
        }
    }

    private void onFileError(in string identifier, in string errorStr, int errorCode = 0, int errorCodeInternal = 0)
    {
        Debug.Log($"{identifier} 解压失败 errorCode:{errorCode} errorCodeInternal:{errorCodeInternal} errorStr:{errorStr}");
    }

    private void onSuccess(in string srcUrl, in string storagePath, in string customId)
    {
        Debug.Log($"[Launcher] onSuccess() - task:{customId} srcUrl:{srcUrl} storagePath:{storagePath}");

        UnzipUnit unit;
        if (_unzipUnits.TryGetValue(customId, out unit))
        {
            if (unit.type == (short)DataType.GZip)
            {
                decompressDownloadedZip(customId, storagePath);
            }
            else
            {
                onFileSuccess(customId, storagePath);
            }
        }
    }

    private void decompressDownloadedZip(in string customId, in string storagePath)
    {
        var asyncData = new AsyncData();
        asyncData.customId = customId;
        asyncData.zipFile = storagePath;
        asyncData.succeed = false;

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(asyncData));
        Marshal.StructureToPtr(asyncData, ptr, false);


        Action<IntPtr> decompressFinished = (IntPtr param) =>
        {
            AsyncData dataInner = (AsyncData)Marshal.PtrToStructure(param, typeof(AsyncData));

            if (dataInner.succeed)
            {
                Debug.Log($"{dataInner.customId} 解压完成 => " + dataInner.zipFile);
                onFileSuccess(dataInner.customId, dataInner.zipFile);
            }
            else
            {
                string errorMsg = "Unable to decompress file " + dataInner.zipFile;
                // Ensure zip file deletion (if decompress failure cause task thread exit abnormally)
                File.Delete(dataInner.zipFile);
                onFileError(dataInner.customId, errorMsg);
            }

            Marshal.FreeHGlobal(ptr);
        };

        int taskType = _count++ % 3;
        AsyncTaskPool.Instance.enqueue((TaskType)taskType, decompressFinished, ptr, () =>
        {
            AsyncData data = (AsyncData)Marshal.PtrToStructure(ptr, typeof(AsyncData));

            // Decompress all zip files
            if (decompress(data.zipFile))
            {
                data.succeed = true;
            }
            Marshal.StructureToPtr(data, ptr, false);

            //File.Delete(asyncData.zipFile);
        });
    }

    private bool decompress(in string filename)
    {
        GZipHelper.uncompress(filename, filename);
        return true;
    }

    public void WrapExtract(in string[] modules, in string writablePath, Action<int, int> progressCallback, Action<int> completeCallback)
    {
        if (Directory.Exists(writablePath))
        {
            Directory.Delete(writablePath, true);
        }
        Directory.CreateDirectory(writablePath);

        _luaProgress = progressCallback;
        _luaComplete = completeCallback;

        Scheduler.Instance.StartCoroutine(ExtractBundle(modules, writablePath));
    }

    private IEnumerator ExtractBundle(string[] modules, string writablePath)
    {
        yield return null;

        foreach (var module in modules)
        {
            string json;
            string url = $"{Application.streamingAssetsPath}/{module}_manifest.json";
#if UNITY_ANDROID
                UnityWebRequest uwr = UnityWebRequest.Get(url);
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    json = uwr.downloadHandler.text;
                }
                else
                {
                    Debug.LogError($"{url} error:" + uwr.error);
                }
#else
            json = File.ReadAllText(url);
#endif
            ManifestInfo manifest = LitJson.JsonMapper.ToObject<ManifestInfo>(json);
            moduleManfiests.Add(module, manifest);

            foreach (var pair in manifest.assets)
            {
                string downloadUrl = $"{Application.streamingAssetsPath}/{pair.Key}";
                string storagePath = $"{writablePath}/remote-{module}/{pair.Key}";

                var unit = new UnzipUnit();
                unit.storagePath = storagePath;
                unit.customId = pair.Key;
                unit.srcUrl = downloadUrl;
                unit.type = pair.Value.type;

                _unzipUnits.Add(pair.Key, unit);
                Debug.Log($"[{module}] --> {pair.Key} downloadUrl:{downloadUrl} storagePath:{storagePath}");
            }

            // cached manifest
            string manifestFile = $"{module}_manifest.json";
            var cachedUnit = new UnzipUnit();
            cachedUnit.storagePath = $"{writablePath}/remote-{module}/{manifestFile}";
            cachedUnit.customId = manifestFile;
            cachedUnit.srcUrl = $"{Application.streamingAssetsPath}/{manifestFile}";
            cachedUnit.type = 0;
            Debug.Log("Extract manifest:" + manifestFile);
            _unzipUnits.Add(manifestFile, cachedUnit);

            // package manifest
            var packageUnit = new UnzipUnit();
            packageUnit.storagePath = $"{writablePath}/{manifestFile}";
            packageUnit.customId = manifestFile;
            packageUnit.srcUrl = $"{Application.streamingAssetsPath}/{manifestFile}";
            packageUnit.type = 0;
            Debug.Log("Extract @manifest:" + manifestFile);
            _unzipUnits.Add("@" + manifestFile, packageUnit);
        }
        batchDownload();
    }

    private void batchDownload()
    {
        _totalFiles = _unzipUnits.Count;
        _downloadedFiles = 0;
        _count = 0;

        foreach (var pair in _unzipUnits)
        {
            _downloader.createDownloadTask(pair.Value.srcUrl, pair.Value.storagePath, pair.Value.customId);
        }
    }
}
