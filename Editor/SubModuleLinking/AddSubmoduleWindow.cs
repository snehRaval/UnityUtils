using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

/// <summary>
/// Unity Editor window for managing Git submodules in the project.
/// Allows adding, updating, removing, and listing submodules, with SSH/HTTPS support and UI feedback.
/// </summary>
public class AddSubmoduleWindow : EditorWindow
{
    private string repoUrl = "";
    private string submodulePath = "Assets/Submodule/"; 
    private string message = "";
    private MessageType messageType = MessageType.None;
    private List<SubmoduleInfo> submodules = new List<SubmoduleInfo>();
    private Vector2 scrollPos;
    private string branchName = "main";
    private bool isLoading = false;
    private string loadingMessage = "";

    private class SubmoduleInfo
    {
        public string Name;
        public string Path;
        public string Url;
        public string Branch;
    }

    /// <summary>
    /// Shows the AddSubmoduleWindow editor window from the Unity menu.
    /// </summary>
    [MenuItem("Sneh/Add Submodules", false, 2)]
    public static void ShowWindow()
    {
        GetWindow<AddSubmoduleWindow>("Add Submodules");
    }

    /// <summary>
    /// Draws the main UI for submodule management, including add, update, remove, and list operations.
    /// </summary>
    private void OnGUI()
    {
        // Block UI with loader if loading
        if (isLoading)
        {
            GUI.enabled = false;
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Add Bitbucket Repo as Submodule", MessageType.None);
        EditorGUILayout.LabelField("Project Root Path:", Directory.GetCurrentDirectory(), EditorStyles.miniLabel);
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        // Repo URL input
        EditorGUILayout.BeginHorizontal();
        repoUrl = EditorGUILayout.TextField(new GUIContent("Repo URL", "Bitbucket repository URL to add as submodule."), repoUrl);

        // Helper: Convert HTTPS to SSH
        if (repoUrl.StartsWith("https://bitbucket.org/") && GUILayout.Button("To SSH", GUILayout.Width(60)))
        {
            // Convert to SSH format
            // Example: https://bitbucket.org/org/repo.git -> git@bitbucket.org:org/repo.git
            var match = System.Text.RegularExpressions.Regex.Match(repoUrl, @"https://bitbucket.org/([^/]+)/([^/]+)(.git)?");
            if (match.Success)
            {
                string org = match.Groups[1].Value;
                string repo = match.Groups[2].Value.Replace(".git", "");
                repoUrl = $"git@bitbucket.org:{org}/{repo}.git";
            }
        }
        EditorGUILayout.EndHorizontal();

        // Show info if using HTTPS
        if (repoUrl.StartsWith("https://"))
        {
            EditorGUILayout.HelpBox("For private repositories, SSH is recommended to avoid authentication issues. Click 'To SSH' to convert.", MessageType.Info);
        }
        submodulePath = EditorGUILayout.TextField(new GUIContent("Submodule Path", "Relative path in your project (e.g. Assets/External/YourLib)"), submodulePath);
        branchName = EditorGUILayout.TextField(new GUIContent("Branch Name", "Branch to track as submodule (e.g. main, develop)"), branchName);
        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(isLoading || string.IsNullOrEmpty(repoUrl) || string.IsNullOrEmpty(submodulePath) || string.IsNullOrEmpty(branchName));
        if (GUILayout.Button("Add Submodule", GUILayout.Height(30)))
        {
            isLoading = true;
            loadingMessage = "Adding submodule...";
            EditorApplication.delayCall += () => {
                AddSubmodule();
                RefreshSubmodules();
                // Reset add UI fields
                // repoUrl = "";
                // submodulePath = "Assets/Submodule/";
                // branchName = "main";
                isLoading = false;
                loadingMessage = "";
                Repaint();
            };
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();

        if (!string.IsNullOrEmpty(message))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(message, messageType);
            if (messageType == MessageType.Error || messageType == MessageType.Warning)
            {
                if (GUILayout.Button("Copy Error", GUILayout.Width(120)))
                {
                    EditorGUIUtility.systemCopyBuffer = message;
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Current Submodules", MessageType.None);
        EditorGUILayout.Space();
        // Remove All Submodules button
        if (submodules.Count > 0)
        {
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Remove All Submodules (Force)", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Remove All Submodules", "Are you sure you want to remove ALL submodules and clean up all tracking? This cannot be undone.", "Yes, Remove All", "Cancel"))
                {
                    isLoading = true;
                    loadingMessage = "Removing all submodules...";
                    EditorApplication.delayCall += () => {
                        RemoveAllSubmodules();
                        RefreshSubmodules();
                        isLoading = false;
                        loadingMessage = "";
                        Repaint();
                    };
                }
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.BeginVertical("box");
        if (submodules.Count == 0)
        {
            GUILayout.Label("No submodules found.", EditorStyles.miniLabel);
        }
        else
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            // Calculate dynamic widths
            float totalWidth = EditorGUIUtility.currentViewWidth - 60; // padding
            float pathWidth = totalWidth * 0.28f;
            float urlWidth = totalWidth * 0.38f;
            float branchWidth = totalWidth * 0.16f;
            float buttonWidth = 70f;
            // Header row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Path", EditorStyles.boldLabel, GUILayout.Width(pathWidth));
            GUILayout.Label("URL", EditorStyles.boldLabel, GUILayout.Width(urlWidth));
            GUILayout.Label("Branch", EditorStyles.boldLabel, GUILayout.Width(branchWidth));
            GUILayout.Label("", GUILayout.Width(buttonWidth)); // Update
            GUILayout.Label("", GUILayout.Width(buttonWidth)); // Remove
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            int rowIdx = 0;
            foreach (var sub in submodules)
            {
                // If branch is (not set), try to get current checked out branch
                string branchToShow = sub.Branch;
                if (branchToShow == "(not set)")
                {
                    branchToShow = GetCurrentSubmoduleBranch(sub.Path);
                }
                // Alternate row background
                Color origColor = GUI.backgroundColor;
                if (rowIdx % 2 == 0)
                    GUI.backgroundColor = new Color(0.96f, 0.98f, 1f);
                else
                    GUI.backgroundColor = Color.white;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                // Path as clickable button
                GUIStyle pathStyle = new GUIStyle(EditorStyles.label);
                pathStyle.wordWrap = true;
                pathStyle.normal.textColor = new Color(0.2f, 0.4f, 0.8f);
                pathStyle.hover.textColor = new Color(0.1f, 0.2f, 0.5f);
                pathStyle.stretchWidth = true;
                if (GUILayout.Button(new GUIContent(sub.Path, sub.Path), pathStyle, GUILayout.Width(pathWidth)))
                {
                    string fullPath = Path.Combine(Directory.GetCurrentDirectory(), sub.Path);
                    EditorUtility.RevealInFinder(fullPath);
                }
                // URL as clickable button
                GUIStyle urlStyle = new GUIStyle(EditorStyles.label);
                urlStyle.wordWrap = true;
                urlStyle.normal.textColor = new Color(0.1f, 0.5f, 0.1f);
                urlStyle.hover.textColor = new Color(0.1f, 0.7f, 0.1f);
                urlStyle.stretchWidth = true;
                if (GUILayout.Button(new GUIContent(sub.Url, sub.Url), urlStyle, GUILayout.Width(urlWidth)))
                {
                    Application.OpenURL(sub.Url);
                }
                // Branch
                GUIStyle branchStyle = new GUIStyle(EditorStyles.label);
                branchStyle.wordWrap = true;
                branchStyle.stretchWidth = true;
                GUILayout.Label(branchToShow, branchStyle, GUILayout.Width(branchWidth));
                // Update button
                GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
                EditorGUI.BeginDisabledGroup(isLoading);
                if (GUILayout.Button("Update", GUILayout.Width(buttonWidth), GUILayout.Height(22)))
                {
                    isLoading = true;
                    loadingMessage = $"Updating submodule '{sub.Path}'...";
                    string path = sub.Path;
                    EditorApplication.delayCall += () => {
                        UpdateSubmodule(path);
                        isLoading = false;
                        loadingMessage = "";
                        Repaint();
                    };
                }
                EditorGUI.EndDisabledGroup();
                // Remove button
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                EditorGUI.BeginDisabledGroup(isLoading);
                if (GUILayout.Button("Remove", GUILayout.Width(buttonWidth), GUILayout.Height(22)))
                {
                    GUI.backgroundColor = Color.white;
                    if (EditorUtility.DisplayDialog("Remove Submodule", $"Are you sure you want to remove submodule at {sub.Path}?", "Yes", "No"))
                    {
                        isLoading = true;
                        loadingMessage = $"Removing submodule '{sub.Path}'...";
                        string path = sub.Path;
                        EditorApplication.delayCall += () => {
                            RemoveSubmodule(path);
                            RefreshSubmodules();
                            isLoading = false;
                            loadingMessage = "";
                            Repaint();
                        };
                        break;
                    }
                }
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = origColor;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                rowIdx++;
            }
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
        // Loader overlay (draw last, on top)
        if (isLoading)
        {
            GUI.enabled = true;
            var rect = new Rect(0, 0, position.width, position.height);
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.35f));
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 18;
            string msg = string.IsNullOrEmpty(loadingMessage) ? "Processing..." : loadingMessage;
            GUI.Label(new Rect(0, position.height / 2 - 20, position.width, 40), msg, style);
        }
    }

    /// <summary>
    /// Called when the window is enabled or recompiled. Refreshes the submodule list.
    /// </summary>
    private void OnEnable()
    {
        RefreshSubmodules();
    }

    /// <summary>
    /// Reads .gitmodules and .git/config to populate the list of current submodules.
    /// </summary>
    private void RefreshSubmodules()
    {
        submodules.Clear();
        string gitmodulesPath = Path.Combine(Directory.GetCurrentDirectory(), ".gitmodules");
        if (!File.Exists(gitmodulesPath)) return;
        var lines = File.ReadAllLines(gitmodulesPath);
        SubmoduleInfo current = null;
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("[submodule"))
            {
                if (current != null) submodules.Add(current);
                current = new SubmoduleInfo();
                int nameStart = line.IndexOf('"') + 1;
                int nameEnd = line.LastIndexOf('"');
                if (nameStart > 0 && nameEnd > nameStart)
                    current.Name = line.Substring(nameStart, nameEnd - nameStart);
            }
            else if (current != null)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("path = "))
                    current.Path = trimmed.Substring("path = ".Length);
                else if (trimmed.StartsWith("url = "))
                    current.Url = trimmed.Substring("url = ".Length);
            }
        }
        if (current != null) submodules.Add(current);

        // Now, for each submodule, try to get its branch from .git/config
        string gitConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ".git", "config");
        if (File.Exists(gitConfigPath))
        {
            var configLines = File.ReadAllLines(gitConfigPath);
            foreach (var sub in submodules)
            {
                string section = $"[submodule \"{sub.Name}\"]";
                bool inSection = false;
                foreach (var cl in configLines)
                {
                    if (cl.Trim() == section)
                    {
                        inSection = true;
                    }
                    else if (inSection && cl.Trim().StartsWith("branch = "))
                    {
                        sub.Branch = cl.Trim().Substring("branch = ".Length);
                        break;
                    }
                    else if (inSection && cl.Trim().StartsWith("["))
                    {
                        // End of section
                        break;
                    }
                }
                if (string.IsNullOrEmpty(sub.Branch))
                {
                    sub.Branch = "(not set)";
                }
            }
        }
    }

    /// <summary>
    /// Removes a single submodule by path, cleaning up .gitmodules, .git/config, .git/modules, and the folder.
    /// Also cleans up Git index to prevent future conflicts.
    /// </summary>
    /// <param name="path">The submodule path to remove.</param>
    private void RemoveSubmodule(string path)
    {
        try
        {
            // 1. Deinit the submodule
            var deinit = RunGitCommand($"submodule deinit -f {path}");
            
            // 2. Remove from Git tracking
            var rm = RunGitCommand($"rm -f {path}");
            
            // 3. Reset any staged changes to clean up the index
            var reset = RunGitCommand("reset HEAD");
            
            // 4. Remove .git/modules entry (optional, for cleanliness)
            var modulesPath = Path.Combine(Directory.GetCurrentDirectory(), ".git", "modules", path);
            if (Directory.Exists(modulesPath))
                Directory.Delete(modulesPath, true);
            
            // 5. Recursively remove empty parent directories up to .git/modules
            var modulesRoot = Path.Combine(Directory.GetCurrentDirectory(), ".git", "modules");
            var parent = Directory.GetParent(modulesPath);
            while (parent != null && parent.FullName.Replace("\\", "/").StartsWith(modulesRoot.Replace("\\", "/")) && parent.FullName != modulesRoot)
            {
                if (Directory.Exists(parent.FullName) && Directory.GetFileSystemEntries(parent.FullName).Length == 0)
                {
                    Directory.Delete(parent.FullName);
                    parent = parent.Parent;
                }
                else
                {
                    break;
                }
            }
            
            // 6. Delete directory if still exists
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
            
            // 7. Delete .meta file if exists
            var metaPath = fullPath + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);
            
            // 8. Clean up .gitmodules if it's empty or only contains whitespace
            string gitmodulesPath = Path.Combine(Directory.GetCurrentDirectory(), ".gitmodules");
            if (File.Exists(gitmodulesPath))
            {
                var content = File.ReadAllText(gitmodulesPath).Trim();
                if (string.IsNullOrEmpty(content))
                {
                    File.Delete(gitmodulesPath);
                    // Also remove it from Git tracking
                    RunGitCommand("rm --cached .gitmodules");
                }
            }
            
            // 9. Clean up .git/config submodule sections for this specific submodule
            CleanupGitConfigSubmodule(path);
            
            // 10. Stage all changes to clean up the working directory
            var add = RunGitCommand("add -A");
            
            message = $"Removed submodule: {path}";
            messageType = MessageType.Info;
            AssetDatabase.Refresh();
        }
        catch (System.Exception ex)
        {
            message = $"Failed to remove submodule: {ex.Message}";
            messageType = MessageType.Error;
        }
    }

    /// <summary>
    /// Cleans up .git/config by removing submodule sections for the specified path.
    /// </summary>
    /// <param name="submodulePath">The submodule path to clean up.</param>
    private void CleanupGitConfigSubmodule(string submodulePath)
    {
        try
        {
            string gitConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ".git", "config");
            if (!File.Exists(gitConfigPath)) return;
            
            var configLines = new List<string>(File.ReadAllLines(gitConfigPath));
            var cleanedLines = new List<string>();
            bool skipSection = false;
            
            for (int i = 0; i < configLines.Count; i++)
            {
                string line = configLines[i];
                
                // Check if this is a submodule section for our path
                if (line.Trim().StartsWith("[submodule "))
                {
                    // Look ahead to see if this section contains our path
                    bool isOurSubmodule = false;
                    for (int j = i + 1; j < configLines.Count; j++)
                    {
                        string nextLine = configLines[j];
                        if (nextLine.Trim().StartsWith("["))
                            break; // Next section
                        if (nextLine.Trim().StartsWith("path = ") && nextLine.Contains(submodulePath))
                        {
                            isOurSubmodule = true;
                            break;
                        }
                    }
                    
                    if (isOurSubmodule)
                    {
                        skipSection = true;
                        continue; // Skip this line and the entire section
                    }
                }
                
                // If we're in a section we want to skip, continue until next section
                if (skipSection)
                {
                    if (line.Trim().StartsWith("["))
                    {
                        skipSection = false; // End of section
                        cleanedLines.Add(line); // Add the next section header
                    }
                    continue;
                }
                
                cleanedLines.Add(line);
            }
            
            // Write back the cleaned config
            File.WriteAllLines(gitConfigPath, cleanedLines);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to clean up .git/config for submodule {submodulePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs a git command and returns its exit code, output, and error.
    /// </summary>
    /// <param name="arguments">Arguments to pass to git.</param>
    private (int exitCode, string output, string error) RunGitCommand(string arguments)
    {
        var process = new Process();
        process.StartInfo.FileName = "git";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    /// <summary>
    /// Adds a new submodule using the provided repo URL, path, and branch.
    /// Shows UI feedback and refreshes the submodule list.
    /// </summary>
    private void AddSubmodule()
    {
        if (string.IsNullOrEmpty(repoUrl) || string.IsNullOrEmpty(submodulePath) || string.IsNullOrEmpty(branchName))
        {
            message = "Please enter the repo URL, submodule path, and branch name.";
            messageType = MessageType.Warning;
            return;
        }

        try
        {
            var process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = $"submodule add -b {branchName} {repoUrl} {submodulePath}";
            process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                message = $"Submodule added successfully on branch '{branchName}'!";
                messageType = MessageType.Info;
                AssetDatabase.Refresh();
                // Reset add UI fields only on success
                repoUrl = "";
                submodulePath = "Assets/Submodule/";
                branchName = "main";
            }
            else
            {
                message = $"Failed to add submodule.\n{error}";
                messageType = MessageType.Error;
            }
        }
        catch (System.Exception ex)
        {
            message = $"Exception: {ex.Message}";
            messageType = MessageType.Error;
        }
    }

    /// <summary>
    /// Updates a submodule to the latest commit on its tracked branch.
    /// </summary>
    /// <param name="path">The submodule path to update.</param>
    private void UpdateSubmodule(string path)
    {
        try
        {
            var update = RunGitCommand($"submodule update --remote {path}");
            if (update.exitCode == 0)
            {
                message = $"Submodule at '{path}' updated to latest on tracked branch.";
                messageType = MessageType.Info;
                AssetDatabase.Refresh();
            }
            else
            {
                message = $"Failed to update submodule at '{path}'.\n{update.error}";
                messageType = MessageType.Error;
            }
        }
        catch (System.Exception ex)
        {
            message = $"Exception while updating submodule: {ex.Message}";
            messageType = MessageType.Error;
        }
    }

    /// <summary>
    /// Gets the currently checked out branch for a submodule.
    /// </summary>
    /// <param name="submodulePath">The submodule path.</param>
    /// <returns>The branch name or "(detached)" if not on a branch.</returns>
    private string GetCurrentSubmoduleBranch(string submodulePath)
    {
        try
        {
            var result = RunGitCommand($"-C {submodulePath} rev-parse --abbrev-ref HEAD");
            if (result.exitCode == 0)
            {
                string branch = result.output.Trim();
                if (!string.IsNullOrEmpty(branch) && branch != "HEAD")
                    return branch;
            }
        }
        catch { }
        return "(detached)";
    }

    /// <summary>
    /// Removes all submodules, cleaning up all tracking and folders. Use with caution.
    /// Also performs comprehensive Git index cleanup to prevent future conflicts.
    /// </summary>
    private void RemoveAllSubmodules()
    {
        try
        {
            // 1. Remove each submodule individually
            foreach (var sub in new List<SubmoduleInfo>(submodules))
            {
                try
                {
                    RemoveSubmodule(sub.Path);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to remove submodule {sub.Path}: {ex.Message}");
                }
            }

            // 2. Reset any remaining staged changes
            var reset = RunGitCommand("reset HEAD");
            
            // 3. Clean up .gitmodules file completely
            string gitmodulesPath = Path.Combine(Directory.GetCurrentDirectory(), ".gitmodules");
            if (File.Exists(gitmodulesPath))
            {
                File.Delete(gitmodulesPath);
                // Remove from Git tracking
                RunGitCommand("rm --cached .gitmodules");
            }

            // 4. Clean up .git/config - remove ALL submodule sections
            string gitConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ".git", "config");
            if (File.Exists(gitConfigPath))
            {
                var configLines = new List<string>(File.ReadAllLines(gitConfigPath));
                var cleanedLines = new List<string>();
                bool skipSection = false;
                
                foreach (string line in configLines)
                {
                    if (line.Trim().StartsWith("[submodule "))
                    {
                        skipSection = true;
                        continue; // Skip this line and the entire section
                    }
                    
                    // If we're in a section we want to skip, continue until next section
                    if (skipSection)
                    {
                        if (line.Trim().StartsWith("["))
                        {
                            skipSection = false; // End of section
                            cleanedLines.Add(line); // Add the next section header
                        }
                        continue;
                    }
                    
                    cleanedLines.Add(line);
                }
                
                File.WriteAllLines(gitConfigPath, cleanedLines);
            }

            // 5. Clean up .git/modules directory completely
            string modulesRoot = Path.Combine(Directory.GetCurrentDirectory(), ".git", "modules");
            if (Directory.Exists(modulesRoot))
            {
                Directory.Delete(modulesRoot, true);
            }

            // 6. Stage all changes to clean up the working directory
            var add = RunGitCommand("add -A");
            
            message = "All submodules removed and Git index cleaned.";
            messageType = MessageType.Info;
            AssetDatabase.Refresh();
        }
        catch (System.Exception ex)
        {
            message = $"Failed to remove all submodules: {ex.Message}";
            messageType = MessageType.Error;
        }
    }
} 