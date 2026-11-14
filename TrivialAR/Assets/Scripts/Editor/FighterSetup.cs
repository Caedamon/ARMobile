#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Combat;
using Fighters;
using UnityEditor;
using UnityEngine;

// Put this file in Assets/Scripts/Editor/
// Run from the menu: Tools > Kaiju > Fighters > Setup Fighter Generation
//
// What it does (safe to run again):
//  1. Creates/updates Assets/Data/FighterCatalog.asset from the prefabs in Prefabs/Player and Prefabs/Enemy
//  2. Removes the fighters that were baked into KaijuArenaAR (remembering where they stood)
//  3. Adds PlayerSpawn / EnemySpawn markers and an ArenaFighterSpawner to the arena prefab
namespace KaijuTools
{
    public static class FighterSetup
    {
        const string CatalogPath = "Assets/Data/FighterCatalog.asset";
        const string ArenaPrefab = "Assets/Prefabs/AR/KaijuArenaAR.prefab";

        [MenuItem("Tools/Kaiju/Fighters/Setup Fighter Generation")]
        public static void Run()
        {
            var catalog = BuildCatalog();
            bool ok = SetupArena(catalog, out int removed);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int p = catalog.ForSide(FighterSide.Player).Count;
            int e = catalog.ForSide(FighterSide.Enemy).Count;
            Debug.Log($"[FighterSetup] Catalog: {catalog.models.Count} models ({p} usable as player, {e} as enemy). " +
                      $"Baked arena fighters removed: {removed}. Arena spawner: {(ok ? "OK" : "FAILED")}");
        }

        static FighterCatalog BuildCatalog()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");

            var catalog = AssetDatabase.LoadAssetAtPath<FighterCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FighterCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            Scan("Assets/Prefabs/Player", catalog, isPlayer: true);
            Scan("Assets/Prefabs/Enemy", catalog, isPlayer: false);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static void Scan(string folder, FighterCatalog catalog, bool isPlayer)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<MeleeAI>() == null) continue;   // only real fighters

                string id = Path.GetFileNameWithoutExtension(path);
                var model = catalog.Find(id);
                if (model == null)
                {
                    model = new FighterCatalog.Model { id = id };
                    catalog.models.Add(model);
                }
                if (isPlayer) model.playerPrefab = prefab; else model.enemyPrefab = prefab;
                if (path.Contains("/Large/")) model.large = true;
            }
        }

        static bool SetupArena(FighterCatalog catalog, out int removed)
        {
            removed = 0;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefab) == null)
            {
                Debug.LogWarning($"[FighterSetup] Arena prefab not found at {ArenaPrefab}");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(ArenaPrefab);
            try
            {
                // 1) Remember where the baked fighters stand, then remove them.
                var baked = root.GetComponentsInChildren<MeleeAI>(true)
                                .Where(ai => ai.gameObject != root).ToList();

                Vector3? playerPos = null, enemyPos = null;
                if (baked.Count > 0)
                {
                    var withPos = baked.Select(ai => new
                    {
                        ai,
                        local = root.transform.InverseTransformPoint(ai.transform.position)
                    }).ToList();

                    var main = withPos.FirstOrDefault(x => x.ai.team == Team.Main);
                    var player = main != null ? main : withPos.OrderBy(x => x.local.z).First();
                    var enemy = withPos.FirstOrDefault(x => x != player);

                    playerPos = player.local;
                    if (enemy != null) enemyPos = enemy.local;
                }

                foreach (var ai in baked)
                {
                    if (ai == null) continue;
                    Object.DestroyImmediate(ai.gameObject);
                    removed++;
                }

                // 2) Spawn markers
                var ps = EnsureMarker(root, "PlayerSpawn", playerPos ?? new Vector3(0f, 0.05f, -0.5f));
                var es = EnsureMarker(root, "EnemySpawn",  enemyPos  ?? new Vector3(0f, 0.05f,  0.5f));

                // 3) Spawner component
                var spawner = root.GetComponent<ArenaFighterSpawner>();
                if (spawner == null) spawner = root.AddComponent<ArenaFighterSpawner>();

                var so = new SerializedObject(spawner);
                so.FindProperty("catalog").objectReferenceValue = catalog;
                so.FindProperty("playerSpawn").objectReferenceValue = ps;
                so.FindProperty("enemySpawn").objectReferenceValue = es;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefab);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform EnsureMarker(GameObject root, string name, Vector3 localPos)
        {
            var t = root.transform.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }
    }
}
#endif
