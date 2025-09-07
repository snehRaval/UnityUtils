using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// Data structure to hold parsed event information.
/// </summary>
[System.Serializable]
public class ParsedEventData
{
    public string eventName;
    public EventToPayloadClassGeneratorWindow.EventType eventType;
    public string jsonPayload;
    public int lineNumber;

    public ParsedEventData(string name, EventToPayloadClassGeneratorWindow.EventType type, string payload, int line)
    {
        eventName = name;
        eventType = type;
        jsonPayload = payload;
        lineNumber = line;
    }
}

/// <summary>
/// Editor window to read text files and parse multiple Event Names, Event Types, and JSON payloads.
/// Automatically populates PayloadClassGeneratorWindow fields.
/// </summary>
public class JSONToPayloadClassesGeneratorWindow : EditorWindow
{
    private string textFilePath = "Assets/payload_content.txt";
    private string message = "";
    private MessageType messageType = MessageType.None;
    private bool isProcessing = false;

    // Parsed data
    private List<ParsedEventData> parsedEvents = new List<ParsedEventData>();
    private int selectedEventIndex = -1;
    private Vector2 eventListScrollPosition = Vector2.zero;
    private Vector2 payloadPreviewScrollPosition = Vector2.zero;

    /// <summary>
    /// Opens the Text File Parser window from the Unity menu.
    /// </summary>
    [MenuItem("Sneh/Payload Gen/JSON to Payload Classes Generator", false, 4)]
    public static void ShowWindow()
    {
        GetWindow<JSONToPayloadClassesGeneratorWindow>("JSON to Payload Classes Generator");
    }

