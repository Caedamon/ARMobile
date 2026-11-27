// File: Assets/Scripts/Editor/ArrivalSetup.cs
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Combat;

// Tools > Kaiju > Arrival > Set Up Arrival
// Finds the "Spawn_Air" clip (the falling and landing arrival) in your Rig_Medium animation files and puts it on every Animation Pack
// as its "Spawn Air Clip". Fighters then arrive by dropping out of a small portal instead of the "crawl out of the ground" clip.
// It only fills the field if it is empty, and it does not touch the Animator controller. Safe to run again.
namespace KaijuTools
{
    public static class ArrivalSetup
    {
        [MenuItem("Tools/Kaiju/Arrival/Set Up Arrival")]
        public static void Run()
        {
            var clip = FindClip("Spawn_Air");
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Arrival", "Could not find a clip named 'Spawn_Air' in any Rig_Medium animation file.\nNothing was changed.", "OK");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:AnimationPack");
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Arrival", "No Animation Pack asset found. Run Tools > Kaiju > Combat > Setup Animation Pack first.", "OK");
                return;
            }

            int set = 0, kept = 0;
            var report = new System.Text.StringBuilder();
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var pack = AssetDatabase.LoadAssetAtPath<AnimationPack>(path);
                if (pack == null) continue;
                if (pack.spawnAirClip == null)
                {
                    pack.spawnAirClip = clip;
                    EditorUtility.SetDirty(pack);
                    set++;
                    report.AppendLine("  " + path + ": Spawn Air Clip set");
                }
                else
                {
                    kept++;
                    report.AppendLine("  " + path + ": already has a Spawn Air Clip (" + pack.spawnAirClip.name + "), kept");
                }
                if (!pack.controllerHasSpawn)
                    report.AppendLine("  WARNING " + path + ": the controller has no 'Spawn' state yet (run Tools > Kaiju > Combat > Setup Animation Pack)");
            }
            AssetDatabase.SaveAssets();

            Debug.Log("[Arrival] Done. Spawn Air Clip set on " + set + " pack(s), kept on " + kept + ".\n" + report);
            EditorUtility.DisplayDialog("Arrival", "Spawn Air Clip set on " + set + " Animation Pack(s) (" + kept + " already had one).\nPress Play: fighters now drop out of a portal.", "OK");
        }

        static AnimationClip FindClip(string clipName)
        {
            var paths = AssetDatabase.FindAssets("Rig_Medium t:Model").Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.Contains("General") ? 0 : 1).ToList();
            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name == clipName && !c.name.StartsWith("__preview__"));
                if (clip != null) return clip;
            }
            return null;
        }
    }
}
#endif
