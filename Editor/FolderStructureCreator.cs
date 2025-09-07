using UnityEditor;
using UnityEngine;
using System.IO;
using Debug = UnityEngine.Debug;

/// <summary>
/// Unity Editor script to create a standardized folder structure for game development.
/// Creates Scripts, StreamingAssets, and AssetsBundles with subfolders for different asset types.
/// </summary>
public class FolderStructureCreator : EditorWindow
{
    private bool createScripts = true;
    private bool createStreamingAssets = true;
    private bool createAssetsBundles = true;
    private bool createAllSubfolders = true;
    
    // Define the folder structure
    private readonly string[] foldersToCreate = {
        "Scripts",
        "StreamingAssets",
        "AssetsBundles/Sprites",
        "AssetsBundles/Animation", 
        "AssetsBundles/SFX",
        "AssetsBundles/VFX",
        "AssetsBundles/Prefabs",
        "AssetsBundles/Scenes"
    };

    /// <summary>
    /// Shows the FolderStructureCreator editor window from the Unity menu.
    /// </summary>
    [MenuItem("Sneh/Create Folder Structure", false, 1)]
    public static void ShowWindow()
    {
        GetWindow<FolderStructureCreator>("Folder Structure Creator");
    }

    /// <summary>
    /// Draws the main UI for folder structure creation with options and feedback.
    /// </summary>
    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Create Standardized Folder Structure", MessageType.None);
        EditorGUILayout.Space();

        // Options with current structure status
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Select folders to create:", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Scripts folder
        EditorGUILayout.BeginHorizontal();
        createScripts = EditorGUILayout.Toggle(createScripts, GUILayout.Width(20));
        EditorGUILayout.LabelField("Scripts", GUILayout.Width(120));
        ShowFolderStatus("Scripts");
        EditorGUILayout.EndHorizontal();
        
        // StreamingAssets folder
        EditorGUILayout.BeginHorizontal();
        createStreamingAssets = EditorGUILayout.Toggle(createStreamingAssets, GUILayout.Width(20));
        EditorGUILayout.LabelField("StreamingAssets", GUILayout.Width(120));
        ShowFolderStatus("StreamingAssets");
        EditorGUILayout.EndHorizontal();
        
        // AssetsBundles main folder
        EditorGUILayout.BeginHorizontal();
        createAssetsBundles = EditorGUILayout.Toggle(createAssetsBundles, GUILayout.Width(20));
        EditorGUILayout.LabelField("AssetsBundles", GUILayout.Width(120));
        ShowFolderStatus("AssetsBundles");
        EditorGUILayout.EndHorizontal();
        
