#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Combat;
using Items;
using UnityEditor;
using UnityEngine;
using Weaponary;

// Put this file in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Weapons > Create Shield Loadouts
// Creates "weapon + shield" loadouts (class OneHandAndShield) from your existing prefabs and adds them to the
// arena's weapon spawn table. Safe to run again. Run "Create Starter Movesets" first (it makes the OneHandShield moveset).
namespace KaijuTools
{
    public static class ShieldSetup
    {
        const string Folder = "Assets/Data/Weapons";
        const string OneH = "Assets/Prefabs/Weapons/Mele/1H/";
        const string Shields = "Assets/Prefabs/Weapons/Shields/";
        const string MovesetPath = "Assets/Data/Movesets/OneHandShield.asset";
        const string ArenaPrefab = "Assets/Prefabs/AR/KaijuArenaAR.prefab";

        struct Loadout
        {
            public string name, main, shield;
            public float damage;
            public Loadout(string n, string m, string s, float d) { name = n; main = m; shield = s; damage = d; }
        }

        static readonly Loadout[] Loadouts =
        {
            new Loadout("Sword & Round Shield",        OneH + "1H_Sword.prefab",        Shields + "Round_Shield.prefab",           24f),
            new Loadout("Axe & Barbarian Shield",      OneH + "1H_Axe.prefab",          Shields + "Barbarian_Round_Shield.prefab", 27f),
            new Loadout("Hammer & Paladin Shield",     OneH + "paladin_hammer.prefab",  Shields + "paladin_shield.prefab",         29f),
            new Loadout("Black Knight Sword & Shield", OneH + "BlackKnight_Sword.prefab", Shields + "BlackKnight_Shield.prefab",  28f),
            new Loadout("Knight's Sword & Tower Shield", OneH + "Tiefling_Sword.prefab", Shields + "Rectangle_Shield.prefab",      26f),
            new Loadout("Club & Spike Shield",         OneH + "Caveman_Club.prefab",    Shields + "Spike_Shield.prefab",           26f),
            new Loadout("Vampire Sword & Badge Shield", OneH + "Vampire_Sword.prefab",  Shields + "Badge_Shield.prefab",           25f),
        };

        [MenuItem("Tools/Kaiju/Weapons/Create Shield Loadouts")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Weapons");

            var moveset = AssetDatabase.LoadAssetAtPath<Moveset>(MovesetPath);
            if (moveset == null)
                Debug.LogWarning("[ShieldSetup] OneHandShield moveset not found. Run Tools > Kaiju > Combat > Create Starter Movesets, then run this again.");

            var defs = new List<WeaponDefinition>();
            foreach (var l in Loadouts)
            {
                string path = Folder + "/" + l.name + ".asset";
                var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (def == null)
                {
                    var main = AssetDatabase.LoadAssetAtPath<GameObject>(l.main);
                    var shield = AssetDatabase.LoadAssetAtPath<GameObject>(l.shield);
                    if (main == null || shield == null)
                    {
                        Debug.LogWarning("[ShieldSetup] Skipped '" + l.name + "': prefab not found (" + l.main + " / " + l.shield + ")");
                        continue;
                    }
                    def = ScriptableObject.CreateInstance<WeaponDefinition>();
                    def.displayName = l.name;
                    def.weaponClass = WeaponClass.OneHandAndShield;
                    def.visualPrefab = main;
                    def.offhandVisualPrefab = shield;
                    def.baseDamage = l.damage;
                    def.reach = 0.10f;
                    AssetDatabase.CreateAsset(def, path);
                }
                if (def.moveset == null && moveset != null) def.moveset = moveset;
                EditorUtility.SetDirty(def);
                defs.Add(def);
            }

            bool arena = AddToSpawner(defs);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShieldSetup] Done. Loadouts: " + defs.Count + ", added to arena spawn table: " + (arena ? "OK" : "FAILED"));
        }

        static bool AddToSpawner(List<WeaponDefinition> defs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefab) == null) return false;
            var root = PrefabUtility.LoadPrefabContents(ArenaPrefab);
            try
            {
                var spawner = root.GetComponent<WeaponSpawner>();
                if (spawner == null) return false;

                var so = new SerializedObject(spawner);
                var table = so.FindProperty("table");
                var present = new HashSet<Object>();
                for (int i = 0; i < table.arraySize; i++)
                    present.Add(table.GetArrayElementAtIndex(i).FindPropertyRelative("weapon").objectReferenceValue);

                foreach (var def in defs)
                {
                    if (present.Contains(def)) continue;
                    int idx = table.arraySize;
                    table.InsertArrayElementAtIndex(idx);
                    var el = table.GetArrayElementAtIndex(idx);
                    el.FindPropertyRelative("weapon").objectReferenceValue = def;
                    el.FindPropertyRelative("weight").floatValue = 0.6f;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefab);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
