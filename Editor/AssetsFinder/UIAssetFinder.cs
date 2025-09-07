using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIAssetFinder : EditorWindow
{
    private Font uiFont;
    private TMP_FontAsset tmpFont;
    private Sprite targetSprite;
    private Object referenceTarget;
    private MonoScript componentScript;

    private string prefabSearchFolder = "Assets/";

    private enum TabType { UI_Text, TMP_Text, Sprite, ObjectRef, ComponentFinder }
    private TabType currentTab = TabType.UI_Text;

    [MenuItem("Sneh/Assets Finder/Find UI Assets (Text, TMP, Image)")]
    public static void ShowWindow()
    {
        GetWindow<UIAssetFinder>("UI Asset Finder");
    }

    private void OnGUI()
    {
        DrawTabs();
        EditorGUILayout.Space();

        switch (currentTab)
        {
            case TabType.UI_Text:
                DrawUIFontSearchTab();
                break;
            case TabType.TMP_Text:
                DrawTMPFontSearchTab();
                break;
            case TabType.Sprite:
                DrawSpriteSearchTab();
                break;
            case TabType.ObjectRef:
                DrawObjectRefSearchTab();
                break;
            case TabType.ComponentFinder:
                DrawComponentFinderTab();
                break;
        }
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(currentTab == TabType.UI_Text, "UI Text", EditorStyles.toolbarButton)) currentTab = TabType.UI_Text;
        if (GUILayout.Toggle(currentTab == TabType.TMP_Text, "TextMeshPro", EditorStyles.toolbarButton)) currentTab = TabType.TMP_Text;
        if (GUILayout.Toggle(currentTab == TabType.Sprite, "UI Sprite", EditorStyles.toolbarButton)) currentTab = TabType.Sprite;
        if (GUILayout.Toggle(currentTab == TabType.ObjectRef, "Object Reference", EditorStyles.toolbarButton)) currentTab = TabType.ObjectRef;
        if (GUILayout.Toggle(currentTab == TabType.ComponentFinder, "Component Finder", EditorStyles.toolbarButton)) currentTab = TabType.ComponentFinder;
        GUILayout.EndHorizontal();
    }

    // ----------------- UI Text Tab -----------------
    private void DrawUIFontSearchTab()
    {
        BeginColoredSection(new Color(0.8f, 0.9f, 1f));
        GUILayout.Label("🔎 Unity UI Text", EditorStyles.boldLabel);
        uiFont = (Font)EditorGUILayout.ObjectField("UI Font", uiFont, typeof(Font), false);

        if (GUILayout.Button("Find UI Text in Scene"))
            FindUnityUITextObjectsInScene(uiFont);

        prefabSearchFolder = EditorGUILayout.TextField("Prefab Folder", prefabSearchFolder);

        if (GUILayout.Button("Find UI Text in Prefabs"))
            FindUnityUITextObjectsInPrefabs(uiFont, prefabSearchFolder);

        EndColoredSection();
    }

    // ----------------- TMP Text Tab -----------------
    private void DrawTMPFontSearchTab()
    {
        BeginColoredSection(new Color(0.85f, 1f, 0.85f));
        GUILayout.Label("🔎 TextMeshPro Text", EditorStyles.boldLabel);
        tmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField("TMP Font", tmpFont, typeof(TMP_FontAsset), false);

        if (GUILayout.Button("Find TMP in Scene"))
            FindTMPTextObjectsInScene(tmpFont);

        prefabSearchFolder = EditorGUILayout.TextField("Prefab Folder", prefabSearchFolder);

        if (GUILayout.Button("Find TMP in Prefabs"))
            FindTMPTextObjectsInPrefabs(tmpFont, prefabSearchFolder);

        EndColoredSection();
    }

    // ----------------- Sprite Tab -----------------
    private void DrawSpriteSearchTab()
    {
        BeginColoredSection(new Color(1f, 0.95f, 0.85f));
        GUILayout.Label("🖼️ UI Image Sprite", EditorStyles.boldLabel);
        targetSprite = (Sprite)EditorGUILayout.ObjectField("Target Sprite", targetSprite, typeof(Sprite), false);

        if (GUILayout.Button("Find Sprite in Scene"))
            FindUIImageObjectsInScene(targetSprite);

        prefabSearchFolder = EditorGUILayout.TextField("Prefab Folder", prefabSearchFolder);

        if (GUILayout.Button("Find Sprite in Prefabs"))
            FindUIImageObjectsInPrefabs(targetSprite, prefabSearchFolder);

        EndColoredSection();
    }

    // ----------------- Object Reference Tab -----------------
    private void DrawObjectRefSearchTab()
    {
        BeginColoredSection(new Color(1f, 1f, 0.8f));
        GUILayout.Label("🔗 Object Reference Finder", EditorStyles.boldLabel);

        referenceTarget = EditorGUILayout.ObjectField("Target Object", referenceTarget, typeof(Object), true);

        if (GUILayout.Button("Find References in Scene"))
            FindReferencesInScene(referenceTarget);

        prefabSearchFolder = EditorGUILayout.TextField("Reference Prefab Folder", prefabSearchFolder);

        if (GUILayout.Button("Find References in Prefabs"))
            FindReferencesInPrefabs(referenceTarget, prefabSearchFolder);

        EndColoredSection();
    }
    
    // ----------------- Component Finder Tab -----------------
    private void DrawComponentFinderTab()
    {
        BeginColoredSection(new Color(0.9f, 0.85f, 1f));
        GUILayout.Label("🔍 Find GameObjects by Component", EditorStyles.boldLabel);

        componentScript = (MonoScript)EditorGUILayout.ObjectField("Component (Script)", componentScript, typeof(MonoScript), false);

        if (GUILayout.Button("Find in Scene"))
        {
            if (componentScript == null)
                Debug.LogWarning("Assign a component script.");
            else
                FindGameObjectsWithComponentInScene(componentScript);
        }

        prefabSearchFolder = EditorGUILayout.TextField("Prefab Folder", prefabSearchFolder);

        if (GUILayout.Button("Find in Prefabs"))
        {
            if (componentScript == null)
                Debug.LogWarning("Assign a component script.");
            else
                FindGameObjectsWithComponentInPrefabs(componentScript, prefabSearchFolder);
        }
        EndColoredSection();
    }
    // ----------------- Utility: UI Box Styling -----------------
    private void BeginColoredSection(Color color)
    {
        GUI.backgroundColor = color;
        GUILayout.BeginVertical("box");
        GUI.backgroundColor = Color.white;
    }

    private void EndColoredSection()
    {
        GUILayout.EndVertical();
    }
    
     // ===== Path Helper =====
    private string GetGameObjectPath(GameObject go)
    {
        string path = go.name;
        Transform current = go.transform;

        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }

    // ===== UI Text =====
    private void FindUnityUITextObjectsInScene(Font font)
    {
        int count = 0;
        Text[] allTexts = GameObject.FindObjectsOfType<Text>(true);

        foreach (Text txt in allTexts)
        {
            if (txt.font == font)
            {
                Debug.Log($"[UI Text] Found on: {GetGameObjectPath(txt.gameObject)}", txt.gameObject);
                count++;
            }
        }

        Debug.Log($"✅ Found {count} GameObject(s) using UI font: {font.name}");
    }

    private void FindUnityUITextObjectsInPrefabs(Font font, string folder)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        int count = 0;

        foreach (var guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) continue;

            Text[] texts = prefab.GetComponentsInChildren<Text>(true);
            foreach (var txt in texts)
            {
                if (txt.font == font)
                {
                    Debug.Log($"[UI Text] Found in prefab '{path}' GameObject: {GetGameObjectPath(txt.gameObject)}");
                    count++;
                }
            }
        }

        Debug.Log($"✅ Found {count} prefab GameObject(s) using UI font: {font.name}");
    }

    // ===== TextMeshPro =====
    private void FindTMPTextObjectsInScene(TMP_FontAsset font)
    {
        int count = 0;
        var tmpUGUI = GameObject.FindObjectsOfType<TextMeshProUGUI>(true);
        var tmpWorld = GameObject.FindObjectsOfType<TextMeshPro>(true);

        foreach (var tmp in tmpUGUI)
        {
            if (tmp.font == font)
            {
                Debug.Log($"[TMP UGUI] Found on: {GetGameObjectPath(tmp.gameObject)}", tmp.gameObject);
                count++;
            }
        }

        foreach (var tmp in tmpWorld)
        {
            if (tmp.font == font)
            {
                Debug.Log($"[TMP World] Found on: {GetGameObjectPath(tmp.gameObject)}", tmp.gameObject);
                count++;
            }
        }

        Debug.Log($"✅ Found {count} GameObject(s) using TMP font: {font.name}");
    }

    private void FindTMPTextObjectsInPrefabs(TMP_FontAsset font, string folder)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        int count = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) continue;

            var tmpUGUI = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
            var tmpWorld = prefab.GetComponentsInChildren<TextMeshPro>(true);

            foreach (var tmp in tmpUGUI)
            {
                if (tmp.font == font)
                {
                    Debug.Log($"[TMP UGUI] Found in prefab '{path}' GameObject: {GetGameObjectPath(tmp.gameObject)}");
                    count++;
                }
            }

            foreach (var tmp in tmpWorld)
            {
                if (tmp.font == font)
                {
                    Debug.Log($"[TMP World] Found in prefab '{path}' GameObject: {GetGameObjectPath(tmp.gameObject)}");
                    count++;
                }
            }
        }

        Debug.Log($"✅ Found {count} prefab GameObject(s) using TMP font: {font.name}");
    }

    // ===== UI Image =====
    private void FindUIImageObjectsInScene(Sprite sprite)
    {
        int count = 0;
        Image[] allImages = GameObject.FindObjectsOfType<Image>(true);

        foreach (Image img in allImages)
        {
            if (img.sprite == sprite)
            {
                Debug.Log($"[Image] Found on: {GetGameObjectPath(img.gameObject)}", img.gameObject);
                count++;
            }
        }

        Debug.Log($"✅ Found {count} GameObject(s) using sprite: {sprite.name}");
    }

    private void FindUIImageObjectsInPrefabs(Sprite sprite, string folder)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        int count = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) continue;

            Image[] images = prefab.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.sprite == sprite)
                {
                    Debug.Log($"[Image] Found in prefab '{path}' GameObject: {GetGameObjectPath(img.gameObject)}");
                    count++;
                }
            }
        }

        Debug.Log($"✅ Found {count} prefab GameObject(s) using sprite: {sprite.name}");
    }

    // ===== Object Reference Finder =====
    private void FindReferencesInScene(Object target)
    {
        int count = 0;
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);

        foreach (var go in allObjects)
        {
            Component[] components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (ObjectReferencesTarget(comp, target))
                {
                    Debug.Log($"[Scene Ref] Found on: {GetGameObjectPath(go)}", go);
                    count++;
                    break;
                }
            }
        }

        Debug.Log($"✅ Found {count} GameObject(s) referencing: {target.name} in scene.");
    }

    private void FindReferencesInPrefabs(Object target, string folder)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        int count = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) continue;

            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            foreach (Component comp in components)
            {
                if (ObjectReferencesTarget(comp, target))
                {
                    Debug.Log($"[Prefab Ref] Found in: {path} → {GetGameObjectPath(comp.gameObject)}", prefab);
                    count++;
                    break;
                }
            }
        }

        Debug.Log($"✅ Found {count} prefab GameObject(s) referencing: {target.name}");
    }

    private bool ObjectReferencesTarget(Component component, Object target)
    {
        if (component == null || target == null) return false;

        SerializedObject so = new SerializedObject(component);
        SerializedProperty sp = so.GetIterator();

        while (sp.NextVisible(true))
        {
            if (sp.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (sp.objectReferenceValue == target)
                {
                    return true;
                }
            }
        }

        return false;
    }
    
    private void FindGameObjectsWithComponentInScene(MonoScript script)
    {
        if (script == null)
        {
            Debug.LogWarning("Assign a component script.");
            return;
        }

        System.Type componentType = script.GetClass();
        if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
        {
            Debug.LogWarning("Selected script is not a Component.");
            return;
        }

        int count = 0;
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);

        foreach (var go in allObjects)
        {
            if (go.GetComponent(componentType) != null)
            {
                Debug.Log($"[Scene Component] Found '{componentType.Name}' on: {GetGameObjectPath(go)}", go);
                count++;
            }
        }

        Debug.Log($"✅ Found {count} GameObject(s) with component {componentType.Name} in Scene.");
    }

    private void FindGameObjectsWithComponentInPrefabs(MonoScript script, string folder)
    {
        if (script == null || string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning("Invalid script or prefab folder.");
            return;
        }

        System.Type componentType = script.GetClass();
        if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
        {
            Debug.LogWarning("Selected script is not a Component.");
            return;
        }

        int count = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) continue;

            Component[] components = prefab.GetComponentsInChildren(componentType, true);
            foreach (var comp in components)
            {
                Debug.Log($"[Prefab Component] Found '{componentType.Name}' in prefab '{path}' on: {GetGameObjectPath(((Component)comp).gameObject)}", prefab);
                count++;
            }
        }

        Debug.Log($"✅ Found {count} prefab GameObject(s) with component {componentType.Name} in folder: {folder}");
    }

}