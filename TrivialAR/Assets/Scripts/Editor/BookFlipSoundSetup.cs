// File: Assets/Scripts/Editor/BookFlipSoundSetup.cs
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game;

// Tools > Kaiju > Book > Set Up Book Flip Sound
// Puts the "PagesFlipping" clip on the BookStage prefab (it is played when the book turns many pages at once: the tabs).
// Only fills the field if it is empty. Safe to run again.
namespace KaijuTools
{
    public static class BookFlipSoundSetup
    {
        [MenuItem("Tools/Kaiju/Book/Set Up Book Flip Sound")]
        public static void Run()
        {
            var clip = FindClip();
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Book flip sound", "Could not find an audio clip named 'PagesFlipping'.\nNothing was changed.", "OK");
                return;
            }

            var paths = AssetDatabase.FindAssets("BookStage t:Prefab").Select(AssetDatabase.GUIDToAssetPath).ToList();
            int set = 0, kept = 0, seen = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var stage = root.GetComponent<BookStage>();
                    if (stage == null) continue;
                    seen++;
                    var so = new SerializedObject(stage);
                    var prop = so.FindProperty("pagesFlippingSound");
                    if (prop == null) continue;
                    if (prop.objectReferenceValue == null)
                    {
                        prop.objectReferenceValue = clip;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        set++;
                    }
                    else kept++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            string msg = seen == 0
                ? "No BookStage prefab found. Run Tools > Kaiju > Game > Create Book Prefabs first."
                : "Flip sound set on " + set + " Book prefab(s) (" + kept + " already had one).";
            Debug.Log("[Book] " + msg + "  Clip: " + AssetDatabase.GetAssetPath(clip));
            EditorUtility.DisplayDialog("Book flip sound", msg, "OK");
        }

        static AudioClip FindClip()
        {
            var guids = AssetDatabase.FindAssets("PagesFlipping t:AudioClip");
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.Contains("EndlessBook") ? 0 : 1).ToList();
            foreach (var p in paths)
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (c != null && c.name == "PagesFlipping") return c;
            }
            return null;
        }
    }
}
#endif
