using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

namespace Extension
{
    public interface ISchedulable
    {
        public void Update();
    }

    internal class TimerItem
    {
        // 定时标识
        public object target = null;
        public int timerID = -1;
        // 重复次数(-1 无限次)
        public int repeat = 0;
        // 间隔时间
        public float interval = 0;
        // 剩余时间
        public float leave = 0;
        // 延迟时间
        public float delay = 0;
        // 是否暂停
        public bool paused = false;
        // 是否停止
        public bool stoped = true;
        public Action callback;
    }

    public class Scheduler : MonoBehaviour
    {
        public static readonly float NO_TIME_LEAVE = -1F;
        public static readonly int REPEAT_FOREVER = -1;

        private static Scheduler s_Instance = null;

        // 流逝时间(上次触发后到此刻的时间, 每次触发时重置)
        private float _elapseTime = 0;
        // 剩余时间
        private float _leaveTime = NO_TIME_LEAVE;

        private List<ISchedulable> _everyframe = new();

        // 空闲
        private List<TimerItem> _idle = new List<TimerItem>();
        // 活跃
        private List<TimerItem> _active = new List<TimerItem>();
        // 所有
        private List<TimerItem> _all = new List<TimerItem>();

        private IdGenerator _idGenerator = new IdGenerator();

        private readonly object _performMutex = new object();
        private List<Action> _functionsToPerform = new();

        private bool _stop = true;

        void Awake()
        {
            Debug.Log("Scheduler::Awake()");
            s_Instance = this;
        }

        //static Scheduler()
        //{

        //}

        //private Scheduler()
        //{

        //}

        public static Scheduler Instance { get { return s_Instance; } }


        public void Resume()
        {
            _stop = false;
        }

        public void Pause()
        {
            _stop = true;
        }

        public void Update()
        {
            //every frame
            foreach (ISchedulable s in _everyframe)
            {
                s.Update();
            }

            //
            // 处理其他线程的注册的函数
            //
            // 判断大小的性能消耗小于锁 通常几乎不会有函数被调用
            if (_functionsToPerform.Count > 0)
            {
                List<Action> temp = null;
                lock (_performMutex)
                {
                    temp = new List<Action>(_functionsToPerform);
                    _functionsToPerform.Clear();
                }

                Debug.Log("函数数目:" + temp.Count);
                foreach (var func in temp)
                {
                    func();
                }
            }

            if (_stop ) { return; }

            if (_leaveTime == NO_TIME_LEAVE)
            {
                return;
            }

            float dt = Time.deltaTime;

            _elapseTime += dt;

            if (_leaveTime >= dt) { 
                _leaveTime -= dt;
                Debug.Log("leaveTime:" +  _leaveTime);
            }
            else _leaveTime = 0;

            //Debug.Log($"leave time: {_leaveTime} = 0 ? {Mathf.Approximately(_leaveTime, 0)} elapseTime = {_elapseTime} timestamp: {Time.time}");

            //查询定时器
            if (Mathf.Approximately(_leaveTime, 0))
            {
                bool bKillTimer = false;
                //最小时间
                float dwTimeLeave = float.MaxValue;

                // 倒序执行 方便删除
                for (var l = _active.Count - 1; l >= 0; --l)
                {
                    TimerItem pTimerItem = _active[l];

                    // 跳过暂停项
                    if (pTimerItem.paused) continue;

                    bKillTimer = false;
                    if (pTimerItem.leave > _elapseTime) pTimerItem.leave -= _elapseTime;
                    else pTimerItem.leave = 0;

                    //时间触发
                    if (pTimerItem.leave == 0)
                    {
                        //Debug.Log($"Schedule() - Trigger timerID:{pTimerItem.timerID} interval:{pTimerItem.interval} repeat:{pTimerItem.repeat} leave:{pTimerItem.leave} delay:{pTimerItem.delay} timestamp: {Time.time}");
                        pTimerItem.callback();

                        //设置次数
                        if (pTimerItem.repeat != REPEAT_FOREVER)
                        {
                            if (pTimerItem.repeat == 1)
                            {
                                Debug.Log($"Schedule() - Recycle timerID:{pTimerItem.timerID} interval:{pTimerItem.interval} repeat:{pTimerItem.repeat} leave:{pTimerItem.leave} delay:{pTimerItem.delay} timestamp: {Time.time}");
                                _idGenerator.recycleId(pTimerItem.timerID);
                                bKillTimer = true;
                                pTimerItem.stoped = true;
                                _active.RemoveAt(l);
                                _idle.Add(pTimerItem);
                            }
                            else
                            {
                                --pTimerItem.repeat;
                            }
                        }

                        //设置时间
                        if (bKillTimer == false) pTimerItem.leave = pTimerItem.interval;
                    }

                    // 重置流逝
                    if (bKillTimer == false)
                    {
                        dwTimeLeave = Math.Min(dwTimeLeave, pTimerItem.interval);
                    }
                }

                //设置响应
                _elapseTime = 0;
                _leaveTime = dwTimeLeave;

                if (_active.Count == 0 || dwTimeLeave == float.MaxValue)
                {
                    _leaveTime = NO_TIME_LEAVE;
                    Pause();
                }
            }
        }

        public int Schedule(Action callback, object target, float interval, bool paused)
        {
            return Schedule(callback, target, interval, -1, 0, paused);
        }

