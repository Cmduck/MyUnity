using System;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Extension
{
    using TaskCallBack = Action<IntPtr>;
    public class AsyncTaskPool
    {

        class ThreadTasks
        {
            private Thread _thread;
            private Queue<Action> _tasks = new();
            private Queue<AsyncTaskCallBack> _taskCallbacks = new();
            // 同步锁
            private readonly object queueMutex = new object();

            private bool _stop = false;

            struct AsyncTaskCallBack
            {
                public TaskCallBack callback;
                public IntPtr callbackParam;
            }

            public ThreadTasks()
            {
                _thread = new Thread(() =>
                {
                    while(true)
                    {
                        Action task;
                        AsyncTaskCallBack callback;

                        lock (queueMutex) {

                            while (_tasks.Count == 0)
                            {
                                Monitor.Wait(queueMutex);

                                Debug.Log($"[AsyncTaskPool] thread({Thread.CurrentThread.ManagedThreadId}) - after wait");
                            }

                            task = this._tasks.Dequeue();
                            callback = this._taskCallbacks.Dequeue();

                            Debug.Log($"[AsyncTaskPool] thread({Thread.CurrentThread.ManagedThreadId}) - run task");
                            task();

                            Scheduler.Instance.performFunctionInMainThread(() =>
                            {
                                callback.callback(callback.callbackParam);
                            });

                            if (_tasks.Count > 0)
                            {
                                Monitor.Pulse(queueMutex);
                            }
                        }
                    }
                });

                _thread.Start();
            }

            ~ThreadTasks()
            {
                lock(queueMutex)
                {
                    _stop = true;
                    while (_tasks.Count > 0)
                    {
                        _tasks.Dequeue();
                    }

                    while (_taskCallbacks.Count > 0) 
                    {
                        _taskCallbacks.Dequeue();
                    }

                    Monitor.PulseAll(queueMutex);
                } 

                _thread.Join();
            }

            public void clear()
            {
                lock(queueMutex)
                {
                    while (_tasks.Count > 0)
                    {
                        _tasks.Dequeue();
                    }

                    while (_taskCallbacks.Count > 0)
                    {
                        _taskCallbacks.Dequeue();
                    }
                }
            }

            public void enqueue(in TaskCallBack callback, in IntPtr callbackParam, in Action f)
            {
                var task = f;
                
                lock (queueMutex) 
                {
                    if (_stop)
                    {
                        Debug.Assert(false, "already stop");
                        return;
                    }

                    AsyncTaskCallBack taskCallBack;
                    taskCallBack.callback = callback;
                    taskCallBack.callbackParam = callbackParam;

                    _tasks.Enqueue(() => { task(); });
                    _taskCallbacks.Enqueue(taskCallBack);

                    Monitor.Pulse(queueMutex);
                }
            }
        }

        public enum TaskType
        {
            TASK_IO,
            TASK_NETWORK,
            TASK_OTHER,
            TASK_MAX_TYPE,
        }

        ThreadTasks[] _threadTasks = new ThreadTasks[(int)TaskType.TASK_MAX_TYPE];

        private static readonly AsyncTaskPool s_Instance = new AsyncTaskPool();

        public static AsyncTaskPool Instance { 
            get 
            {
                return s_Instance; 
            } 
        }

        private AsyncTaskPool()
        {
            for (var i = 0; i < _threadTasks.Length; i++)
            {
                _threadTasks[i] = new ThreadTasks();
            }
        }

        /// <summary>
        /// 停止任务
        /// </summary>
        /// <param name="taskType">任务类型</param>
        public void stopTasks(TaskType taskType)
        {
            var threadTask = _threadTasks[(int)taskType];
            threadTask.clear();
        }

        /// <summary>
        /// 入列异步任务 当异步任务执行完成后 调用任务回调并传参
        /// </summary>
        /// <param name="type">任务类型</param>
        /// <param name="callBack">任务回调</param>
        /// <param name="callbackParam">回调参数</param>
        /// <param name="f">异步任务</param>
        public void enqueue(TaskType type, in TaskCallBack callback, in IntPtr callbackParam, in Action f)
        {
            Debug.Log("[AsyncTaskPool] enqueue() - ");

            var threadTask = _threadTasks[(int)(type)];

            if (threadTask == null)
            {
                threadTask = new ThreadTasks();
                _threadTasks[(int)type] = threadTask;
            }


            threadTask.enqueue(callback, callbackParam, f);
        }
    }
}
