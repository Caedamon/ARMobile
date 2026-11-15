using System;
using System.IO;
using Fighters;
using UnityEngine;

namespace Game
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public bool hasFighter;                         // JsonUtility can't store "null", so use a flag
        public FighterData fighter = new FighterData();
        public int totalWins;
    }

    /// <summary>One JSON file in Application.persistentDataPath.</summary>
    public static class SaveSystem
    {
        static string FilePath { get { return Path.Combine(Application.persistentDataPath, "save.json"); } }

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Could not read save file: " + e.Message);
                return null;
            }
        }

        public static void Save(SaveData data)
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(data, true)); }
            catch (Exception e) { Debug.LogWarning("[Save] Could not write save file: " + e.Message); }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning("[Save] Could not delete save file: " + e.Message); }
        }
    }
}
