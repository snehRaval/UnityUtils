using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class UnusedAssetFinder : EditorWindow
{
    private string targetFolder = "Assets/";
    private List<string> unusedAssets = new List<string>();
    private Vector2 scroll;

    [MenuItem("Sneh/Assets Finder/Find Unused Assets (With Folder Filter)")]
    public static void ShowWindow()
    {
        GetWindow<UnusedAssetFinder>("Unused Asset Finder");
    }

    private void OnGUI()
    {
        GUILayout.Label("🔍 Unused Asset Scanner", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Target Folder:", GUILayout.Width(90));
        targetFolder = EditorGUILayout.TextField(targetFolder);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Scan for Unused Assets"))
        {
            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                Debug.LogWarning("Invalid folder path. Must start with 'Assets/'.");
            }
            else
            {
                FindUnusedAssets();
            }
        }

        GUILayout.Space(10);
        GUILayout.Label($"Found {unusedAssets.Count} unused asset(s):", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (var asset in unusedAssets)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(asset);
            if (GUILayout.Button("Ping", GUILayout.Width(50)))
            {
                var obj = AssetDatabase.LoadAssetAtPath<Object>(asset);
                EditorGUIUtility.PingObject(obj);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void FindUnusedAssets()
    {
        unusedAssets.Clear();

        // 1. Get all assets in target folder (excluding .cs files, folders)
        string[] allAssetPaths = Directory.GetFiles(targetFolder, "*.*", SearchOption.AllDirectories)
            .Where(path =>
                !path.EndsWith(".meta") &&
                !path.EndsWith(".cs") &&
                !Directory.Exists(path))
            .Select(p => p.Replace('\\', '/')) // normalize slashes
            .ToArray();

        HashSet<string> allAssetsInFolder = new HashSet<string>(allAssetPaths);

        // 2. Get dependencies from all scenes and prefabs
        string[] scenePaths = AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        HashSet<string> usedAssets = new HashSet<string>();

        foreach (string path in scenePaths.Concat(prefabPaths))
        {
            string[] deps = AssetDatabase.GetDependencies(path, true);
            foreach (string dep in deps)
            {
                usedAssets.Add(dep);
            }
        }

        // 3. Find unused assets in the target folder
        foreach (var asset in allAssetsInFolder)
        {
            if (!usedAssets.Contains(asset))
            {
                if (IsTargetAsset(asset)) // optional: limit to images, fonts, etc.
                {
                    unusedAssets.Add(asset);
                }
            }
        }

        Debug.Log($"✅ Scan complete. Found {unusedAssets.Count} unused assets in: {targetFolder}");
    }

    private bool IsTargetAsset(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" ||
               ext == ".tga" || ext == ".bmp" || ext == ".psd" ||
               ext == ".ttf" || ext == ".otf" || ext == ".fontsettings" ||
               ext == ".mat" || ext == ".prefab" || ext == ".asset";
    }
}
