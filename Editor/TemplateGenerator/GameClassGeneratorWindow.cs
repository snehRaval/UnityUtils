using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEditor.Events;
using System.Reflection;

/// <summary>
/// Unity Editor window for generating game classes and scene setup from template files.
/// Supports class generation, prefab setup, and scene creation for new games using a template library.
/// </summary>
public class GameClassGeneratorWindow : EditorWindow
{
    private string gameName = "";
    private string libraryPath = "Assets/Submodule/"; // Default library path
    private string outputFolderPath = "Assets/AssetsBundles"; // Default output folder path
    private Vector2 scrollPosition;
    private bool showSuccessMessage = false;
    private float messageTimer = 0f;
    private List<string> templateFiles;
    private bool templatesFound = false;
    private bool canCreateScene = false;
    private bool isPortrait = false;

    // Library structure paths
    private string LibrarySamplesPath => Path.Combine(libraryPath, "Sample");
    private string LibraryPrefabsPath => Path.Combine(libraryPath, "Prefabs");

  
    // Add ScriptableObject to persist data across recompilation
    private class PendingOperation : ScriptableObject
    {
        public bool shouldCreateScene;
        public string gameName;
        public bool isPortrait;
    }

    private static string PendingOperationPath = "Assets/Editor/GameGeneratorPendingOp.asset";

    /// <summary>
    /// Shows the GameClassGeneratorWindow editor window from the Unity menu.
    /// </summary>
    [MenuItem("Sneh/Template Generator", false, 3)]
    public static void ShowWindow()
    {
        GetWindow<GameClassGeneratorWindow>("Game Class Generator");
    }

