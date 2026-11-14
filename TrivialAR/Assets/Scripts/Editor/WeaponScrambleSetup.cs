#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Combat;
using Items;
using UnityEditor;
using UnityEngine;
using Weaponary;

// Put this file in Assets/Scripts/Editor/
// Run from the menu: Tools > Kaiju > Weapons > Setup Weapon Scramble
//
// Safe to run more than once:
//  - weapon assets that already exist are NOT overwritten (your tuning stays)
//  - components are only added if missing
//  - the arena's weapon table is only filled if it is empty
namespace KaijuTools
{
    public static class WeaponScrambleSetup
    {
        const string DataFolder   = "Assets/Data/Weapons";
        const string AttackFbx    = "Assets/Animations/Animations/Rig_Medium/Rig_Medium_CombatMelee.fbx";
        const string BaseMelee    = "Assets/Animations/Controllers/Animator/BaseMelee.controller";
        const string ArenaPrefab  = "Assets/Prefabs/AR/KaijuArenaAR.prefab";
        static readonly string[] FighterFolders = { "Assets/Prefabs/Player", "Assets/Prefabs/Enemy" };

        [MenuItem("Tools/Kaiju/Weapons/Setup Weapon Scramble")]
        public static void RunAll()
        {
            var defs = CreateDefinitions();
            int fighters = SetupFighters();
            bool arena = SetupArena(defs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WeaponSetup] Done. Weapon assets: {defs.Count}, fighter prefabs updated: {fighters}, arena spawner: {(arena ? "OK" : "FAILED")}");
        }

        // ---------------------------------------------------------------- weapon assets
        static List<WeaponDefinition> CreateDefinitions()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(DataFolder);

            var clips = LoadClips(AttackFbx);
            if (clips.Count == 0)
                Debug.LogWarning($"[WeaponSetup] No animation clips found in {AttackFbx}. Weapons will be created without attack clips.");

            var oneHand = Clips(clips, "Melee_1H_Attack", exclude: "Jump");
            var twoHand = Clips(clips, "Melee_2H_Attack");
            var dual    = Clips(clips, "Melee_Dualwield_Attack");
            var spear   = twoHand.Where(c => c.name.Contains("Stab") || c.name.Contains("Chop")).ToList();

            const string H1 = "Assets/Prefabs/Weapons/Mele/1H/";
            const string H2 = "Assets/Prefabs/Weapons/Mele/2H/";

            var list = new List<WeaponDefinition>();
            //   name           class                   main prefab                  offhand prefab                  dmg  reach  clips
            Make(list, "Sword",      WeaponClass.OneHand,     H1 + "1H_Sword.prefab",      null,                           25f, 0.11f, oneHand);
            Make(list, "Axe",        WeaponClass.OneHand,     H1 + "1H_Axe.prefab",        null,                           28f, 0.11f, oneHand);
            Make(list, "Warhammer",  WeaponClass.OneHand,     H1 + "paladin_hammer.prefab", null,                          30f, 0.11f, oneHand);
            Make(list, "Dual Knives",WeaponClass.DualOneHand, H1 + "Knife.prefab",         H1 + "Knife_Offhand.prefab",    20f, 0.10f, dual);
            Make(list, "Greatsword", WeaponClass.TwoHand,     H2 + "2H_Sword.prefab",      null,                           40f, 0.14f, twoHand);
            Make(list, "Greataxe",   WeaponClass.TwoHand,     H2 + "2H_Axe.prefab",        null,                           45f, 0.14f, twoHand);
            Make(list, "Spear",      WeaponClass.TwoHand,     H2 + "Caveman_Spear.prefab", null,                           32f, 0.16f, spear);
            return list;
        }

        static void Make(List<WeaponDefinition> outList, string name, WeaponClass cls, string prefabPath,
                         string offhandPath, float dmg, float reach, List<AnimationClip> clips)
        {
            string path = $"{DataFolder}/{name}.asset";
            var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (def == null)
            {
                var main = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (main == null)
                {
                    Debug.LogWarning($"[WeaponSetup] Skipped '{name}': prefab not found at {prefabPath}");
                    return;
                }
                def = ScriptableObject.CreateInstance<WeaponDefinition>();
                def.displayName = name;
                def.weaponClass = cls;
                def.visualPrefab = main;
                if (!string.IsNullOrEmpty(offhandPath)) def.offhandVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(offhandPath);
                def.baseDamage = dmg;
                def.reach = reach;
                def.attackClips = new List<AnimationClip>(clips);
                AssetDatabase.CreateAsset(def, path);
            }
            outList.Add(def);
        }

        static List<AnimationClip> LoadClips(string fbxPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => c.name).Select(g => g.First())
                .OrderBy(c => c.name)
                .ToList();
        }

        static List<AnimationClip> Clips(List<AnimationClip> all, string prefix, string exclude = null)
        {
            return all.Where(c => c.name.StartsWith(prefix) && (exclude == null || !c.name.Contains(exclude))).ToList();
        }

        // ---------------------------------------------------------------- fighters
        static int SetupFighters()
        {
            string lightClip = ReadAttackLightClipName();
            if (string.IsNullOrEmpty(lightClip))
                Debug.LogWarning("[WeaponSetup] Could not read the 'Attack_Light' clip from BaseMelee.controller; WeaponHolder keeps its default clip name.");

            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", FighterFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<MeleeAI>() == null) continue;   // not a fighter

                    var holder = root.GetComponent<WeaponHolder>();
                    if (holder == null) holder = root.AddComponent<WeaponHolder>();
                    if (!string.IsNullOrEmpty(lightClip)) holder.lightAttackOriginalClip = lightClip;

                    if (root.GetComponent<WeaponSeeker>() == null) root.AddComponent<WeaponSeeker>();

                    var old = root.GetComponent<AutoEquipController>();   // replaced by WeaponSeeker
                    if (old != null) Object.DestroyImmediate(old, true);

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return count;
        }

        static string ReadAttackLightClipName()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(BaseMelee);
            if (ctrl == null) return null;
            foreach (var layer in ctrl.layers)
                foreach (var cs in layer.stateMachine.states)
                    if (cs.state.name == "Attack_Light" && cs.state.motion != null)
                        return cs.state.motion.name;
            return null;
        }

        // ---------------------------------------------------------------- arena
        static bool SetupArena(List<WeaponDefinition> defs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefab) == null)
            {
                Debug.LogWarning($"[WeaponSetup] Arena prefab not found at {ArenaPrefab}");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(ArenaPrefab);
            try
            {
                var spawner = root.GetComponent<WeaponSpawner>();
                if (spawner == null) spawner = root.AddComponent<WeaponSpawner>();

                var so = new SerializedObject(spawner);
                var table = so.FindProperty("table");
                if (table.arraySize == 0)
                {
                    for (int i = 0; i < defs.Count; i++)
                    {
                        table.InsertArrayElementAtIndex(i);
                        var el = table.GetArrayElementAtIndex(i);
                        el.FindPropertyRelative("weapon").objectReferenceValue = defs[i];
                        el.FindPropertyRelative("weight").floatValue = 1f;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefab);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }
}
#endif
