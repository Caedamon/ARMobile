using System;
using System.Collections.Generic;
using System.IO;
using Fighters;
using UnityEngine;

namespace Game
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 2;
        public bool hasFighter;                         // JsonUtility can't store "null", so use a flag
        public FighterData fighter = new FighterData();
        public int totalWins;
        public List<FallenFighter> fallen = new List<FallenFighter>();   // the graveyard (oldest first)
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
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (data != null && data.fallen == null) data.fallen = new List<FallenFighter>();   // saves from before the Book
                return data;
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
