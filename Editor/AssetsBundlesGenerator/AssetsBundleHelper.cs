using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Linq;

/// <summary>
/// Unity Editor window for creating and managing asset bundles for games.
/// Allows specifying Game IDs, selecting the mpla-generator.jar, and building bundles for Android/iOS.
/// Stores settings as a ScriptableObject asset.
/// </summary>
public class AssetsBundleHelperWindow : EditorWindow
{
    private string gameIDs = "";
    private string mplaJarPath = null;
    private string errorMsg = "";
    private const string GameNamePrefsKey = "AssetsBundleHelper_GameID_";
    // Path to the settings asset
    private static string SettingsAssetPath = "Assets/Editor/AssetsBundlesGenerator/AssetsBundleHelperSettings.asset";
    private AssetsBundleHelperSettings settingsAsset;


    /// <summary>
    /// Shows the AssetsBundleHelperWindow editor window from the Unity menu.
    /// </summary>
    [MenuItem("Sneh/AssetsBundle Creator")]
    public static void ShowWindow()
    {
        GetWindow<AssetsBundleHelperWindow>("Assets Bundle Helper");
    }

    /// <summary>
    /// Loads settings asset and initializes fields when the window is enabled.
    /// </summary>
    void OnEnable()
    {
        LoadOrCreateSettingsAsset();
        if (settingsAsset != null)
        {
            gameIDs = settingsAsset.gameIDs;
        }
        if (string.IsNullOrEmpty(mplaJarPath))
            mplaJarPath = GetJarPath();
    }

