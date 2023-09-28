using Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Native
{
    using TaskWrapper = KeyValuePair<DownloadTask, DownloadIO>;

    public delegate void onTaskProgress(in DownloadTask task, uint bytesReceived, uint totalBytesReceived, uint totalBytesExpected);

    public delegate void onTaskFinish(in DownloadTask task, int errorCode, int errorCodeInternal, in string errorStr, in MemoryStream data);

    public interface IDownloadScheduler
    {
        public event onTaskProgress onProgress;
        public event onTaskFinish onFinish;

        public IDownloadIO createCoTask(in DownloadTask task);

        public void abort(in IDownloadIO coTask);
    }

    public class DownloadScheduler : IDownloadScheduler
    {
        public event onTaskProgress onProgress;
        public event onTaskFinish onFinish;

        private DownloadExecutor _executor;

        public DownloadScheduler(in DownloaderHints hints)
        {
            _executor = new DownloadExecutor();
            _executor.hints = hints;

            Debug.Log("Construct DownloadScheduler:" + GetHashCode());

            Scheduler.Instance.Schedule(onSchedule, this, 0.1f, -1);
        }

        ~DownloadScheduler()
        {
            Debug.Log("Destruct DownloadScheduler:" + GetHashCode());
        }

        private void onSchedule()
        {
            var processTasks = _executor.getProcessTasks();

            foreach (var processTask in processTasks)
            {
                var task = processTask.Key;
                var coTask = processTask.Value;

                if (coTask._bytesReceived > 0)
                {
                    onProgress(task, coTask._bytesReceived, coTask._totalBytesReceived, coTask._totalBytesExpected);
                    Debug.Log($"    progress -> bytesReceived:{coTask._bytesReceived}  {coTask._totalBytesReceived}/{coTask._totalBytesExpected}");
                    coTask._bytesReceived = 0;
                }
            }

            List<TaskWrapper> finishedTasks;
            _executor.getFinishedTasks(out finishedTasks);


            foreach(var finishedTask in finishedTasks)
            {
                var task = finishedTask.Key;
                var coTask = finishedTask.Value;

                if (coTask._bytesReceived > 0)
                {
                    onProgress(task, coTask._bytesReceived, coTask._totalBytesReceived, coTask._totalBytesExpected);
                    coTask._bytesReceived = 0;
                }

                coTask.finish();

                onFinish(task, coTask._errCode, coTask._errCodeInternal, coTask._errDescription, coTask._buf);
                Debug.Log($"    DownloadScheduler: finish Task: Id({coTask.serialId})");
            }
        }

        public void abort(in IDownloadIO coTask)
        {
            throw new NotImplementedException();
        }

        public IDownloadIO createCoTask(in DownloadTask task)
        {
            var coTask = new DownloadIO();

            coTask.init(task.storagePath, _executor.hints.tempFileNameSuffix);

            Debug.Log($"    DownloadScheduler: createCoTask: Id({coTask.serialId})");

            _executor.addTask(task, coTask);
            _executor.run();

            return coTask;
        }
    }
}
