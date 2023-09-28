using UnityEngine;

public static class Device
{
    public static string Platform
    {
        get
        {
            var target = Application.platform;

            if (target == RuntimePlatform.WindowsEditor || target == RuntimePlatform.WindowsPlayer)
            {
                return "windows";
            }

            if (target == RuntimePlatform.OSXPlayer || target == RuntimePlatform.OSXPlayer)
            {
                return "mac";
            }

            if (target == RuntimePlatform.Android)
            {
                return "android";
            }

            if (target == RuntimePlatform.IPhonePlayer)
            {
                return "ios";
            }

            return "unknown";
        }
    }

    public static string Model
    {
        get
        {
            return SystemInfo.deviceModel;
        }
    }

    public static string System
    {
        get
        {
            return SystemInfo.operatingSystem;
        }
    }

    public static string Name
    {
        get
        {
            return SystemInfo.deviceName;
        }
    }

    public static string Uid
    {
        get
        {
            return SystemInfo.deviceUniqueIdentifier;
        }
    }

    public static bool IsMobile
    {
        get
        {
            return Application.isMobilePlatform;
        }
    }

    public static bool IsEditor
    {
        get { return Application.isEditor; }
    }

    /// <summary>
    /// MacOS Windows Editor上获取剪切板
    /// </summary>
    public static string Clipboard 
    {
        get
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            return GUIUtility.systemCopyBuffer;
#else
            throw new NotImplementedException();
#endif
        }
    }
}