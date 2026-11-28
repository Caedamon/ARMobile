// File: Assets/Scripts/Editor/BookHowToSetup.cs
#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Game;

// Tools > Kaiju > Book > Reset Book Texts (Arena Rules)
// A BookStage prefab remembers the texts and tab colours it was saved with, and those win over the defaults in the code.
// So after this update the prefab still has the OLD "Levels and fate" page and too few tab colours.
// This puts the current defaults back: the Arena Rules pages, the Stats pages, and the six tab colours.
// It replaces any text you edited by hand in the BookStage prefab. Safe to run again.
namespace KaijuTools
{
    public static class BookHowToSetup
    {
        [MenuItem("Tools/Kaiju/Book/Reset Book Texts (Arena Rules)")]
        public static void Run()
        {
            if (!EditorUtility.DisplayDialog("Reset book texts",
                    "This puts the default Arena Rules and Stats texts, and the six bookmark colours, into every BookStage prefab.\n\nAny text you changed by hand in those prefabs is replaced.",
                    "Reset", "Cancel"))
                return;

            var paths = AssetDatabase.FindAssets("BookStage t:Prefab").Select(AssetDatabase.GUIDToAssetPath).ToList();
            int done = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var stage = root.GetComponent<BookStage>();
                    if (stage == null) continue;

                    Set(stage, "statsTexts", BookTexts.DefaultStats());
                    Set(stage, "howToPlayTexts", BookTexts.DefaultHowToPlay());
                    Set(stage, "howToName", "Arena Rules");
                    Set(stage, "tabColors", new[]
                    {
                        new Color(0.55f, 0.16f, 0.16f, 1f), new Color(0.78f, 0.52f, 0.12f, 1f), new Color(0.16f, 0.36f, 0.62f, 1f),
                        new Color(0.20f, 0.50f, 0.26f, 1f), new Color(0.10f, 0.45f, 0.48f, 1f), new Color(0.36f, 0.14f, 0.46f, 1f)
                    });

                    EditorUtility.SetDirty(stage);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            string msg = done == 0
                ? "No BookStage prefab found. Run Tools > Kaiju > Game > Create Book Prefabs first."
                : "Book texts and tab colours reset on " + done + " BookStage prefab(s).";
            Debug.Log("[Book] " + msg);
            EditorUtility.DisplayDialog("Reset book texts", msg, "OK");
        }

        static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) { Debug.LogWarning("[Book] Field '" + field + "' not found on BookStage."); return; }
            f.SetValue(target, value);
        }
    }
}
#endif
