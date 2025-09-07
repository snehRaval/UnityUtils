using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// Editor window to generate/update C# payload classes from JSON event payloads.
/// Supports client/server event types, nested class generation, and UI browsing of all payload classes.
/// </summary>
public class EventToPayloadClassGeneratorWindow : EditorWindow
{
    private string eventName = "";
    private string jsonPayload = "";
    private string message = "";
    private MessageType messageType = MessageType.None;
    private const string PayloadsFolder = "Assets/Scripts/Payloads";
    private const string ClientPayloadFile = "Assets/Scripts/Payloads/ClientPayload.cs";
    private const string ServerPayloadFile = "Assets/Scripts/Payloads/ServerPayload.cs";
    private const string CommonPayloadFile = "Assets/Scripts/Payloads/CommonPayload.cs";

    private Vector2 jsonScroll;
    private List<ClassInfo> classList = new List<ClassInfo>();
    private int selectedClassIndex = -1;

    public enum EventType { ClientEvent, ClientEventAck, ServerEvent }
    private EventType selectedEventType = EventType.ClientEvent;
    private static readonly HashSet<string> CommonClassNames = new HashSet<string> { "COMMON", "BASE", "SHARED", "UTIL", "HELPER" };
    private class ClassInfo
    {
        public string Name;
        public List<string> Members = new List<string>();
        public string SourceFile; // "Client" or "Server"
        public bool IsAck => Name.EndsWith("_ACKDATA");
    }

    private int hoveredClassIndex = -1;
    private Vector2 eventListScroll = Vector2.zero;
    private Vector2 memberScroll = Vector2.zero;

    /// <summary>
    /// Opens the Payload Class Generator window from the Unity menu.
    /// </summary>
    [MenuItem("Sneh/Payload Gen/Event to Payload Class Generator", false, 5)] 
    public static void ShowWindow()
    {
        GetWindow<EventToPayloadClassGeneratorWindow>("Event to Payload Class Generator");
    }