    #region UI
    /// <summary>
    /// Draws the main UI for class generation, template selection, and scene setup.
    /// </summary>
    private void OnGUI()
    {
        GUILayout.Label("Game Class Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Library folder selection
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Library Path:", GUILayout.Width(100));
        EditorGUILayout.TextField(libraryPath, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Library Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                libraryPath = path;
                ValidateLibraryPath();
            }
        }

        EditorGUILayout.EndHorizontal();

        // Output folder selection
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Output Folder:", GUILayout.Width(100));
        EditorGUILayout.TextField(outputFolderPath, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Output Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                outputFolderPath = path;
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Orientation selection
        isPortrait = EditorGUILayout.Toggle("Portrait Orientation:", isPortrait);

        // Template files status
        if (!string.IsNullOrEmpty(libraryPath))
        {
            EditorGUILayout.LabelField("Template Files Status:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));

            foreach (string file in templateFiles)
            {
                bool exists = File.Exists(Path.Combine(LibrarySamplesPath, file));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(file);
                EditorGUILayout.LabelField(exists ? "✓" : "✗", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space();

        // Game name input
        gameName = EditorGUILayout.TextField("Game Name:", gameName);

        EditorGUILayout.Space(10);

        // Generate Classes button
        GUI.enabled = templatesFound && !string.IsNullOrEmpty(gameName) && !string.IsNullOrEmpty(outputFolderPath);
        if (GUILayout.Button("Generate Classes"))
        {
            GenerateClasses();
        }

        GUI.enabled = true;

        // Create Scene Setup button
        EditorGUILayout.Space(10);
        GUI.enabled = canCreateScene;
        if (GUILayout.Button("Create Scene Setup"))
        {
            CreateAndAssignGameManagerPrefab(gameName, isPortrait);
            // CreateGameManagerScene(gameName);
        }

        GUI.enabled = true;

        if (!canCreateScene)
        {
            EditorGUILayout.HelpBox("Generate classes first to enable scene creation.", MessageType.Info);
        }

        if (showSuccessMessage)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"Classes generated successfully with prefix '{gameName}'!", MessageType.Info);
        }

        // Show warnings or errors
        if (!templatesFound && !string.IsNullOrEmpty(libraryPath))
        {
            EditorGUILayout.HelpBox("Not all template files were found in the library Samples folder!",
                MessageType.Warning);
        }

        if (string.IsNullOrEmpty(gameName))
        {
            EditorGUILayout.HelpBox("Please enter a game name!", MessageType.Warning);
        }

        if (string.IsNullOrEmpty(outputFolderPath))
        {
            EditorGUILayout.HelpBox("Please select an output folder!", MessageType.Warning);
        }
    }

    /// <summary>
    /// Handles the success message timer for UI feedback.
    /// </summary>
    private void Update()
    {
        if (showSuccessMessage)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0)
            {
                showSuccessMessage = false;
                Repaint();
            }
        }
    }

    #endregion

    #region Core
    /// <summary>
    /// Initializes template file list and checks for pending operations or existing generated classes.
    /// </summary>
    private void OnEnable()
    {
        templateFiles = new List<string>
        {
            "SampleGameManager.cs",
            "SamplePopUp.cs",
            "SampleSessionInfo.cs",
            "SampleSignupPayload.cs",
            "SampleSocketHandler.cs",
            "SampleTable.cs",
            "SampleTableDataHandler.cs",
            "SampleEvents.cs"
        };

        // Check for pending operation
        var pendingOp = AssetDatabase.LoadAssetAtPath<PendingOperation>(PendingOperationPath);
        if (pendingOp != null && pendingOp.shouldCreateScene)
        {
            canCreateScene = true;
            gameName = pendingOp.gameName;
            isPortrait = pendingOp.isPortrait;
            // Clear the pending operation
            AssetDatabase.DeleteAsset(PendingOperationPath);
        }
        else
        {
            // Check if classes have already been generated by looking for generated types
            CheckIfClassesExist();
        }

        // Validate the default library path to enable the Generate Classes button
        ValidateLibraryPath();
    }

    /// <summary>
    /// Generates new game classes from template files, replacing placeholders and updating namespaces.
    /// </summary>
    public void GenerateClasses()
    {
        try
        {
            // Scripts should be stored in Assets/Scripts folder
            string gameScriptsPath = Path.Combine("Assets/Scripts", gameName);
            if (Directory.Exists(gameScriptsPath))
            {
                // Check for old generated files
                List<string> oldFiles = new List<string>();
                foreach (string templateFile in templateFiles)
                {
                    string newFileName = templateFile.Replace("Sample", gameName);
                    string newFilePath = Path.Combine(gameScriptsPath, newFileName);
                    if (File.Exists(newFilePath))
                    {
                        oldFiles.Add(newFileName);
                    }
                }

                if (oldFiles.Count > 0)
                {
                    string message =
                        $"The following files already exist and will be replaced:\n\n{string.Join("\n", oldFiles)}\n\nDo you want to remove them before generating new ones?";
                    bool shouldDelete = EditorUtility.DisplayDialog("Old Generated Files Found", message,
                        "Remove and Continue", "Cancel");
                    if (!shouldDelete)
                    {
                        Debug.Log("Generation cancelled by user due to existing files.");
                        return;
                    }

                    // Delete old files
                    foreach (string file in oldFiles)
                    {
                        try
                        {
                            string filePath = Path.Combine(gameScriptsPath, file);
                            File.Delete(filePath);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"Failed to delete {file}: {e.Message}");
                        }
                    }
                    
                    AssetDatabase.Refresh();
                }
            }
            else
                Directory.CreateDirectory(gameScriptsPath);

            // Create and save pending operation
            var pendingOp = ScriptableObject.CreateInstance<PendingOperation>();
            pendingOp.shouldCreateScene = true;
            pendingOp.gameName = gameName;
            pendingOp.isPortrait = isPortrait;

            if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                AssetDatabase.CreateFolder("Assets", "Editor");

            AssetDatabase.CreateAsset(pendingOp, PendingOperationPath);
            AssetDatabase.SaveAssets();

            // Generate classes from Samples folder
            foreach (string templateFile in templateFiles)
            {
                string templatePath = Path.Combine(LibrarySamplesPath, templateFile);
                if (!File.Exists(templatePath))
                {
                    Debug.LogError($"Template file not found: {templatePath}");
                    continue;
                }

                string templateContent = File.ReadAllText(templatePath);

                // Update namespace and SOCKETHANDLER_PATH in GameManager
                string contentWithNewNamespace = UpdateNamespace(templateContent);
                if (templateFile.Contains("GameManager"))
                {
                    contentWithNewNamespace = UpdateSocketHandlerPath(contentWithNewNamespace);
                }

                // Replace "Sample" with the game name
                string newContent = Regex.Replace(contentWithNewNamespace, "Sample", gameName);

                // Generate new filename
                string newFileName = templateFile.Replace("Sample", gameName);
                string newFilePath = Path.Combine(gameScriptsPath, newFileName);

                // Create directories if they don't exist
                Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));

                // Write the new file
                File.WriteAllText(newFilePath, newContent);

                Debug.Log($"Generated: {newFilePath}");
            }

