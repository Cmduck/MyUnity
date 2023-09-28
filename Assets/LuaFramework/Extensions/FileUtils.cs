using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 一些约定:
///     所有文件路径参数，除绝对地址 相对地址以 xxx/yyy/zzz形式 不要使用以'/'开头的相对路径 /xxx/yyy/zzz
///     
///     所有路径参数以unix风格传入 x/y/z 不要使用windows风格 x\y\z 或者 x\\y\\z
///     所有参数均不使用string.IsNullOrEmpty判断,由外部调用函数确保参数非空
///     
///     所有的搜索路径_searchPathArray自带'/'结尾
/// </summary>

public class FileUtils
{

    private static FileUtils s_Instance = null;
    private List<string> _searchPathArray = new();
    private string _defaultResRootPath = string.Empty;

    private Dictionary<string, string> _fullPathCache = new();
    private string _writablePath = string.Empty;




    public static FileUtils Instance 
    {
        get 
        {
            if (s_Instance == null) 
            {
                s_Instance = new FileUtils();

                if (!s_Instance.init())
                {
                    s_Instance = null;
                }
            }

            return s_Instance; 
        } 
    }

    public static void destroy()
    {
        s_Instance = null;
    }

    private FileUtils()
    {

    }

    ~FileUtils()
    {

    }

    /// <summary>
    /// Purges the file searching cache.
    ///     It should be invoked after the resources were updated.
    ///     All the resources will be downloaded to the writable folder, before new app launchs
    ///     this method should be invoked to clean the file search cache.
    /// </summary>
    public void purgeCachedEntries()
    {
        _fullPathCache.Clear();
    }

    /// <summary>
    /// Gets string from a file.
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public string getStringFromFile(in string filename)
    {
        string fullpath = fullPathForFilename(filename);

        if (string.IsNullOrEmpty(fullpath))
        {
            Debug.LogError($"Can't found filename: [{filename}]");
            return null;
        }

        return File.ReadAllText(filename);
    }

    /// <summary>
    /// Gets bytes from a file.
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public byte[] getDataFromFile(in string filename)
    {
        string fullpath = fullPathForFilename(filename);

        if (string.IsNullOrEmpty(fullpath))
        {
            Debug.LogError($"Can't found filename: [{filename}]");
            return null;
        }

        return File.ReadAllBytes(filename);
    }

    public string fullPathForFilename(in string filename)
    {
        if (isAbsolutePath(filename)) return filename;

        if (_fullPathCache.ContainsKey(filename)) 
        {
            return _fullPathCache[filename];
        }

        foreach (var search in _searchPathArray)
        {   
            string fullpath = $"{search}{filename}";

            if (File.Exists(fullpath))
            {
                _fullPathCache.Add(filename, fullpath);

                return fullpath;
            }
        }

        return string.Empty;
    }

    public void setSearchPaths(in List<string> searchPaths)
    {
        bool existDefaultRootPath = false;
        
        _fullPathCache.Clear();
        _searchPathArray.Clear();
        foreach (var p in searchPaths)
        {
            string prefix = string.Empty;
            string path = string.Empty;
            
            if (!isAbsolutePath(p))
            { // Not an absolute path
                prefix = _defaultResRootPath;
            }
            path = prefix + (p);
            if (path.Length > 0 && path[path.Length-1] != '/')
            {
                path += "/";
            }
            if (!existDefaultRootPath && path == _defaultResRootPath)
            {
                existDefaultRootPath = true;
            }
            _searchPathArray.Add(path);
        }
        
        if (!existDefaultRootPath)
        {
            _searchPathArray.Add(_defaultResRootPath);
        }
    }

    public void addSearchPath(in string searchpath, bool front = false)
    {
        string path = isAbsolutePath(searchpath) ? searchpath : _defaultResRootPath+searchpath;

        if (path.Length > 0 && path[path.Length-1] != '/')
        {
            path += "/";
        }
        if (front) {
            _searchPathArray.Insert(0, path);
        } else {
            _searchPathArray.Add(path);
        }
    }

