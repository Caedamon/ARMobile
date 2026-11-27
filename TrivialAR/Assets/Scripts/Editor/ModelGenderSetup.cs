#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Combat;
using Fighters;
using UnityEditor;
using UnityEngine;

// Put in Assets/Scripts/Editor/   (replaces the earlier ModelGenderSetup.cs)
// Menus:  Tools > Kaiju > Fighters > Set Up Gender Tags    adds a "Fighter Gender Tag" to every fighter prefab in
//                                                           Assets/Prefabs/Player and Assets/Prefabs/Enemy, and sets the ones
//                                                           still on "Not Set" from the list below. It NEVER changes a tag you set yourself.
//         Tools > Kaiju > Fighters > Check Gender Tags     lists every fighter prefab with its gender and warns about any "Not Set".
//
// After that you can open any prefab and change the dropdown on its Fighter Gender Tag component (Inspector). It does nothing in the
// game and is shown nowhere: only the name generator reads it.
//   Female = Ninja, Tiefling.   Male = Barbarian, Caveman, Necromancer, Werewolf_Wolf, Vampire.
//   Robot = Robot_One, Clanker.   Undead = the Skeletons, FrostGolem.   Everything else = Either (any human name).
namespace KaijuTools
{
    public static class ModelGenderSetup
    {
        static readonly string[] Folders = { "Assets/Prefabs/Player", "Assets/Prefabs/Enemy" };

        static readonly Dictionary<string, FighterGender> Known = new Dictionary<string, FighterGender>
        {
            { "Ninja", FighterGender.Female },
            { "Tiefling", FighterGender.Female },
            { "Barbarian", FighterGender.Male },
            { "Caveman", FighterGender.Male },
            { "Necromancer", FighterGender.Male },
            { "Werewolf_Wolf", FighterGender.Male },
            { "Vampire", FighterGender.Male },
            { "Robot_One", FighterGender.Robot },
            { "Clanker", FighterGender.Robot },
            { "Skeleton_Warrior", FighterGender.Undead },
            { "Skeleton_Minion", FighterGender.Undead },
            { "Skeleton_Rogue", FighterGender.Undead },
            { "Skeleton_Golem", FighterGender.Undead },
            { "FrostGolem", FighterGender.Undead },
        };

        [MenuItem("Tools/Kaiju/Fighters/Set Up Gender Tags")]
        public static void Run()
        {
            int added = 0, set = 0, kept = 0;
            var lines = new List<string>();

            foreach (var path in FighterPrefabs())
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    var tag = root.GetComponent<FighterGenderTag>();
                    if (tag == null) { tag = root.AddComponent<FighterGenderTag>(); added++; changed = true; }

                    var so = new SerializedObject(tag);
                    var prop = so.FindProperty("gender");
                    var current = (FighterGender)prop.enumValueIndex;
                    string id = Path.GetFileNameWithoutExtension(path);
                    FighterGender known;
                    bool has = Known.TryGetValue(id, out known);

                    // Not Set gets the list; an "Either" from the first version of this tool becomes Robot / Undead where it is listed.
                    bool upgrade = current == FighterGender.Either && has && (known == FighterGender.Robot || known == FighterGender.Undead);
                    if (current == FighterGender.NotSet || upgrade)
                    {
                        prop.enumValueIndex = (int)(has ? known : FighterGender.Either);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        set++; changed = true;
                    }
                    else kept++;

                    lines.Add(path + "  =  " + (FighterGender)prop.enumValueIndex);
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Genders] Tags added: " + added + ", set: " + set + ", left as you had them: " + kept + ".\n - " +
                      (lines.Count == 0 ? "(no fighter prefabs found in Assets/Prefabs/Player or Enemy)" : string.Join("\n - ", lines.ToArray())) +
                      "\nChange any of them on the prefab (Fighter Gender Tag in the Inspector). Undead = human or grim names, Robot = machine names, Either = any human name.");
        }

        [MenuItem("Tools/Kaiju/Fighters/Check Gender Tags")]
        public static void Check()
        {
            var lines = new List<string>();
            int missing = 0;
            foreach (var path in FighterPrefabs())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var tag = prefab != null ? prefab.GetComponent<FighterGenderTag>() : null;
                var g = tag != null ? tag.Gender : FighterGender.NotSet;
                if (g == FighterGender.NotSet) missing++;
                lines.Add(path + "  =  " + (tag == null ? "NO TAG" : g.ToString()) + (g == FighterGender.NotSet ? "   <-- FORGOTTEN" : ""));
            }
            string msg = "[Genders] " + lines.Count + " fighter prefabs, " + missing + " without a gender:\n - " + string.Join("\n - ", lines.ToArray());
            if (missing > 0) Debug.LogWarning(msg + "\nRun Tools > Kaiju > Fighters > Set Up Gender Tags, or pick it by hand on the prefab.");
            else Debug.Log(msg);
        }

        static List<string> FighterPrefabs()
        {
            var list = new List<string>();
            foreach (var folder in Folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && prefab.GetComponent<MeleeAI>() != null) list.Add(path);   // real fighters only
                }
            }
            return list;
        }
    }
}
#endif
