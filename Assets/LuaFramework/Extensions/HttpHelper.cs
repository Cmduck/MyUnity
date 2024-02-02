using KissFramework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using static Extensions.RemoteLoader;
using UnityEngine.Networking;
using UnityEngine;
using System.Collections;

namespace Extensions
{
    public class HttpHelper : Singleton<HttpHelper>
    {
        public IEnumerator Get(string url)
        {
            using (var uwr = UnityWebRequest.Get(url))
            {
                yield return uwr.SendWebRequest();

                switch (uwr.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.LogError("RemoteLoader: Error: " + uwr.error);
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.LogError("RemoteLoader: HTTP Error: " + uwr.error);
                        break;
                    case UnityWebRequest.Result.Success:
                        Debug.Log("RemoteLoader:\nReceived: " + uwr.downloadHandler.text);

                        yield return uwr.downloadHandler.text;
                        break;
                }
            }
        }
    }
}