    /// <summary>
    /// Main Unity Editor UI rendering function.
    /// </summary>
    private void OnGUI()
    {
        GUILayout.Space(8);
        EditorGUILayout.HelpBox("Read text files and parse multiple Event Names, Event Types, and JSON payloads. Automatically populates PayloadClassGeneratorWindow.", MessageType.Info);
        GUILayout.Space(6);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("JSON File Parser", EditorStyles.boldLabel);
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

        // Parse button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.enabled = !isProcessing;
        if (GUILayout.Button("Parse Text File", GUILayout.Width(150), GUILayout.Height(32)))
        {
            ParseTextFile();
        }
        GUI.enabled = true;
        if (GUILayout.Button("Clear", GUILayout.Width(100), GUILayout.Height(32)))
        {
            ClearFields();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Parsed events display
        if (parsedEvents.Count > 0)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Parsed Events ({parsedEvents.Count} found)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Generate for all events button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Generate Payload for All Events", GUILayout.Width(250), GUILayout.Height(28)))
            {
                GeneratePayloadForAllEvents();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Events list and details in horizontal layout
            EditorGUILayout.BeginHorizontal();

            // Left side - Events list
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.4f));
            EditorGUILayout.LabelField("Events List:", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            eventListScrollPosition = EditorGUILayout.BeginScrollView(eventListScrollPosition, GUILayout.Height(300));
            
            for (int i = 0; i < parsedEvents.Count; i++)
            {
                var eventData = parsedEvents[i];
                EditorGUILayout.BeginHorizontal("box");
                
                // Selection indicator
                if (selectedEventIndex == i)
                {
                    GUI.backgroundColor = Color.green;
                }
                
                // Event button
                if (GUILayout.Button($"{eventData.eventName}\n{eventData.eventType}", GUILayout.Height(40)))
                {
                    selectedEventIndex = i;
                }
                
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Right side - Selected event details
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.6f - 20));
            
            if (selectedEventIndex >= 0 && selectedEventIndex < parsedEvents.Count)
            {
                var selectedEvent = parsedEvents[selectedEventIndex];
                
                EditorGUILayout.LabelField("Selected Event Details:", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                // Event Name
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Event Name:", GUILayout.Width(100));
                EditorGUILayout.LabelField(selectedEvent.eventName, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                // Event Type
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Event Type:", GUILayout.Width(100));
                EditorGUILayout.LabelField(selectedEvent.eventType.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                // JSON Payload preview
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("JSON Payload Preview:");
                EditorGUILayout.BeginVertical("box");
                payloadPreviewScrollPosition = EditorGUILayout.BeginScrollView(payloadPreviewScrollPosition, GUILayout.Height(150));
                EditorGUILayout.TextArea(selectedEvent.jsonPayload, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(4);

                // Generate Payload button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Generate Payload", GUILayout.Width(200), GUILayout.Height(28)))
                {
                    GeneratePayloadForEvent(selectedEvent);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Select an event from the list to view details.", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);

        // Message display
        if (!string.IsNullOrEmpty(message))
        {
            EditorGUILayout.HelpBox(message, messageType);
        }

        // Processing indicator
        if (isProcessing)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Parsing Text File...", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Please wait while the text file is being parsed.");
            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// Parses the text file to extract multiple Event Names, Event Types, and JSON payloads.
    /// </summary>
    private void ParseTextFile()
    {
        message = "";
        messageType = MessageType.None;
        isProcessing = true;

        try
        {
            // Validate input file
            if (string.IsNullOrEmpty(textFilePath))
            {
                message = "Please select a text file.";
                messageType = MessageType.Warning;
                isProcessing = false;
                return;
            }

            string fullTextPath = GetFullPath(textFilePath);
            if (!File.Exists(fullTextPath))
            {
                message = $"Text file not found: {fullTextPath}";
                messageType = MessageType.Error;
                isProcessing = false;
                return;
            }

            // Read text file content
            string fileContent = File.ReadAllText(fullTextPath);
            
            if (string.IsNullOrEmpty(fileContent))
            {
                message = "Text file is empty.";
                messageType = MessageType.Warning;
                isProcessing = false;
                return;
            }

            // Parse the content
            ParseMultipleEvents(fileContent);

            message = $"Successfully parsed {parsedEvents.Count} events from text file.";
            messageType = MessageType.Info;
        }
        catch (System.Exception ex)
        {
            message = $"Error parsing text file: {ex.Message}";
            messageType = MessageType.Error;
            Debug.LogError($"Text file parsing error: {ex.Message}");
        }
        finally
        {
            isProcessing = false;
        }
    }

    /// <summary>
    /// Parses the text content to extract multiple Event Names, Event Types, and JSON payloads.
    /// </summary>
    /// <param name="content">The text content to parse</param>
    private void ParseMultipleEvents(string content)
    {
        // Reset parsed data
        parsedEvents.Clear();
        selectedEventIndex = -1;

        // Split content into lines for line number tracking
        string[] lines = content.Split('\n');
        
        // Find all event blocks in the content
        var eventBlocks = FindEventBlocks(content, lines);
        
        // Track unique events to prevent duplicates
        var uniqueEvents = new Dictionary<string, ParsedEventData>();
        
        foreach (var block in eventBlocks)
        {
            var eventData = ParseEventBlock(block, lines);
            if (eventData != null && !string.IsNullOrEmpty(eventData.eventName))
            {
                // Create a unique key that includes both event name and type to prevent duplicates
                // but allow same name with different types (e.g., GROUP_CARDS vs GROUP_CARDS_ACK)
                string uniqueKey = $"{eventData.eventName}_{eventData.eventType}";
                
                if (!uniqueEvents.ContainsKey(uniqueKey))
                {
                    uniqueEvents[uniqueKey] = eventData;
                    Debug.Log($"Added event: {eventData.eventName} ({eventData.eventType})");
                }
                else
                {
                    Debug.Log($"Skipping duplicate event: {eventData.eventName} ({eventData.eventType})");
                }
            }
        }
        
        // Add unique events to the list
        parsedEvents.AddRange(uniqueEvents.Values);
        
        // Generate ACK events for Client and Server events
        GenerateAckEvents();
    }

    /// <summary>
    /// Finds all event blocks in the content.
    /// </summary>
    /// <param name="content">Full content</param>
    /// <param name="lines">Content split into lines</param>
    /// <returns>List of event block strings</returns>
    private List<string> FindEventBlocks(string content, string[] lines)
    {
        var blocks = new List<string>();
        var processedPositions = new HashSet<int>();
        
        // Look for patterns that indicate event blocks
        // Pattern 1: Event Name: followed by Event Type: and JSON
        var eventNameMatches = Regex.Matches(content, @"Event Name:\s*(.+)", RegexOptions.IgnoreCase);
        
        foreach (System.Text.RegularExpressions.Match match in eventNameMatches)
        {
            int startIndex = match.Index;
            int endIndex = content.Length;
            
            // Find the end of this event block (next Event Name: or end of content)
            var nextEventMatch = Regex.Match(content.Substring(startIndex + match.Length), @"Event Name:\s*", RegexOptions.IgnoreCase);
            if (nextEventMatch.Success)
            {
                endIndex = startIndex + match.Length + nextEventMatch.Index;
            }
            
            string block = content.Substring(startIndex, endIndex - startIndex).Trim();
            blocks.Add(block);
        }
        
        // If no Event Name: patterns found, try to find JSON blocks with "en" key
        if (blocks.Count == 0)
        {
            // Look for JSON objects that contain "en" key
            var enMatches = Regex.Matches(content, @"""en""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match enMatch in enMatches)
            {
                int jsonStart = enMatch.Index;
                
                // Skip if we've already processed this position
                if (processedPositions.Contains(jsonStart))
                    continue;
                
                // Find the start of the JSON object (look backwards for {)
                int braceStart = content.LastIndexOf('{', jsonStart);
                if (braceStart == -1 || braceStart < Math.Max(0, jsonStart - 1000))
                    continue;
                
                // Find the end of the JSON object using brace counting
                int braceCount = 0;
                int jsonEnd = -1;
                bool inString = false;
                bool escaped = false;
                
                for (int i = braceStart; i < content.Length; i++)
                {
                    char c = content[i];
                    
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }
                    
                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    
                    if (c == '"' && !escaped)
                    {
                        inString = !inString;
                        continue;
                    }
                    
                    if (!inString)
                    {
                        if (c == '{')
                        {
                            braceCount++;
                        }
                        else if (c == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                jsonEnd = i;
                                break;
                            }
                        }
                    }
                }
                
                if (jsonEnd > jsonStart)
                {
                    // Extract the complete JSON object
                    string jsonObject = content.Substring(braceStart, jsonEnd - braceStart + 1);
                    
                    // Add the JSON object as a block
                    blocks.Add(jsonObject);
                    
                    // Mark this position as processed
                    processedPositions.Add(jsonStart);
                }
            }
        }
        
        return blocks;
    }

    /// <summary>
    /// Parses a single event block to extract event data.
    /// </summary>
    /// <param name="block">Event block string</param>
    /// <param name="lines">Full content lines for line number calculation</param>
    /// <returns>Parsed event data or null if parsing failed</returns>
    private ParsedEventData ParseEventBlock(string block, string[] lines)
    {
        string eventName = "";
        EventToPayloadClassGeneratorWindow.EventType eventType = EventToPayloadClassGeneratorWindow.EventType.ClientEvent;
        string jsonPayload = "";
        int lineNumber = 1;

        // Parse JSON Payload first
        jsonPayload = ParseJsonFromBlock(block);

        // Extract event name from JSON "en" key (priority)
        eventName = ExtractEventNameFromJson(block);
        
        // If no event name found in JSON, try Event Name: field as fallback
        if (string.IsNullOrEmpty(eventName))
        {
            var eventNameMatch = Regex.Match(block, @"Event Name:\s*(.+)", RegexOptions.IgnoreCase);
            if (eventNameMatch.Success)
            {
                eventName = eventNameMatch.Groups[1].Value.Trim();
            }
        }

        // Extract event type from JSON "Event Type" key (priority)
        eventType = ExtractEventTypeFromJson(block);
        
        // If no event type found in JSON, try Event Type: field as fallback
        if (eventType == EventToPayloadClassGeneratorWindow.EventType.ClientEvent)
        {
            var eventTypeMatch = Regex.Match(block, @"Event Type:\s*(.+)", RegexOptions.IgnoreCase);
            if (eventTypeMatch.Success)
            {
                string eventTypeStr = eventTypeMatch.Groups[1].Value.Trim();
                eventType = ParseEventType(eventTypeStr);
            }
        }

        // Calculate line number
        lineNumber = CalculateLineNumber(block, lines);

        // Only return if we have at least an event name or JSON payload
        if (!string.IsNullOrEmpty(eventName) || !string.IsNullOrEmpty(jsonPayload))
        {
            return new ParsedEventData(eventName, eventType, jsonPayload, lineNumber);
        }

        return null;
    }

    /// <summary>
    /// Parses JSON payload from an event block.
    /// </summary>
    /// <param name="block">Event block string</param>
    /// <returns>Extracted JSON string</returns>
    private string ParseJsonFromBlock(string block)
    {
        // Method 1: Try to find JSON using brace counting (more reliable for complex JSON)
        string json = ExtractJsonByBraceCounting(block);
        if (!string.IsNullOrEmpty(json))
        {
            return CleanJsonPayload(json);
        }

        // Method 2: Try regex as fallback
        var jsonMatch = Regex.Match(block, @"(\{(?:[^{}]|(?:\{[^{}]*\}))*\})", RegexOptions.Singleline);
        if (jsonMatch.Success)
        {
            string jsonRegex = jsonMatch.Groups[1].Value.Trim();
            return CleanJsonPayload(jsonRegex);
        }

        // Method 3: Try alternative approach - look for content between specific markers
        var startMatch = Regex.Match(block, @"JSON Payload:\s*", RegexOptions.IgnoreCase);
        if (startMatch.Success)
        {
            int startIndex = startMatch.Index + startMatch.Length;
            string remainingContent = block.Substring(startIndex);
            
            // Find the first { and last }
            int firstBrace = remainingContent.IndexOf('{');
            int lastBrace = remainingContent.LastIndexOf('}');
            
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                string jsonMarker = remainingContent.Substring(firstBrace, lastBrace - firstBrace + 1);
                return CleanJsonPayload(jsonMarker);
            }
        }

        return "";
    }

    /// <summary>
    /// Extracts JSON using brace counting method for complex nested structures.
    /// </summary>
    /// <param name="block">Text block to search</param>
    /// <returns>Extracted JSON string or empty string</returns>
    private string ExtractJsonByBraceCounting(string block)
    {
        int startIndex = block.IndexOf('{');
        if (startIndex == -1) return "";

        int braceCount = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = startIndex; i < block.Length; i++)
        {
            char c = block[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"' && !escaped)
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == '{')
                {
                    braceCount++;
                }
                else if (c == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        // Found complete JSON object
                        return block.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }
        }

        return "";
    }

    /// <summary>
    /// Extracts event name from JSON payload using the "en" key.
    /// </summary>
    /// <param name="block">Event block string</param>
    /// <returns>Extracted event name or empty string</returns>
    private string ExtractEventNameFromJson(string block)
    {
        // Try to find JSON content first
        string jsonContent = ParseJsonFromBlock(block);
        
        if (!string.IsNullOrEmpty(jsonContent))
        {
            try
            {
                // Parse JSON and look for "en" key
                var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonContent);
                
                if (jsonObject != null && ((IDictionary<string, JToken>) jsonObject).ContainsKey("en"))
                {
                    string eventName = jsonObject["en"].ToString();
                    Debug.Log($"Extracted event name from JSON 'en' key: {eventName}");
                    return eventName;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to parse JSON for event name extraction: {ex.Message}");
            }
        }

        // Fallback: try regex extraction from the block
        var enMatch = Regex.Match(block, @"""en""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (enMatch.Success)
        {
            string eventName = enMatch.Groups[1].Value.Trim();
            Debug.Log($"Extracted event name using regex: {eventName}");
            return eventName;
        }

        return "";
    }

    /// <summary>
    /// Extracts event type from JSON payload using the "Event Type" key.
    /// </summary>
    /// <param name="block">Event block string</param>
    /// <returns>Extracted event type or ClientEvent as default</returns>
    private EventToPayloadClassGeneratorWindow.EventType ExtractEventTypeFromJson(string block)
    {
        // Try to find JSON content first
        string jsonContent = ParseJsonFromBlock(block);
        
        if (!string.IsNullOrEmpty(jsonContent))
        {
            try
            {
                // Parse JSON and look for "Event Type" key
                var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonContent);
                
                if (jsonObject != null && ((IDictionary<string, JToken>) jsonObject).ContainsKey("Event Type"))
                {
                    string eventTypeStr = jsonObject["Event Type"].ToString();
                    var eventType = ParseEventType(eventTypeStr);
                    Debug.Log($"Extracted event type from JSON 'Event Type' key: {eventTypeStr} -> {eventType}");
                    return eventType;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to parse JSON for event type extraction: {ex.Message}");
            }
        }

        // Fallback: try regex extraction from the block (handle both quoted and unquoted keys)
        var eventTypeMatch = Regex.Match(block, @"""Event Type""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (eventTypeMatch.Success)
        {
            string eventTypeStr = eventTypeMatch.Groups[1].Value.Trim();
            var eventType = ParseEventType(eventTypeStr);
            Debug.Log($"Extracted event type using regex: {eventTypeStr} -> {eventType}");
            return eventType;
        }

        // Try alternative regex pattern for different quote styles
        eventTypeMatch = Regex.Match(block, @"""Event Type""\s*:\s*'([^']+)'", RegexOptions.IgnoreCase);
        if (eventTypeMatch.Success)
        {
            string eventTypeStr = eventTypeMatch.Groups[1].Value.Trim();
            var eventType = ParseEventType(eventTypeStr);
            Debug.Log($"Extracted event type using regex (single quotes): {eventTypeStr} -> {eventType}");
            return eventType;
        }

        return EventToPayloadClassGeneratorWindow.EventType.ClientEvent;
    }

    /// <summary>
    /// Calculates the line number for an event block.
    /// </summary>
    /// <param name="block">Event block string</param>
    /// <param name="lines">Full content lines</param>
    /// <returns>Line number</returns>
    private int CalculateLineNumber(string block, string[] lines)
    {
        // Find the first line that contains part of the block
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(block.Substring(0, Math.Min(50, block.Length))))
            {
                return i + 1;
            }
        }
        return 1;
    }

    /// <summary>
    /// Parses the Event Type string to enum value.
    /// </summary>
    /// <param name="eventTypeStr">Event type string</param>
    /// <returns>Parsed EventType enum value</returns>
    private EventToPayloadClassGeneratorWindow.EventType ParseEventType(string eventTypeStr)
    {
        if (string.IsNullOrEmpty(eventTypeStr))
            return EventToPayloadClassGeneratorWindow.EventType.ClientEvent;

        switch (eventTypeStr.ToLower().Trim())
        {
            case "client event":
            case "client":
            case "cts":
                return EventToPayloadClassGeneratorWindow.EventType.ClientEvent;
            case "client ack":
            case "clientack":
            case "client event ack":
            case "cts ack":
                return EventToPayloadClassGeneratorWindow.EventType.ClientEventAck;
            case "server event":
            case "server":
            case "stc":
                return EventToPayloadClassGeneratorWindow.EventType.ServerEvent;
            default:
                Debug.LogWarning($"Unknown event type: '{eventTypeStr}', defaulting to ClientEvent");
                return EventToPayloadClassGeneratorWindow.EventType.ClientEvent;
        }
    }

    /// <summary>
    /// Cleans up the JSON payload by removing extra whitespace and formatting.
    /// </summary>
    /// <param name="json">Raw JSON string</param>
    /// <returns>Cleaned JSON string</returns>
    private string CleanJsonPayload(string json)
    {
        // Remove extra whitespace and newlines
        json = Regex.Replace(json, @"\s+", " ");
        json = json.Trim();
        
        // Ensure proper JSON formatting
        try
        {
            // Try to parse and format the JSON
            var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
            return Newtonsoft.Json.JsonConvert.SerializeObject(jsonObject, Newtonsoft.Json.Formatting.Indented);
        }
        catch
        {
            // If JSON parsing fails, return the cleaned string
            return json;
        }
    }

    /// <summary>
    /// Generates payload classes for all parsed events and updates events classes.
    /// </summary>
    private void GeneratePayloadForAllEvents()
    {
        if (parsedEvents.Count == 0)
        {
            message = "No events to generate payloads for.";
            messageType = MessageType.Warning;
            return;
        }

        try
        {
            int successCount = 0;
            int errorCount = 0;
            var eventsByGame = new Dictionary<string, Dictionary<EventToPayloadClassGeneratorWindow.EventType, List<ParsedEventData>>>();

            // Find or create PayloadClassGeneratorWindow
            var payloadWindow = EditorWindow.GetWindow<EventToPayloadClassGeneratorWindow>("Payload Class Generator");

            // Use reflection to get the GenerateOrUpdatePayloadClass method
            var generateMethod = typeof(EventToPayloadClassGeneratorWindow).GetMethod("GenerateOrUpdatePayloadClass", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (generateMethod == null)
            {
                message = "Could not find GenerateOrUpdatePayloadClass method in PayloadClassGeneratorWindow.";
                messageType = MessageType.Error;
                return;
            }

            // Process each event
            foreach (var eventData in parsedEvents)
            {
                try
                {
                    // Set the fields using reflection
                    var eventNameField = typeof(EventToPayloadClassGeneratorWindow).GetField("eventName", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var eventTypeField = typeof(EventToPayloadClassGeneratorWindow).GetField("selectedEventType", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var jsonPayloadField = typeof(EventToPayloadClassGeneratorWindow).GetField("jsonPayload", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (eventNameField != null && !string.IsNullOrEmpty(eventData.eventName))
                    {
                        eventNameField.SetValue(payloadWindow, eventData.eventName);
                    }

                    if (eventTypeField != null)
                    {
                        eventTypeField.SetValue(payloadWindow, eventData.eventType);
                    }

                    if (jsonPayloadField != null && !string.IsNullOrEmpty(eventData.jsonPayload))
                    {
                        jsonPayloadField.SetValue(payloadWindow, eventData.jsonPayload);
                    }

                    // Call the generate method
                    generateMethod.Invoke(payloadWindow, null);

                    // Group events by game AND event type for proper categorization
                    string gameName = ExtractGameNameFromEventName(eventData.eventName);
                    if (!string.IsNullOrEmpty(gameName))
                    {
                        if (!eventsByGame.ContainsKey(gameName))
                        {
                            eventsByGame[gameName] = new Dictionary<EventToPayloadClassGeneratorWindow.EventType, List<ParsedEventData>>();
                        }
                        
                        if (!eventsByGame[gameName].ContainsKey(eventData.eventType))
                        {
                            eventsByGame[gameName][eventData.eventType] = new List<ParsedEventData>();
                        }
                        
                        eventsByGame[gameName][eventData.eventType].Add(eventData);
                        Debug.Log($"Grouped event '{eventData.eventName}' (Type: {eventData.eventType}) under game '{gameName}'");
                    }

                    successCount++;
                    Debug.Log($"Successfully generated payload for event: {eventData.eventName} (Type: {eventData.eventType})");
                    
                }
                catch (System.Exception ex)
                {
                    errorCount++;
                    Debug.LogError($"Error generating payload for event '{eventData.eventName}': {ex.Message}");
                }
            }

            // Update events classes for each game with proper event type categorization
            foreach (var gameEvents in eventsByGame)
            {
                try
                {
                    string gameName = gameEvents.Key;
                    var eventsByType = gameEvents.Value;

                    // Create events class if it doesn't exist
                    EventsClassUpdater.CreateEventsClassIfNotExists(gameName);
                    
                    // Update the events class with events properly categorized by type
                    foreach (var eventTypeGroup in eventsByType)
                    {
                        var eventType = eventTypeGroup.Key;
                        var events = eventTypeGroup.Value;
                        
                        // Update events class for each event type separately
                        foreach (var eventData in events)
                        {
                            EventsClassUpdater.UpdateEventsClass(gameName, eventData.eventName, eventData.eventType);
                        }
                        
                        Debug.Log($"Updated {gameName}Events class with {events.Count} events of type {eventType}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error updating events class for {gameEvents.Key}: {ex.Message}");
                }
            }

            message = $"Generated payloads for {successCount} events successfully. {errorCount} errors occurred.";
            messageType = errorCount > 0 ? MessageType.Warning : MessageType.Info;
        }
        catch (System.Exception ex)
        {
            message = $"Error generating payloads for all events: {ex.Message}";
            messageType = MessageType.Error;
            Debug.LogError($"Error generating payloads for all events: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates payload class for a single event and updates the events class.
    /// </summary>
    /// <param name="eventData">The event data to generate payload for</param>
    private void GeneratePayloadForEvent(ParsedEventData eventData)
    {
        try
        {
            // Validate event data
            if (string.IsNullOrEmpty(eventData.eventName) && string.IsNullOrEmpty(eventData.jsonPayload))
            {
                message = "Event data is empty. Cannot generate payload.";
                messageType = MessageType.Warning;
                return;
            }

            // Find or create PayloadClassGeneratorWindow
            var payloadWindow = EditorWindow.GetWindow<EventToPayloadClassGeneratorWindow>("Payload Class Generator");
            
            // Use reflection to set private fields
            var eventNameField = typeof(EventToPayloadClassGeneratorWindow).GetField("eventName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var eventTypeField = typeof(EventToPayloadClassGeneratorWindow).GetField("selectedEventType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var jsonPayloadField = typeof(EventToPayloadClassGeneratorWindow).GetField("jsonPayload", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (eventNameField != null && !string.IsNullOrEmpty(eventData.eventName))
            {
                eventNameField.SetValue(payloadWindow, eventData.eventName);
            }

            if (eventTypeField != null)
            {
                eventTypeField.SetValue(payloadWindow, eventData.eventType);
            }

            if (jsonPayloadField != null && !string.IsNullOrEmpty(eventData.jsonPayload))
            {
                jsonPayloadField.SetValue(payloadWindow, eventData.jsonPayload);
            }

            // Use reflection to call the GenerateOrUpdatePayloadClass method
            var generateMethod = typeof(EventToPayloadClassGeneratorWindow).GetMethod("GenerateOrUpdatePayloadClass", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (generateMethod == null)
            {
                message = "Could not find GenerateOrUpdatePayloadClass method in PayloadClassGeneratorWindow.";
                messageType = MessageType.Error;
                return;
            }

            // Call the generate method
            generateMethod.Invoke(payloadWindow, null);

            // Update the events class for this event
            string gameName = ExtractGameNameFromEventName(eventData.eventName);
            if (!string.IsNullOrEmpty(gameName))
            {
                // Create events class if it doesn't exist
                EventsClassUpdater.CreateEventsClassIfNotExists(gameName);
                
                // Update the events class with this event
                EventsClassUpdater.UpdateEventsClass(gameName, eventData.eventName, eventData.eventType);
            }

            message = $"Successfully generated payload for event: {eventData.eventName}";
            messageType = MessageType.Info;
        }
        catch (System.Exception ex)
        {
            message = $"Error generating payload for event '{eventData.eventName}': {ex.Message}";
            messageType = MessageType.Error;
            Debug.LogError($"Error generating payload: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the game name from an event name by looking for common patterns.
    /// </summary>
    /// <param name="eventName">The event name to extract game name from</param>
    /// <returns>The extracted game name or null if not found</returns>
    private string ExtractGameNameFromEventName(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
            return null;

        // Look for game names in the scripts folder
        string scriptsPath = "Assets/Scripts";
        if (Directory.Exists(scriptsPath))
        {
            var gameFolders = Directory.GetDirectories(scriptsPath);
            foreach (var folder in gameFolders)
            {
                string gameName = Path.GetFileName(folder);
                
                // Check if the event name contains the game name (case insensitive)
                if (eventName.ToLower().Contains(gameName.ToLower()))
                {
                    return gameName;
                }
                
                // Check if there's an events class for this game
                string eventsClassPath = Path.Combine(folder, $"{gameName}Events.cs");
                if (File.Exists(eventsClassPath))
                {
                    return gameName;
                }
            }
        }

        // If no specific game found, try to extract from event name patterns
        // Common patterns: GAME_EVENT_NAME, GameEventName, etc.
        string[] parts = eventName.Split('_');
        if (parts.Length > 0)
        {
            // Try to find a game folder that matches the first part
            string firstPart = parts[0];
            string scriptsPath2 = "Assets/Scripts";
            if (Directory.Exists(scriptsPath2))
            {
                var gameFolders2 = Directory.GetDirectories(scriptsPath2);
                foreach (var folder in gameFolders2)
                {
                    string gameName = Path.GetFileName(folder);
                    if (gameName.Equals(firstPart, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return gameName;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a relative path to a full path.
    /// </summary>
    /// <param name="relativePath">Relative path (e.g., "Assets/file.txt")</param>
    /// <returns>Full path to the file</returns>
    private string GetFullPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }
        else
        {
            return Path.Combine(Application.dataPath, relativePath.Replace("Assets/", ""));
        }
    }

    /// <summary>
    /// Generates ACK events for Client and Server events that don't already have ACK versions.
    /// </summary>
    private void GenerateAckEvents()
    {
        var ackEvents = new List<ParsedEventData>();
        var existingEventNames = new HashSet<string>();
        
        // Get all existing event names
        foreach (var eventData in parsedEvents)
        {
            existingEventNames.Add(eventData.eventName);
        }
        
        // Generate ACK events only for Client events that have ackRequired: true
        foreach (var eventData in parsedEvents)
        {
            // Skip if it's already an ACK event
            if (eventData.eventType == EventToPayloadClassGeneratorWindow.EventType.ClientEventAck)
                continue;
                
            // Skip if it's a Server event (ACKs are typically for Client events)
            if (eventData.eventType == EventToPayloadClassGeneratorWindow.EventType.ServerEvent)
                continue;
            
            // Only generate ACK for Client events that have ackRequired: true
            if (eventData.eventType == EventToPayloadClassGeneratorWindow.EventType.ClientEvent)
            {
                // Check if the event has ackRequired: true
                if (HasAckRequired(eventData.jsonPayload))
                {
                    // Create ACK event name
                    string ackEventName = eventData.eventName + "_ACK";
                    
                    // Only create ACK if it doesn't already exist
                    if (!existingEventNames.Contains(ackEventName))
                    {
                        // Create ACK JSON payload
                        string ackJsonPayload = CreateAckJsonPayload(eventData.jsonPayload, eventData.eventName);
                        
                        // Create ACK event data
                        var ackEventData = new ParsedEventData(
                            ackEventName,
                            EventToPayloadClassGeneratorWindow.EventType.ClientEventAck,
                            ackJsonPayload,
                            eventData.lineNumber
                        );
                        
                        ackEvents.Add(ackEventData);
                        existingEventNames.Add(ackEventName);
                        
                        Debug.Log($"Generated ACK event: {ackEventName} (ackRequired: true)");
                    }
                }
                else
                {
                    Debug.Log($"Skipping ACK generation for {eventData.eventName} (ackRequired: false or not found)");
                }
            }
        }
        
        // Add ACK events to the main list
        parsedEvents.AddRange(ackEvents);
    }

    /// <summary>
    /// Checks if the JSON payload has ackRequired: true.
    /// </summary>
    /// <param name="jsonPayload">JSON payload to check</param>
    /// <returns>True if ackRequired is true, false otherwise</returns>
    private bool HasAckRequired(string jsonPayload)
    {
        try
        {
            var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonPayload);
            
            if (jsonObject != null && ((IDictionary<string, JToken>) jsonObject).ContainsKey("ackRequired"))
            {
                var ackRequired = jsonObject["ackRequired"];
                if (ackRequired.Type == Newtonsoft.Json.Linq.JTokenType.Boolean)
                {
                    return ackRequired.Value<bool>();
                }
                else if (ackRequired.Type == Newtonsoft.Json.Linq.JTokenType.String)
                {
                    return ackRequired.Value<string>().ToLower() == "true";
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to check ackRequired: {ex.Message}");
        }
        
        return false;
    }

    /// <summary>
    /// Creates ACK JSON payload based on the original event payload.
    /// </summary>
    /// <param name="originalJson">Original JSON payload</param>
    /// <param name="eventName">Original event name</param>
    /// <returns>ACK JSON payload</returns>
    private string CreateAckJsonPayload(string originalJson, string eventName)
    {
        try
        {
            // Parse the original JSON
            var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(originalJson);
            
            if (jsonObject != null)
            {
                // Create ACK JSON structure
                var ackJson = new Newtonsoft.Json.Linq.JObject();
                
                // Set the ACK event name
                ackJson["en"] = eventName + "_ACK";
                
                // Copy the data section if it exists
                if (((IDictionary<string, JToken>) jsonObject).ContainsKey("data"))
                {
                    ackJson["data"] = jsonObject["data"];
                }
                
                // Add ACK-specific fields
                ackJson["ackRequired"] = false;
                ackJson["status"] = "success";
                
                // Copy metrics if they exist
                if (((IDictionary<string, JToken>) jsonObject).ContainsKey("metrics"))
                {
                    ackJson["metrics"] = jsonObject["metrics"];
                }
                
                return Newtonsoft.Json.JsonConvert.SerializeObject(ackJson, Newtonsoft.Json.Formatting.Indented);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to create ACK JSON payload: {ex.Message}");
        }
        
        // Fallback: create simple ACK JSON
        return $"{{\n  \"en\": \"{eventName}_ACK\",\n  \"ackRequired\": false,\n  \"status\": \"success\"\n}}";
    }

    /// <summary>
    /// Clears all fields and parsed data.
    /// </summary>
    private void ClearFields()
    {
        textFilePath = "Assets/payload_content.txt";
        message = "";
        messageType = MessageType.None;
        isProcessing = false;
        parsedEvents.Clear();
        selectedEventIndex = -1;
    }
} 