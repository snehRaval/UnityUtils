using UnityEngine;
using System.IO;

public class SaveLoadManager : ISaveManager
{
    private readonly string savePath;
    private readonly ISerializer serializer;
    private SaveWrapper cache;

    public SaveLoadManager(ISerializer serializer = null, string fileName = "SavesGame.json")
    {
        this.serializer = serializer ?? new UnityJsonSerializer();
        savePath = Path.Combine(Application.persistentDataPath, fileName);
        cache = LoadAll();
    }

    private SaveWrapper LoadAll()
    {
        if (!File.Exists(savePath))
            return new SaveWrapper();

        string json = File.ReadAllText(savePath);
        return serializer.Deserialize<SaveWrapper>(json);
    }

    private void SaveAll()
    {
        string json = serializer.Serialize(cache);
        File.WriteAllText(savePath, json);
    }

    public void Save(string key, SaveData data)
    {
        var existing = cache.entries.Find(e => e.key == key);
        if (existing != null)
            existing.data = data;
        else
            cache.entries.Add(new SaveEntry { key = key, data = data });

        SaveAll();
        Debug.Log($"Game saved for key {key}");
    }

    public SaveData Load(string key) => cache.entries.Find(e => e.key == key)?.data;

    public void Delete(string key)
    {
        cache.entries.RemoveAll(e => e.key == key);
        SaveAll();
        Debug.Log($"Save deleted for key {key}");
    }

    public bool HasSave(string key) => cache.entries.Exists(e => e.key == key);
}