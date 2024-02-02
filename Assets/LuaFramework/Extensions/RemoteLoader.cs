using Extension;
using KissFramework;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

using Settings = System.Collections.Generic.Dictionary<string, string>;
using Bundle = System.Collections.Generic.Dictionary<string, string>;
using Bundles = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;


namespace Extensions
{
    public class RemoteLoader : Singleton<RemoteLoader>
    {
        /// <summary>
        /// 配置总控
        /// </summary>
        internal class ControlData
        {
            public int beta;
            public int env_0;
            public int env_1;
            public int env_2;
        }

        internal class Route
        {
            public string url = null;
            public string path = null;
            public string bundle = null;
            public string ext = null;
        }


        /// <summary>
        /// bundle配置
        /// key: bundle名称 value: bundle版本
        /// </summary>
        public Settings settings;

        /// <summary>
        /// bundle详情
        /// key: bundle名称 value: bundle信息(key:value 资源路径:md5)
        /// </summary>
        public Bundles bundles;

        private ControlData controlData = null;

        private string cdn = null;
        private string envName = null;
        private int ver = 0;

        public void Init(string url, int env)
        {
            Route tmp = null;
            ParseUrl("ab_config://icon/shop_xiandou2.png", out tmp);

            cdn = url;
            string controlUrl = $"{url}/control.json";

            Http.Get(controlUrl, new Action<ControlData>((c)=>{
                if (c == null) 
                {
                    Debug.LogError("请求配置总控异常");
                    return;
                }

                Debug.Log("请求配置总控完成");

                controlData = c;

                if (env == 0)
                {
                    envName = "dev";
                    ver = controlData.env_0;
                }
                else if (env == 1)
                {
                    envName = "test";
                    ver = controlData.env_1;
                }
                else
                {
                    envName = "online";
                    ver = controlData.env_2;
                }

                string settingsUrl = $"{url}/{envName}/ver_{ver}.json";

                Http.Get(settingsUrl, new Action<Settings>(s=>{
                    if (s == null) 
                    {
                        Debug.LogError("请求配置settings异常");
                        return;
                    }
                    settings = s;
                    Debug.Log("请求配置settings完成");
                }));
            }));
        }

        public void LoadBunlde(string nameOrUrl, Action<Bundle> action)
        {
            if (bundles.ContainsKey(nameOrUrl))
            {
                action(bundles[nameOrUrl]);
                return;
            }
            
            string url = $"{cdn}/{envName}/{nameOrUrl}/bundle.{settings[nameOrUrl]}";

            Http.Get(url, new Action<Bundle>(b=>{
                bundles.Add(nameOrUrl, b);

                action(bundles[nameOrUrl]);
            }));
        }

        public void Load<T>(in string path, Action<T> action)
        {
            Route r = null;
            ParseUrl(path, out r);

            LoadBunlde(r.bundle, new Action<Bundle>(b=>{
                if (b == null)
                {
                    Debug.LogError("加载bundle异常:" + r.bundle);
                    return;
                }

                string md5 = b[r.path];
                string url = $"{cdn}/{envName}/{r.bundle}/{r.path}.{md5}.{r.ext}";

                // 图片资源
                if (typeof(T) == typeof(Texture2D))
                {
                    Http.GetTexture(url, (Action<Texture2D>)(object)(action));

                    return;
                }

                // 多媒体资源

                // 文本资源 string bytes[] 任意 json的结构

                Http.Get(url, action);
            }));
        }

        private void ParseUrl(in string url, out Route r)
        {
            var colon = url.IndexOf("://");
            var lastDot = url.LastIndexOf(".");

            r = new Route();
            r.url = url;
            r.bundle = url.Substring(0, colon);
            r.path = url.Substring(colon + 3, lastDot);
            r.ext = url.Substring(lastDot + 1);
        }
    }
}
