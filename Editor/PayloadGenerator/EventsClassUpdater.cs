using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Automatically updates the generated events class when payload classes are generated.
/// Adds client events to ClientEvents enum and server events to ServerEvents enum.
/// </summary>
public class EventsClassUpdater
{
    /// <summary>
    /// Updates the events class for a specific game when payload classes are generated.
    /// </summary>
    /// <param name="gameName">The name of the game</param>
    /// <param name="eventName">The name of the event</param>
    /// <param name="eventType">The type of event (Client or Server)</param>
    public static void UpdateEventsClass(string gameName, string eventName, EventToPayloadClassGeneratorWindow.EventType eventType)
    {
        try
        {
            Debug.Log($"RS: UpdateEventsClass {eventName} : {eventType} ");
            string eventsClassPath = Path.Combine("Assets/Scripts", gameName, $"{gameName}Events.cs");
            
            if (!File.Exists(eventsClassPath))
            {
                Debug.LogWarning($"Events class not found: {eventsClassPath}");
                return;
            }

            string content = File.ReadAllText(eventsClassPath);
            string updatedContent = UpdateEventsClassContent(content, eventName, eventType);
            
            if (content != updatedContent)
            {
                File.WriteAllText(eventsClassPath, updatedContent);
                AssetDatabase.Refresh();
                Debug.Log($"Updated events class for {gameName}: Added {eventName} to {(eventType == EventToPayloadClassGeneratorWindow.EventType.ClientEvent ? "ClientEvents" : "ServerEvents")}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to update events class for {gameName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the events class content by adding the new event to the appropriate enum.
    /// </summary>
    /// <param name="content">The current content of the events class</param>
    /// <param name="eventName">The name of the event to add</param>
    /// <param name="eventType">The type of event (Client, ClientAck, or Server)</param>
    /// <returns>The updated content</returns>
    private static string UpdateEventsClassContent(string content, string eventName, EventToPayloadClassGeneratorWindow.EventType eventType)
    {
        // Determine which enum to update based on event type
        string targetEnum;
        switch (eventType)
        {
            case EventToPayloadClassGeneratorWindow.EventType.ClientEvent:
            case EventToPayloadClassGeneratorWindow.EventType.ClientEventAck:
                targetEnum = "ClientEvents";
                break;
            case EventToPayloadClassGeneratorWindow.EventType.ServerEvent:
                targetEnum = "ServerEvents";
                break;
            default:
                Debug.LogWarning($"Unknown event type: {eventType}, defaulting to ClientEvents");
                targetEnum = "ClientEvents";
                break;
        }
        
        // Find the target enum
        string enumPattern = $@"public enum {targetEnum}\s*\{{([^}}]*)\}}";
        Match match = Regex.Match(content, enumPattern, RegexOptions.Singleline);
        
        if (!match.Success)
        {
            Debug.LogWarning($"Could not find {targetEnum} enum in events class");
            return content;
        }

        string enumContent = match.Groups[1].Value.Trim();
        
        // Check if event already exists
        if (enumContent.Contains(eventName))
        {
            Debug.Log($"Event {eventName} already exists in {targetEnum} enum");
            return content;
        }

        // Add the new event to the enum
        string newEnumContent = enumContent;
        if (!string.IsNullOrEmpty(enumContent))
        {
            newEnumContent += ",\n        ";
        }
        newEnumContent += eventName;

        // Replace the enum content
        string newContent = content.Replace(match.Groups[0].Value, 
            $"public enum {targetEnum}\n    {{\n        {newEnumContent}\n    }}");

        return newContent;
    }

    /// <summary>
    /// Updates events class for all events in a batch operation.
    /// </summary>
    /// <param name="gameName">The name of the game</param>
    /// <param name="events">List of events to add</param>
    public static void UpdateEventsClassBatch(string gameName, List<ParsedEventData> events)
    {
        try
        {
            string eventsClassPath = Path.Combine("Assets/Scripts", gameName, $"{gameName}Events.cs");
            
            if (!File.Exists(eventsClassPath))
            {
                Debug.LogWarning($"Events class not found: {eventsClassPath}");
                return;
            }

            string content = File.ReadAllText(eventsClassPath);
            string updatedContent = content;
            int addedCount = 0;

            foreach (var eventData in events)
            {
                updatedContent = UpdateEventsClassContent(updatedContent, eventData.eventName, eventData.eventType);
                addedCount++;
            }

            if (content != updatedContent)
            {
                File.WriteAllText(eventsClassPath, updatedContent);
                AssetDatabase.Refresh();
                Debug.Log($"Updated events class for {gameName}: Added {addedCount} events");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to update events class for {gameName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new events class if it doesn't exist.
    /// </summary>
    /// <param name="gameName">The name of the game</param>
    public static void CreateEventsClassIfNotExists(string gameName)
    {
        try
        {
            string scriptsFolder = Path.Combine("Assets/Scripts", gameName);
            string eventsClassPath = Path.Combine(scriptsFolder, $"{gameName}Events.cs");
            
            if (File.Exists(eventsClassPath))
            {
                return; // Already exists
            }

            // Create scripts folder if it doesn't exist
            if (!Directory.Exists(scriptsFolder))
            {
                Directory.CreateDirectory(scriptsFolder);
            }

            // Create the events class template
            string template = $@"using System;

namespace {gameName}.NCL.AutoGenerated
{{
    /// <summary>
    /// Contains event enums for room and client events used in socket communication.
    /// </summary>
    public static class {gameName}Events
    {{
        /// <summary>
        /// Client events that can be sent to the server (including ACK events).
        /// </summary>
        public enum ClientEvents
        {{
            // Client events will be added here automatically
        }}

        /// <summary>
        /// Room events that can be received from the server.
        /// </summary>
        public enum ServerEvents
        {{
            // Server events will be added here automatically
        }}
    }}
}}";

            File.WriteAllText(eventsClassPath, template);
            AssetDatabase.Refresh();
            Debug.Log($"Created events class: {eventsClassPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to create events class for {gameName}: {ex.Message}");
        }
    }
} 