    public ref readonly List<string> getSearchPaths()
    {
        return ref _searchPathArray;
    }

    public string getWritablePath()
    {
        return _writablePath;
    }

    public bool writeToFile(in string content, in string fullPath)
    {
        var folder = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(content, fullPath);

        return true;
    }

    public bool isAbsolutePath(in string path)
    {
        return Path.IsPathFullyQualified(path);
    }

    public bool isFileExist(in string filename)
    {
        if (isAbsolutePath(filename))
        {
            return isFileExistInternal(filename);
        }

        string fullPath = searchFullPathForFilename(filename);
        if (string.IsNullOrEmpty(fullPath)) return false;

        return true;
    }

    public bool isDirectoryExist(in string dirPath)
    {
        if (isAbsolutePath(dirPath))
        {
            return isDirectoryExistInternal(dirPath);
        }

        // Already Cached ?
        if (_fullPathCache.ContainsKey(dirPath))
        {
            return isDirectoryExistInternal(_fullPathCache[dirPath]);
        }
        
        string fullpath;
        foreach (var searchIt in _searchPathArray)
        {
            fullpath = searchIt + dirPath ;
            if (isDirectoryExistInternal(fullpath))
            {
                _fullPathCache.Add(dirPath, fullpath);
                return true;
            }
            
        }

        return false;
    }

    public bool createDirectory(in string dirPath)
    {
        if (isDirectoryExist(dirPath)) return true;

        try
        {
            DirectoryInfo di = Directory.CreateDirectory(dirPath);
            if (di.Exists) return true;

            return false;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    public bool removeDirectory(in string dirPath)
    {
        try 
        {
            Directory.Delete(dirPath, true);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    public bool removeFile(in string fullpath)
    {
        try
        {
            File.Delete(fullpath);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    public bool renameFile(in string dir, in string oldname, in string newname)
    {
        string normalizePath = dir.Replace("\\", "/");

        if (!normalizePath.EndsWith("/"))
        {
            normalizePath += "/";
        }

        string oldPath = $"{normalizePath}{oldname}";
        string newPath = $"{normalizePath}{newname}";

        try
        {
            File.Move(oldPath, newPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    public long getFileSize(in string filepath)
    {
        string fullpath = string.Empty;

        if (!isAbsolutePath(filepath))
        {
            fullpath = searchFullPathForFilename(filepath);

            if (string.IsNullOrEmpty(fullpath)) return -1;
        }

        FileInfo fi = new FileInfo(filepath);

        if (fi.Exists) 
        {
            return fi.Length;
        }

        return -1; 
    }

    public ref readonly Dictionary<string, string> getFullPathCache()
    {
        return ref _fullPathCache;
    }

    private bool init()
    {
        _defaultResRootPath = Application.dataPath + "/";
        _searchPathArray.Add(_defaultResRootPath);
        _writablePath = Application.persistentDataPath + "/";

#if UNITY_EDITOR
        _searchPathArray.Add($"{Application.dataPath}/Resources/src/");
        _searchPathArray.Add($"{Application.dataPath}/Resources/src/tolua/");
        _searchPathArray.Add($"{Application.dataPath}/Resources/res/");
#else
        // _searchPathArray.Add($"{Application.dataPath}/Resources/src");
        // _searchPathArray.Add($"{Application.dataPath}/Resources/res");
#endif

        return true;
    }

    private bool isFileExistInternal(in string fullpath)
    {
        return File.Exists(fullpath);
    }

    private bool isDirectoryExistInternal(in string fulldir)
    {
        return Directory.Exists(fulldir);
    }

    private string searchFullPathForFilename(in string fileOrFull)
    {
        if (isAbsolutePath(fileOrFull))
        {
            return fileOrFull;
        }

        return fullPathForFilename(fileOrFull);
    }
}
