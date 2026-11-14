#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

// Put this file in Assets/Scripts/Editor/
// Run from the menu: Tools > Kaiju > Fix Animators On Character Prefabs
namespace KaijuTools
{
    public static class FixDuplicateAnimators
    {
        const string ControllerPath = "Assets/Animations/Controllers/Animator/BaseMelee.controller";
        static readonly string[] Folders = { "Assets/Prefabs/Player", "Assets/Prefabs/Enemy" };

        [MenuItem("Tools/Kaiju/Fix Animators On Character Prefabs")]
        public static void Run()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                Debug.LogError($"[FixAnimators] Controller not found at {ControllerPath}");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", Folders);
            int touched = 0, removedDupes = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var anims = root.GetComponents<Animator>();
                    if (anims.Length == 0) continue;

                    // Keep the Animator that comes from the FBX (it has a counterpart in the
                    // source prefab). The hand-added one has no counterpart.
                    Animator keep = anims.FirstOrDefault(a => PrefabUtility.GetCorrespondingObjectFromSource(a) != null)
                                    ?? anims[0];

                    foreach (var extra in anims)
                    {
                        if (extra == keep) continue;
                        if (keep.avatar == null && extra.avatar != null) keep.avatar = extra.avatar;
                        Object.DestroyImmediate(extra, true);
                        removedDupes++;
                    }

                    keep.runtimeAnimatorController = ctrl;
                    keep.applyRootMotion = false;

                    // Repair WeaponAnimatorBinder.baseController (namespace "Animation" clashes
                    // with UnityEngine.Animation, so use global::)
                    var binder = root.GetComponent<global::Animation.WeaponAnimatorBinder>();
                    if (binder != null)
                    {
                        var so = new SerializedObject(binder);
                        var prop = so.FindProperty("baseController");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = ctrl;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    touched++;
                    Debug.Log($"[FixAnimators] Fixed {path} (Animators found: {anims.Length}, avatar: {(keep.avatar ? keep.avatar.name : "NONE")})");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FixAnimators] Done. Prefabs updated: {touched}, duplicate Animators removed: {removedDupes}");
        }
    }
}
#endif