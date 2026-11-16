#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Combat;
using UnityEditor;
using UnityEngine;
using Weaponary;

// Menu: Tools > Kaiju > Combat > Create Starter Movesets
// Creates one Moveset per weapon class from the KayKit attack clips, assigns them to your WeaponDefinitions,
// and assigns the unarmed one to the arena's ArenaFighterSpawner. Existing assets are never overwritten.
// The timings (hitAt etc.) are STARTING GUESSES: tune them in the Moveset assets.
namespace KaijuTools
{
    public static class MovesetSetup
    {
        const string Folder = "Assets/Data/Movesets";
        const string AttackFbx = "Assets/Animations/Animations/Rig_Medium/Rig_Medium_CombatMelee.fbx";
        const string ArenaPrefab = "Assets/Prefabs/AR/KaijuArenaAR.prefab";

        [MenuItem("Tools/Kaiju/Combat/Create Starter Movesets")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Movesets");

            var clips = AssetDatabase.LoadAllAssetsAtPath(AttackFbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => c.name).Select(g => g.First()).OrderBy(c => c.name).ToList();
            if (clips.Count == 0) Debug.LogWarning("[MovesetSetup] No clips found in " + AttackFbx);

            var unarmed = Make("Unarmed",   clips, "Melee_Unarmed_Attack",  null,   1.15f, new Vector2(0.20f, 0.45f));
            var oneHand = Make("OneHand",   clips, "Melee_1H_Attack",       "Jump", 1.00f, new Vector2(0.30f, 0.60f));
            var twoHand = Make("TwoHand",   clips, "Melee_2H_Attack",       null,   0.85f, new Vector2(0.50f, 0.90f));
            var dual    = Make("DualWield", clips, "Melee_Dualwield_Attack", null,  1.20f, new Vector2(0.25f, 0.50f));
            var shield  = Make("OneHandShield", clips, "Melee_1H_Attack", "Jump", 0.95f, new Vector2(0.35f, 0.70f));
            AddBash(shield, clips);

            int assigned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.moveset != null) continue;

                Moveset m = null;
                switch (def.weaponClass)
                {
                    case WeaponClass.OneHand:     m = oneHand; break;
                    case WeaponClass.TwoHand:     m = twoHand; break;
                    case WeaponClass.DualOneHand: m = dual;    break;
                    case WeaponClass.OneHandAndShield: m = shield; break;
                }
                if (m == null) continue;
                def.moveset = m;
                EditorUtility.SetDirty(def);
                assigned++;
            }

            bool arena = AssignUnarmed(unarmed);
            AssetDatabase.SaveAssets();
            Debug.Log("[MovesetSetup] Done. Weapons given a moveset: " + assigned + ". Arena unarmed moveset: " + (arena ? "OK" : "FAILED"));
        }

        // Sword-and-shield fighters also get a shield bash (the "Block_Attack" clip).
        static void AddBash(Moveset set, List<AnimationClip> all)
        {
            const string label = "Shield bash!";
            if (set.attacks.Any(a => a.label == label)) return;
            var clip = all.FirstOrDefault(c => c.name == "Melee_Block_Attack");
            if (clip == null) { Debug.LogWarning("[MovesetSetup] Melee_Block_Attack clip not found (no shield bash added)."); return; }
            set.attacks.Add(new AttackMove
            {
                label = label, clip = clip, speed = 1f, hitAt = 0.45f, damageMul = 0.8f,
                lungeModelMeters = 0.15f, knockbackModelMeters = 0.8f, stagger = 1.6f, weight = 0.6f
            });
            EditorUtility.SetDirty(set);
        }

        static Moveset Make(string name, List<AnimationClip> all, string prefix, string exclude, float speed, Vector2 cooldown)
        {
            string path = Folder + "/" + name + ".asset";
            var set = AssetDatabase.LoadAssetAtPath<Moveset>(path);
            if (set != null) return set;

            set = ScriptableObject.CreateInstance<Moveset>();
            set.displayName = name;
            set.cooldownRange = cooldown;
            foreach (var c in all.Where(c => c.name.StartsWith(prefix) && (exclude == null || !c.name.Contains(exclude))))
                set.attacks.Add(DefaultMove(c, speed));
            AssetDatabase.CreateAsset(set, path);
            return set;
        }

        // Starting guesses based on the clip's name. Tune in the asset.
        static AttackMove DefaultMove(AnimationClip c, float speed)
        {
            string n = c.name;
            var m = new AttackMove { label = Pretty(n), clip = c, speed = speed };

            if (n.Contains("Stab"))        { m.hitAt = 0.40f; m.lungeModelMeters = 0.40f; m.knockbackModelMeters = 0.25f; }
            else if (n.Contains("Chop"))   { m.hitAt = 0.50f; m.damageMul = 1.15f; m.knockbackModelMeters = 0.40f; m.stagger = 1.2f; }
            else if (n.Contains("Spin"))   { m.hitAt = 0.50f; m.damageMul = 1.30f; m.reachMul = 1.15f; m.knockbackModelMeters = 0.60f; m.stagger = 1.3f; m.speed = speed * 0.9f; m.weight = 0.6f; }
            else if (n.Contains("Kick"))   { m.hitAt = 0.50f; m.damageMul = 1.10f; m.knockbackModelMeters = 0.50f; }
            else if (n.Contains("Punch"))  { m.hitAt = 0.40f; m.damageMul = 0.90f; m.lungeModelMeters = 0.20f; m.knockbackModelMeters = 0.25f; }
            else                           { m.hitAt = 0.45f; }
            return m;
        }

        static string Pretty(string clipName)
        {
            string s = clipName.Replace("Melee_", "").Replace("_Attack", "").Replace("_", " ");
            return s + "!";
        }

        static bool AssignUnarmed(Moveset unarmed)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefab) == null) return false;
            var root = PrefabUtility.LoadPrefabContents(ArenaPrefab);
            try
            {
                var spawner = root.GetComponent<Fighters.ArenaFighterSpawner>();
                if (spawner == null) return false;
                var so = new SerializedObject(spawner);
                var prop = so.FindProperty("unarmedMoveset");
                if (prop == null) return false;
                if (prop.objectReferenceValue == null) prop.objectReferenceValue = unarmed;
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