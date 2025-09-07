//	PlayerPrefs Unity Editor Window
//
//	Copyright (c) 2013 Fuzzy Logic (info@fuzzy-logic.co.za)
//	
//	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//	THE SOFTWARE.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Microsoft.Win32;
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Xml;

/// <summary> Editor window that displays editable list of entries stored in player prefs </summary>
public class PlayerPrefsEditor : EditorWindow
{
    #region FIELDS
    /// <summary> Key/value pairs read from disk </summary>
    Dictionary<string, object> m_Plist;

    /// <summary> Keeps track of position for scroll view </summary>
    Vector2 m_ScrollPos;

    /// <summary> Strings used for case insensitive filter display of entries </summary>
    string m_FilterIndex = "";
    string m_FilterKey = "";
    string m_FilterValue = "";

    /// <summary> Used to keep track of play state changes </summary>
    bool m_PrevPlayState = false;

    /// <summary> Indicates whether values can be changed during play mode </summary>
    bool m_CanEditInPlayMode = false;
    #endregion

    #region FUNCTIONS
    [MenuItem("Sneh/PlayerPrefs/Delete Player prefs")]
    static void PlayerPrefsDeleteAll()
    {
        PlayerPrefs.DeleteAll();
    }
    
    /// <summary> Gets called by system, responsible for creating/opening editor window </summary>
    [MenuItem("Sneh/PlayerPrefs/Editor")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        PlayerPrefsEditor window = (PlayerPrefsEditor)EditorWindow.GetWindow(typeof(PlayerPrefsEditor));
        window.titleContent.text = "PlayerPrefs";
    }

