using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.VisualScripting;
using Debug = UnityEngine.Debug;
using System.Collections;
using System.Linq;

/// <summary>
/// Editor script that takes a text file and game name as input, then runs the AddSubmodule() function
/// with the specified repository and branch. This is useful for automated submodule setup.
/// Also provides comprehensive submodule management capabilities.
/// </summary>
///
namespace OneClickSetup
{
    public class SubmoduleSetupFromFile : EditorWindow
    {
        private string textFilePath = "Assets/submodule_setup.txt";
        private string gameName = "";
        private bool isPortraitGame = false;
        private string message = "";
        private MessageType messageType = MessageType.None;
        private bool isProcessing = false;
        private string loadingMessage = "";

        // Default values for the submodule
        private const string REPO_URL = "";
        private const string REPO_URL_HTTPS = "";
        private const string BRANCH = "develop";
        private const string DEFAULT_SUBMODULE_PATH = "Assets/Submodule";
        string pillarsEditorPathRelative = "Assets/Submodule/PILLARS/Assets/Editor";

        /// <summary>
        /// Shows the SubmoduleSetupFromFile editor window from the Unity menu.
        /// </summary>
        [MenuItem("MPL/Submodule Setup/Setup from File", false, 3)]
        public static void ShowWindow()
        {
            GetWindow<SubmoduleSetupFromFile>("Submodule Setup from File");
        }

        /// <summary>
        /// Main Unity Editor UI rendering function.
        /// </summary>
        private void OnGUI()
        {
            GUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Setup submodules from text file with game name. This will add two submodules: NCL and PILLARS with their specified repositories and branches.",
                MessageType.Info);
            GUILayout.Space(6);

