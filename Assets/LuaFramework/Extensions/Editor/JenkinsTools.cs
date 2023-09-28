

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LitJson;
using UnityEditor;
using UnityEngine;

public class JenkinsTools
{

   [Flags]
   enum DataType : short
   {
      Normal = 0,
      GZip = 1,
      Archive = 2,
      XXTea = 4,
      Unknown = 8
   };


    public struct Manifest
    {
        // public string packageUrl;
        // public string remoteManifestUrl;
        // public string remoteVersionUrl;
        public string version;
        
        public Dictionary<string, Bundle> assets;
    }

    public enum ManifestType
    {
        Version,
        Project,
    }


    public struct Bundle {
        // public string[] deps;
        public string md5;
        public int size;
        public short type;
    }

    // 资源路径
    public static readonly string RES_ROOT = "Assets/Resources/res";
    // 代码路径
    public static readonly string CODE_ROOT = "Assets/Resources/src";

    public static readonly string STREAM_ASSETS = "Assets/StreamingAssets";

    public static readonly string[] FILE_WHIHT_LIST = {
        ".shader",
        ".rendertexture",
        ".jpg",
        ".png",
        ".mat",
        ".anim",
        ".fbx",
        ".prefab",
        ".mp3",
        ".asset",
        ".json",
        ".txt",
        ".lua",
        ".wav",
        ".ttf",
        ".fontsettings",
        ".controller",
        ".overrideController",
        ".psd",
        ".tga"
    };

    static void UpdateProgress(int progress, int progressMax, string desc)
    {
        string title = "压缩加密中...[" + progress + " - " + progressMax + "]";
        float value = (float)progress / (float)progressMax;
        EditorUtility.DisplayProgressBar(title, desc, value);
    }


    public static void BuildLuaScript(in string codeRoot)
    {
        string srcDir = GetStreamAssetsSrc();

        if (Directory.Exists(srcDir))
        {
            Directory.Delete(srcDir, true);
        }

        // 此函数会创建 libDir目录
        CopyDirectory(codeRoot, srcDir, true, "*.lua");

        // 重命名 路径中的/ 替换成文件名中的.
        RenameLuaFiles(srcDir);

        // 压缩和加密lua
        CompressOrEncryptLua(srcDir);

        // 清理无用文件
        ClearUnneedLuaFiles(srcDir);

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 修改lua文件名
    ///     例如 srcDir/app/ui/xxx.lua 重命名为 app.ui.xxx.lua 并移至 libsDir路径下
    /// </summary>
    /// <param name="srcDir"></param>
    private static void RenameLuaFiles(in string srcDir)
    {
        // ["xxxx/app", "xxxx/framework", "xxxx/launcher"]
        string[] scriptDirs = Directory.GetDirectories(srcDir, "*", SearchOption.TopDirectoryOnly);

        foreach(var scriptDir in scriptDirs)
        {
            string normalize = scriptDir.Replace("\\", "/");
            string module = normalize.Substring(srcDir.Length + 1);

            string[] filesInModule = Directory.GetFiles(normalize, "*.lua", SearchOption.AllDirectories);

            foreach (var f in filesInModule)
            {
                string newName = f.Substring(srcDir.Length+1).Replace("\\", "/").Replace("/", ".");
                // string newNameWithoutExt = Path.ChangeExtension;

                File.Move(f, $"{srcDir}/{module}/{newName}");

                // Debug.Log("脚本地址:" + $"{srcDir}/{module}/{newName}");
            }

            // 子目录中的文件都根据路径生成新的文件名后 删除子目录 xxxx/app/x1  xxxx/app/x2
            string[] subFolders = Directory.GetDirectories(scriptDir, "*", SearchOption.TopDirectoryOnly);

            foreach (var folder in subFolders)
            {
                Directory.Delete(folder, true);
            }
        }
    }

    private static void CompressOrEncryptLua(in string srcDir)
    {
        string[] subDirs = Directory.GetDirectories(srcDir, "*", SearchOption.TopDirectoryOnly);

        foreach (var subDir in subDirs)
        {
            string module = subDir.Substring(srcDir.Length + 1);
            string zipname = $"{srcDir}/{module}.zip";

            Debug.Log($"待压缩目录:{subDir} 压缩名:{zipname}");
            GZipHelper.compressZip(zipname, null, subDir);
        }
    }

    private static void ClearUnneedLuaFiles(in string srcDir)
    {
        string[] dirs = Directory.GetDirectories(srcDir, "*", SearchOption.TopDirectoryOnly);

        foreach (var dir in dirs)
        {
            Directory.Delete(dir, true);
        }
    }

    public static void UncompressLuaZip()
    {
        string srcDir = GetStreamAssetsSrc();

        string zipname = $"{srcDir}/app.zip";
        string output = $"{srcDir}/appex";

        GZipHelper.uncompressZip(zipname, null, output);
    }

    /// <summary>
    /// 递归目录计算哪些目录需要达成asset bundle
    /// </summary>
    /// <param name="resRoot"></param>
    /// <returns></returns>
    public static List<AssetBundleBuild> CalculateAssetBundleBuilds(in string resRoot) 
    {
        List<AssetBundleBuild> abBuilds = new List<AssetBundleBuild>();

        string[] mdirs = Directory.GetDirectories(resRoot);
        string mname;
        foreach (string mdir in mdirs)
        {
            mname = new DirectoryInfo(mdir).Name;
            PackModuleRes(mname, mdir, abBuilds);
        }

        return abBuilds;
    }

    /// <summary>
    /// 打包模块资源
    /// </summary>
    /// <param name="module">模块名称</param>
    /// <param name="path">模块路径</param>
    public static void PackModuleRes(in string module, in string path, in List<AssetBundleBuild> abBuilds)
    {
        Debug.Log($"模块名:{module} path: {path}");
        string[] bdirs = Directory.GetDirectories(path);

        string bname = String.Empty;
        foreach (string bdir in bdirs)
        {
            bname = new DirectoryInfo(bdir).Name;

            if (bname.EndsWith("#")) {
                // 遍历子目录
                PackModuleRes(module, bdir, abBuilds);
            } else {
                PackBundleRes(module, bname, bdir, abBuilds);
            }
        }
    }

    /// <summary>
    /// 打包bundle资源
    /// </summary>
    /// <param name="module">模块名称</param>
    /// <param name="bundle">bundle名称</param>
    /// <param name="path">bundle路径</param>
    public static void PackBundleRes(in string module, in string bundle, in string path, in List<AssetBundleBuild> abBuilds)
    {
        Debug.Log($"        bundle: {bundle} path: " + path);
        // 获取bundle中所有文件
        string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);

        if (files.Length  < 1) return;

        string[] results = Array.FindAll(files, (f) => FILE_WHIHT_LIST.Contains(Path.GetExtension(f)));
        

        AssetBundleBuild abBuild = new AssetBundleBuild();
        abBuild.assetBundleName = $"{module}_{bundle}";
        abBuild.assetNames = results;

        abBuilds.Add(abBuild);
    }

