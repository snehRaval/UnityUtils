public interface ISaveManager
{
    void Save(string key, SaveData data);
    SaveData Load(string key);
    void Delete(string key);
    bool HasSave(string key);
}