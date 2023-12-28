using LuaFramework;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Extension
{

    using TaskWrapper = KeyValuePair<DownloadTask, DownloadIO>;

    public class DownloadExecutor
    {
        public DownloaderHints hints;
        private Queue<TaskWrapper> _requestQueue = new Queue<TaskWrapper>();
        private Queue<TaskWrapper> _finishedQueue = new Queue<TaskWrapper>();
        private HashSet<TaskWrapper> _processSet = new HashSet<TaskWrapper>();

        private bool _running = false;

        public DownloadExecutor()
        {
            Debug.Log("Construct DownloadExecutor:" + GetHashCode());
        }

        ~DownloadExecutor()
        {
            Debug.Log("Destruct DownloadExecutor:" + GetHashCode());
        }

        public void addTask(in DownloadTask task, DownloadIO coTask)
        {
            if (DownloadTask.ERROR_NO_ERROR == coTask._errCode)
            {
                _requestQueue.Enqueue(new TaskWrapper(task, coTask));
            }
            else
            {
                _finishedQueue.Enqueue(new TaskWrapper(task, coTask));
            }
        }

        public void run()
        {
            if (!_running)
            {
                _running = true;
                Scheduler.Instance.StartCoroutine(process());
            }
        }

        public void stop()
        {

        }

        public bool stoped()
        {
            return _running == false && _processSet.Count == 0 && _finishedQueue.Count == 0;
        }

        public ref readonly HashSet<TaskWrapper> getProcessTasks()
        {
            return ref _processSet;
        }

        public void getFinishedTasks(out List<TaskWrapper> outList)
        {
            outList = new List<TaskWrapper>(_finishedQueue);
            _finishedQueue.Clear();
        }

        private IEnumerator process()
        {
            _running = true;
            uint countOfMaxProcessingTasks = hints.countOfMaxProcessingTasks;

            uint size = (uint)_processSet.Count;
            while ((0 == countOfMaxProcessingTasks || size < countOfMaxProcessingTasks) && _requestQueue.Count > 0)
            {
                TaskWrapper wrapper = _requestQueue.Dequeue();
                _processSet.Add(wrapper);

                Debug.Log("Create a new request " + wrapper.Key.requestURL);
                yield return download(wrapper);
            }

            _running = false;
        }

        private IEnumerator getHeaderInfo(TaskWrapper wrapper)
        {
            var headRequest = UnityWebRequest.Head(wrapper.Key.requestURL);
            yield return headRequest.SendWebRequest();

            if (headRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"When request url({wrapper.Key.requestURL}) header info, return unexcept http response code({headRequest.responseCode})");
                wrapper.Value.setErrorProc(DownloadTask.ERROR_IMPL_INTERNAL, (int)headRequest.responseCode, headRequest.error);
            }
            else
            {
                wrapper.Value._headerAchieved = true;
                uint totalSize = uint.Parse(headRequest.GetResponseHeader("Content-Length"));
                wrapper.Value._totalBytesExpected = totalSize;
                bool acceptRanges = headRequest.GetResponseHeader("Accept-Ranges") != null;

                if (File.Exists(wrapper.Value._tempFileName))
                {
                    var fileinfo = new FileInfo(wrapper.Value._tempFileName);
                    wrapper.Value._fileSize = (uint)fileinfo.Length;
                }

                bool append = acceptRanges && wrapper.Value._fileSize > 0;
                wrapper.Value._acceptRanges = append;
                wrapper.Value._totalBytesReceived = append ? wrapper.Value._fileSize : 0; // 仅断点续传时 设置文件已经下载大小
                // wrapper.Value.createHandler();

                Debug.Log($"{wrapper.Key.requestURL} HEAD acceptRanges:{acceptRanges} totalSize:{totalSize} {wrapper.Value._tempFileName} fileSize:{wrapper.Value._fileSize}");
            }

            headRequest.Dispose();
        }

        private IEnumerator download(TaskWrapper wrapper)
        {
            if (hints.allowResume)
            {
                yield return getHeaderInfo(wrapper);
            }

            do
            {
                if (wrapper.Value._errCode != DownloadTask.ERROR_NO_ERROR)
                {
                    break;
                }

                if (wrapper.Value._totalBytesReceived != 0 && wrapper.Value._totalBytesReceived == wrapper.Value._totalBytesExpected)
                {
                    Debug.Log($"{wrapper.Key.requestURL} has already download finished.");
                    break;
                }

                Debug.Log($"begin download {wrapper.Key.requestURL}");
                wrapper.Value.createHandler();
                var uwr = new UnityWebRequest(wrapper.Key.requestURL, UnityWebRequest.kHttpVerbGET);
                uwr.timeout = (int)hints.timeoutInSeconds;
                uwr.disposeDownloadHandlerOnDispose = false;
                uwr.downloadHandler = wrapper.Value.handler;
                uwr.SetRequestHeader("Range", "bytes=" + wrapper.Value._totalBytesReceived + "-");

                uwr.SendWebRequest();

                FileInfo file = new FileInfo(wrapper.Value._tempFileName);
                ulong fileLength = (ulong)file.Length;

                while (!uwr.isDone)
                {
                    wrapper.Value._bytesReceived = (uint)uwr.downloadedBytes;
                    wrapper.Value._totalBytesReceived = wrapper.Value._fileSize + wrapper.Value._bytesReceived;

                    Debug.Log($"{wrapper.Key.requestURL} downloading. _totalBytesReceived:{wrapper.Value._totalBytesReceived}");
                    yield return new WaitForSeconds(.1f);
                }

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("File successfully downloaded and saved to " + wrapper.Key.storagePath);
                }
                else
                {
                    Debug.LogError($"{wrapper.Key.requestURL} error:" + uwr.error);

                    wrapper.Value.setErrorProc(DownloadTask.ERROR_IMPL_INTERNAL, (int)uwr.responseCode, uwr.error);
                }

                uwr.Dispose();

            } while (false);

            _processSet.Remove(wrapper);
            _finishedQueue.Enqueue(wrapper);
        }
    }
}
