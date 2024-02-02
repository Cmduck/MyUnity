using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Extensions;
using KissFramework;
using UnityEngine;

public class App : MonoSingleton<App>
{
    void Awake()
    {
        Debug.Log("App Awake() - ");
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("App Start() - ");

        UnitTestRemoteLoader();
        // UnitTestHttp();

        Debug.Log("App run other.");
    }

    void UnitTestRemoteLoader()
    {
        string url = "http://192.168.20.222:5555/remote-asset";
        RemoteLoader.Instance.Init(url, 0);
    }

    void UnitTestHttp()
    {
        string url = "http://192.168.20.222:5555/remote-asset/control.json";
        Action<string> stringAction = (str)=> Debug.Log($"Processing string: {str}");
        Action<byte[]> byteArrAction = (bytes)=> Debug.Log($"Processing bytes: {string.Join(", ", bytes)}");
        Action<Dictionary<string, string> > dictAction = (dict)=> {
            foreach(var pair in dict)
            {
                Debug.Log($"Key = {pair.Key}, Value = {pair.Value}");
            }
        };


        // Http.Get(url, stringAction);
        // Http.Get(url, byteArrAction);
        Http.Get(url, dictAction);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
