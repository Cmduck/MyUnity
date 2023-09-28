using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class JenkinsWindow : EditorWindow
{
    // 资源路径
    public static readonly string RES_ROOT = "Assets/Resources/res";
    // 代码路径
    public static readonly string CODE_ROOT = "Assets/Resources/src";
    // 热更补丁路径
    public static readonly string REMOTE_ASSETS = Path.Combine(Application.dataPath, "../remote-assets");

    // 热更版本(jenkins传入后设置)
    private static Dictionary<string, int> modulesVersion = new Dictionary<string, int>{{"hall", 1}, {"gameddz", 1}};
    // 构建版本 (取时间戳md5)
    private static string buildVer = "ab43f";

    enum Mode
    {
        Browser,
        Builder,
        Inspect,
    }

    Mode m_Mode;

    private VisualElement m_RightPane;

    [MenuItem("Jenkins/JenkinsWindow")]
    public static void ShowExample()
    {
        JenkinsWindow wnd = GetWindow<JenkinsWindow>();
        wnd.titleContent = new GUIContent("JenkinsWindow");
        // Limit size of the window
        wnd.minSize = new Vector2(450, 200);
        wnd.maxSize = new Vector2(1920, 720);
    }

    [MenuItem("Jenkins/BuildLua")]
    public static void BuildLuaScript()
    {
        JenkinsTools.BuildLuaScript(CODE_ROOT);
    }

    [MenuItem("Jenkins/UnzipLua")]
    public static void UncompressLuaZip()
    {
        JenkinsTools.UncompressLuaZip();
    }


    [MenuItem("Jenkins/Test GZip")]
    public static void GZipTest()
    {
        string unzipfile = Application.streamingAssetsPath + "/data/word.txt";
        string zipfile = Application.streamingAssetsPath + "/data/word_zip.txt";

        GZipHelper.compress(unzipfile, zipfile);
        GZipHelper.uncompress(zipfile, zipfile + "_unzip.txt");
    }

    // [MenuItem("Jenkins/Build Patch")]
    public static void BuildPatch()
    {
        Debug.Log("构建补丁" + EditorUserBuildSettings.activeBuildTarget);
        Debug.Log("资源路径:" + RES_ROOT);
        Debug.Log("代码路径:" + CODE_ROOT);

        // 1 计算所有AB Build
        // 2 生成AB到指定目录
        // 3 压缩加密AB
        // 4 拷贝AB到远程资源目录
        // 5 计算manifest 并写入remote-assets
        // 6 清理StreamingAssets_XXX 下.manifest和.manifest.meta

        bool isOK = true;

        do {
            #region 1 计算所有AB Build
            var abBuilds = JenkinsTools.CalculateAssetBundleBuilds(RES_ROOT);
            #endregion    

            // 2 生成AB到指定目录
            #region 2 生成AB到指定目录
            string abdir = JenkinsTools.GetStreamAssetsRes();
            // 构建所有ab
            AssetBundleManifest manifest = JenkinsTools.BuildAllAssetBundles(abdir, abBuilds);
            #endregion

            #region 3 压缩加密AB
            string[] excludes = {"hall_ui_updater"};
            isOK = JenkinsTools.CompressOrEncryptBundles(abdir, manifest, excludes);
            if (!isOK) break;
            #endregion

            #region 4 拷贝AB到远程资源目录
            string[] filterExts = {".meta", ".manifest"};
            isOK = JenkinsTools.CopyBundlesToRemoteAssets(abdir, REMOTE_ASSETS, manifest);
            if (!isOK) break;
            #endregion

            #region 5 计算manifest 并写入
            // JenkinsTools.GenerateManifest(manifest, modulesVersion, abdir, excludes);
            #endregion


            #region 6 清理StreamingAssets_XXX 下.manifest和.manifest.meta
            // 需要删除的后缀
            string[] deletedSuffix = {".manifest", ".manifest.meta"}; 
            // isOK = JenkinsTools.ClearFilesWithSuffixInDirectory(abdir, deletedSuffix);
            if (!isOK) break;

            // 删除Manifest Bundle 和 meta
            // isOK = JenkinsTools.ClearManifestBundle(abdir, "res");
            if (!isOK) break;
            #endregion

            AssetDatabase.Refresh();
        } while (false);

        if (!isOK) 
        {
            Debug.LogError("构建patch失败!!!");
        }
    }

    [MenuItem("Jenkins/Build Package")]
    public static void BuildPackage()
    {
        Debug.Log("构建整包:" + EditorUserBuildSettings.activeBuildTarget);
        Debug.Log("资源路径:" + RES_ROOT);
        Debug.Log("代码路径:" + CODE_ROOT);

        // 1 计算所有AB Build
        // 2 生成AB到指定目录
        // 3 压缩加密AB
        // 4 拷贝AB到远程资源目录
        // 5 计算manifest 并写入remote-assets
        // 6 清理StreamingAssets_XXX 下.manifest和.manifest.meta
        // 7 从bundle平台目录拷贝至 Application.streamingAssetsPath + "/res"

        bool isOK = true;

        do {
            var platformRoot = JenkinsTools.GetStreamAssetsRoot();
            JenkinsTools.ClearDirectory(platformRoot);

            #region 1 计算所有AB Build
            var abBuilds = JenkinsTools.CalculateAssetBundleBuilds(RES_ROOT);
            #endregion    

            // 2 生成AB到指定目录
            #region 2 生成AB到指定目录
            string abdir = JenkinsTools.GetStreamAssetsRes();
            // 构建所有ab
            AssetBundleManifest manifest = JenkinsTools.BuildAllAssetBundles(abdir, abBuilds);
            #endregion

            #region 3 压缩加密AB
            // 不参与压缩的bundle
            string[] excludes = {"hall_ui_updater"};
            isOK = JenkinsTools.CompressOrEncryptBundles(abdir, manifest, excludes);
            if (!isOK) break;
            #endregion

            // #region 4 拷贝AB到远程资源目录
            // string[] filterExts = {".meta", ".manifest"};
            // isOK = JenkinsTools.CopyBundlesToRemoteAssets(abdir, REMOTE_ASSETS, manifest);
            // EditorUtility.ClearProgressBar();
            // if (!isOK) break;

            // #endregion

            #region 5 计算Res Bundle依赖
            JenkinsTools.GenerateDeps(manifest, abdir);
            // JenkinsTools.GenerateManifest(manifest, modulesVersion, abdir, excludes, false);
            #endregion

            #region 6 清理StreamingAssets_XXX 下.manifest和.manifest.meta
            // 需要删除的后缀
            string[] deletedSuffix = {".manifest", ".manifest.meta"}; 
            isOK = JenkinsTools.ClearFilesWithSuffixInDirectory(abdir, deletedSuffix);
            if (!isOK) break;

            // 删除Manifest Bundle 和 meta
            isOK = JenkinsTools.ClearManifestBundle(abdir, "res");
            if (!isOK) break;
            #endregion

            #region 构建lua
            BuildLuaScript();
            #endregion

            #region 生成manifest
            JenkinsTools.GenerateManifest(platformRoot, modulesVersion, excludes, false);
            #endregion

            #region 7 从bundle平台目录拷贝至 Application.streamingAssetsPath + "/res"
            JenkinsTools.ClearDirectory(Application.streamingAssetsPath);
            JenkinsTools.CopyDirectory(platformRoot, Application.streamingAssetsPath, true, "*.*");
            #endregion

            AssetDatabase.Refresh();
        } while (false);

        if (!isOK) 
        {
            Debug.LogError("构建patch失败!!!");
        }
    }


    public void Awake()
    {
        Debug.Log("JenkinsWindow Awake");
    }

    // public void Update()
    // {
    //     Debug.Log("JenkinsWindow Update");
    // }

    public void OnGUI()
    {
        // Debug.Log("JenkinsWindow OnGUI");
    }

    public void CreateGUI()
    {
        Debug.Log("JenkinsWindow CreateGUI");

        // float toolbarWidth = position.width - 15 * 4 - 20;
        // string[] labels = new string[3] { "Configure", "Build", "Inspect" };
        // m_Mode = (Mode)GUILayout.Toolbar((int)m_Mode, labels, "LargeButton", GUILayout.Width(toolbarWidth) );

        // Create a toggle and register callback
        var m_MyToggle = new Toggle("Test Toggle") { name = "My Toggle" };
        rootVisualElement.Add(m_MyToggle);



        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Get a list of all sprites in the project
        var allObjectGuids = AssetDatabase.FindAssets("t:Sprite");
        var allObjects = new List<Sprite>();
        foreach (var guid in allObjectGuids)
        {
            allObjects.Add(AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        // Create a two-pane view with the left pane being fixed with
        var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);

        // Add the view to the visual tree by adding it as a child to the root element
        rootVisualElement.Add(splitView);

        // A TwoPaneSplitView always needs exactly two child elements
        var leftPane = new ListView();
        leftPane.style.flexDirection = FlexDirection.Row;
        splitView.Add(leftPane);

        // Initialize the list view with all sprites' names
        leftPane.makeItem = () => new Label();
        leftPane.bindItem = (item, index) => { (item as Label).text = allObjects[index].name; };
        leftPane.itemsSource = allObjects;
        // React to the user's selection
        leftPane.selectionChanged += OnSpriteSelectionChange;
        m_RightPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal); //new VisualElement();
        splitView.Add(m_RightPane);

        // // VisualElements objects can contain other VisualElement following a tree hierarchy.
        // VisualElement label = new Label("Hello World! From C#");
        // root.Add(label);

        var newButton = new Button() { text = "Click me!" };
        rootVisualElement.Add(newButton);
    }

    private void OnSpriteSelectionChange(IEnumerable<object> selectedItems)
    {
        // Clear all previous content from the pane
        m_RightPane.Clear();

        // Get the selected sprite
        var selectedSprite = selectedItems.First() as Sprite;
        if (selectedSprite == null)
            return;

        // Add a new Image control and display the sprite
        var spriteImage = new Image();
        spriteImage.scaleMode = ScaleMode.ScaleToFit;
        spriteImage.sprite = selectedSprite;

        // Add the Image control to the right-hand pane
        m_RightPane.Add(spriteImage);
    }
}
