using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEngine;

namespace Native
{
    public struct DownloadTask
    {
        public static readonly int ERROR_NO_ERROR = 0;
        public static readonly int ERROR_INVALID_PARAMS = -1;
        public static readonly int ERROR_FILE_OP_FAILED = -2;
        public static readonly int ERROR_IMPL_INTERNAL = -3;
        public static readonly int ERROR_ABORT = -4;

        public string identifier;
        public string requestURL;
        public string storagePath;
        public Dictionary<string, string> header;

        public IDownloadIO _coTask;
    }

    public struct DownloaderHints
    {
        // 并发数目
        public uint countOfMaxProcessingTasks;
        // 超时时间
        public uint timeoutInSeconds;
        // 临时后缀
        public string tempFileNameSuffix;
        // 断点续传
        public bool allowResume;
    }

    public class Downloader
    {
        public delegate void OnDataTaskSuccess(in DownloadTask task, in MemoryStream data);
        public delegate void OnFileTaskSuccess(in DownloadTask task);

        public delegate void OnTaskProgress(in DownloadTask task, uint bytesReceived, uint totalBytesReceived, uint totalBytesExpected);
        public delegate void OnTaskError(in DownloadTask task, int errorCode, int errorCodeInternal, in string errorStr);

        public OnDataTaskSuccess onDataTaskSuccess;
        public OnFileTaskSuccess onFileTaskSuccess;
        public OnTaskProgress onTaskProgress;
        public OnTaskError onTaskError;
        private DownloadScheduler _scheduler;

        public Downloader(in DownloaderHints hints)
        {
            Debug.Log("Construct Downloader " + GetHashCode());

            _scheduler = new DownloadScheduler(hints);

            _scheduler.onProgress += (in DownloadTask task, uint bytesReceived, uint totalBytesReceived, uint totalBytesExpected) =>
            {
                if (onTaskProgress != null)
                    onTaskProgress(task, bytesReceived, totalBytesReceived, totalBytesExpected);
            };

            _scheduler.onFinish += (in DownloadTask task, int errorCode, int errorCodeInternal, in string errorStr, in MemoryStream data) =>
            {
                if (DownloadTask.ERROR_NO_ERROR != errorCode)
                {
                    if (onTaskError != null)
                    {
                        onTaskError(task, errorCode, errorCodeInternal, errorStr);
                    }
                    return;
                }

                // success callback
                if (!string.IsNullOrEmpty(task.storagePath))
                {
                    if (onFileTaskSuccess != null)
                    {
                        onFileTaskSuccess(task);
                    }
                }
                else
                {
                    // data task
                    if (onDataTaskSuccess != null)
                    {
                        onDataTaskSuccess(task, data);
                    }
                }
            };
        }

        ~Downloader()
        {
            Debug.Log("Destruct Downloader " + GetHashCode());
        }

        public DownloadTask createDataTask(in string srcUrl, in string identifier = "")
        {
            var task = new DownloadTask();

            task.requestURL = srcUrl;
            task.identifier = identifier;

            if (string.IsNullOrEmpty(srcUrl))
            {
                if (onTaskError != null)
                {
                    onTaskError(task, DownloadTask.ERROR_INVALID_PARAMS, 0, "URL or is empty.");
                }
            }

            task._coTask = _scheduler.createCoTask(task);

            return task;
        }

        public DownloadTask createDownloadTask(in string srcUrl, in string storagePath, in string identifier = "")
        {
            return createDownloadTask(srcUrl, storagePath, null, identifier);
        }

        public DownloadTask createDownloadTask(in string srcUrl, in string storagePath, in Dictionary<string, string> header, in string identifier = "")
        {
            var task = new DownloadTask();

            task.requestURL = srcUrl;
            task.storagePath = storagePath;
            task.identifier = identifier;
            task.header = header;

            if (string.IsNullOrEmpty(srcUrl) || string.IsNullOrEmpty(storagePath))
            {
                if (onTaskError != null)
                {
                    onTaskError(task, DownloadTask.ERROR_INVALID_PARAMS, 0, "URL or storage path is empty.");
                }
            }

            task._coTask = _scheduler.createCoTask(task);

            return task;
        }

        public void abort(in DownloadTask task)
        {
            _scheduler.abort(task._coTask);
        }
    }
}
