#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AR;
using Fighters;
using Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Weaponary;

// Put this file in Assets/Scripts/Editor/
// Run from the menu (with your game scene open): Tools > Kaiju > Game > Setup Game Flow
//
// It adds a "GameController" object to the open scene and fills in its references
// (fighter catalog, arena prefab, showcase weapons), switches off the old ARPlaceArena, and saves the scene.
// Safe to run again.
namespace KaijuTools
{
    public static class GameFlowSetup
    {
        const string CatalogPath = "Assets/Data/FighterCatalog.asset";
        const string ArenaPath   = "Assets/Prefabs/AR/KaijuArenaAR.prefab";

        [MenuItem("Tools/Kaiju/Game/Setup Game Flow")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[GameSetup] Open and save your game scene first (the one with the XR Origin).");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<FighterCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[GameSetup] FighterCatalog not found. Run Tools > Kaiju > Fighters > Setup Fighter Generation first.");
                return;
            }

            // Arena prefab: prefer the one the old placement script used.
            var oldPlacer = Object.FindFirstObjectByType<ARPlaceArena>(FindObjectsInactive.Include);
            GameObject arena = null;
            if (oldPlacer != null)
            {
                var oldSo = new SerializedObject(oldPlacer);
                var p = oldSo.FindProperty("arenaPrefab");
                if (p != null) arena = p.objectReferenceValue as GameObject;
            }
            if (arena == null) arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath);
            if (arena == null)
            {
                Debug.LogError("[GameSetup] Could not find the arena prefab.");
                return;
            }

            var weapons = AssetDatabase.FindAssets("t:WeaponDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null)
                .ToList();

            // The controller object
            var existing = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
            GameObject go = existing != null ? existing.gameObject : new GameObject("GameController");
            var gc = go.GetComponent<GameController>();
            if (gc == null) gc = go.AddComponent<GameController>();
            if (go.GetComponent<ARStagePlacer>() == null) go.AddComponent<ARStagePlacer>();

            var so = new SerializedObject(gc);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.FindProperty("arenaPrefab").objectReferenceValue = arena;
            var list = so.FindProperty("showcaseWeapons");
            list.arraySize = weapons.Count;
            for (int i = 0; i < weapons.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            if (oldPlacer != null)
            {
                oldPlacer.enabled = false;
                EditorUtility.SetDirty(oldPlacer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[GameSetup] Done. GameController added to '{scene.name}'. Arena: {arena.name}, showcase weapons: {weapons.Count}, " +
                      $"old ARPlaceArena: {(oldPlacer != null ? "disabled" : "not found")}.");
        }
    }
}
#endif