    /// <summary>
    /// Draws the main UI for asset bundle creation, including Game ID input, jar selection, and build buttons.
    /// </summary>
    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("MPLA Bundle Creator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");
        try
        {
            GUILayout.Label("Game ID(s) (required, comma-separated for multiple):", EditorStyles.label);
            string newGameIDs = EditorGUILayout.TextField(gameIDs);
            // Filter: allow only numbers and commas
            string filteredGameIDs = System.Text.RegularExpressions.Regex.Replace(newGameIDs, "[^0-9,]", "");
            if (newGameIDs != filteredGameIDs)
                EditorGUILayout.HelpBox("Only numbers and commas are allowed in Game ID(s).", MessageType.Warning);

            if (string.IsNullOrWhiteSpace(filteredGameIDs))
                EditorGUILayout.HelpBox("Game ID is required.", MessageType.Error);

            if (gameIDs != filteredGameIDs)
            {
                gameIDs = filteredGameIDs;
                SaveGameIDsToAsset();
            }
        }
        finally
        {
            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(8);
        EditorGUILayout.BeginVertical("box");
        try
        {
            GUILayout.Label("mpla-generator.jar (optional):", EditorStyles.label);
            EditorGUILayout.BeginHorizontal();
            mplaJarPath = EditorGUILayout.TextField(mplaJarPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string file = EditorUtility.OpenFilePanel("Select mpla-generator.jar", Application.dataPath, "jar");
                if (!string.IsNullOrEmpty(file))
                    mplaJarPath = file;
            }
            EditorGUILayout.EndHorizontal();
        }
        finally
        {
            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(15);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.enabled = !string.IsNullOrWhiteSpace(gameIDs);
        if (GUILayout.Button("Create Android Bundle", GUILayout.Width(180), GUILayout.Height(32)))
        {
            errorMsg = string.Empty;
            CreateBundle(BuildTarget.Android);
        }
        GUILayout.Space(10);
        if (GUILayout.Button("Create iOS Bundle", GUILayout.Width(180), GUILayout.Height(32)))
        {
            errorMsg = string.Empty;
            CreateBundle(BuildTarget.iOS);
        }
        GUI.enabled = true;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(errorMsg))
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(errorMsg, MessageType.Error);
        }
    }

    /// <summary>
    /// Creates asset bundles for the specified build target (Android/iOS), runs mpla-generator.jar, and handles output.
    /// </summary>
    /// <param name="target">The build target (Android or iOS).</param>
    void CreateBundle(BuildTarget target)
    {
        if (string.IsNullOrWhiteSpace(gameIDs))
        {
            errorMsg = "Game ID is required.";
            return;
        }

        // Check if any assets have AssetBundle assignments
        if (!HasAssetBundleAssignments())
        {
            EditorUtility.DisplayDialog("No Asset Bundles", "No assets have been assigned to Asset Bundles. Please assign assets to bundles first.", "OK");
            return;
        }

        // Find mpla-generator.jar if not provided
        string jarPath = mplaJarPath;
        if (string.IsNullOrEmpty(jarPath))
        {
           jarPath =  GetJarPath();
        }
        if (string.IsNullOrEmpty(jarPath) || !File.Exists(jarPath))
        {
            errorMsg = "mpla-generator.jar not found. Please select it manually.";
            return;
        }

        // Set build target
        EditorUserBuildSettings.SwitchActiveBuildTarget(target == BuildTarget.Android ? BuildTargetGroup.Android : BuildTargetGroup.iOS, target);

        string srcPath = Path.Combine(Directory.GetCurrentDirectory(), "AssetBundles", target.ToString());
        if (!Directory.Exists(srcPath))
            Directory.CreateDirectory(srcPath);

        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(srcPath, BuildAssetBundleOptions.None, target);
        if (!Directory.Exists(srcPath))
        {
            errorMsg = "Build Asset Bundles first.";
            return;
        }

        string outputDirPath = Path.Combine(Directory.GetCurrentDirectory(), "AssetBundles", $"{target}_AB");
        if (Directory.Exists(outputDirPath))
            Directory.Delete(outputDirPath, true);

        string[] gameIDArray = gameIDs.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string gameID in gameIDArray)
        {
            string trimmedGameID = gameID.Trim();
            if (string.IsNullOrEmpty(trimmedGameID))
                continue;

            string abFolder = CreateAndPrepareFolder(outputDirPath, trimmedGameID);
            if (abFolder == null)
            {
                errorMsg = $"Unable to create folder for game ID {trimmedGameID}";
                return;
            }

            if (!CopyFiles(srcPath, abFolder, trimmedGameID))
            {
                errorMsg = "Failed to copy files.";
                return;
            }

            if (!MakeZip(outputDirPath, trimmedGameID))
            {
                errorMsg = "Failed to create zip.";
                return;
            }

            if (target == BuildTarget.Android)
            {
                if (!RunCommand(outputDirPath, trimmedGameID, jarPath))
                {
                    errorMsg = "Failed to run mpla-generator.jar.";
                    return;
                }
                if (!RenameFile(outputDirPath, trimmedGameID))
                {
                    errorMsg = "Failed to rename .mpla file.";
                    return;
                }
                if (!RemoveFilesAndFolder(outputDirPath, trimmedGameID))
                {
                    errorMsg = "Failed to clean up files.";
                    return;
                }
            }
            else if (target == BuildTarget.iOS)
            {
                if (!RemoveFilesAndFolder(outputDirPath, trimmedGameID, false))
                {
                    errorMsg = "Failed to clean up files.";
                    return;
                }
            }
        }

        EditorUtility.DisplayDialog("Success", $"Bundle created for {target} at {outputDirPath}", "OK");
        EditorUtility.RevealInFinder(outputDirPath);
    }

    /// <summary>
    /// Checks if any assets in the project have AssetBundle assignments.
    /// </summary>
    /// <returns>True if at least one asset has an AssetBundle name assigned.</returns>
    bool HasAssetBundleAssignments()
    {
        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
        foreach (string assetPath in allAssetPaths)
        {
            if (assetPath.StartsWith("Assets/"))
            {
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Creates and prepares the output folder for a given Game ID.
    /// </summary>
    /// <param name="outputDirPath">The base output directory.</param>
    /// <param name="gameID">The Game ID.</param>
    /// <returns>The created directory path, or null on failure.</returns>
    static string CreateAndPrepareFolder(string outputDirPath, string gameID)
    {
        string dirPath = Path.Combine(outputDirPath, gameID);
        try
        {
            if (Directory.Exists(dirPath))
                Directory.Delete(dirPath, true);
            Directory.CreateDirectory(dirPath);
            return dirPath;
        }
        catch (Exception e)
        {
            Debug.LogError($"ERR: Exception in CreateAndPrepareFolder : {e.Message}");
            return null;
        }
    }
    /// <summary>
    /// Copies built asset bundle files to the output directory for a given Game ID.
    /// </summary>
    /// <param name="source">Source directory.</param>
    /// <param name="dest">Destination directory.</param>
    /// <param name="gameID">The Game ID.</param>
    /// <returns>True on success, false on failure.</returns>
    static bool CopyFiles(string source, string dest, string gameID)
    {
        try
        {
            string[] files = Directory.GetFiles(source);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(dest, fileName);
                if (!file.Contains("preview_assets_"))
                {
                    File.Copy(file, destFile, true);
                }
                else if (file.Contains("preview_assets_" + gameID))
                {
                    string newFileName = Path.Combine(dest, fileName.Replace($"_{gameID}", ""));
                    File.Copy(file, newFileName, true);
                }
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"ERR: Exception in CopyFiles : {e.Message}");
            return false;
        }
    }
    /// <summary>
    /// Creates a zip archive of the asset bundle output for a given Game ID.
    /// </summary>
    /// <param name="outputDirPath">The output directory.</param>
    /// <param name="gameID">The Game ID.</param>
    /// <returns>True on success, false on failure.</returns>
    static bool MakeZip(string outputDirPath, string gameID)
    {
        try
        {
            string zipFileName = gameID + ".zip";
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "zip";
            psi.Arguments = $"-vr {zipFileName} {gameID}/ ";
            psi.WorkingDirectory = outputDirPath;
            psi.UseShellExecute = false;
            Process process = new Process();
            process.StartInfo = psi;
            process.Start();
            process.WaitForExit();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"ERR: Exception in MakeZip : {e.Message}");
            return false;
        }
    }
    /// <summary>
    /// Runs the mpla-generator.jar tool to process the asset bundle for a given Game ID.
    /// </summary>
    /// <param name="outputDirPath">The output directory.</param>
    /// <param name="gameID">The Game ID.</param>
    /// <param name="jarfilePath">Path to the mpla-generator.jar file.</param>
    /// <returns>True on success, false on failure.</returns>
    static bool RunCommand(string outputDirPath, string gameID, string jarfilePath)
    {
        try
        {
            string filename = $"{gameID}.zip";
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-jar \"{jarfilePath}\" {gameID} {filename} ",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = outputDirPath
            };
            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    Debug.Log($"Command output: {output}");
                    return true;
                }
                else
                {
                    Debug.Log($"Command error: {error}");
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log($"Error: {e.Message}");
            return false;
        }
    }
    /// <summary>
    /// Renames the generated .mpla file for Android builds.
    /// </summary>
    /// <param name="outputDirPath">The output directory.</param>
    /// <param name="gameID">The Game ID.</param>
    /// <returns>True on success, false on failure.</returns>
    static bool RenameFile(string outputDirPath, string gameID)
    {
        try
        {
            string filename = Path.Combine(outputDirPath, gameID);
            string mplaFileName = $"{filename}.mpla";
            if (File.Exists(mplaFileName))
            {
                File.Move(mplaFileName, $"{filename}.android.u19.mpla");
                return true;
            }
            else
            {
                Debug.LogError($"ERR: Exception in RenameFile {mplaFileName} not Found");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"ERR: Exception in RenameFile : {e.Message}");
            return false;
        }
    }
    /// <summary>
    /// Removes temporary files and folders after bundle creation.
    /// </summary>
    /// <param name="outputDirPath">The output directory.</param>
    /// <param name="gameID">The Game ID.</param>
    /// <param name="RemoveZip">Whether to remove the zip file as well.</param>
    /// <returns>True on success, false on failure.</returns>
    static bool RemoveFilesAndFolder(string outputDirPath, string gameID, bool RemoveZip = true)
    {
        try
        {
            string dirPath = Path.Combine(outputDirPath, gameID);
            Directory.Delete(dirPath, true);
            if (RemoveZip)
            {
                string zipFilePath = dirPath + ".zip";
                File.Delete(zipFilePath);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"ERR: Exception in RemoveFile : {e.Message}");
            return false;
        }
    }
    /// <summary>
    /// Gets the current file path using the caller file path attribute.
    /// </summary>
    static string GetCurrentFilePath([System.Runtime.CompilerServices.CallerFilePath] string path = "")
    {
        return path;
    }

    /// <summary>
    /// Loads or creates the ScriptableObject settings asset for storing Game IDs.
    /// </summary>
    void LoadOrCreateSettingsAsset()
    {
        settingsAsset = AssetDatabase.LoadAssetAtPath<AssetsBundleHelperSettings>(SettingsAssetPath);
        if (settingsAsset == null)
        {
            // Create the asset if it doesn't exist
            settingsAsset = ScriptableObject.CreateInstance<AssetsBundleHelperSettings>();
            AssetDatabase.CreateAsset(settingsAsset, SettingsAssetPath);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// Saves the current Game IDs to the ScriptableObject settings asset.
    /// </summary>
    void SaveGameIDsToAsset()
    {
        if (settingsAsset != null)
        {
            settingsAsset.gameIDs = gameIDs;
            EditorUtility.SetDirty(settingsAsset);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// Attempts to find the mpla-generator.jar file in the Editor folder.
    /// </summary>
    /// <returns>Path to the jar file, or null if not found.</returns>
    string GetJarPath()
    {
        string scriptPath = GetCurrentFilePath();
        string editorDir = Path.GetDirectoryName(scriptPath);
        string[] jars = Directory.GetFiles(editorDir, "mpla-generator.jar", SearchOption.TopDirectoryOnly);
        if (jars.Length > 0)
            return jars[0];
        return null;
    }
}
