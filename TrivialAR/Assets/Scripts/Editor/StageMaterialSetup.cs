#if UNITY_EDITOR
using Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Put in Assets/Scripts/Editor/
// Menu (with your game scene open): Tools > Kaiju > Game > Create Stage Material
// Creates Assets/Data/StageMaterial.mat (URP Lit) and assigns it to the GameController, then saves the scene.
namespace KaijuTools
{
    public static class StageMaterialSetup
    {
        const string Path = "Assets/Data/StageMaterial.mat";

        [MenuItem("Tools/Kaiju/Game/Create Stage Material")]
        public static void Run()
        {
            var gc = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
            if (gc == null) { Debug.LogError("[StageMaterial] No GameController in the open scene. Open your game scene first."); return; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(Path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) { Debug.LogError("[StageMaterial] URP Lit shader not found."); return; }
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                mat = new Material(shader) { name = "StageMaterial" };
                mat.SetFloat("_Smoothness", 0.15f);
                mat.SetFloat("_Metallic", 0f);
                AssetDatabase.CreateAsset(mat, Path);
            }

            // If the controller is a prefab instance, set the value on the prefab itself so every instance gets it.
            var target = gc;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(gc);
            if (source != null) target = source;

            var so = new SerializedObject(target);
            so.FindProperty("stageMaterial").objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[StageMaterial] Done. Material assigned to GameController and scene saved.");
        }
    }
}
#endif