        // AssetsBundles subfolders
        EditorGUILayout.BeginHorizontal();
        createAllSubfolders = EditorGUILayout.Toggle(createAllSubfolders, GUILayout.Width(20));
        EditorGUILayout.LabelField("AssetsBundles sub-Dirs:", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();
        
        // Show subfolders with indentation
        if (createAssetsBundles)
        {
            EditorGUI.indentLevel++;
            ShowSubfolderStatus("AssetsBundles/Sprites");
            ShowSubfolderStatus("AssetsBundles/Animation");
            ShowSubfolderStatus("AssetsBundles/SFX");
            ShowSubfolderStatus("AssetsBundles/VFX");
            ShowSubfolderStatus("AssetsBundles/Prefabs");
            ShowSubfolderStatus("AssetsBundles/Scenes");
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
        
        // Create button
        EditorGUI.BeginDisabledGroup(!createScripts && !createStreamingAssets && !createAssetsBundles);
        if (GUILayout.Button("Create Folder Structure", GUILayout.Height(30)))
        {
            CreateFolderStructure();
        }
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>
    /// Shows the status of a folder next to its checkbox.
    /// </summary>
    /// <param name="folderPath">The folder path to check.</param>
    private void ShowFolderStatus(string folderPath)
    {
        string fullPath = Path.Combine("Assets", folderPath);
        bool exists = Directory.Exists(fullPath);
        
        // Color coding: green for exists, red for missing
        Color originalColor = GUI.color;
        GUI.color = exists ? Color.green : Color.red;
        
        string status = exists ? "✓ Exists" : "✗ Missing";
        GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
        statusStyle.fontStyle = FontStyle.Bold;
        
        EditorGUILayout.LabelField(status, statusStyle);
        
        GUI.color = originalColor;
    }

    /// <summary>
    /// Shows the status of a subfolder with proper indentation.
    /// </summary>
    /// <param name="folderPath">The subfolder path to check.</param>
    private void ShowSubfolderStatus(string folderPath)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("", GUILayout.Width(20)); // Extra indentation
        EditorGUILayout.LabelField("└─ " + folderPath.Substring(folderPath.LastIndexOf('/') + 1), GUILayout.Width(120));
        ShowFolderStatus(folderPath);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Creates the folder structure based on user selections.
    /// </summary>
    public void CreateFolderStructure()
    {
        try
        { 
            Debug.Log("FolderStructureCreator: 9 CreateFolderStructure()");

            string assetsPath = "Assets";
            int createdCount = 0;
            int skippedCount = 0;

            foreach (string folder in foldersToCreate)
            {
                // Skip based on user selections
                if (!ShouldCreateFolder(folder))
                    continue;

                string fullPath = Path.Combine(assetsPath, folder);
                
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    createdCount++;
                    Debug.Log($"Created folder: {fullPath}");
                }
                else
                {
                    skippedCount++;
                    Debug.Log($"Folder already exists: {fullPath}");
                }
            }

            // Refresh AssetDatabase to show new folders
            AssetDatabase.Refresh();

            // Show result
            string message = $"Folder structure creation completed!\n" +
                           $"Created: {createdCount} folders\n" +
                           $"Skipped: {skippedCount} existing folders";
            
            EditorUtility.DisplayDialog("Folder Structure Created", message, "OK");
            
            Debug.Log($"Folder Structure Creator: {message}");
        }
        catch (System.Exception ex)
        {
            string errorMessage = $"Failed to create folder structure: {ex.Message}";
            EditorUtility.DisplayDialog("Error", errorMessage, "OK");
            Debug.LogError($"Folder Structure Creator: {errorMessage}");
        }
    }

    /// <summary>
    /// Determines if a folder should be created based on user selections.
    /// </summary>
    /// <param name="folderPath">The folder path to check.</param>
    /// <returns>True if the folder should be created, false otherwise.</returns>
    private bool ShouldCreateFolder(string folderPath)
    {
        if (folderPath == "Scripts")
            return createScripts;
        
        if (folderPath == "StreamingAssets")
            return createStreamingAssets;
        
        if (folderPath.StartsWith("AssetsBundles/"))
            return createAssetsBundles && createAllSubfolders;
        
        if (folderPath == "AssetsBundles")
            return createAssetsBundles;
        
        return true; // Default to creating if not specified
    }

    /// <summary>
    /// Shows the current folder structure in the UI.
    /// </summary>
    private void ShowCurrentStructure()
    {
        string assetsPath = "Assets";
        
        foreach (string folder in foldersToCreate)
        {
            string fullPath = Path.Combine(assetsPath, folder);
            bool exists = Directory.Exists(fullPath);
            
            // Color coding: green for exists, red for missing
            Color originalColor = GUI.color;
            GUI.color = exists ? Color.green : Color.red;
            
            string status = exists ? "✓" : "✗";
            string displayName = folder;
            
            // Indent subfolders for better readability
            if (folder.Contains("/"))
            {
                displayName = "  └─ " + folder.Substring(folder.LastIndexOf('/') + 1);
            }
            
            EditorGUILayout.LabelField($"{status} {displayName}", EditorStyles.miniLabel);
            
            GUI.color = originalColor;
        }
    }

    /// <summary>
    /// Creates the folder structure programmatically without UI.
    /// Can be called from other scripts.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool CreateFolderStructureProgrammatically()
    {
        try
        {
            string[] foldersToCreate = {
                "Scripts",
                "StreamingAssets", 
                "AssetsBundles/Sprites",
                "AssetsBundles/Animation",
                "AssetsBundles/SFX",
                "AssetsBundles/VFX",
                "AssetsBundles/Prefabs",
                "AssetsBundles/Scenes"
            };

            string assetsPath = "Assets";
            int createdCount = 0;

            foreach (string folder in foldersToCreate)
            {
                string fullPath = Path.Combine(assetsPath, folder);
                
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    createdCount++;
                    Debug.Log($"Created folder: {fullPath}");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"Folder Structure Creator: Created {createdCount} folders");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Folder Structure Creator: Failed to create folder structure: {ex.Message}");
            return false;
        }
    }
} 