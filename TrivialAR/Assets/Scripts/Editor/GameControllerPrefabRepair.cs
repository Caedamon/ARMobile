#if UNITY_EDITOR
using AR;
using Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Put in Assets/Scripts/Editor/
// Menu (with your game scene open): Tools > Kaiju > Game > Repair GameController Prefab
//
// The GameController builds its menus at runtime. If the object is dragged into the Project WHILE PLAYING,
// Unity saves those runtime-built menus into the prefab, and every start then shows a dead duplicate menu.
// This removes everything but the GameController and ARStagePlacer from the prefab and clears overrides on the scene instance.
namespace KaijuTools
{
    public static class GameControllerPrefabRepair
    {
        const string PrefabPath = "Assets/Prefabs/AR/GameController.prefab";
        const string MaterialPath = "Assets/Data/StageMaterial.mat";

        [MenuItem("Tools/Kaiju/Game/Repair GameController Prefab")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError("[PrefabRepair] Prefab not found at " + PrefabPath);
                return;
            }

            int children = 0, components = 0;
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                    children++;
                }
                foreach (var ui in root.GetComponents<GameUI>()) { Object.DestroyImmediate(ui); components++; }
                foreach (var ov in root.GetComponents<DeviceDebugOverlay>()) { Object.DestroyImmediate(ov); components++; }

                var gc = root.GetComponent<GameController>();
                if (gc == null) gc = root.AddComponent<GameController>();
                if (root.GetComponent<ARStagePlacer>() == null) root.AddComponent<ARStagePlacer>();

                var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (mat != null)
                {
                    var so = new SerializedObject(gc);
                    so.FindProperty("stageMaterial").objectReferenceValue = mat;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Scene: clear overrides on prefab instances, and report how many GameControllers there are.
            int reverted = 0;
            var all = Object.FindObjectsByType<GameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var gc in all)
            {
                var go = gc.gameObject;
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    PrefabUtility.RevertPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(go), InteractionMode.AutomatedAction);
                    reverted++;
                }
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();

            Debug.Log("[PrefabRepair] Done. Removed " + children + " baked child objects and " + components + " baked components from the prefab. " +
                      "Scene instances cleaned: " + reverted + ". GameControllers in the open scene: " + all.Length +
                      (all.Length == 1 ? " (good)." : " (there should be exactly 1!)."));
        }
    }
}
#endif
