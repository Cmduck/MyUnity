using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Extensions
{
    public static class Http
    {
        public static void Get<T>(string url, Action<T> action)
        {
            App.Instance.StartCoroutine(Fetch(url, new Action<byte[]>(bytes=>{
                var asset = Convert<T>(bytes);
                action(asset);
            })));
        }

        public static void GetTexture(string url, Action<Texture2D> action)
        {
            App.Instance.StartCoroutine(FetchTexture(url, action));
        }

        private static T Convert<T>(byte[] bytes)
        {
            // 根据 T 类型做特殊处理
            if (typeof(T) == typeof(string))
            {
                return (T)(object)(Encoding.UTF8.GetString(bytes));
            }
            else if (typeof(T) == typeof(byte[]))
            {
                return (T)(object)(bytes);
            }

            var raw = Encoding.UTF8.GetString(bytes);

            if (string.IsNullOrEmpty(raw))
            {
                return default(T);
            }

            return (T)(object)(JsonConvert.DeserializeObject<T>(raw));
        }

        private static IEnumerator Fetch(string url, Action<byte[]> action)
        {
            using (var uwr = UnityWebRequest.Get(url))
            {
                yield return uwr.SendWebRequest();

                switch (uwr.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.LogError("Http: Error: " + uwr.error);
                        action(null);
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.LogError("Http: HTTP Error: " + uwr.error);
                        action(null);
                        break;
                    case UnityWebRequest.Result.Success:
                        // Debug.Log("Http:\nReceived: " + uwr.downloadHandler.data);
                        action(uwr.downloadHandler.data);
                        break;
                }
            }
        }


        private static IEnumerator FetchTexture(string url, Action<Texture2D> action)
        {
            using (var uwr =  UnityWebRequestTexture.GetTexture(url))
            {
                yield return uwr.SendWebRequest();

                switch (uwr.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.LogError("Http: Error: " + uwr.error);
                        action(null);
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.LogError("Http: HTTP Error: " + uwr.error);
                        action(null);
                        break;
                    case UnityWebRequest.Result.Success:
                        action(DownloadHandlerTexture.GetContent(uwr));
                        break;
                }
            }
        }
    }
}
