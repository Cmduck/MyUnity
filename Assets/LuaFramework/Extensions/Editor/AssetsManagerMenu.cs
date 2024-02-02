using UnityEditor;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using Extension;
using static Extension.Manifest;

public class AssetsManagerMenu
{
    public static string WritablePath { get { return Application.persistentDataPath + "/remote-hall/"; } }
    public static string DataPath { get { return Application.persistentDataPath + "/remote-hall/"; } }
    public static string TempDataPath { get { return Application.persistentDataPath + "/down_res/temp/"; } }
    public static string AppContentPath { get { return Application.dataPath + "/Raw/data/"; } }
    public static string AppContentPathURL { get { return "file://" + Application.dataPath + "/Raw/data/"; } }

    [MenuItem("UnitTest/Test Manifest")]
    static void ManifestUnitTest()
    {
        Manifest manifestA = new Manifest();
        Manifest manifestB = new Manifest();

        var pathA = Path.Combine(Application.dataPath, "Resources/projectA.manifest");
        var pathB = Path.Combine(Application.dataPath, "Resources/projectB.manifest");

        manifestA.parseFile(pathA);
        manifestB.parseFile(pathB);

        bool vGreater = manifestA.versionGreater(manifestB, null);
        Debug.Log("versionGreater:" + vGreater);

        bool vGreaterOrEquals = manifestA.versionGreaterOrEquals(manifestB, null);
        Debug.Log("vGreaterOrEquals:" + vGreaterOrEquals);

        bool vEquals = manifestA.versionEquals(manifestB);
        Debug.Log("vEquals:" + vEquals);

        var diffMap = manifestA.genDiff(manifestB);

        foreach(KeyValuePair<string, Manifest.AssetDiff> pair in diffMap)
        {
            if (pair.Value.type == DiffType.ADDED)
            {
                Debug.Log("ADDED Asset:" + pair.Key);
            }
            else if (pair.Value.type == DiffType.DELETED)
            {
                Debug.Log("DELETED Asset:" + pair.Key);
            }
            else if (pair.Value.type == DiffType.MODIFIED)
            {
                Debug.Log("MODIFIED Asset:" + pair.Key);
            }
        }

        // 测试 saveToFile
        

        manifestA.saveToFile(Path.Combine(WritablePath, "projectA.manifest"));
        manifestB.saveToFile(Path.Combine(WritablePath, "projectB.manifest"));
    }

    [MenuItem("UnitTest/Test IdGenerator")]
    static void IdGeneratorUnitTest()
    {
        IdGenerator generator = new IdGenerator();

        List<int> ints = new List<int>();

        for (var i = 0; i < 5; i++)
        {
            int id = generator.generateId();
            ints.Add(id);

            Debug.Log("生成ID: " + id);

            generator.dump();
        }

        Debug.Log("回收ID: 1");
        generator.recycleId(1);
        generator.dump();

        Debug.Log("回收ID: 5");
        generator.recycleId(5);
        generator.dump();
    }
}
