using System.Collections.Generic;
using UnityEngine.Serialization;


[System.Serializable]
public class SaveData
{
    public int rows;
    public int cols;
    public int turns;
    public int matches;
    public int combo;
    public int score;

    public List<int> cardValues;
    public List<bool> cardMatched;
    public List<bool> cardFaceDown;
}

[System.Serializable]
public class SaveCollection
{
    public Dictionary<string, SaveData> saves = new Dictionary<string, SaveData>();
}

[System.Serializable]
public class SaveEntry
{
    public string key;
    public SaveData data;
}

[System.Serializable]
public class SaveWrapper
{
    public List<SaveEntry> entries = new List<SaveEntry>();
}