            // Refresh AssetDatabase - this will trigger recompilation
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error generating classes: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to generate classes: {e.Message}", "OK");

            if (File.Exists(PendingOperationPath))
            {
                AssetDatabase.DeleteAsset(PendingOperationPath);
            }
        }
    }

    /// <summary>
    /// Creates and assigns a new GameManager prefab for the generated game, including UI and component setup.
    /// </summary>
    public void CreateAndAssignGameManagerPrefab(string gameName, bool isPortrait)
    {
        try
        {
            string prefabName = "GameManager";

            // 1. Load source prefab from library's Prefabs folder
            string sourcePrefabPath = Path.Combine(LibraryPrefabsPath, $"{prefabName}.prefab");
            int assetsIndex = sourcePrefabPath.IndexOf("Assets/");
            string sourcePrefabPathFromAssetsDir = assetsIndex >= 0 ? sourcePrefabPath.Substring(assetsIndex) : sourcePrefabPath;

            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPathFromAssetsDir);
            if (sourcePrefab == null)
            {
                Debug.LogError($"Source prefab not found: {sourcePrefabPathFromAssetsDir}");
                Debug.LogError($"Please ensure the GameManager.prefab exists in: {LibraryPrefabsPath}");
                return;
            }

            string instanceName = $"{gameName}GameManager.prefab";
            // Remove old instance if exists
            var oldInstance = GameObject.Find(instanceName);
            if (oldInstance != null)
            {
                bool shouldDelete = EditorUtility.DisplayDialog(
                    "Old Instance Found",
                    $"A GameObject named '{instanceName}' already exists in the scene.\n\nDo you want to remove it before creating a new one?",
                    "Remove and Continue",
                    "Cancel"
                );
                if (!shouldDelete)
                {
                    Debug.Log("Operation cancelled by user due to existing instance.");
                    return;
                }

                Object.DestroyImmediate(oldInstance);
            }

            // 2. Instantiate the prefab in the scene (temporarily)
            GameObject instance = (GameObject) PrefabUtility.InstantiatePrefab(sourcePrefab);
            instance.name = instanceName;

            var canvas = instance.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null)
                {
                    scaler.referenceResolution = isPortrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
                    EditorUtility.SetDirty(scaler);
                }
            }

            // 3. Find and replace all SamplePopUp components with new game-specific PopUp components
            ReplaceSamplePopUpComponents(instance, gameName);

            // 4. Replace SampleGameManager with new GameManager class
            ReplaceSampleGameManager(instance, gameName);
            
            
            // 5. Save the modified instance as a new prefab

            SaveAsPrefab(instance, gameName);

            // 6. Clean up
            // Object.DestroyImmediate(instance);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error creating prefab: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to create prefab: {e.Message}", "OK");
        }
    }

    /// <summary>
    /// Replaces SamplePopUp components in the prefab with the generated PopUp class, copying field values.
    /// </summary>
    private void ReplaceSamplePopUpComponents(GameObject rootObject, string gameName)
    {
        var newPopUpType = GetGeneratedType($"{gameName}PopUp");

        if (newPopUpType == null)
        {
            Debug.LogError($"Could not find generated PopUp type for: {gameName}");
            return;
        }

        // Replace all SamplePopUp components and build mapping
        var samplePopUpComponents = rootObject.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var component in samplePopUpComponents)
        {
            if (component != null && component.GetType().Name == "SamplePopUp")
            {
                GameObject gameObject = component.gameObject;

                // Store the references from SamplePopUp before removing it
                var titleField = component.GetType().GetField("title");
                var descriptionField = component.GetType().GetField("description");
                var rectTransformSelfField = component.GetType().GetField("rectTransformSelf");

                object titleValue = titleField?.GetValue(component);
                object descriptionValue = descriptionField?.GetValue(component);
                object rectTransformSelfValue = rectTransformSelfField?.GetValue(component);

                // Remove the old SamplePopUp component
                Object oldPopUp = component;
                Object.DestroyImmediate(component, true);

                // Add the new PopUp component
                var newPopUpComponent = gameObject.AddComponent(newPopUpType);

                // Assign the same references to the new component
                var newTitleField = newPopUpComponent.GetType().GetField("title");
                var newDescriptionField = newPopUpComponent.GetType().GetField("description");
                var newRectTransformSelfField = newPopUpComponent.GetType().GetField("rectTransformSelf");

                if (newTitleField != null && titleValue != null)
                    newTitleField.SetValue(newPopUpComponent, titleValue);
                if (newDescriptionField != null && descriptionValue != null)
                    newDescriptionField.SetValue(newPopUpComponent, descriptionValue);
                if (newRectTransformSelfField != null && rectTransformSelfValue != null)
                    newRectTransformSelfField.SetValue(newPopUpComponent, rectTransformSelfValue);

                Debug.Log($"Replaced SamplePopUp with {gameName}PopUp on {gameObject.name}");
            }
        }

    }

    /// <summary>
    /// Replaces the SampleGameManager component with the generated GameManager class and assigns references.
    /// </summary>
    private void ReplaceSampleGameManager(GameObject rootObject, string gameName)
    {
        // 4. Assign new GameManager script
        var newGameManagerType = GetGeneratedType($"{gameName}GameManager");
        if (newGameManagerType == null)
        {
            Debug.LogError($"Could not find generated GameManager type for: {gameName}");
            return;
        }
        else
        {
            // Remove old GameManager component(s)
            foreach (var oldComp in rootObject.GetComponents<MonoBehaviour>())
            {
                if (oldComp != null && oldComp.GetType().Name.Contains("GameManager"))
                    DestroyImmediate(oldComp, true);
            }

            // Add new GameManager component
            var newGameManager = rootObject.AddComponent(newGameManagerType);

            // Assign references to fields (UI, popups, table, etc.)
            SetFieldValue(newGameManager, "waitScreen", FindChildByName(rootObject, "WaitScreen"));
            SetFieldValue(newGameManager, "reconnectingScreen", FindChildByName(rootObject, "ReconnectingScreen"));
            SetFieldValue(newGameManager, "insufficientFundsToast",FindChildByName(rootObject, "InsufficientFundsToast"));

            string[] popupNames = {"ReconnectionPopup", "ErrorPopup", "TerminatePopup"};
            foreach (string popupName in popupNames)
            {
                GameObject popupObj = FindChildByName(rootObject, popupName);
                var popUpType = GetGeneratedType($"{gameName}PopUp");
                var popUpComponent = popupObj != null ? popupObj.GetComponent(popUpType) : null;
                SetFieldValue(newGameManager, char.ToLowerInvariant(popupName[0]) + popupName.Substring(1),
                    popUpComponent);
            }

            var tableObj = FindChildByName(rootObject, $"TestingTable");
            if (tableObj != null)
            {
                // Find the GameObject that had the table (from the old reference)
                if (tableObj != null)
                {
                    var oldSampleTable = tableObj.GetComponent("SampleTable");
                    if (oldSampleTable != null)
                        Object.DestroyImmediate(oldSampleTable, true);

                    // Add the new generated table class
                    var newTableType = GetGeneratedType($"{gameName}Table");
                    if (newTableType != null)
                    {
                        var newTableComp = tableObj.GetComponent(newTableType) ?? tableObj.AddComponent(newTableType);
                        var newTableField = newGameManager.GetType().GetField("table");
                        if (newTableField != null)
                            newTableField.SetValue(newGameManager, newTableComp);
                    }
                }
            }
            
            // Assign Methods on default buttons
            AssignPopupButtonHandlers(newGameManagerType,rootObject, gameName);
        }
    }

    /// <summary>
    /// Assigns button handlers for popups to the new GameManager component.
    /// </summary>
    private void AssignPopupButtonHandlers( System.Type newGameManagerType, GameObject rootObject, string gameName)
    {
        // Get the new GameManager component
        var newGameManager = rootObject.GetComponentInChildren(newGameManagerType, true);
        if (newGameManager == null)
        {
            Debug.LogError($"Could not find {gameName}GameManager in prefab instance.");
            return;
        }
        
        // Helper to assign a method to a button
        void AssignButton(GameObject popupObj, string buttonName, string methodName)
        {
            if (popupObj == null) return;
            var buttonObj = FindChildByName(popupObj, buttonName);
            if (buttonObj == null)
            {
                Debug.LogWarning($"Button '{buttonName}' not found under {popupObj.name}");
                return;
            }
            var button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning($"No Button component on '{buttonName}' under {popupObj.name}");
                return;
            }
           
            CleanButtonListeners(button);
            
            var method = newGameManagerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                UnityEventTools.AddPersistentListener( button.onClick,    System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), newGameManager, methodName) as UnityEngine.Events.UnityAction);
            }
            else
            {
                Debug.LogWarning($"Method '{methodName}' not found on {gameName}GameManager");
            }
            
            EditorUtility.SetDirty(button);
        }

        // Find popups by name
        var reconnectionPopupObj = FindChildByName(rootObject, "ReconnectionPopup");
        var errorPopupObj = FindChildByName(rootObject, "ErrorPopup");
        var terminatePopupObj = FindChildByName(rootObject, "TerminatePopup");

        // Assign handlers as per your requirements
        AssignButton(reconnectionPopupObj, "ButtonNeutral", "OnClickRetry");
        AssignButton(errorPopupObj, "ButtonCancel", "Quit");
        AssignButton(terminatePopupObj, "ButtonNeutral", "Quit");
    }
    #endregion

    #region Utils
    /// <summary>
    /// Validates the library path and checks for required template files.
    /// </summary>
    private void ValidateLibraryPath()
    {
        Debug.Log($"Validating library path: {libraryPath}");
        if (string.IsNullOrEmpty(libraryPath)) return;

        templatesFound = true;
        foreach (string file in templateFiles)
        {
            Debug.Log($"Validating library path: {LibrarySamplesPath} {Path.Combine(LibrarySamplesPath, file)}");
            if (!File.Exists(Path.Combine(LibrarySamplesPath, file)))
            {
                templatesFound = false;
                break;
            }
        }

        // Also check if classes exist when library path changes
        CheckIfClassesExist();
    }

    /// <summary>
    /// Updates the namespace in the template content to match the new game.
    /// </summary>
    private string UpdateNamespace(string content)
    {
        string namespacePattern = @"namespace\s+([^\n{]+)\s*{";
        return Regex.Replace(content, namespacePattern, match =>
        {
            string newNamespace = $"namespace {gameName}.NCL.AutoGenerated";
            return newNamespace + "\n{";
        });
    }

    /// <summary>
    /// Updates the socket handler path in the GameManager template content.
    /// </summary>
    private string UpdateSocketHandlerPath(string content)
    {
        string pattern = @"private\s+string\s+SOCKETHANDLER_PATH\s*=\s*""[^""]*""\s*;";
        string replacement =
            $"private string SOCKETHANDLER_PATH = \"{gameName}.NCL.AutoGenerated.{gameName}SocketHandler\";";
        return Regex.Replace(content, pattern, replacement);
    }

    /// <summary>
    /// Checks if generated classes already exist for the current game name.
    /// </summary>
    private void CheckIfClassesExist()
    {
        if (!string.IsNullOrEmpty(gameName))
        {
            // Try to find the generated GameManager class
            string fullTypeName = $"{gameName}.NCL.AutoGenerated.{gameName}GameManager";
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var type = assembly.GetType(fullTypeName);
                if (type != null)
                {
                    canCreateScene = true;
                    Debug.Log($"Found generated class {fullTypeName}, enabling scene creation");
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Recursively finds a child GameObject by name.
    /// </summary>
    private GameObject FindChildByName(GameObject parent, string name)
    {
        foreach (Transform child in parent.transform)
        {
            if (child.name == name)
                return child.gameObject;
            var result = FindChildByName(child.gameObject, name);
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Sets a field value on a component, handling type compatibility.
    /// </summary>
    private void SetFieldValue(Component gameManager, string fieldName, object value)
    {
        var field = gameManager.GetType().GetField(fieldName);
        if (field != null)
        {
            if (field.FieldType == typeof(GameObject) && value is Component component)
            {
                field.SetValue(gameManager, component.gameObject);
            }
            else if (field.FieldType == typeof(GameObject) && value is GameObject)
            {
                field.SetValue(gameManager, value);
            }
            else if (field.FieldType.IsAssignableFrom(value.GetType()))
            {
                field.SetValue(gameManager, value);
            }
            else
            {
                Debug.LogError($"Field {fieldName} expects type {field.FieldType} but we have {value.GetType()}");
            }
        }
        else
        {
            Debug.LogError($"Field {fieldName} not found in {gameManager.GetType().Name}");
        }
    }

    /// <summary>
    /// Saves the given GameObject as a prefab asset in the output folder's Prefabs subfolder.
    /// </summary>
    private void SaveAsPrefab(GameObject obj, string gameNameForPrefab)
    {
        // Create Prefabs subfolder in the output folder
        string prefabDirectory = Path.Combine(outputFolderPath, "Prefabs");
        if (!AssetDatabase.IsValidFolder(prefabDirectory))
        {
            // Create the Prefabs folder in the output directory
            string outputFolderName = Path.GetFileName(outputFolderPath);
            string parentFolder = outputFolderPath.Replace("/" + outputFolderName, "");
            AssetDatabase.CreateFolder(parentFolder, outputFolderName);
            AssetDatabase.CreateFolder(outputFolderPath, "Prefabs");
        }

        string prefabPath = Path.Combine(prefabDirectory, $"{gameNameForPrefab}GameManager.prefab");
        // Check if prefab already exists and ask for confirmation
        if (File.Exists(prefabPath))
        {
            bool shouldOverwrite = EditorUtility.DisplayDialog(
                "Prefab Exists",
                $"Prefab '{prefabPath}' already exists. Overwrite?",
                "Yes",
                "No"
            );
            if (!shouldOverwrite)
            {
                Object.DestroyImmediate(obj);
                return;
            }
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.UserAction);
        Debug.Log($"Prefab saved at: {prefabPath}");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Gets the relative path from an absolute path, starting from the Assets folder.
    /// </summary>
    private string GetRelativePath(string absolutePath)
    {
        string projectPath = Application.dataPath.Replace("/Assets", "");
        if (absolutePath.StartsWith(projectPath))
        {
            return "Assets" + absolutePath.Substring(projectPath.Length).Replace('\\', '/');
        }

        return absolutePath.Replace('\\', '/');
    }

    /// <summary>
    /// Gets the generated type for a class name in the new game's namespace.
    /// </summary>
    private System.Type GetGeneratedType(string className)
    {
        string fullTypeName = $"{gameName}.NCL.AutoGenerated.{className}";
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var type = assembly.GetType(fullTypeName);
            if (type != null)
            {
                return type;
            }
        }

        Debug.LogError($"Could not find type: {fullTypeName}");
        return null;
    }

    /// <summary>
    /// Removes all persistent and runtime listeners from a Unity UI Button.
    /// </summary>
    void CleanButtonListeners(Button button)
    {
        // Remove all persistent listeners
        int persistentCount = button.onClick.GetPersistentEventCount();
        for (int i = persistentCount - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
        
        // Remove all runtime listeners
        button.onClick.RemoveAllListeners();
    }

    #endregion
}