        /// <summary>
        /// 开启定时器
        /// </summary>
        /// <param name="callback">回调函数</param>
        /// <param name="target">目标对象</param>
        /// <param name="interval">间隔时间</param>
        /// <param name="repeat">重复次数</param>
        /// <param name="delay">延迟时间</param>
        /// <param name="paused">是否暂停</param>
        /// <returns>是否成功</returns>
        public int Schedule(Action callback, object target, float interval, int repeat, float delay, bool paused)
        {
            Assert.IsTrue(interval >= 0);

            if (repeat < 0)
            {
                repeat = REPEAT_FOREVER;
            }

            int timerID = _idGenerator.generateId();

            TimerItem item = CreateItem(timerID);

            item.timerID = timerID;
            item.target = target;
            item.interval = interval;
            item.repeat = repeat;
            item.leave = interval + _elapseTime + delay;
            item.delay = delay;
            item.paused = paused;
            item.callback = callback;
            item.stoped = false;

            if (_leaveTime == NO_TIME_LEAVE) _leaveTime = interval;
            else _leaveTime = Math.Min(_leaveTime, interval);


            _active.Add(item);

            Debug.Log($"Schedule() - register timerID:{timerID} interval:{interval} repeat:{repeat} leave:{item.leave} delay:{delay} timestamp: {Time.time}");

            Resume();

            return timerID;
        }

        private TimerItem CreateItem(int nTimerID)
        {
            TimerItem item;
            if (_idle.Count > 0)
            {
                item = _idle[0];
                _idle.RemoveAt(0);
            }
            else
            {
                item = new TimerItem();
                _all.Add(item);

                Debug.Log("Scheduler::CreateItem() - new id:" + nTimerID);
            }

            return item;
        }

        public bool ScheduleOnce(Action callback, object target, float delay = 0F)
        {

            Schedule(callback, target, 0, 1, delay, false);
            return true;
        }

        public void ScheduleUpdate(ISchedulable target = null)
        {
            _everyframe.Add(target);
        }

        public void UnScheduleUpdate(ISchedulable target)
        {
            _everyframe.Remove(target);
        }

        public void Unschedule(int nTimerID)
        {
            if (nTimerID < 0) return;

            for (var i = 0; i < _active.Count; ++i)
            {
                var pTimerItem = _active[i];
                if (pTimerItem.timerID == nTimerID)
                {
                    _idGenerator.recycleId(pTimerItem.timerID);
                    _active.RemoveAt(i);
                    _idle.Add(pTimerItem);

                    break;
                }
            }

            if (_active.Count == 0)
            {
                _elapseTime = 0;
                _leaveTime = NO_TIME_LEAVE;
            }
        }

        public void UnscheduleAllForTarget(object target)
        {
            for (var l = _active.Count - 1; l >= 0; --l)
            {
                var pTimerItem = _active[l];
                if (pTimerItem.target != target) continue;
                _idGenerator.recycleId(pTimerItem.timerID);
                _active.RemoveAt(l);
                _idle.Add(pTimerItem);
            }

            if (_active.Count == 0)
            {
                _elapseTime = 0;
                _leaveTime = NO_TIME_LEAVE;
            }
        }

        public void UnscheduleAll()
        {
            for (var l = _active.Count - 1; l >= 0; --l)
            {
                var pTimerItem = _active[l];
                _idGenerator.recycleId(pTimerItem.timerID);
                _active.RemoveAt(l);
                _idle.Add(pTimerItem);
            }

            _elapseTime = 0;
            _leaveTime = NO_TIME_LEAVE;
        }

        // public void ResumeTarget(object target)
        // {
        //     Debug.Assert(target != null);

        //     Resume();

        //     for (var i = 0; i < _active.Count; ++i)
        //     {
        //         var pTimerItem = _active[i];
        //         if (pTimerItem.target != target) continue;
        //         //减去当轮过去时间
        //         if (pTimerItem.leave > _elapseTime) pTimerItem.leave -= _elapseTime;
        //         else pTimerItem.leave = 0;

        //         pTimerItem.paused = false;
        //     }
        // }

        // public void PauseTarget(object target)
        // {
        //     Debug.Assert(target != null);
        //     Resume();

        //     for (var l = _active.Count - 1; l >= 0; --l)
        //     {
        //         var pTimerItem = _active[l];
        //         if (pTimerItem.target != target) continue;
        //         //减去当轮过去时间
        //         if (pTimerItem.leave > _elapseTime) pTimerItem.leave -= _elapseTime;
        //         else pTimerItem.leave = 0;

        //         pTimerItem.paused = true;
        //     }
        // }

        /// <summary>
        /// 恢复计时
        /// </summary>
        /// <param name="nTimerID"></param>
        public void ResumeTimer(int nTimerID)
        {
            var item = _all[nTimerID];
            if (item.stoped)
            {
                Debug.LogWarning("Scheduler::ResumeTimer() - timer is stoped!");
                return;
            }

            if (!item.paused) return;

            item.paused = false;
            if (_leaveTime == NO_TIME_LEAVE) _leaveTime = item.interval;
            else _leaveTime = Math.Min(_leaveTime, item.interval);
            
            Resume();
        }

        /// <summary>
        /// 暂停计时
        /// </summary>
        /// <param name="nTimerID"></param>
        public void PauseTimer(int nTimerID)
        {
            var item = _all[nTimerID];
            if (item.stoped)
            {
                Debug.LogWarning("Scheduler::PauseTimer() - timer is stoped!");
                return;
            }
            
            if (item.paused) return;

            //减去当轮过去时间
            if (item.leave > _elapseTime) item.leave -= _elapseTime;
            else item.leave = 0;
            item.paused = true;
        }

        public void performFunctionInMainThread(Action function)
        {
            lock(_performMutex)
            {
                Debug.Log("添加到主线程执行");
                _functionsToPerform.Add(function);
            }
        }

        public void removeAllFunctionsToBePerformedInMainThread()
        {
            lock (_performMutex)
            {
                _functionsToPerform.Clear();
            }
        }
    }
}