    /// <summary>
    /// Called after window is created 
    /// (interestingly, it seems the window gets recreated whenever you press play...) 
    /// </summary>
    void OnEnable()
    {
        // If first time, load list of player prefs keys
        if (m_Plist == null)
        {
            Load();
        }

        // We want to know whenever the play state changes (this is so that we can reload
        // the prefs list when coming out of play mode)
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    /// <summary> Called when window is about to be destroyed </summary>
    void OnDisable()
    {
        // Clean up by unsubscribing from event
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    /// <summary> Notification whenever editor play mode changes </summary>
    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // This gets called twice whenever the state changes
        // When going from stopped -> play, isPlaying is first false, then true
        // When going from play -> stop, isPlaying is first true, then false
        if (m_PrevPlayState != EditorApplication.isPlaying)
        {
            m_PrevPlayState = EditorApplication.isPlaying;

            // Have we now entered "stop" mode?
            if (!m_PrevPlayState)
            {
                // Yes, so reload list of player prefs keys, as it could have changed
                Load();
            }
        }
    }

    /// <summary>
    /// Gets called at 10fps, allows us to update the display of the window regularly 
    /// (as opposed to just when window has focus)
    /// This should allow us to see any changes in player prefs values during play mode
    /// </summary>
    void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying)
        {
            Repaint();
        }
    }

    /// <summary> GUI display logic </summary>
    void OnGUI()
    {
        bool changesMade = false;

        // Work out element widths based on window width
        float widthIndex = 30.0f;
        float widthButton = 25.0f;
        float widthWindow = this.position.width;
        float widthKey = (widthWindow - widthIndex) * 0.3f;
        float widthValue = widthWindow - widthIndex - widthKey - widthButton - 33.0f;

        if (m_Plist != null)
        {
            // Search filter
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:");
            m_CanEditInPlayMode = EditorGUILayout.Toggle("Editable in Play Mode", m_CanEditInPlayMode, GUILayout.Width(180.0f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            m_FilterIndex = EditorGUILayout.TextField(m_FilterIndex, GUILayout.Width(widthIndex));
            m_FilterKey = EditorGUILayout.TextField(m_FilterKey, GUILayout.Width(widthKey));
            m_FilterValue = EditorGUILayout.TextField(m_FilterValue, GUILayout.MaxWidth(widthValue));
            EditorGUILayout.EndHorizontal();

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);

            // Disable GUI in play mode, as we don't want to potentially interfere with changes made by the game code
            GUI.enabled = m_CanEditInPlayMode || !EditorApplication.isPlaying;

            // Keep track of index through iteration for display purposes
            int i = 1;

            int filterIndex = -1;
            if (!string.IsNullOrEmpty(m_FilterIndex))
            {
                System.Int32.TryParse(m_FilterIndex, out filterIndex);
            }

            // Use sorted copy of dictionary for display purposes (allows us to also change dictionary in place)
            SortedDictionary<string, object> copy = new SortedDictionary<string, object>(m_Plist);
            foreach (KeyValuePair<string, object> entry in copy)
            {
                // Only display entries that match filters (or if no filters specified, show all)
                if (((filterIndex == -1) || (i == filterIndex)) &&
                     (string.IsNullOrEmpty(m_FilterKey) || (entry.Key.IndexOf(m_FilterKey, System.StringComparison.OrdinalIgnoreCase) >= 0)) &&
                     (string.IsNullOrEmpty(m_FilterValue) || (entry.Value.ToString().IndexOf(m_FilterValue, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    )
                {
                    EditorGUILayout.BeginHorizontal();

                    // Index
                    EditorGUILayout.LabelField(i.ToString() + ".", GUILayout.Width(widthIndex));

                    // Label
                    EditorGUILayout.LabelField(entry.Key, GUILayout.Width(widthKey));

                    // Value
                    changesMade |= OnGUIEntryValue(entry, widthValue);

                    // Remove button
                    bool clicked = Button("X", widthButton, Color.red);
                    if (clicked)
                    {
                        if (EditorUtility.DisplayDialog("Delete entry", "Are you sure you want to delete " + entry.Key + "?", "Yes", "No"))
                        {
                            m_Plist.Remove(entry.Key);
                            PlayerPrefs.DeleteKey(entry.Key);
                            changesMade = true;
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                // Increase counter
                i++;
            }

            GUI.enabled = true;

            EditorGUILayout.EndScrollView();
        }

        // If any changes have been made to any values, then save the playerprefs immediately
        if (changesMade)
        {
            Save();
        }
    }

    /// <summary> 
    /// Handles GUI display for a specific player pref, taking type into account
    /// TODO: look into using generics to avoid the duplicated code for each type
    /// </summary>
    bool OnGUIEntryValue(KeyValuePair<string, object> entry, float width)
    {
        System.Type valueType = entry.Value.GetType();
        if (valueType == typeof(int))
        {
            int newValue = EditorGUILayout.IntField(PlayerPrefs.GetInt(entry.Key), GUILayout.MaxWidth(width));
            if (newValue != (int)entry.Value)
            {
                m_Plist[entry.Key] = newValue;
                PlayerPrefs.SetInt(entry.Key, newValue);
                return true;
            }
        }
        else if (valueType == typeof(float))
        {
            float newValue = EditorGUILayout.FloatField(PlayerPrefs.GetFloat(entry.Key), GUILayout.MaxWidth(width));
            if (newValue != (float)entry.Value)
            {
                m_Plist[entry.Key] = newValue;
                PlayerPrefs.SetFloat(entry.Key, newValue);
                return true;
            }
        }
        else if (valueType == typeof(string))
        {
            string newValue = EditorGUILayout.TextField(PlayerPrefs.GetString(entry.Key), GUILayout.MaxWidth(width));
            if (newValue != (string)entry.Value)
            {
                m_Plist[entry.Key] = newValue;
                PlayerPrefs.SetString(entry.Key, newValue);
                return true;
            }
        }
        else
        {
            // Type not supported
            EditorGUILayout.LabelField("(editing of type " + valueType.ToString() + " not supported)", GUILayout.MaxWidth(width));
        }

        // No change made
        return false;
    }

    /// <summary> Renders a button </summary>
    bool Button(string label, float width, Color color)
    {
        GUI.backgroundColor = color;
        bool result = GUILayout.Button(label, GUILayout.Width(width));
        GUI.backgroundColor = Color.white;
        return result;
    }

    /// <summary>
    /// Loads the dictionary of keys and their values/types from disk.
    /// Note we have to do this, because although Unity loads PlayerPrefs automatically
    /// there is no way to iterate through all the keys in the PlayerPrefs, so we have to
    /// manually load the file from disk.
    /// </summary>
    void Load()
    {
        // The following operations can throw exceptions
        try
        {
            // On Windows, read from the registry
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                m_Plist = new Dictionary<string, object>();
                string keyName = "Software\\" + PlayerSettings.companyName + "\\" + PlayerSettings.productName;
                RegistryKey key = Registry.CurrentUser.OpenSubKey(keyName);
                if (key != null)
                {
                    string[] valueNames = key.GetValueNames();
                    foreach (string s in valueNames)
                    {
                        // Remove the "_h...." part from the end of the name of the value
                        string valueName = s;
                        int i = valueName.LastIndexOf("_");
                        if (i >= 0)
                        {
                            valueName = s.Remove(i);
                        }

                        m_Plist.Add(valueName, key.GetValue(s));
                    }
                }
            }
            // On OS, read from "plist" file in preferences folder
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                // Uses open source Plist code from https://github.com/animetrics/PlistCS:
                string path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/Library/Preferences/";
                string fullPath = path + "unity." + PlayerSettings.companyName + "." + PlayerSettings.productName + ".plist";
                m_Plist = (Dictionary<string, object>)PlistCS.Plist.readPlist(fullPath);
            }

            //Validate();
        }
        catch
        {
            // No need to notify user
        }
    }

    /// <summary> Ensures that all player prefs entries are supported </summary>
    void Validate()
    {
        if (m_Plist != null)
        {
            foreach (KeyValuePair<string, object> entry in m_Plist)
            {
                System.Type valueType = entry.Value.GetType();
                if ((valueType != typeof(int)) &&
                    (valueType != typeof(float)) &&
                    (valueType != typeof(string)))
                {
                    Debug.LogWarning("PlayerPrefs Editor: entry '" + entry.Key.ToString() + "' type (" + valueType.ToString() + ") is not supported");
                    //MPLLogger.LogWarning("PlayerPrefs Editor: entry '" + entry.Key.ToString() + "' type (" + valueType.ToString() + ") is not supported");
                }
            }
        }
    }

    /// <summary> Saves latest values back into playerprefs </summary>
    void Save()
    {
        PlayerPrefs.Save();
    }
    #endregion
}

namespace PlistCS
{
    public static class Plist
    {
        private static List<int> offsetTable = new List<int>();
        private static List<byte> objectTable = new List<byte>();
        private static int refCount;
        private static int objRefSize;
        private static int offsetByteSize;
        private static long offsetTableOffset;

        #region Public Functions

        public static object readPlist(string path)
        {
            using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return readPlist(f, plistType.Auto);
            }
        }

        public static object readPlistSource(string source)
        {
            return readPlist(System.Text.Encoding.UTF8.GetBytes(source));
        }

        public static object readPlist(byte[] data)
        {
            return readPlist(new MemoryStream(data), plistType.Auto);
        }

        public static plistType getPlistType(Stream stream)
        {
            byte[] magicHeader = new byte[8];
            stream.Read(magicHeader, 0, 8);

            if (BitConverter.ToInt64(magicHeader, 0) == 3472403351741427810)
            {
                return plistType.Binary;
            }
            else
            {
                return plistType.Xml;
            }
        }

        public static object readPlist(Stream stream, plistType type)
        {
            if (type == plistType.Auto)
            {
                type = getPlistType(stream);
                stream.Seek(0, SeekOrigin.Begin);
            }

            if (type == plistType.Binary)
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    byte[] data = reader.ReadBytes((int) reader.BaseStream.Length);
                    return readBinary(data);
                }
            }
            else
            {
                XmlDocument xml = new XmlDocument();
                xml.XmlResolver = null;
                xml.Load(stream);
                return readXml(xml);
            }
        }

        public static void writeXml(object value, string path)
        {
            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.Write(writeXml(value));
            }
        }

        public static void writeXml(object value, Stream stream)
        {
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(writeXml(value));
            }
        }

        public static string writeXml(object value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
                xmlWriterSettings.Encoding = new System.Text.UTF8Encoding(false);
                xmlWriterSettings.ConformanceLevel = ConformanceLevel.Document;
                xmlWriterSettings.Indent = true;

                using (XmlWriter xmlWriter = XmlWriter.Create(ms, xmlWriterSettings))
                {
                    xmlWriter.WriteStartDocument(); 
                    //xmlWriter.WriteComment("DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" " + "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"");
                    xmlWriter.WriteDocType("plist", "-//Apple Computer//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null);
                    xmlWriter.WriteStartElement("plist");
                    xmlWriter.WriteAttributeString("version", "1.0");
                    compose(value, xmlWriter);
                    xmlWriter.WriteEndElement();
                    xmlWriter.WriteEndDocument();
                    xmlWriter.Flush();
                    xmlWriter.Close();
                    return System.Text.Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }

        public static void writeBinary(object value, string path)
        {
            using (BinaryWriter writer = new BinaryWriter(new FileStream(path, FileMode.Create)))
            {
                writer.Write(writeBinary(value));
            }
        }

        public static void writeBinary(object value, Stream stream)
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(writeBinary(value));
            }
        }

        public static byte[] writeBinary(object value)
        {
            offsetTable.Clear();
            objectTable.Clear();
            refCount = 0;
            objRefSize = 0;
            offsetByteSize = 0;
            offsetTableOffset = 0;

            //Do not count the root node, subtract by 1
            int totalRefs = countObject(value) - 1;

            refCount = totalRefs;

            objRefSize = RegulateNullBytes(BitConverter.GetBytes(refCount)).Length;

            composeBinary(value);

            writeBinaryString("bplist00", false);

            offsetTableOffset = (long)objectTable.Count;

            offsetTable.Add(objectTable.Count - 8);

            offsetByteSize = RegulateNullBytes(BitConverter.GetBytes(offsetTable[offsetTable.Count-1])).Length;

            List<byte> offsetBytes = new List<byte>();

            offsetTable.Reverse();

            for (int i = 0; i < offsetTable.Count; i++)
            {
                offsetTable[i] = objectTable.Count - offsetTable[i];
                byte[] buffer = RegulateNullBytes(BitConverter.GetBytes(offsetTable[i]), offsetByteSize);
                Array.Reverse(buffer);
                offsetBytes.AddRange(buffer);
            }

            objectTable.AddRange(offsetBytes);

            objectTable.AddRange(new byte[6]);
            objectTable.Add(Convert.ToByte(offsetByteSize));
            objectTable.Add(Convert.ToByte(objRefSize));

            var a = BitConverter.GetBytes((long) totalRefs + 1);
            Array.Reverse(a);
            objectTable.AddRange(a);

            objectTable.AddRange(BitConverter.GetBytes((long)0));
            a = BitConverter.GetBytes(offsetTableOffset);
            Array.Reverse(a);
            objectTable.AddRange(a);

            return objectTable.ToArray();
        }

        #endregion

        #region Private Functions

        private static object readXml(XmlDocument xml)
        {
            XmlNode rootNode = xml.DocumentElement.ChildNodes[0];
            return (Dictionary<string, object>)parse(rootNode);
        }

        private static object readBinary(byte[] data)
        {
            offsetTable.Clear();
            List<byte> offsetTableBytes = new List<byte>();
            objectTable.Clear();
            refCount = 0;
            objRefSize = 0;
            offsetByteSize = 0;
            offsetTableOffset = 0;

            List<byte> bList = new List<byte>(data);

            List<byte> trailer = bList.GetRange(bList.Count - 32, 32);

            parseTrailer(trailer);

            objectTable = bList.GetRange(0, (int)offsetTableOffset);

            offsetTableBytes = bList.GetRange((int)offsetTableOffset, bList.Count - (int)offsetTableOffset - 32);

            parseOffsetTable(offsetTableBytes);

            return parseBinary(0);
        }

        private static Dictionary<string, object> parseDictionary(XmlNode node)
        {
            XmlNodeList children = node.ChildNodes;
            if (children.Count % 2 != 0)
            {
                throw new DataMisalignedException("Dictionary elements must have an even number of child nodes");
            }

            Dictionary<string, object> dict = new Dictionary<string, object>();

            for (int i = 0; i < children.Count; i += 2)
            {
                XmlNode keynode = children[i];
                XmlNode valnode = children[i + 1];

                if (keynode.Name != "key")
                {
                    throw new ApplicationException("expected a key node");
                }

                object result = parse(valnode);

                if (result != null)
                {
                    dict.Add(keynode.InnerText, result);
                }
            }

            return dict;
        }

        private static List<object> parseArray(XmlNode node)
        {
            List<object> array = new List<object>();

            foreach (XmlNode child in node.ChildNodes)
            {
                object result = parse(child);
                if (result != null)
                {
                    array.Add(result);
                }
            }

            return array;
        }

        private static void composeArray(List<object> value, XmlWriter writer)
        {
            writer.WriteStartElement("array");
            foreach (object obj in value)
            {
                compose(obj, writer);
            }
            writer.WriteEndElement();
        }

        private static object parse(XmlNode node)
        {
            switch (node.Name)
            {
                case "dict":
                    return parseDictionary(node);
                case "array":
                    return parseArray(node);
                case "string":
                    return node.InnerText;
                case "integer":
                  //  int result;
                    //int.TryParse(node.InnerText, System.Globalization.NumberFormatInfo.InvariantInfo, out result);
                    return Convert.ToInt32(node.InnerText, System.Globalization.NumberFormatInfo.InvariantInfo);
                case "real":
                    return Convert.ToDouble(node.InnerText,System.Globalization.NumberFormatInfo.InvariantInfo);
                case "false":
                    return false;
                case "true":
                    return true;
                case "null":
                    return null;
                case "date":
                    return XmlConvert.ToDateTime(node.InnerText, XmlDateTimeSerializationMode.Utc);
                case "data":
                    return Convert.FromBase64String(node.InnerText);
            }

            throw new ApplicationException(String.Format("Plist Node `{0}' is not supported", node.Name));
        }

        private static void compose(object value, XmlWriter writer)
        {

            if (value == null || value is string)
            {
                writer.WriteElementString("string", value as string);
            }
            else if (value is int || value is long)
            {
                writer.WriteElementString("integer", ((int)value).ToString(System.Globalization.NumberFormatInfo.InvariantInfo));
            }
            else if (value is System.Collections.Generic.Dictionary<string, object> ||
              value.GetType().ToString().StartsWith("System.Collections.Generic.Dictionary`2[System.String"))
            {
                //Convert to Dictionary<string, object>
                Dictionary<string, object> dic = value as Dictionary<string, object>;
                if (dic == null)
                {
                    dic = new Dictionary<string, object>();
                    IDictionary idic = (IDictionary)value;
                    foreach (var key in idic.Keys)
                    {
                        dic.Add(key.ToString(), idic[key]);
                    }
                }
                writeDictionaryValues(dic, writer);
            }
            else if (value is List<object>)
            {
                composeArray((List<object>)value, writer);
            }
            else if (value is byte[])
            {
                writer.WriteElementString("data", Convert.ToBase64String((Byte[])value));
            }
            else if (value is float || value is double)
            {
                writer.WriteElementString("real", ((double)value).ToString(System.Globalization.NumberFormatInfo.InvariantInfo));
            }
            else if (value is DateTime)
            {
                DateTime time = (DateTime)value;
                string theString = XmlConvert.ToString(time, XmlDateTimeSerializationMode.Utc);
                writer.WriteElementString("date", theString);//, "yyyy-MM-ddTHH:mm:ssZ"));
            }
            else if (value is bool)
            {
                writer.WriteElementString(value.ToString().ToLower(), "");
            }
            else
            {
                throw new Exception(String.Format("Value type '{0}' is unhandled", value.GetType().ToString()));
            }
        }

        private static void writeDictionaryValues(Dictionary<string, object> dictionary, XmlWriter writer)
        {
            writer.WriteStartElement("dict");
            foreach (string key in dictionary.Keys)
            {
                object value = dictionary[key];
                writer.WriteElementString("key", key);
                compose(value, writer);
            }
            writer.WriteEndElement();
        }

        private static int countObject(object value)
        {
            int count = 0;
            switch (value.GetType().ToString())
            {
                case "System.Collections.Generic.Dictionary`2[System.String,System.Object]":
                    Dictionary<string, object> dict = (Dictionary<string, object>)value;
                    foreach (string key in dict.Keys)
                    {
                        count += countObject(dict[key]);
                    }
                    count += dict.Keys.Count;
                    count++;
                    break;
                case "System.Collections.Generic.List`1[System.Object]":
                    List<object> list = (List<object>)value;
                    foreach (object obj in list)
                    {
                        count += countObject(obj);
                    }
                    count++;
                    break;
                default:
                    count++;
                    break;
            }
            return count;
        }

        private static byte[] writeBinaryDictionary(Dictionary<string, object> dictionary)
        {
            List<byte> buffer = new List<byte>();
            List<byte> header = new List<byte>();
            List<int> refs = new List<int>();
            for (int i = dictionary.Count - 1; i >= 0; i--)
            {
                var o = new object[dictionary.Count];
                dictionary.Values.CopyTo(o, 0);
                composeBinary(o[i]);
                offsetTable.Add(objectTable.Count);
                refs.Add(refCount);
                refCount--;
            }
            for (int i = dictionary.Count - 1; i >= 0; i--)
            {
                var o = new string[dictionary.Count];
                dictionary.Keys.CopyTo(o, 0);
                composeBinary(o[i]);//);
                offsetTable.Add(objectTable.Count);
                refs.Add(refCount);
                refCount--;
            }

            if (dictionary.Count < 15)
            {
                header.Add(Convert.ToByte(0xD0 | Convert.ToByte(dictionary.Count)));
            }
            else
            {
                header.Add(0xD0 | 0xf);
                header.AddRange(writeBinaryInteger(dictionary.Count, false));
            }


            foreach (int val in refs)
            {
                byte[] refBuffer = RegulateNullBytes(BitConverter.GetBytes(val), objRefSize);
                Array.Reverse(refBuffer);
                buffer.InsertRange(0, refBuffer);
            }

            buffer.InsertRange(0, header);


            objectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

        private static byte[] composeBinaryArray(List<object> objects)
        {
            List<byte> buffer = new List<byte>();
            List<byte> header = new List<byte>();
            List<int> refs = new List<int>();

            for (int i = objects.Count - 1; i >= 0; i--)
            {
                composeBinary(objects[i]);
                offsetTable.Add(objectTable.Count);
                refs.Add(refCount);
                refCount--;
            }

            if (objects.Count < 15)
            {
                header.Add(Convert.ToByte(0xA0 | Convert.ToByte(objects.Count)));
            }
            else
            {
                header.Add(0xA0 | 0xf);
                header.AddRange(writeBinaryInteger(objects.Count, false));
            }

            foreach (int val in refs)
            {
                byte[] refBuffer = RegulateNullBytes(BitConverter.GetBytes(val), objRefSize);
                Array.Reverse(refBuffer);
                buffer.InsertRange(0, refBuffer);
            }

            buffer.InsertRange(0, header);

            objectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

        private static byte[] composeBinary(object obj)
        {
            byte[] value;
            switch (obj.GetType().ToString())
            {
                case "System.Collections.Generic.Dictionary`2[System.String,System.Object]":
                    value = writeBinaryDictionary((Dictionary<string, object>)obj);
                    return value;

                case "System.Collections.Generic.List`1[System.Object]":
                    value = composeBinaryArray((List<object>)obj);
                    return value;

                case "System.Byte[]":
                    value = writeBinaryByteArray((byte[])obj);
                    return value;

                case "System.Double":
                    value = writeBinaryDouble((double)obj);
                    return value;

                case "System.Int32":
                    value = writeBinaryInteger((int)obj, true);
                    return value;

                case "System.String":
                    value = writeBinaryString((string)obj, true);
                    return value;

                case "System.DateTime":
                    value = writeBinaryDate((DateTime)obj);
                    return value;

                case "System.Boolean":
                    value = writeBinaryBool((bool)obj);
                    return value;

                default:
                    return new byte[0];
            }
        }

        public static byte[] writeBinaryDate(DateTime obj)
        {
            List<byte> buffer =new List<byte>(RegulateNullBytes(BitConverter.GetBytes(PlistDateConverter.ConvertToAppleTimeStamp(obj)), 8));
            buffer.Reverse();
            buffer.Insert(0, 0x33);
            objectTable.InsertRange(0, buffer);
            return buffer.ToArray();
        }

        public static byte[] writeBinaryBool(bool obj)
        {
            List<byte> buffer = new List<byte>(new byte[1] { (bool)obj ? (byte)9 : (byte)8 });
            objectTable.InsertRange(0, buffer);
            return buffer.ToArray();
        }

        private static byte[] writeBinaryInteger(int value, bool write)
        {
            List<byte> buffer = new List<byte>(BitConverter.GetBytes((long) value));
            buffer =new List<byte>(RegulateNullBytes(buffer.ToArray()));
            while (buffer.Count != Math.Pow(2, Math.Log(buffer.Count) / Math.Log(2)))
                buffer.Add(0);
            int header = 0x10 | (int)(Math.Log(buffer.Count) / Math.Log(2));

            buffer.Reverse();

            buffer.Insert(0, Convert.ToByte(header));

            if (write)
                objectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

        private static byte[] writeBinaryDouble(double value)
        {
            List<byte> buffer =new List<byte>(RegulateNullBytes(BitConverter.GetBytes(value), 4));
            while (buffer.Count != Math.Pow(2, Math.Log(buffer.Count) / Math.Log(2)))
                buffer.Add(0);
            int header = 0x20 | (int)(Math.Log(buffer.Count) / Math.Log(2));

            buffer.Reverse();

            buffer.Insert(0, Convert.ToByte(header));

            objectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

        private static byte[] writeBinaryByteArray(byte[] value)
        {
            List<byte> buffer = new List<byte>(value);
            List<byte> header = new List<byte>();
            if (value.Length < 15)
            {
                header.Add(Convert.ToByte(0x40 | Convert.ToByte(value.Length)));
            }
            else
            {
                header.Add(0x40 | 0xf);
                header.AddRange(writeBinaryInteger(buffer.Count, false));
            }

            buffer.InsertRange(0, header);

            objectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

        private static byte[] writeBinaryString(string value, bool head)
        {
            List<byte> buffer = new List<byte>();
            List<byte> header = new List<byte>();
            foreach (char chr in value.ToCharArray())
                buffer.Add(Convert.ToByte(chr));

            if (head)
            {
                if (value.Length < 15)
                {
                    header.Add(Convert.ToByte(0x50 | Convert.ToByte(value.Length)));
                }
                else
                {
                    header.Add(0x50 | 0xf);
                    header.AddRange(writeBinaryInteger(buffer.Count, false));
                }
            }

            buffer.InsertRange(0, header);

            objectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

        private static byte[] RegulateNullBytes(byte[] value)
        {
            return RegulateNullBytes(value, 1);
        }

        private static byte[] RegulateNullBytes(byte[] value, int minBytes)
        {
            Array.Reverse(value);
            List<byte> bytes = new List<byte>(value);
            for (int i = 0; i < bytes.Count; i++)
            {
                if (bytes[i] == 0 && bytes.Count > minBytes)
                {
                    bytes.Remove(bytes[i]);
                    i--;
                }
                else
                    break;
            }

            if (bytes.Count < minBytes)
            {
                int dist = minBytes - bytes.Count;
                for (int i = 0; i < dist; i++)
                    bytes.Insert(0, 0);
            }

            value = bytes.ToArray();
            Array.Reverse(value);
            return value;
        }

        private static void parseTrailer(List<byte> trailer)
        {
            offsetByteSize = BitConverter.ToInt32(RegulateNullBytes(trailer.GetRange(6, 1).ToArray(), 4), 0);
            objRefSize = BitConverter.ToInt32(RegulateNullBytes(trailer.GetRange(7, 1).ToArray(), 4), 0);
            byte[] refCountBytes = trailer.GetRange(12, 4).ToArray();
            Array.Reverse(refCountBytes);
            refCount = BitConverter.ToInt32(refCountBytes, 0);
            byte[] offsetTableOffsetBytes = trailer.GetRange(24, 8).ToArray();
            Array.Reverse(offsetTableOffsetBytes);
            offsetTableOffset = BitConverter.ToInt64(offsetTableOffsetBytes, 0);
        }

        private static void parseOffsetTable(List<byte> offsetTableBytes)
        {
            for (int i = 0; i < offsetTableBytes.Count; i += offsetByteSize)
            {
                byte[] buffer = offsetTableBytes.GetRange(i, offsetByteSize).ToArray();
                Array.Reverse(buffer);
                offsetTable.Add(BitConverter.ToInt32(RegulateNullBytes(buffer, 4), 0));
            }
        }

        private static object parseBinaryDictionary(int objRef)
        {
            Dictionary<string, object> buffer = new Dictionary<string, object>();
            List<int> refs = new List<int>();
            int refCount = 0;

            //byte dictByte = objectTable[offsetTable[objRef]];
            
            int refStartPosition;
            refCount = getCount(offsetTable[objRef], out refStartPosition);


            if (refCount < 15)
                refStartPosition = offsetTable[objRef] + 1;
            else
                refStartPosition = offsetTable[objRef] + 2 + RegulateNullBytes(BitConverter.GetBytes(refCount), 1).Length;

            for (int i = refStartPosition; i < refStartPosition + refCount * 2 * objRefSize; i += objRefSize)
            {
                byte[] refBuffer = objectTable.GetRange(i, objRefSize).ToArray();
                Array.Reverse(refBuffer);
                refs.Add(BitConverter.ToInt32(RegulateNullBytes(refBuffer, 4), 0));
            }

            for (int i = 0; i < refCount; i++)
            {
                buffer.Add((string)parseBinary(refs[i]), parseBinary(refs[i + refCount]));
            }

            return buffer;
        }

        private static object parseBinaryArray(int objRef)
        {
            List<object> buffer = new List<object>();
            List<int> refs = new List<int>();
            int refCount = 0;

            //byte arrayByte = objectTable[offsetTable[objRef]];

            int refStartPosition;
            refCount = getCount(offsetTable[objRef], out refStartPosition);


            if (refCount < 15)
                refStartPosition = offsetTable[objRef] + 1;
            else
                //The following integer has a header aswell so we increase the refStartPosition by two to account for that.
                refStartPosition = offsetTable[objRef] + 2 + RegulateNullBytes(BitConverter.GetBytes(refCount), 1).Length;

            for (int i = refStartPosition; i < refStartPosition + refCount * objRefSize; i += objRefSize)
            {
                byte[] refBuffer = objectTable.GetRange(i, objRefSize).ToArray();
                Array.Reverse(refBuffer);
                refs.Add(BitConverter.ToInt32(RegulateNullBytes(refBuffer, 4), 0));
            }

            for (int i = 0; i < refCount; i++)
            {
                buffer.Add(parseBinary(refs[i]));
            }

            return buffer;
        }

        private static int getCount(int bytePosition, out int newBytePosition)
        {
            byte headerByte = objectTable[bytePosition];
            byte headerByteTrail = Convert.ToByte(headerByte & 0xf);
            int count;
            if (headerByteTrail < 15)
            {
                count = headerByteTrail;
                newBytePosition = bytePosition + 1;
            }
            else
                count = (int)parseBinaryInt(bytePosition + 1, out newBytePosition);
            return count;
        }

        private static object parseBinary(int objRef)
        {
            byte header = objectTable[offsetTable[objRef]];
            switch (header & 0xF0)
            {
                case 0:
                    {
                        //If the byte is
                        //0 return null
                        //9 return true
                        //8 return false
                        return (objectTable[offsetTable[objRef]] == 0) ? (object)null : ((objectTable[offsetTable[objRef]] == 9) ? true : false);
                    }
                case 0x10:
                    {
                        return parseBinaryInt(offsetTable[objRef]);
                    }
                case 0x20:
                    {
                        return parseBinaryReal(offsetTable[objRef]);
                    }
                case 0x30:
                    {
                        return parseBinaryDate(offsetTable[objRef]);
                    }
                case 0x40:
                    {
                        return parseBinaryByteArray(offsetTable[objRef]);
                    }
                case 0x50://String ASCII
                    {
                        return parseBinaryAsciiString(offsetTable[objRef]);
                    }
                case 0x60://String Unicode
                    {
                        return parseBinaryUnicodeString(offsetTable[objRef]);
                    }
                case 0xD0:
                    {
                        return parseBinaryDictionary(objRef);
                    }
                case 0xA0:
                    {
                        return parseBinaryArray(objRef);
                    }
            }
            throw new Exception("This type is not supported");
        }

        public static object parseBinaryDate(int headerPosition)
        {
            byte[] buffer = objectTable.GetRange(headerPosition + 1, 8).ToArray();
            Array.Reverse(buffer);
            double appleTime = BitConverter.ToDouble(buffer, 0);
            DateTime result = PlistDateConverter.ConvertFromAppleTimeStamp(appleTime);
            return result;
        }
        
        private static object parseBinaryInt(int headerPosition)
        {
            int output;
            return parseBinaryInt(headerPosition, out output);
        }

        private static object parseBinaryInt(int headerPosition, out int newHeaderPosition)
        {
            byte header = objectTable[headerPosition];
            int byteCount = (int)Math.Pow(2, header & 0xf);
            byte[] buffer = objectTable.GetRange(headerPosition + 1, byteCount).ToArray();
            Array.Reverse(buffer);
            //Add one to account for the header byte
            newHeaderPosition = headerPosition + byteCount + 1;
            return BitConverter.ToInt32(RegulateNullBytes(buffer, 4), 0);
        }

        private static object parseBinaryReal(int headerPosition)
        {
            byte header = objectTable[headerPosition];
            int byteCount = (int)Math.Pow(2, header & 0xf);
            byte[] buffer = objectTable.GetRange(headerPosition + 1, byteCount).ToArray();
            Array.Reverse(buffer);

            return BitConverter.ToDouble(RegulateNullBytes(buffer, 8), 0);
        }

        private static object parseBinaryAsciiString(int headerPosition)
        {
            int charStartPosition;
            int charCount = getCount(headerPosition, out charStartPosition);

            var buffer = objectTable.GetRange(charStartPosition, charCount);
            return buffer.Count > 0 ? Encoding.ASCII.GetString(buffer.ToArray()) : string.Empty;
        }

        private static object parseBinaryUnicodeString(int headerPosition)
        {
            int charStartPosition;
            int charCount = getCount(headerPosition, out charStartPosition);
            charCount = charCount * 2;

            byte[] buffer = new byte[charCount];
            byte one, two;

            for (int i = 0; i < charCount; i+=2)
            {
                one = objectTable.GetRange(charStartPosition+i,1)[0];
                two = objectTable.GetRange(charStartPosition + i+1, 1)[0];

                if (BitConverter.IsLittleEndian)
                {
                    buffer[i] = two;
                    buffer[i + 1] = one;
                }
                else
                {
                    buffer[i] = one;
                    buffer[i + 1] = two;
                }
            }

            return Encoding.Unicode.GetString(buffer);
        }

        private static object parseBinaryByteArray(int headerPosition)
        {
            int byteStartPosition;
            int byteCount = getCount(headerPosition, out byteStartPosition);
            return objectTable.GetRange(byteStartPosition, byteCount).ToArray();
        }

        #endregion
    }
    
    public enum plistType
    {
        Auto, Binary, Xml
    }

    public static class PlistDateConverter
    {
        public static long timeDifference = 978307200;

        public static long GetAppleTime(long unixTime)
        {
            return unixTime - timeDifference;
        }

        public static long GetUnixTime(long appleTime)
        {
            return appleTime + timeDifference;
        }

        public static DateTime ConvertFromAppleTimeStamp(double timestamp)
        {
            DateTime origin = new DateTime(2001, 1, 1, 0, 0, 0, 0);
            return origin.AddSeconds(timestamp);
        }

        public static double ConvertToAppleTimeStamp(DateTime date)
        {
            DateTime begin = new DateTime(2001, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = date - begin;
            return Math.Floor(diff.TotalSeconds);
        }
    }
}