    public static void ClearDirectory(in string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }

        Directory.CreateDirectory(dir);
    }

    public static void CopyDirectory(in string sourceDir, in string destinationDir, bool recursive, in string filePattern = "*.*", bool withoutMeta = true)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles(filePattern))
        {
            if (withoutMeta && file.Name.EndsWith(".meta")) continue;

            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true, filePattern);
            }
        }
    }

    public static AssetBundleManifest BuildAllAssetBundles(string outputPath, in List<AssetBundleBuild> abBuilds)
    {
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }

        Directory.CreateDirectory(outputPath);


        return BuildPipeline.BuildAssetBundles(outputPath, abBuilds.ToArray(),
        BuildAssetBundleOptions.UncompressedAssetBundle, EditorUserBuildSettings.activeBuildTarget);
    }

    public static string GetStreamAssetsRes()
    {
        string root = GetStreamAssetsRoot();

        return $"{root}/res";
    }

    public static string GetStreamAssetsSrc()
    {
        string root = GetStreamAssetsRoot();
        return $"{root}/src";
    }

    public static string GetStreamAssetsRoot()
    {
        string suffix = String.Empty;

        switch (EditorUserBuildSettings.activeBuildTarget)
        {
            case BuildTarget.StandaloneWindows:
                suffix = "X86";
                break;
            case BuildTarget.StandaloneWindows64:
                suffix = "X64";
                break;
            case BuildTarget.iOS:
                suffix = "IOS";
                break;
            case BuildTarget.Android:
                suffix = "ANDROID";
                break;
            default:
                Debug.LogError("未知的构建平台");
                throw new ArgumentException("未知的构建平台");
        }

        return $"{STREAM_ASSETS}_{suffix}";
    }

    /// <summary>
    /// 压缩或加密bundle
    /// </summary>
    /// <param name="dir">bundles路径</param>
    /// <param name="manifest">清单信息</param>
    /// <param name="excludes">排除bundle</param>
    /// <returns></returns>
    public static bool CompressOrEncryptBundles(in string dir, in AssetBundleManifest manifest, in string[] excludes)
    {
        // 获取所有bundle名称
        string[] names = manifest.GetAllAssetBundles();
        if (names.Length  < 1) {
            Debug.LogError("没有生成所有AssetBundle");
            return false;
        }

        string filepath;
        int i = 0;
        foreach (string name in names) 
        {
            UpdateProgress(++i, names.Length, name);

            if (excludes.Contains(name)) {
                Debug.Log("跳过加密压缩:" + name);
                
                continue;
            }


            filepath = $"{dir}/{name}";
            
            GZipHelper.compress(filepath, filepath);
        }

        EditorUtility.ClearProgressBar();

        return true;
    }

    /// <summary>
    /// 拷贝所有bundle到指定目录
    /// </summary>
    /// <param name="from">AssetBundles 原始路径</param>
    /// <param name="to">AssetBundles 目录路径</param>
    /// <param name="manifest">AssetBundle manifest</param>
    /// <returns></returns>
    public static bool CopyBundlesToRemoteAssets(in string from, in string to, in AssetBundleManifest manifest)
    {
        // 获取所有bundle名称
        string[] names = manifest.GetAllAssetBundles();
        if (names.Length  < 1) {
            Debug.LogError("没有生成所有AssetBundle");
            return false;
        }

        if (Directory.Exists(to)) 
        {
            Directory.Delete(to, true);
        }

        Directory.CreateDirectory(to);

        string filepath;
        string dest;
        foreach (string name in names) 
        {
            filepath = $"{from}/{name}";

            dest = $"{to}/{name}";
            Debug.Log($"Copy {filepath} to {dest}");
            File.Copy(filepath, dest);
        }

        return true;
    }

    /// <summary>
    /// 生成模块依赖文件到指定路径
    /// </summary>
    /// <param name="manifest"></param>
    /// <param name="output"></param>
    public static void GenerateDeps(in AssetBundleManifest manifest, in string output)
    {
        string[] bundles = manifest.GetAllAssetBundles();
        Dictionary<string, Dictionary<string, string[]>> moduleBundleDeps = new();

        // 填充字典
        foreach (var bundle in bundles)
        {
            // 所属模块
            string module = bundle.Split("_")[0];

            Dictionary<string, string[]> bundleDeps = null;
            if (!moduleBundleDeps.TryGetValue(module, out bundleDeps))
            {
                bundleDeps = new Dictionary<string, string[]>();
                moduleBundleDeps.Add(module, bundleDeps);
            }

            string[] deps = manifest.GetDirectDependencies(bundle);

            if (deps.Length > 0)
            {
                bundleDeps.Add(bundle, manifest.GetDirectDependencies(bundle));
            }
        }

        // 写入依赖
        foreach(var valuePair in moduleBundleDeps)
        {
            StringBuilder sb = new StringBuilder();
            JsonWriter writer = new JsonWriter(sb);
            var module = valuePair.Key;
            var bundleDepsDict = valuePair.Value;

            writer.WriteObjectStart();
            foreach(var bundlePair in bundleDepsDict)
            {
                string bundle = bundlePair.Key;
                string[] deps = bundlePair.Value;
                writer.WritePropertyName(bundle);
                writer.WriteObjectStart();
                    // 写入deps
                    writer.WritePropertyName("deps");
                    writer.WriteArrayStart();
                    foreach (var dep in deps)
                    {
                        writer.Write(dep);
                    }
                    writer.WriteArrayEnd();
                writer.WriteObjectEnd();
            }
            writer.WriteObjectEnd();

            SaveToPath(sb.ToString(), output, $"{module}_depend.json");
        }
    }

    /// <summary>
    /// 平台路径生成manifest
    /// </summary>
    /// <param name="platformPath"></param>
    /// <param name="versions"></param>
    /// <param name="unzipBundles"></param>
    /// <param name="hasVersion"></param>
    public static void GenerateManifest(in string platformPath, in Dictionary<string, int> versions, in string[] unzipBundles, bool hasVersion = true)
    {
        string resPath = platformPath + "/res";
        string srcPath = platformPath + "/src";

        string[] bundlePaths = Directory.GetFiles(resPath, "*.*", SearchOption.AllDirectories);

        Dictionary<string, Manifest> moduleManfiest = new();

        // 计算所有bundle所属模块的manifest信息
        foreach (var bpath in bundlePaths)
        {
            if (bpath.EndsWith(".meta")) continue;

            string bundle = Path.GetFileName(bpath);
            // 所属模块
            string module = bundle.Split("_")[0];

            Manifest mf;
            if (!moduleManfiest.TryGetValue(module, out mf))
            {
                mf = new Manifest();
                // m.packageUrl = ""; //"${placeholder}/";
                // m.remoteManifestUrl = ""; //"${placeholder}/project.manifest";
                // m.remoteVersionUrl = ""; //"${placeholder}/version.manifest";
                mf.version = versions[module].ToString();
                mf.assets = new();
                moduleManfiest.Add(module, mf);
            }

            // 计算manifest信息
            var fileinfo = new FileInfo(bpath);
            var relativePath = Path.GetRelativePath(platformPath, bpath).Replace("\\", "/"); 

            Bundle b = new Bundle();
            // b.deps = manifest.GetDirectDependencies(name); // 移至depend.json
            b.md5 = CalculateFileMd5(fileinfo);
            b.size = (int)fileinfo.Length;

            if (!unzipBundles.Contains(bundle) && !bpath.EndsWith(".json"))
            {
                b.type = (short)DataType.GZip;
            }
            else
            {
                b.type = (short)DataType.Normal;
            }

            mf.assets.Add(relativePath, b);
        }

        // 计算src的manifest 游戏代码打入指定游戏的manifest 其他都打入hall
        if (Directory.Exists(srcPath))
        {
            string[] files = Directory.GetFiles(srcPath, "*.zip", SearchOption.AllDirectories);

            foreach(var fpath in files)
            {
                var fileinfo = new FileInfo(fpath);
                var relativePath = Path.GetRelativePath(platformPath, fpath).Replace("\\", "/"); 
                string filename = Path.GetFileNameWithoutExtension(fileinfo.Name);
                Manifest mm;
                if (filename.StartsWith("game"))
                {
                    mm = moduleManfiest[filename];
                }
                else
                {
                    mm = moduleManfiest["hall"];
                }

                Bundle b = new Bundle();
                b.md5 = CalculateFileMd5(fileinfo);
                b.size = (int)fileinfo.Length;
                b.type = (short)DataType.Normal;

                mm.assets.Add(relativePath, b);
            }
        }
        else
        {
            Debug.LogError("找不到脚本路径 无法生成脚本manifest:" + srcPath);
        }

        // 写入manifest
        foreach (var kvpair in moduleManfiest)
        {
            var module = kvpair.Key;
            var mf = kvpair.Value;
            string manifestContent = LitJson.JsonMapper.ToJson(mf);
            SaveToPath(manifestContent, GetStreamAssetsRoot(), $"{module}_manifest.json");

            if (hasVersion)
            {
                mf.assets = null;
                string versionContent = LitJson.JsonMapper.ToJson(mf);
                SaveToPath(versionContent, GetStreamAssetsRoot(), $"{module}_version.json");
            }
        }
    }

    public static void WriteBuildVersion(in string dir, in AssetBundleManifest manifest)
    {
        StringBuilder sb = new StringBuilder();
        JsonWriter writer = new JsonWriter(sb);

        writer.WriteObjectStart();
        writer.WritePropertyName("time");
        writer.Write(DateTimeOffset.Now.ToUnixTimeMilliseconds());
        writer.WriteObjectEnd();

        SaveToPath(sb.ToString(), dir, "app.json");
    }

    public static bool ClearFilesWithSuffixInDirectory(in string path, in string[] suffixs)
    {
        string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
        if (files.Length  < 1) {
            Debug.LogError("没有生成所有AssetBundle");
            return false;
        }

        // string[] results = Array.FindAll(files, (f) => filterExts.Contains(Path.GetExtension(f)));

        foreach (string file in files) 
        {
            Debug.Log("file:" + file);
            string filename = Path.GetFileName(file);

            foreach (string suffix in suffixs)
            {
                if (filename.EndsWith(suffix))
                {
                    Debug.Log("删除StreamAssets -> " + file);
                    File.Delete(file);
                    continue;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 删除Manifest Bundle和meta
    ///     Manifest Bundle由引擎自动生成 用于记录bundle依赖信息 仅Editor模式下查找bundle依赖使用 正式构建时不需要
    /// </summary>
    /// <param name="path"></param>
    /// <param name="abname"></param>
    /// <returns></returns>
    public static bool ClearManifestBundle(in string path, in string abname)
    {
        File.Delete($"{path}/{abname}");
        File.Delete($"{path}/{abname}.meta");

        return true;
    }


    public static bool ClearFilesWithFilenameInDirectory(in string path, in string[] filenames)
    {
        string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
        if (files.Length  < 1) 
        {
            Debug.LogError("没有生成所有AssetBundle");
            return false;
        }

        foreach (string file in files) 
        {
            Debug.Log("file:" + file);
            string filename = Path.GetFileName(file);

            foreach (string fname in filenames)
            {
                if (filename.Equals(fname))
                {
                    Debug.Log("删除StreamAssets -> " + file);
                    File.Delete(file);
                    continue;
                }
            }
        }

        return true;
    }

    static string CalculateFileMd5(string filePath)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = md5.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower().Substring(0, 5);
            }
        }
    }

    static string CalculateFileMd5(in FileInfo finfo)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = finfo.OpenRead())
            {
                byte[] hashBytes = md5.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower().Substring(0, 5);
            }
        }
    }

    static void SaveToPath(in string content, in string dir, in string filename)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // var fs = new FileStream(path, FileMode.Create);

        // fs.Write(content, 0, content.Length)

        File.WriteAllText($"{dir}/{filename}", content);
    }
}
