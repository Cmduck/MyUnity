using System;
using System.Collections.Generic;

namespace iGame
{
    public class IDataCaching
    {   
        private Dictionary<string, object> _datas = new();
        private Dictionary<string, long> _timestamps = new();
        private CallbackHelper<object> _callbacks = new();

        /// <summary>
        /// 缓存数据
        /// </summary>
        /// <param name="key">数据唯一key</param>
        /// <param name="obj">数据</param>
        /// <param name="refresh">是否刷新时间戳</param>
        public void SetCache(in string key, object obj)
        {
            if (_datas.ContainsKey(key))
            {
                _datas[key] = obj;
                _timestamps[key] = UnityTools.Now();
            }
            else
            {
                _datas.Add(key, obj);
                _timestamps.Add(key, UnityTools.Now());
            }
        }

        /// <summary>
        /// 删除缓存
        /// </summary>
        /// <param name="key"></param>
        public void DeleteCache(in string key)
        {
            _datas.Remove(key);
            _timestamps.Remove(key);
        }

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object GetCache(in string key)
        {
            if (_datas.ContainsKey(key)) return _datas[key];

            return null;
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            _datas.Clear();
            _timestamps.Clear();
            _callbacks.Clear();
        }

        /// <summary>
        /// 获取数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">数据唯一key</param>
        /// <param name="fetch">数据拉取</param>
        /// <param name="action">数据回调</param>
        /// <param name="cd">超时cd(秒)</param>
        public void GetData<T>(string key, Action<Action<T>> fetch, Action<T> action, int cd = -1) where T : class
        {
            if (_callbacks.Contains(key))
            {
                Logger.Log($"[IDataCaching] 已存在key:{key} 加载回调列表.");
                _callbacks.SaveCallback(key, (obj)=>{
                    action?.Invoke(obj as T);
                });

                return;
            }

            do
            {
                if (cd == 0) break;

                if (cd > 0)
                {
                    var now = UnityTools.Now();
                    if (_timestamps.TryGetValue(key, out long past))
                    {
                        int delta = (int)(now - past);
                        if (cd < delta)
                        {
                            // 超出cd时间 重新拉取
                            break;
                        }
                    }
                }

                // 缓存读取
                Logger.Log($"[IDataCaching] 缓存读取 key:{key} cd: {cd}");
                _callbacks.DealCallback(key, _datas[key]);
                return;
            } while (false);

            // 拉取最新
            fetch(ret=>{
                if (ret != null)
                {
                    SetCache(key, ret);
                }

                Logger.Log($"[IDataCaching] 拉取最新 key:{key} cd: {cd}");
                _callbacks.DealCallback(key, ret);
            });
        }
    }
}