            // Block UI with loader if processing
            if (isProcessing)
            {
                GUI.enabled = false;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Text file path
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Text File:", GUILayout.Width(80));
            textFilePath = EditorGUILayout.TextField(textFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string selectedPath = EditorUtility.OpenFilePanel("Select Text File", "Assets", "txt");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    if (selectedPath.StartsWith(Application.dataPath))
                    {
                        textFilePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        textFilePath = selectedPath;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Game name input
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Game Name:", GUILayout.Width(80));
            gameName = EditorGUILayout.TextField(gameName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Portrait game checkbox
            bool newPortraitGame = EditorGUILayout.Toggle("Portrait Game", isPortraitGame);
            if (newPortraitGame != isPortraitGame)
            {
                isPortraitGame = newPortraitGame;
            }

            EditorGUILayout.Space(8);

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = !isProcessing && !string.IsNullOrEmpty(textFilePath) && !string.IsNullOrEmpty(gameName);
            if (GUILayout.Button("Create Folder Structure", GUILayout.Width(150), GUILayout.Height(32)))
            {
                CreateFolderStructure();
            }

            GUI.enabled = !isProcessing && !string.IsNullOrEmpty(gameName);
            if (GUILayout.Button("Generate Game Classes", GUILayout.Width(150), GUILayout.Height(32)))
            {
                GenerateGameClasses();
            }

            GUI.enabled = !isProcessing && !string.IsNullOrEmpty(gameName);
            if (GUILayout.Button("Create GameManager Prefab", GUILayout.Width(150), GUILayout.Height(32)))
            {
                CreateGameManagerPrefab();
            }

            GUI.enabled = !isProcessing && !string.IsNullOrEmpty(textFilePath);
            if (GUILayout.Button("Parse Text & Generate Payloads", GUILayout.Width(180), GUILayout.Height(32)))
            {
                ParseTextFileAndGeneratePayloads();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Sequential Execution Button
            GUI.enabled = !isProcessing && !string.IsNullOrEmpty(gameName);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f); // Green color
            if (GUILayout.Button("🔄 SEQUENTIAL SETUP", GUILayout.Height(40)))
            {
                ExecuteSequentialSetup();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("This button will execute:\n1. Create Folder Structure\n2. Assets Database Refresh & Wait\n3. Generate Game Classes\n4. Wait for Compilation & Asset Import\n5. Assets Database Refresh & Wait\n6. Create GameManager Prefab\n7. Final Assets Database Refresh", MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Clear", GUILayout.Width(100), GUILayout.Height(32)))
            {
                ClearFields();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Message display
            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(message, messageType);
            }
            
            // Loading message display
            if (isProcessing && !string.IsNullOrEmpty(loadingMessage))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(loadingMessage, MessageType.Info);
            }
        }
       
        /// <summary>
        /// Clears all input fields and messages.
        /// </summary>
        private void ClearFields()
        {
            textFilePath = "Assets/submodule_setup.txt";
            gameName = "";
            isPortraitGame = false;
            message = "";
            messageType = MessageType.None;
            Repaint();
        }

        /// <summary>
        /// Called when the window is enabled. Can be used for initialization.
        /// </summary>
        private void OnEnable()
        {
            // Initialize with default values
            if (string.IsNullOrEmpty(textFilePath))
            {
                textFilePath = "Assets/submodule_setup.txt";
            }
        }
        
        /// <summary>
        /// Calls the CreateFolderStructureProgrammatically method from FolderStructureCreator class.
        /// </summary>
        private void CreateFolderStructure()
        {
            try
            {
                // Find the FolderStructureCreator class
                var folderCreatorType = System.Type.GetType("FolderStructureCreator, Assembly-CSharp-Editor");
                if (folderCreatorType == null)
                {
                    Debug.LogWarning("FolderStructureCreator: Could not find FolderStructureCreator class in Assembly-CSharp-Editor");
                    // Try searching in all assemblies as fallback
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies)
                    {
                        if (assembly.FullName.Contains("Editor"))
                        {
                            folderCreatorType = assembly.GetType("FolderStructureCreator");
                            if (folderCreatorType != null)
                            {
                                Debug.Log($"Found FolderStructureCreator in assembly: {assembly.FullName}");
                                break;
                            }
                        }
                    }
                    
                    if (folderCreatorType == null)
                    {
                        message = "FolderStructureCreator class not found. Please check if the class is properly loaded.";
                        messageType = MessageType.Warning;
                        return;
                    }
                }

                // Call the static CreateFolderStructureProgrammatically method
                var createMethod = folderCreatorType.GetMethod("CreateFolderStructureProgrammatically",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (createMethod != null)
                {
                    Debug.Log("Calling CreateFolderStructureProgrammatically method...");
                    var result = createMethod.Invoke(null, null);
                    bool success = result is bool ? (bool)result : true;

                    if (success)
                    {
                        Debug.Log("Folder structure created successfully");
                        message = "Folder structure created successfully using FolderStructureCreator!";
                        messageType = MessageType.Info;
                    }
                    else
                    {
                        Debug.LogWarning("Folder structure creation may have failed");
                        message = "Folder structure creation completed but may have encountered issues.";
                        messageType = MessageType.Warning;
                    }
                }
                else
                {
                    Debug.LogError("CreateFolderStructureProgrammatically method not found in FolderStructureCreator");
                    message = "CreateFolderStructureProgrammatically method not found in FolderStructureCreator class.";
                            messageType = MessageType.Error;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error calling FolderStructureCreator: {ex.Message}");
                message = $"Error creating folder structure: {ex.Message}";
                messageType = MessageType.Error;
            }
        }

        /// <summary>
        /// Calls the GenerateClasses method from GameClassGeneratorWindow class.
        /// Sets libraryPath to Assets/Submodule/NCL/ConnectionLibrary and isPortrait to isPortraitGame.
        /// </summary>
        private void GenerateGameClasses()
        {
            try
            {
                // Find the GameClassGeneratorWindow class
                var gameGeneratorType = System.Type.GetType("GameClassGeneratorWindow, Assembly-CSharp");
                if (gameGeneratorType == null)
                {
                    Debug.LogWarning("GameClassGeneratorWindow: Could not find GameClassGeneratorWindow class in Assembly-CSharp");
                    // Try searching in all assemblies as fallback
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies)
                    {
                        if (!assembly.FullName.StartsWith("System.") && 
                            !assembly.FullName.StartsWith("mscorlib") &&
                            !assembly.FullName.StartsWith("UnityEngine.") &&
                            !assembly.FullName.StartsWith("UnityEditor."))
                        {
                        gameGeneratorType = assembly.GetType("GameClassGeneratorWindow");
                        if (gameGeneratorType != null)
                        {
                            Debug.Log($"Found GameClassGeneratorWindow in assembly: {assembly.FullName}");
                            break;
                        }
                    }
                }

                if (gameGeneratorType == null)
                {
                        message = "GameClassGeneratorWindow class not found. Please check if the class is properly loaded.";
                        messageType = MessageType.Warning;
                        return;
                    }
                }

                // Create an instance of GameClassGeneratorWindow
                var gameGeneratorInstance = System.Activator.CreateInstance(gameGeneratorType);
                if (gameGeneratorInstance == null)
                {
                    Debug.LogError("Failed to create instance of GameClassGeneratorWindow");
                    message = "Failed to create instance of GameClassGeneratorWindow class.";
                    messageType = MessageType.Error;
                    return;
                }

                // Set the libraryPath and isPortrait fields using reflection
                var libraryPathField = gameGeneratorType.GetField("libraryPath", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var isPortraitField = gameGeneratorType.GetField("isPortrait", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var gameNameField = gameGeneratorType.GetField("gameName", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (libraryPathField != null)
                {
                    // Check if the NCL submodule path exists, if not use a fallback path
                    string nclPath = "Assets/Submodule/NCL/ConnectionLibrary";
                    if (!Directory.Exists(nclPath))
                    {
                        // Try alternative paths
                        string[] alternativePaths = {
                            "Assets/Submodule/NCL",
                            "Assets/Submodule/NCL/Sample",
                            "Assets/Submodule/NCL/ConnectionLibrary/Sample"
                        };
                        
                        foreach (string altPath in alternativePaths)
                        {
                            if (Directory.Exists(altPath))
                            {
                                nclPath = altPath;
                                Debug.Log($"Using alternative NCL path: {nclPath}");
                                break;
                            }
                        }
                        
                        if (!Directory.Exists(nclPath))
                        {
                            message = "NCL submodule not found. Please ensure the NCL submodule is properly added to Assets/Submodule/NCL/";
                            messageType = MessageType.Error;
                            return;
                        }
                    }
                    
                    libraryPathField.SetValue(gameGeneratorInstance, nclPath);
                    Debug.Log($"Set libraryPath to: {nclPath}");
                }
                else
                {
                    Debug.LogWarning("libraryPath field not found in GameClassGeneratorWindow");
                }

                if (isPortraitField != null)
                {
                    isPortraitField.SetValue(gameGeneratorInstance, isPortraitGame);
                    Debug.Log($"Set isPortrait to: {isPortraitGame}");
                }
                else
                {
                    Debug.LogWarning("isPortrait field not found in GameClassGeneratorWindow");
                }

                if (gameNameField != null)
                {
                    gameNameField.SetValue(gameGeneratorInstance, gameName);
                    Debug.Log($"Set gameName to: {gameName}");
                }
                else
                {
                    Debug.LogWarning("gameName field not found in GameClassGeneratorWindow");
                }

                // Verify that required template files exist before proceeding
                string[] requiredTemplates = {
                    "SampleGameManager.cs",
                    "SamplePopUp.cs",
                    "SampleSessionInfo.cs",
                    "SampleSignupPayload.cs",
                    "SampleSocketHandler.cs",
                    "SampleTable.cs",
                    "SampleTableDataHandler.cs",
                    "SampleEvents.cs"
                };

                string samplePath = "Assets/Submodule/NCL/ConnectionLibrary/Sample";
                
                Debug.Log($"Sneh: samplePath = {samplePath}");
                bool templatesExist = true;
                List<string> missingTemplates = new List<string>();
                
                foreach (string template in requiredTemplates)
                {
                    string templatePath = Path.Combine(samplePath, template);
                    if (!File.Exists(templatePath))
                    {
                        templatesExist = false;
                        missingTemplates.Add(template);
                    }
                }
                
                if (!templatesExist)
                {
                    message = $"Required template files not found in {samplePath}. Missing: {string.Join(", ", missingTemplates)}";
                    messageType = MessageType.Error;
                    Debug.LogError($"Template files missing: {string.Join(", ", missingTemplates)}");
                    return;
                }
                
                Debug.Log($"All required template files found in: {samplePath}");

                // Call the GenerateClasses method
                var generateMethod = gameGeneratorType.GetMethod("GenerateClasses", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (generateMethod != null)
                {
                    Debug.Log("Calling GenerateClasses method...");
                    generateMethod.Invoke(gameGeneratorInstance, null);
                    Debug.Log("GenerateClasses method completed successfully");
                    
                    message = $"Game classes generated successfully for '{gameName}'!";
                    messageType = MessageType.Info;
                }
                else
                {
                    Debug.LogError("GenerateClasses method not found in GameClassGeneratorWindow");
                    message = "GenerateClasses method not found in GameClassGeneratorWindow class.";
                    messageType = MessageType.Error;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error calling GameClassGeneratorWindow: {ex.Message}");
                message = $"Error generating game classes: {ex.Message}";
                messageType = MessageType.Error;
            }
        }

                /// <summary>
        /// Calls the CreateAndAssignGameManagerPrefab method from GameClassGeneratorWindow class.
        /// Passes gameName and isPortraitGame as parameters.
        /// </summary>
        private void CreateGameManagerPrefab()
        {
            try
            {
                // Find the GameClassGeneratorWindow class
                var gameGeneratorType = System.Type.GetType("GameClassGeneratorWindow, Assembly-CSharp");
                if (gameGeneratorType == null)
                {
                    Debug.LogWarning("GameClassGeneratorWindow: Could not find GameClassGeneratorWindow class in Assembly-CSharp");
                    // Try searching in all assemblies as fallback
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies)
                    {
                        if (!assembly.FullName.StartsWith("System.") && 
                            !assembly.FullName.StartsWith("mscorlib") &&
                            !assembly.FullName.StartsWith("UnityEngine.") &&
                            !assembly.FullName.StartsWith("UnityEditor."))
                        {
                        gameGeneratorType = assembly.GetType("GameClassGeneratorWindow");
                        if (gameGeneratorType != null)
                        {
                            Debug.Log($"Found GameClassGeneratorWindow in assembly: {assembly.FullName}");
                            break;
                        }
                    }
                }

                if (gameGeneratorType == null)
                {
                        message = "GameClassGeneratorWindow class not found. Please check if the class is properly loaded.";
                        messageType = MessageType.Warning;
                        return;
                    }
                }

                // Create an instance of GameClassGeneratorWindow
                var gameGeneratorInstance = System.Activator.CreateInstance(gameGeneratorType);
                if (gameGeneratorInstance == null)
                {
                    Debug.LogError("Failed to create instance of GameClassGeneratorWindow");
                    message = "Failed to create instance of GameClassGeneratorWindow class.";
                    messageType = MessageType.Error;
                    return;
                }

                // Set the libraryPath field using reflection to ensure it's set correctly
                var libraryPathField = gameGeneratorType.GetField("libraryPath", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var isPortraitField = gameGeneratorType.GetField("isPortrait", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var gameNameField = gameGeneratorType.GetField("gameName", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (libraryPathField != null)
                {
                    libraryPathField.SetValue(gameGeneratorInstance, "Assets/Submodule/NCL/ConnectionLibrary");
                    Debug.Log($"Set libraryPath to: Assets/Submodule/NCL/ConnectionLibrary");
                }

                if (isPortraitField != null)
                {
                    isPortraitField.SetValue(gameGeneratorInstance, isPortraitGame);
                    Debug.Log($"Set isPortrait to: {isPortraitGame}");
                }
                else
                {
                    Debug.LogWarning("isPortrait field not found in GameClassGeneratorWindow");
                }

                if (gameNameField != null)
                {
                    gameNameField.SetValue(gameGeneratorInstance, gameName);
                    Debug.Log($"Set gameName to: {gameName}");
                }
                else
                {
                    Debug.LogWarning("gameName field not found in GameClassGeneratorWindow");
                }
                
                // Call the CreateAndAssignGameManagerPrefab method
                var createPrefabMethod = gameGeneratorType.GetMethod("CreateAndAssignGameManagerPrefab", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (createPrefabMethod != null)
                {
                    Debug.Log($"Calling CreateAndAssignGameManagerPrefab with gameName: {gameName}, isPortrait: {isPortraitGame}");
                    createPrefabMethod.Invoke(gameGeneratorInstance, new object[] { gameName, isPortraitGame });
                    Debug.Log("CreateAndAssignGameManagerPrefab method completed successfully");
                    
                    message = $"GameManager prefab created successfully for '{gameName}'!";
                    messageType = MessageType.Info;
                }
                else
                {
                    Debug.LogError("CreateAndAssignGameManagerPrefab method not found in GameClassGeneratorWindow");
                    message = "CreateAndAssignGameManagerPrefab method not found in GameClassGeneratorWindow class.";
                    messageType = MessageType.Error;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error calling CreateAndAssignGameManagerPrefab: {ex.Message}");
                message = $"Error creating GameManager prefab: {ex.Message}";
                messageType = MessageType.Error;
            }
        }

        /// <summary>
        /// Calls ParseTextFile() and then GeneratePayloadForAllEvents() from JSONToPayloadClassesGeneratorWindow class.
        /// Uses the textFilePath value from the UI.
        /// </summary>
        private void ParseTextFileAndGeneratePayloads()
        {
            try
            {
                // Find the JSONToPayloadClassesGeneratorWindow class
                var payloadGeneratorType = System.Type.GetType("JSONToPayloadClassesGeneratorWindow, Assembly-CSharp");
                if (payloadGeneratorType == null)
                {
                    Debug.LogWarning("JSONToPayloadClassesGeneratorWindow: Could not find JSONToPayloadClassesGeneratorWindow class in Assembly-CSharp");
                    // Try searching in all assemblies as fallback
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies)
                    {
                        if (!assembly.FullName.StartsWith("System.") && 
                            !assembly.FullName.StartsWith("mscorlib") &&
                            !assembly.FullName.StartsWith("UnityEngine.") &&
                            !assembly.FullName.StartsWith("UnityEditor."))
                        {
                            payloadGeneratorType = assembly.GetType("JSONToPayloadClassesGeneratorWindow");
                            if (payloadGeneratorType != null)
                            {
                                Debug.Log($"Found JSONToPayloadClassesGeneratorWindow in assembly: {assembly.FullName}");
                                break;
                            }
                        }
                    }
                    
                    if (payloadGeneratorType == null)
                    {
                        message = "JSONToPayloadClassesGeneratorWindow class not found. Please check if the class is properly loaded.";
                        messageType = MessageType.Warning;
                        return;
                    }
                }

                // Create an instance of JSONToPayloadClassesGeneratorWindow
                var payloadGeneratorInstance = System.Activator.CreateInstance(payloadGeneratorType);
                if (payloadGeneratorInstance == null)
                {
                    Debug.LogError("Failed to create instance of JSONToPayloadClassesGeneratorWindow");
                    message = "Failed to create instance of JSONToPayloadClassesGeneratorWindow class.";
                    messageType = MessageType.Error;
                    return;
                }

                // Set the textFilePath field using reflection
                var textFilePathField = payloadGeneratorType.GetField("textFilePath", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (textFilePathField != null)
                {
                    textFilePathField.SetValue(payloadGeneratorInstance, textFilePath);
                    Debug.Log($"Set textFilePath to: {textFilePath}");
                }
                else
                {
                    Debug.LogWarning("textFilePath field not found in JSONToPayloadClassesGeneratorWindow");
                }

                // Step 1: Call ParseTextFile method
                var parseTextFileMethod = payloadGeneratorType.GetMethod("ParseTextFile", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (parseTextFileMethod != null)
                {
                    Debug.Log("Calling ParseTextFile method...");
                    parseTextFileMethod.Invoke(payloadGeneratorInstance, null);
                    Debug.Log("ParseTextFile method completed successfully");
                    
                    // Step 2: Call GeneratePayloadForAllEvents method after ParseTextFile completes
                    var generatePayloadMethod = payloadGeneratorType.GetMethod("GeneratePayloadForAllEvents", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (generatePayloadMethod != null)
                    {
                        Debug.Log("Calling GeneratePayloadForAllEvents method...");
                        generatePayloadMethod.Invoke(payloadGeneratorInstance, null);
                        Debug.Log("GeneratePayloadForAllEvents method completed successfully");
                        
                        message = $"Text file parsed and payloads generated successfully from: {textFilePath}";
                        messageType = MessageType.Info;
                    }
                    else
                    {
                        Debug.LogError("GeneratePayloadForAllEvents method not found in JSONToPayloadClassesGeneratorWindow");
                        message = "ParseTextFile completed, but GeneratePayloadForAllEvents method not found.";
                        messageType = MessageType.Warning;
                    }
                }
                else
                {
                    Debug.LogError("ParseTextFile method not found in JSONToPayloadClassesGeneratorWindow");
                    message = "ParseTextFile method not found in JSONToPayloadClassesGeneratorWindow class.";
                    messageType = MessageType.Error;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error calling JSONToPayloadClassesGeneratorWindow methods: {ex.Message}");
                message = $"Error processing text file and generating payloads: {ex.Message}";
                messageType = MessageType.Error;
            }
        }

        /// <summary>
        /// Sequentially executes the setup steps: Create Folder Structure, Assets Database Refresh & Wait, Generate Game Classes, Assets Database Refresh & Wait.
        /// </summary>
        private void ExecuteSequentialSetup()
        {
            if (isProcessing)
            {
                Debug.LogWarning("Setup is already in progress.");
                return;
            }

            isProcessing = true;
            currentStep = 1;
            totalSteps = 7;
            stepStartTime = Time.realtimeSinceStartup;
            waitingForRefresh = false;
            refreshAttempts = 0;
            
            loadingMessage = $"Step {currentStep}/{totalSteps}: Creating folder structure...";
            Repaint();
            
            EditorApplication.update += ExecuteNextSequentialStep;
            Debug.Log("Sequential setup started - EditorApplication.update registered for ExecuteNextSequentialStep");
        }
        
        // Step tracking variables for sequential execution
        private int currentStep = 1;
        private int totalSteps = 4;
        private float stepStartTime = 0f;
        private bool waitingForRefresh = false;
        private int refreshAttempts = 0;
        private const int MAX_REFRESH_ATTEMPTS = 10;
        private bool waitingForCompilation = false;
        private int compilationAttempts = 0;
        private const int MAX_COMPILATION_ATTEMPTS = 10;
        
        /// <summary>
        /// Executes the next step in the sequential setup.
        /// </summary>
        private void ExecuteNextSequentialStep()
        {
            Debug.Log($"ExecuteNextSequentialStep called - Current Step: {currentStep}, Total Steps: {totalSteps}, Waiting for Refresh: {waitingForRefresh}");
            
            if (currentStep > totalSteps)
            {
                // All steps completed successfully
                loadingMessage = "";
                message = "🎉 SEQUENTIAL SETUP COMPLETED SUCCESSFULLY! 🎉\n\n" +
                         "✓ Step 1: Folder structure created\n" +
                         "✓ Step 2: Assets database refreshed\n" +
                         "✓ Step 3: Game classes generated\n" +
                         "✓ Step 4: Compilation and asset import completed\n" +
                         "✓ Step 5: Final assets database refresh completed\n" +
                         "✓ Step 6: GameManager prefab created\n" +
                         "✓ Step 7: Final assets database refresh completed\n\n" +
                         $"Game '{gameName}' setup completed!";
                messageType = MessageType.Info;
                
                Debug.Log("=== SEQUENTIAL SETUP COMPLETED SUCCESSFULLY ===");
                
                isProcessing = false;
                currentStep = 1;
                EditorApplication.update -= ExecuteNextSequentialStep;
                Repaint();
                return;
            }
            
            Debug.Log($"Executing Sequential Step {currentStep}...");
            switch (currentStep)
            {
                case 1:
                    ExecuteSequentialStep1();
                    break;
                case 2:
                    ExecuteSequentialStep2();
                    break;
                case 3:
                    ExecuteSequentialStep3();
                    break;
                case 4:
                    ExecuteSequentialStep4();
                    break;
                case 5:
                    ExecuteSequentialStep5();
                    break;
                case 6:
                    ExecuteSequentialStep6();
                    break;
                case 7:
                    ExecuteSequentialStep7();
                    break;
            }
        }
        
        /// <summary>
        /// Step 1: Create Folder Structure
        /// </summary>
        private void ExecuteSequentialStep1()
        {
            loadingMessage = $"Step {currentStep}/{totalSteps}: Creating folder structure...";
            Repaint();
            Debug.Log($"Sequential Step {currentStep}: Creating folder structure...");
            
            CreateFolderStructure();
            Debug.Log("✓ Sequential Step 1 completed successfully");
            
            // Move to step 2 (assets refresh)
            currentStep = 2;
            waitingForRefresh = true;
            refreshAttempts = 0;
            stepStartTime = Time.realtimeSinceStartup;
            
            Debug.Log($"Sequential Step 1: Set currentStep to {currentStep}, waitingForRefresh to {waitingForRefresh}");
        }
        
        /// <summary>
        /// Step 2: Assets Database Refresh & Wait
        /// </summary>
        private void ExecuteSequentialStep2()
        {
            Debug.Log($"ExecuteSequentialStep2: waitingForRefresh={waitingForRefresh}, refreshAttempts={refreshAttempts}, stepStartTime={stepStartTime}");
            
            if (!waitingForRefresh)
            {
                Debug.LogWarning("ExecuteSequentialStep2 called but waitingForRefresh is false");
                return;
            }
            
            // We're still waiting for refresh, check if it's complete
            if (Time.realtimeSinceStartup - stepStartTime < 2f)
            {
                // Still in initial wait period
                loadingMessage = $"Step {currentStep}/{totalSteps}: Refreshing assets database...";
                Repaint();
                Debug.Log($"Sequential Step 2: Still in initial wait period ({Time.realtimeSinceStartup - stepStartTime:F2}s elapsed)");
                return; // Continue waiting
            }
            
            // Check if assets are refreshed by looking for any recent changes
            if (refreshAttempts == 0)
            {
                // First attempt - trigger the refresh
                AssetDatabase.Refresh();
                Debug.Log("Sequential Step 2: Triggered AssetDatabase.Refresh()");
                refreshAttempts++;
                return; // Wait for refresh to complete
            }
            
            if (refreshAttempts < MAX_REFRESH_ATTEMPTS)
            {
                refreshAttempts++;
                loadingMessage = $"Step {currentStep}/{totalSteps}: Waiting for assets refresh (Attempt {refreshAttempts}/{MAX_REFRESH_ATTEMPTS})...";
                Repaint();
                
                Debug.Log($"Attempt {refreshAttempts}: Waiting for assets refresh to complete...");
                
                // Check if refresh is complete by looking for any compilation errors or new assets
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    Debug.Log("✓ Sequential Step 2 completed successfully - Assets refresh complete");
                    waitingForRefresh = false;
                    currentStep++;
                    Debug.Log($"Sequential Step 2: Moving to step {currentStep}");
                    return;
                }
                
                return; // Continue waiting
            }
            
            // Max attempts reached, proceed anyway
            Debug.LogWarning($"Sequential Step 2: Max refresh attempts reached, proceeding to next step");
            waitingForRefresh = false;
            currentStep++;
            Debug.Log($"Sequential Step 2: Moving to step {currentStep}");
        }
        
        /// <summary>
        /// Step 3: Generate Game Classes
        /// </summary>
        private void ExecuteSequentialStep3()
        {
            loadingMessage = $"Step {currentStep}/{totalSteps}: Generating game classes...";
            Repaint();
            Debug.Log($"Sequential Step {currentStep}: Generating game classes...");
            
            GenerateGameClasses();
            Debug.Log("✓ Sequential Step 3 completed successfully");
            
            // Move to step 4 (wait for compilation and assets import)
            currentStep = 4;
            waitingForCompilation = true;
            compilationAttempts = 0;
            stepStartTime = Time.realtimeSinceStartup;
            
            Debug.Log($"Sequential Step 3: Set currentStep to {currentStep}, waitingForCompilation to {waitingForCompilation}");
        }
        
        /// <summary>
        /// Step 4: Wait for Asset Import and Class Compilation
        /// </summary>
        private void ExecuteSequentialStep4()
        {
            Debug.Log($"ExecuteSequentialStep4: waitingForCompilation={waitingForCompilation}, compilationAttempts={compilationAttempts}, stepStartTime={stepStartTime}");
            
            if (!waitingForCompilation)
            {
                Debug.LogWarning("ExecuteSequentialStep4 called but waitingForCompilation is false");
                return;
            }
            
            // We're still waiting for compilation, check if it's complete
            if (Time.realtimeSinceStartup - stepStartTime < 2f)
            {
                // Still in initial wait period
                loadingMessage = $"Step {currentStep}/{totalSteps}: Waiting for classes to compile and assets to import...";
                Repaint();
                Debug.Log($"Sequential Step 4: Still in initial wait period ({Time.realtimeSinceStartup - stepStartTime:F2}s elapsed)");
                return; // Continue waiting
            }
            
            // Check if compilation and asset import are complete
            if (compilationAttempts == 0)
            {
                // First attempt - trigger asset refresh and wait for compilation
                AssetDatabase.Refresh();
                Debug.Log("Sequential Step 4: Triggered AssetDatabase.Refresh() and waiting for compilation");
                compilationAttempts++;
                return; // Wait for compilation to complete
            }
            
            if (compilationAttempts < MAX_COMPILATION_ATTEMPTS)
            {
                compilationAttempts++;
                loadingMessage = $"Step {currentStep}/{totalSteps}: Waiting for compilation and asset import (Attempt {compilationAttempts}/{MAX_COMPILATION_ATTEMPTS})...";
                Repaint();
                
                Debug.Log($"Attempt {compilationAttempts}: Waiting for compilation and asset import to complete...");
                
                // Check if compilation is complete and assets are imported
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    // Additional check: verify that the generated classes are actually available
                    bool classesAvailable = CheckGeneratedClassesAvailability();
                    
                    if (classesAvailable)
                    {
                        Debug.Log("✓ Sequential Step 4 completed successfully - Compilation and asset import complete");
                        waitingForCompilation = false;
                        currentStep++;
                        waitingForRefresh = true;
                        refreshAttempts = 0;
                        stepStartTime = Time.realtimeSinceStartup;
                        Debug.Log($"Sequential Step 4: Moving to step {currentStep}, waitingForRefresh to {waitingForRefresh}");
                        return;
                    }
                    else
                    {
                        Debug.Log("Compilation complete but classes not yet available, continuing to wait...");
                        return; // Continue waiting for classes to be available
                    }
                }
                
                return; // Continue waiting
            }
            
            // Max attempts reached, proceed anyway
            Debug.LogWarning($"Sequential Step 4: Max compilation attempts reached, proceeding to next step");
            waitingForCompilation = false;
            currentStep++;
            Debug.Log($"Sequential Step 4: Moving to step {currentStep}");
        }
        
        /// <summary>
        /// Step 5: Final Assets Database Refresh
        /// </summary>
        private void ExecuteSequentialStep5()
        {
            Debug.Log($"ExecuteSequentialStep5: waitingForRefresh={waitingForRefresh}, refreshAttempts={refreshAttempts}, stepStartTime={stepStartTime}");
            
            if (!waitingForRefresh)
            {
                Debug.LogWarning("ExecuteSequentialStep5 called but waitingForRefresh is false");
                return;
            }
            
            // We're still waiting for refresh, check if it's complete
            if (Time.realtimeSinceStartup - stepStartTime < 2f)
            {
                // Still in initial wait period
                loadingMessage = $"Step {currentStep}/{totalSteps}: Waiting for final assets refresh...";
                Repaint();
                Debug.Log($"Sequential Step 5: Still in initial wait period ({Time.realtimeSinceStartup - stepStartTime:F2}s elapsed)");
                return; // Continue waiting
            }
            
            if (refreshAttempts == 0)
            {
                // First attempt - trigger the refresh
                AssetDatabase.Refresh();
                Debug.Log("Sequential Step 5: Triggered AssetDatabase.Refresh()");
                refreshAttempts++;
                return; // Wait for refresh to complete
            }
            
            if (refreshAttempts < MAX_REFRESH_ATTEMPTS)
            {
                refreshAttempts++;
                loadingMessage = $"Step {currentStep}/{totalSteps}: Waiting for final assets refresh (Attempt {refreshAttempts}/{MAX_REFRESH_ATTEMPTS})...";
                Repaint();
                
                Debug.Log($"Attempt {refreshAttempts}: Waiting for final assets refresh to complete...");
                
                // Check if refresh is complete by looking for any compilation errors or new assets
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    Debug.Log("✓ Sequential Step 5 completed successfully - Final assets refresh complete");
                    waitingForRefresh = false;
                    currentStep++;
                    Debug.Log($"Sequential Step 5: Moving to step {currentStep}");
                    return;
                }
                
                return; // Continue waiting
            }
            
            // Max attempts reached, proceed anyway
            Debug.LogWarning($"Sequential Step 5: Max refresh attempts reached, proceeding to next step");
            waitingForRefresh = false;
            currentStep++;
            Debug.Log($"Sequential Step 5: Moving to step {currentStep}");
        }

        /// <summary>
        /// Step 6: Create GameManager Prefab
        /// </summary>
        private void ExecuteSequentialStep6()
        {
            loadingMessage = $"Step {currentStep}/{totalSteps}: Creating GameManager prefab...";
            Repaint();
            Debug.Log($"Sequential Step {currentStep}: Creating GameManager prefab...");

            CreateGameManagerPrefab();
            Debug.Log("✓ Sequential Step 6 completed successfully");

            // Move to step 7 (final refresh)
            currentStep = 7;
            waitingForRefresh = true;
            refreshAttempts = 0;
            stepStartTime = Time.realtimeSinceStartup;

            Debug.Log($"Sequential Step 6: Set currentStep to {currentStep}, waitingForRefresh to {waitingForRefresh}");
        }
        
        /// <summary>
        /// Step 7: Final Assets Database Refresh
        /// </summary>
        private void ExecuteSequentialStep7()
        {
            Debug.Log($"ExecuteSequentialStep7: waitingForRefresh={waitingForRefresh}, refreshAttempts={refreshAttempts}, stepStartTime={stepStartTime}");
            
            if (!waitingForRefresh)
            {
                Debug.LogWarning("ExecuteSequentialStep7 called but waitingForRefresh is false");
                return;
            }
            
            // We're still waiting for refresh, check if it's complete
            if (Time.realtimeSinceStartup - stepStartTime < 2f)
            {
                // Still in initial wait period
                loadingMessage = $"Step {currentStep}/{totalSteps}: Waiting for final assets refresh...";
                Repaint();
                Debug.Log($"Sequential Step 7: Still in initial wait period ({Time.realtimeSinceStartup - stepStartTime:F2}s elapsed)");
                return; // Continue waiting
            }
            
            if (refreshAttempts == 0)
            {
                // First attempt - trigger the refresh
                AssetDatabase.Refresh();
                Debug.Log("Sequential Step 7: Triggered AssetDatabase.Refresh()");
                refreshAttempts++;
                return; // Wait for refresh to complete
            }
            
            if (refreshAttempts < MAX_REFRESH_ATTEMPTS)
            {
                refreshAttempts++;
                loadingMessage = $"Step {currentStep}/{totalSteps}: Waiting for final assets refresh (Attempt {refreshAttempts}/{MAX_REFRESH_ATTEMPTS})...";
                Repaint();
                
                Debug.Log($"Attempt {refreshAttempts}: Waiting for final assets refresh to complete...");
                
                // Check if refresh is complete by looking for any compilation errors or new assets
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    Debug.Log("✓ Sequential Step 7 completed successfully - Final assets refresh complete");
                    waitingForRefresh = false;
                    currentStep++;
                    Debug.Log($"Sequential Step 7: Moving to step {currentStep}");
                    return;
                }
                
                return; // Continue waiting
            }
            
            // Max attempts reached, proceed anyway
            Debug.LogWarning($"Sequential Step 7: Max refresh attempts reached, proceeding to next step");
            waitingForRefresh = false;
            currentStep++;
            Debug.Log($"Sequential Step 7: Moving to step {currentStep}");
        }
        
        /// <summary>
        /// Checks if the generated game classes are available in the assembly.
        /// </summary>
        private bool CheckGeneratedClassesAvailability()
        {
            try
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                Debug.Log($"Checking {assemblies.Length} assemblies for generated classes...");
                Debug.Log($"Looking for classes with gameName: '{gameName}'");
                
                foreach (var assembly in assemblies)
                {
                    if (!assembly.FullName.StartsWith("System.") && 
                        !assembly.FullName.StartsWith("mscorlib") &&
                        !assembly.FullName.StartsWith("UnityEngine.") &&
                        !assembly.FullName.StartsWith("UnityEditor."))
                    {
                        Debug.Log($"Checking assembly: {assembly.FullName}");
                        
                        // Check for the main GameManager class
                        string fullTypeName = $"{gameName}.NCL.AutoGenerated.{gameName}GameManager";
                        var type = assembly.GetType(fullTypeName);
                        if (type != null)
                        {
                            Debug.Log($"✓ Found GameManager class: {fullTypeName} in assembly: {assembly.FullName}");
                            
                            // Also check for the PopUp class to ensure it's available
                            string popUpTypeName = $"{gameName}.NCL.AutoGenerated.{gameName}PopUp";
                            var popUpType = assembly.GetType(popUpTypeName);
                            if (popUpType != null)
                            {
                                Debug.Log($"✓ Found PopUp class: {popUpTypeName} in assembly: {assembly.FullName}");
                                Debug.Log($"✓ Both required classes found - compilation complete!");
                                return true;
                            }
                            else
                            {
                                Debug.Log($"⚠ PopUp class not found: {popUpTypeName} in assembly: {assembly.FullName}");
                            }
                        }
                    }
                }
                
                Debug.Log("Generated classes not yet available in any assembly");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error checking generated classes: {ex.Message}");
                return false;
            }
        }
    }
}