    /// <summary>
    /// Main Unity Editor UI rendering function. Handles all input, buttons, and layout.
    /// </summary>
    private void OnGUI()
    {
        GUILayout.Space(8);
        EditorGUILayout.HelpBox("Generate and manage payload C# classes from JSON event payloads. Select event type, paste JSON, and generate. Browse and inspect all generated classes below.", MessageType.Info);
        GUILayout.Space(6);
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Event to Payload Class Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        eventName = EditorGUILayout.TextField(new GUIContent("Event Name", "Name of the event (e.g. UPDATE_GTI)"), eventName);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField(new GUIContent("Event Type:", "Choose the type of event for this payload class."));
        EditorGUILayout.BeginHorizontal();
        selectedEventType = (EventType)GUILayout.Toolbar((int)selectedEventType, new[] { "Client Event", "Client Event's Ack", "Server Event" }, GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(new GUIContent("JSON Payload:", "Paste the JSON payload here. Only the 'data' object will be used."));
        jsonScroll = EditorGUILayout.BeginScrollView(jsonScroll, GUILayout.Height(100));
        jsonPayload = EditorGUILayout.TextArea(jsonPayload, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("Generate/Update Payload Class", "Generate or update the C# class for this event"),  GUILayout.Height(32)))
        {
            GenerateOrUpdatePayloadClass();
            LoadClassList(); // Refresh class list after generation
        }
        if (GUILayout.Button(new GUIContent("Refresh Class List", "Reload the list of payload classes from disk"), GUILayout.Width(160), GUILayout.Height(32)))
        {
            LoadClassList();
        }
        if (GUILayout.Button(new GUIContent("Reset", "Reset all fields and selections"), GUILayout.Width(100), GUILayout.Height(32)))
        {
            ResetUIFields();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
        // Section: List of classes/events
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Existing Payload Classes", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        float leftColWidth = Mathf.Clamp(position.width * 0.35f, 180, 350);
        float rightColWidth = Mathf.Max(200, position.width - leftColWidth - 30);
        // Left: Event/class list
        EditorGUILayout.BeginVertical(GUILayout.Width(leftColWidth));
        if (classList.Count == 0)
        {
            EditorGUILayout.HelpBox("No payload classes found. Click refresh if you just added one.", MessageType.Info);
        }
        else
        {
            float availableHeight = position.height - 340;
            int maxVisible = Mathf.FloorToInt(availableHeight / 24f);
            int totalCount = classList.Count;
            bool needsScroll = totalCount > maxVisible && maxVisible > 0;
            Action renderList = () => {
                var clientEvents = classList.FindAll(c => c.SourceFile == "Client" && !c.IsAck);
                var clientAcks = classList.FindAll(c => c.SourceFile == "Client" && c.IsAck);
                var serverEvents = classList.FindAll(c => c.SourceFile == "Server");
                var commonEvents = classList.FindAll(c => c.SourceFile == "Common");
                if (clientEvents.Count > 0)
                {
                    EditorGUILayout.LabelField("Client Events:", EditorStyles.miniBoldLabel);
                    foreach (var c in clientEvents)
                    {
                        int idx = classList.IndexOf(c);
                        Rect rowRect = GUILayoutUtility.GetRect(leftColWidth - 20, 22, GUILayout.ExpandWidth(false));
                        bool isHovered = rowRect.Contains(Event.current.mousePosition);
                        if (Event.current.type == UnityEngine.EventType.Repaint && isHovered) hoveredClassIndex = idx;
                        Color orig = GUI.backgroundColor;
                        if (idx == selectedClassIndex)
                            GUI.backgroundColor = new Color(0.2f, 0.5f, 1f, 0.18f);
                        else if (isHovered)
                            GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.12f);
                        GUI.Box(rowRect, GUIContent.none);
                        GUI.backgroundColor = orig;
                        GUIStyle style = (idx == selectedClassIndex) ? EditorStyles.whiteLabel : EditorStyles.label;
                        string displayName = c.Name;
                        if (GUI.Button(rowRect, new GUIContent(displayName, "Click to view class members"), style))
                        {
                            selectedClassIndex = idx;
                        }
                    }
                }
                if (clientAcks.Count > 0)
                {
                    EditorGUILayout.LabelField("Client Event Acks:", EditorStyles.miniBoldLabel);
                    foreach (var c in clientAcks)
                    {
                        int idx = classList.IndexOf(c);
                        Rect rowRect = GUILayoutUtility.GetRect(leftColWidth - 20, 22, GUILayout.ExpandWidth(false));
                        bool isHovered = rowRect.Contains(Event.current.mousePosition);
                        if (Event.current.type == UnityEngine.EventType.Repaint && isHovered) hoveredClassIndex = idx;
                        Color orig = GUI.backgroundColor;
                        if (idx == selectedClassIndex)
                            GUI.backgroundColor = new Color(0.2f, 0.5f, 1f, 0.18f);
                        else if (isHovered)
                            GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.12f);
                        GUI.Box(rowRect, GUIContent.none);
                        GUI.backgroundColor = orig;
                        GUIStyle style = (idx == selectedClassIndex) ? EditorStyles.whiteLabel : EditorStyles.label;
                        string displayName = c.Name;
                        if (GUI.Button(rowRect, new GUIContent(displayName, "Click to view class members"), style))
                        {
                            selectedClassIndex = idx;
                        }
                    }
                }
                if (serverEvents.Count > 0)
                {
                    EditorGUILayout.LabelField("Server Events:", EditorStyles.miniBoldLabel);
                    foreach (var c in serverEvents)
                    {
                        int idx = classList.IndexOf(c);
                        Rect rowRect = GUILayoutUtility.GetRect(leftColWidth - 20, 22, GUILayout.ExpandWidth(false));
                        bool isHovered = rowRect.Contains(Event.current.mousePosition);
                        if (Event.current.type == UnityEngine.EventType.Repaint && isHovered) hoveredClassIndex = idx;
                        Color orig = GUI.backgroundColor;
                        if (idx == selectedClassIndex)
                            GUI.backgroundColor = new Color(0.2f, 0.5f, 1f, 0.18f);
                        else if (isHovered)
                            GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.12f);
                        GUI.Box(rowRect, GUIContent.none);
                        GUI.backgroundColor = orig;
                        GUIStyle style = (idx == selectedClassIndex) ? EditorStyles.whiteLabel : EditorStyles.label;
                        string displayName = c.Name; 
                        if (GUI.Button(rowRect, new GUIContent(displayName, "Click to view class members"), style))
                        {
                            selectedClassIndex = idx;
                        }
                    }
                }
                if (commonEvents.Count > 0)
                {
                    EditorGUILayout.LabelField("Common Events:", EditorStyles.miniBoldLabel);
                    foreach (var c in commonEvents)
                    {
                        int idx = classList.IndexOf(c);
                        Rect rowRect = GUILayoutUtility.GetRect(leftColWidth - 20, 22, GUILayout.ExpandWidth(false));
                        bool isHovered = rowRect.Contains(Event.current.mousePosition);
                        if (Event.current.type == UnityEngine.EventType.Repaint && isHovered) hoveredClassIndex = idx;
                        Color orig = GUI.backgroundColor;
                        if (idx == selectedClassIndex)
                            GUI.backgroundColor = new Color(0.2f, 0.5f, 1f, 0.18f);
                        else if (isHovered)
                            GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.12f);
                        GUI.Box(rowRect, GUIContent.none);
                        GUI.backgroundColor = orig;
                        GUIStyle style = (idx == selectedClassIndex) ? EditorStyles.whiteLabel : EditorStyles.label;
                        string displayName = c.Name; 
                        if (GUI.Button(rowRect, new GUIContent(displayName, "Click to view class members"), style))
                        {
                            selectedClassIndex = idx;
                        }
                    }
                }
            };
            if (needsScroll)
            {
                Vector2 eventListScroll = Vector2.zero;
                eventListScroll = EditorGUILayout.BeginScrollView(eventListScroll, GUILayout.Height(maxVisible * 24f));
                renderList();
                EditorGUILayout.EndScrollView();
            }
            else
            {
                renderList();
            }
        }
        EditorGUILayout.EndVertical();
        // Right: Members of selected class
        EditorGUILayout.BeginVertical(GUILayout.Width(rightColWidth));
        EditorGUILayout.BeginVertical("box");
        memberScroll = EditorGUILayout.BeginScrollView(memberScroll, GUILayout.Height(180));
        if (selectedClassIndex >= 0 && selectedClassIndex < classList.Count)
        {
            EditorGUILayout.LabelField($"Members of {classList[selectedClassIndex].Name}:", EditorStyles.boldLabel);
            foreach (var member in classList[selectedClassIndex].Members)
            {
                EditorGUILayout.LabelField(member, EditorStyles.miniLabel);
            }
        }
        else
        {
            EditorGUILayout.LabelField("Select a class to view its members.", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        if (!string.IsNullOrEmpty(message))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(message, messageType);
        }
    }

    /// <summary>
    /// Called when the window is enabled or recompiled. Loads the class list for display.
    /// </summary>
    private void OnEnable()
    {
        // Fix any existing files that might have structural issues
        FixPayloadFile(ClientPayloadFile);
        FixPayloadFile(ServerPayloadFile);
        FixPayloadFile(CommonPayloadFile);
        
        LoadClassList();
    }

    /// <summary>
    /// Loads all payload classes from the client and server payload files into the class list for UI display.
    /// </summary>
    private void LoadClassList()
    {
        classList.Clear();
        selectedClassIndex = -1;
        LoadClassListFromFile(ClientPayloadFile, "Client");
        LoadClassListFromFile(ServerPayloadFile, "Server");
        LoadClassListFromFile(CommonPayloadFile, "Common");
    }

    /// <summary>
    /// Loads all classes from a given payload file, parsing class names, members, and event names for UI display.
    /// </summary>
    private void LoadClassListFromFile(string filePath, string sourceFile)
    {
        if (!File.Exists(filePath)) return;
        var lines = File.ReadAllLines(filePath);
        ClassInfo current = null;
        foreach (var line in lines)
        {
            var beginMatch = Regex.Match(line, @"// BEGIN ([A-Za-z0-9_]+)(?:\s*\((.*?)\))?");
            var endMatch = Regex.Match(line, @"// END ([A-Za-z0-9_]+)");
            var classMatch = Regex.Match(line, @"public class ([A-Za-z0-9_]+)");
            var memberMatch = Regex.Match(line, @"public ([^ ]+) ([A-Za-z0-9_]+);");
            if (beginMatch.Success)
            {
                current = new ClassInfo { Name = beginMatch.Groups[1].Value, SourceFile = sourceFile };
            }
            else if (endMatch.Success && current != null)
            {
                classList.Add(current);
                current = null;
            }
            else if (classMatch.Success && current != null)
            {
                // skip, already have name
            }
            else if (memberMatch.Success && current != null)
            {
                current.Members.Add($"{memberMatch.Groups[1].Value} {memberMatch.Groups[2].Value}");
            }
        }
    }

    /// <summary>
    /// Handles the main logic for generating or updating payload classes from the provided JSON and event name.
    /// Handles nested class generation, file writing, and UI reset.
    /// </summary>
    private void GenerateOrUpdatePayloadClass()
    {
        message = "";
        messageType = MessageType.None;
        if (string.IsNullOrEmpty(eventName) || string.IsNullOrEmpty(jsonPayload))
        {
            message = "Event name and JSON payload are required.";
            messageType = MessageType.Error;
            return;
        }
        // Do not generate payload class for common classes
        string eventNameUpper = eventName.Trim().ToUpper();
        if (CommonClassNames.Contains(eventNameUpper))
        {
            message = $"Skipping generation for common class/event name: {eventNameUpper}";
            messageType = MessageType.Info;
            return;
        }
        string rootClassName = ToEventDataClassName(eventName, selectedEventType);
        string filePath = selectedEventType == EventType.ServerEvent ? ServerPayloadFile : ClientPayloadFile;
        string fileHeader = "// Auto-generated payload classes\nusing System;\nusing System.Collections.Generic;\n\nnamespace NCL.AutoGenerated.Payload\n{\n";
        Dictionary<string, string> allGeneratedClasses;
        Dictionary<string, string> allGeneratedClassEventNames = new Dictionary<string, string>();
        try
        {
            // Always use the 'data' object for class generation
            var root = JObject.Parse(jsonPayload);
            if (root["data"] == null)
            {
                message = "JSON must contain a 'data' object at the root.";
                messageType = MessageType.Error;
                return;
            }
            var dataJson = root["data"].ToString();
            allGeneratedClasses = GenerateClassFromJsonRecursive(rootClassName, dataJson, new HashSet<string>(), eventName.Trim(), allGeneratedClassEventNames);
        }
        catch (Exception ex)
        {
            message = $"Failed to parse JSON: {ex.Message}";
            messageType = MessageType.Error;
            return;
        }
        if (!Directory.Exists(PayloadsFolder))
            Directory.CreateDirectory(PayloadsFolder);
        string fileContent = File.Exists(filePath) ? File.ReadAllText(filePath) : "";
        // If file does not exist, create it with a header
        if (!File.Exists(filePath))
        {
            fileContent = fileHeader;
        }
        // Remove any trailing closing brace for the namespace (if present)
        fileContent = Regex.Replace(fileContent, "}\\s*$", "", RegexOptions.Multiline);
        bool anyClassUpdated = false;
        foreach (var kvp in allGeneratedClasses)
        {
            string thisClassName = kvp.Key;
            string classCode = kvp.Value;
            string eventNameForComment = allGeneratedClassEventNames.ContainsKey(thisClassName) ? allGeneratedClassEventNames[thisClassName] : thisClassName;
            bool classExists = Regex.IsMatch(fileContent, $@"class\s+{thisClassName}\b");
            if (classExists)
            {
                if (!EditorUtility.DisplayDialog("Class Exists", $"Class {thisClassName} already exists in {Path.GetFileName(filePath)}. Update it?", "Update", "Skip"))
                {
                    continue;
                }
                // Replace the existing class with proper structure
                string replacementClass = $"// BEGIN {thisClassName} ({eventNameForComment})\n{classCode}\n// END {thisClassName}";
                fileContent = Regex.Replace(fileContent, $@"(\/\/\s*BEGIN\s*{thisClassName}.*?\/\/\s*END\s*{thisClassName})", replacementClass, RegexOptions.Singleline);
                anyClassUpdated = true;
            }
            else
            {
                // Append new class with proper spacing
                if (!string.IsNullOrWhiteSpace(fileContent) && !fileContent.EndsWith("\n"))
                    fileContent += "\n";
                fileContent += $"// BEGIN {thisClassName} ({eventNameForComment})\n{classCode}\n// END {thisClassName}\n";
                anyClassUpdated = true;
            }
        }
                // Clean up extra blank lines
        fileContent = Regex.Replace(fileContent, "\n{3,}", "\n\n");
        fileContent = fileContent.Trim();
        
        if (anyClassUpdated)
        {
            // Fix the file structure before writing
            fileContent = FixAllClassClosuresInFile(fileContent);
            
            // Validate and clean up the final structure
            fileContent = ValidateAndCleanFileStructure(fileContent);
            
            File.WriteAllText(filePath, fileContent);
            AssetDatabase.Refresh();
            
            // Automatically create common payload after generating/updating classes
            CreateCommonPayloadFile();
            
            // Add event to the appropriate enum in CardGameEvents class
            AddEventToCardGameEvents();
            
            message = $"Payload class(es) have been generated/updated in {Path.GetFileName(filePath)}.";
            messageType = MessageType.Info;
            ResetUIFields(); // Reset all UI fields after generation
        }
        else
        {
            message = "No classes were generated or updated.";
            messageType = MessageType.Info;
        }
    }


    /// <summary>
    /// Ensures all class blocks in the file end with a closing bracket '}' before the // END marker, and the namespace is closed.
    /// </summary>
    private string FixAllClassClosuresInFile(string fileContent)
    {
        var lines = fileContent.Split('\n');
        var result = new List<string>();
        bool inClass = false;
        int namespaceBraceCount = 0;
        string currentClassName = "";
        bool classHasClosingBrace = false;
        
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            
            // Check if we're starting a class
            if (line.Trim().StartsWith("// BEGIN"))
            {
                inClass = true;
                classHasClosingBrace = false;
                // Extract class name from BEGIN marker
                var match = Regex.Match(line, @"// BEGIN ([A-Za-z0-9_]+)");
                if (match.Success)
                {
                    currentClassName = match.Groups[1].Value;
                }
                result.Add(line);
                continue;
            }
            
            // Check if we're ending a class
            if (line.Trim().StartsWith("// END"))
            {
                // Only add a closing brace if we don't already have one
                if (inClass && !classHasClosingBrace)
                {
                    Debug.Log($"Adding closing brace for class {currentClassName}");
                    result.Add("    }");
                }
                result.Add(line);
                inClass = false;
                currentClassName = "";
                classHasClosingBrace = false;
                continue;
            }
            
            // Check if this line has a closing brace for the class
            if (inClass && line.Trim() == "}")
            {
                classHasClosingBrace = true;
            }
            
            // Count namespace braces (ignoring braces in strings and comments)
            namespaceBraceCount += CountBracesInLine(line, false);
            result.Add(line);
        }
        
        // If we're still in a class at the end and don't have a closing brace, add one
        if (inClass && !classHasClosingBrace)
        {
            Debug.Log($"Adding final closing brace for class {currentClassName}");
            result.Add("    }");
        }
        
        // Only add namespace closing brace if we're not in a class and namespace is open
        if (!inClass && namespaceBraceCount > 0)
        {
            result.Add("}");
        }
        
        return string.Join("\n", result);
    }
    
    /// <summary>
    /// Counts opening and closing braces in a line, optionally ignoring braces in strings and comments.
    /// </summary>
    private int CountBracesInLine(string line, bool ignoreInStrings)
    {
        int count = 0;
        bool inString = false;
        bool inComment = false;
        bool escapeNext = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }
            
            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }
            
            if (ignoreInStrings)
            {
                if (c == '"' && !inComment)
                {
                    inString = !inString;
                    continue;
                }
                
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/' && !inString)
                {
                    break; // Single line comment
                }
                
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '*' && !inString)
                {
                    inComment = true;
                    i++; // Skip the *
                    continue;
                }
                
                if (c == '*' && i + 1 < line.Length && line[i + 1] == '/' && inComment)
                {
                    inComment = false;
                    i++; // Skip the /
                    continue;
                }
                
                if (inString || inComment)
                    continue;
            }
            
            if (c == '{')
                count++;
            else if (c == '}')
                count--;
        }
        
        return count;
    }
    
    /// <summary>
    /// Validates and cleans up the final file structure to ensure proper formatting.
    /// </summary>
    private string ValidateAndCleanFileStructure(string fileContent)
    {
        // Remove any extra closing braces that might be after END markers
        fileContent = Regex.Replace(fileContent, @"(\/\/\s*END\s*[A-Za-z0-9_]+)\s*}\s*", "$1", RegexOptions.Multiline);
        
        // Clean up extra blank lines
        fileContent = Regex.Replace(fileContent, "\n{3,}", "\n\n");
        fileContent = fileContent.Trim();
        
        // Ensure the file ends with the namespace closing brace only if we have content
        if (!string.IsNullOrWhiteSpace(fileContent) && !fileContent.TrimEnd().EndsWith("}"))
        {
            fileContent += "\n}";
        }
        
        return fileContent;
    }
    
    /// <summary>
    /// Recursively generates all required classes (main and nested) from the JSON, returning a dictionary of class name to class code.
    /// The eventNameForThisClass is always the root event name for all classes in a single generation.
    /// </summary>
    private Dictionary<string, string> GenerateClassFromJsonRecursive(string className, string json, HashSet<string> generatedClassNames, string eventNameForThisClass, Dictionary<string, string> classEventNames)
    {
        var newClasses = new Dictionary<string, string>();
        if (generatedClassNames.Contains(className)) return newClasses;
        generatedClassNames.Add(className);
        if (!classEventNames.ContainsKey(className))
            classEventNames[className] = eventNameForThisClass;
        var root = JObject.Parse(json);
        var sb = new StringBuilder();
        sb.AppendLine($"[System.Serializable]");
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");
        foreach (var prop in root.Properties())
        {
            if (prop.Name == "metrics") continue;
            var nestedClasses = new Dictionary<string, string>();
            var nestedClassEventNames = new Dictionary<string, string>();
            string memberType = GetCSharpTypeRecursive(prop.Value, prop.Name, nestedClasses, generatedClassNames, prop.Name, nestedClassEventNames);
            sb.AppendLine($"    public {memberType} {prop.Name};");
            // Merge nested classes and their event names
            foreach (var kvp in nestedClasses)
            {
                if (!newClasses.ContainsKey(kvp.Key)) newClasses.Add(kvp.Key, kvp.Value);
            }
            foreach (var kvp in nestedClassEventNames)
            {
                if (!classEventNames.ContainsKey(kvp.Key)) classEventNames[kvp.Key] = kvp.Value;
            }
        }
        sb.AppendLine("}");
        newClasses[className] = sb.ToString();
        return newClasses;
    }

    /// <summary>
    /// Helper for recursive type generation. Handles arrays, objects, and primitive types.
    /// Ensures all nested classes are generated and event names are tracked.
    /// </summary>
    private string GetCSharpTypeRecursive(JToken token, string propName, Dictionary<string, string> allGeneratedClasses, HashSet<string> generatedClassNames, string eventNameForThisClass, Dictionary<string, string> classEventNames)
    {
        switch (token.Type)
        {
            case JTokenType.Integer: return "int";
            case JTokenType.Float: return "float";
            case JTokenType.Boolean: return "bool";
            case JTokenType.Array:
                var arr = token as JArray;
                if (arr.Count > 0)
                    return $"List<{GetCSharpTypeRecursive(arr[0], propName + "Item", allGeneratedClasses, generatedClassNames, eventNameForThisClass, classEventNames)}>";
                else
                    return "List<object>";
            case JTokenType.Object:
                string nestedClassName = propName;
                var nestedClasses = GenerateClassFromJsonRecursive(nestedClassName, token.ToString(), generatedClassNames, eventNameForThisClass, classEventNames);
                // Merge newly generated classes and event names into the dictionaries
                foreach (var kvp in nestedClasses)
                {
                    if (!allGeneratedClasses.ContainsKey(kvp.Key)) allGeneratedClasses.Add(kvp.Key, kvp.Value);
                }
                foreach (var kvp in classEventNames)
                {
                    if (!classEventNames.ContainsKey(kvp.Key)) classEventNames[kvp.Key] = kvp.Value;
                }
                return nestedClassName;
            case JTokenType.String: return "string";
            case JTokenType.Null: return "string";
            default: return "string";
        }
    }

    /// <summary>
    /// Resets all UI fields and state to their defaults.
    /// </summary>
    private void ResetUIFields()
    {
        eventName = "";
        jsonPayload = "";
        selectedEventType = EventType.ClientEvent;
        selectedClassIndex = -1;
        hoveredClassIndex = -1;
        message = "";
        messageType = MessageType.None;
        jsonScroll = Vector2.zero;
        eventListScroll = Vector2.zero;
        memberScroll = Vector2.zero;
    }

    /// <summary>
    /// Converts a string to PascalCase for class and property names.
    /// </summary>
    private string ToPascalCase(string input)
    {
        return Regex.Replace(input, "(^|_)([a-z])", m => m.Groups[2].Value.ToUpper());
    }

    /// <summary>
    /// Converts an event name and type to the correct class name (e.g., _DATA, _ACKDATA).
    /// </summary>
    private string ToEventDataClassName(string input, EventType type)
    {
        // Convert to uppercase, replace non-alphanumeric with _
        string baseName = Regex.Replace(input.Trim(), "[^a-zA-Z0-9]+", "_").ToUpper();
        if (type == EventType.ClientEventAck)
            return baseName + "_ACKDATA";
        else
            return baseName + "_DATA";
    }
    
    /// <summary>
    /// Creates CommonPayload.cs by scanning ClientPayload.cs and ServerPayload.cs for common classes.
    /// Moves identical classes to CommonPayload.cs and removes them from the original files.
    /// Also removes classes from client/server files that already exist in CommonPayload.cs.
    /// </summary>
    private void CreateCommonPayloadFile()
    {
        message = "";
        messageType = MessageType.None;

        try
        {
            var clientClasses = ParseClassesFromFile(ClientPayloadFile);
            var serverClasses = ParseClassesFromFile(ServerPayloadFile);
            var existingCommonClasses = ParseClassesFromFile(CommonPayloadFile);
            var commonClasses = new Dictionary<string, string>();
            var classesToRemove = new HashSet<string>();

            // Find common classes by name and member signature between client and server
            foreach (var kvp in clientClasses)
            {
                if (serverClasses.TryGetValue(kvp.Key, out var serverClassCode))
                {
                    if (NormalizeClassCode(kvp.Value) == NormalizeClassCode(serverClassCode))
                    {
                        commonClasses[kvp.Key] = kvp.Value;
                        classesToRemove.Add(kvp.Key);
                    }
                }
            }

            // Also find classes in client/server that already exist in CommonPayload.cs
            foreach (var kvp in clientClasses)
            {
                if (existingCommonClasses.TryGetValue(kvp.Key, out var commonClassCode))
                {
                    if (NormalizeClassCode(kvp.Value) == NormalizeClassCode(commonClassCode))
                    {
                        classesToRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var kvp in serverClasses)
            {
                if (existingCommonClasses.TryGetValue(kvp.Key, out var commonClassCode))
                {
                    if (NormalizeClassCode(kvp.Value) == NormalizeClassCode(commonClassCode))
                    {
                        classesToRemove.Add(kvp.Key);
                    }
                }
            }

            if (classesToRemove.Count == 0 && commonClasses.Count == 0)
            {
                message = "No common classes found between Client and Server payloads.";
                messageType = MessageType.Info;
                return;
            }

            // Remove common classes from client/server files
            RemoveClassesFromFile(ClientPayloadFile, classesToRemove);
            RemoveClassesFromFile(ServerPayloadFile, classesToRemove);

            // Fix the remaining files to ensure proper structure
            FixPayloadFile(ClientPayloadFile);
            FixPayloadFile(ServerPayloadFile);

            // Create CommonPayload.cs
            string fileHeader = "// Auto-generated payload classes\nusing System;\nusing System.Collections.Generic;\n\nnamespace NCL.AutoGenerated.Payload\n{\n";
            string commonFileContent = File.Exists(CommonPayloadFile) ? File.ReadAllText(CommonPayloadFile) : fileHeader;

            // Remove any trailing closing brace for the namespace (if present)
            commonFileContent = Regex.Replace(commonFileContent, "}\\s*$", "", RegexOptions.Multiline);

            foreach (var kvp in commonClasses)
            {
                // Remove any existing version of this class
                commonFileContent = Regex.Replace(commonFileContent, $@"(\/\/\s*BEGIN\s*{kvp.Key}.*?\/\/\s*END\s*{kvp.Key})", "", RegexOptions.Singleline);
                
                // Add the class and ensure it has proper structure
                string classContent = kvp.Value.TrimEnd();
                commonFileContent += classContent + "\n";
            }

            // Clean up the common file content and fix any structural issues
            commonFileContent = FixAllClassClosuresInFile(commonFileContent);
            commonFileContent = ValidateAndCleanFileStructure(commonFileContent);
            
            // Ensure the file ends with the namespace closing brace
            if (!commonFileContent.TrimEnd().EndsWith("}"))
                commonFileContent += "}\n";

            // Create directory if it doesn't exist
            if (!Directory.Exists(PayloadsFolder))
                Directory.CreateDirectory(PayloadsFolder);

            File.WriteAllText(CommonPayloadFile, commonFileContent);
            AssetDatabase.Refresh();

            string actionMessage = "";
            if (commonClasses.Count > 0)
            {
                actionMessage += $"Moved {commonClasses.Count} new common class(es) to CommonPayload.cs: {string.Join(", ", commonClasses.Keys)}. ";
            }
            if (classesToRemove.Count > commonClasses.Count)
            {
                actionMessage += $"Removed {classesToRemove.Count - commonClasses.Count} duplicate class(es) from client/server files.";
            }
            
            message = actionMessage.Trim();
            messageType = MessageType.Info;
        }
        catch (Exception ex)
        {
            message = $"Failed to create CommonPayload.cs: {ex.Message}";
            messageType = MessageType.Error;
        }
    }

    /// <summary>
    /// Parses all classes from a payload file, returning a dictionary of class name to full class block (including BEGIN/END markers).
    /// </summary>
    private Dictionary<string, string> ParseClassesFromFile(string filePath)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(filePath)) return result;

        var lines = File.ReadAllLines(filePath);
        StringBuilder currentClass = null;
        string currentClassName = null;

        foreach (var line in lines)
        {
            var beginMatch = Regex.Match(line, @"// BEGIN ([A-Za-z0-9_]+)");
            var endMatch = Regex.Match(line, @"// END ([A-Za-z0-9_]+)");

            if (beginMatch.Success)
            {
                currentClassName = beginMatch.Groups[1].Value;
                currentClass = new StringBuilder();
                currentClass.AppendLine(line);
            }
            else if (endMatch.Success && currentClass != null && currentClassName != null)
            {
                currentClass.AppendLine(line);
                result[currentClassName] = currentClass.ToString();
                currentClass = null;
                currentClassName = null;
            }
            else if (currentClass != null)
            {
                currentClass.AppendLine(line);
            }
        }

        return result;
    }

    /// <summary>
    /// Removes classes by name from a payload file.
    /// </summary>
    private void RemoveClassesFromFile(string filePath, IEnumerable<string> classNames)
    {
        if (!File.Exists(filePath)) return;

        string content = File.ReadAllText(filePath);
        
        // Remove each class block
        foreach (var className in classNames)
        {
            content = Regex.Replace(content, $@"(\/\/\s*BEGIN\s*{className}.*?\/\/\s*END\s*{className})", "", RegexOptions.Singleline);
        }

        // Remove extra blank lines and clean up the file
        content = Regex.Replace(content, "\n{3,}", "\n\n");
        content = content.Trim();
        
        // Ensure the file has proper structure
        if (!content.StartsWith("// Auto-generated payload classes"))
        {
            content = "// Auto-generated payload classes\nusing System;\nusing System.Collections.Generic;\n\nnamespace NCL.AutoGenerated.Payload\n{\n" + content;
        }
        
        // Clean up extra blank lines
        content = Regex.Replace(content, "\n{3,}", "\n\n");
        content = content.Trim();
        
        // Validate and clean up the structure
        content = ValidateAndCleanFileStructure(content);
        
        // Fix class closures and namespace
        content = FixAllClassClosuresInFile(content);
        
        File.WriteAllText(filePath, content);
    }

    /// <summary>
    /// Normalizes class code for comparison (ignores whitespace and line endings).
    /// </summary>
    private string NormalizeClassCode(string code)
    {
        return Regex.Replace(code, "\\s+", " ").Trim();
    }
    
    /// <summary>
    /// Fixes the structure of a payload file by ensuring proper closing braces.
    /// </summary>
    private void FixPayloadFile(string filePath)
    {
        if (!File.Exists(filePath)) return;
        
        string content = File.ReadAllText(filePath);
        string fixedContent = FixAllClassClosuresInFile(content);
        
        if (content != fixedContent)
        {
            File.WriteAllText(filePath, fixedContent);
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// Adds the generated event to the appropriate enum in CardGameEvents.cs.
    /// </summary>
    private void AddEventToCardGameEvents()
    {
        try
        {
            // Use the EventsClassUpdater to add the event to the correct enum
            // Default to CardGame if no specific game is detected
            EventsClassUpdater.UpdateEventsClass("CardGame", eventName, selectedEventType);
            
            Debug.Log($"Successfully added event '{eventName}' (Type: {selectedEventType}) to CardGameEvents class");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to add event to CardGameEvents: {ex.Message}");
            // Don't show error message to user as this is not critical for payload generation
        }
    }
} 