#if UNITY_EDITOR
using Combat;
using Fighters;
using UnityEditor;
using UnityEngine;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Combat > Create Combat Tuning
//
// Creates Assets/Data/CombatTuning.asset (if missing) and plugs it into every ArenaFighterSpawner it finds in
// Assets/Prefabs that has none yet. Never overwrites an asset or an assignment you already have.
namespace KaijuTools
{
    public static class CombatTuningSetup
    {
        const string AssetPath = "Assets/Data/CombatTuning.asset";

        [MenuItem("Tools/Kaiju/Combat/Create Combat Tuning")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");

            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(AssetPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<CombatTuning>();
                AssetDatabase.CreateAsset(tuning, AssetPath);
                Debug.Log("[Combat] Created " + AssetPath);
            }

            int plugged = 0, found = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var probe = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (probe == null || probe.GetComponentInChildren<ArenaFighterSpawner>(true) == null) continue;
                found++;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var sp in root.GetComponentsInChildren<ArenaFighterSpawner>(true))
                    {
                        var so = new SerializedObject(sp);
                        var p = so.FindProperty("combatTuning");
                        if (p.objectReferenceValue != null) continue;
                        p.objectReferenceValue = tuning;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                    if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); plugged++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // Also any spawner sitting in the open scene.
            foreach (var sp in Object.FindObjectsByType<ArenaFighterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (EditorUtility.IsPersistent(sp)) continue;
                var so = new SerializedObject(sp);
                var p = so.FindProperty("combatTuning");
                if (p.objectReferenceValue != null) continue;
                p.objectReferenceValue = tuning;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sp);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = tuning;
            EditorGUIUtility.PingObject(tuning);
            Debug.Log("[Combat] Combat Tuning is at " + AssetPath + ". Arena prefabs with an ArenaFighterSpawner: " + found + ", newly plugged in: " + plugged +
                      ". Edit the asset to change the balance; check it with Tools > Kaiju > Combat > Balance Simulator.");
        }
    }
}
#endif
