#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Combat;
using Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Put in Assets/Scripts/Editor/   (replaces the Step 1 version)
// Menu: Tools > Kaiju > Duel > Set Up Duel
//
// 1. Finds the clips in your animation packs BY NAME (any Rig_Medium file: Simulation, General, ...):
//      Sit_Floor_Down  -> the Animation Pack's "Yield Clip"  (the loser of a duel sits down)
//      Cheering        -> the Animation Pack's "Cheer Clip"  (the winner celebrates)
//    and only fills them if they are empty, so you can swap in other clips afterwards.
// 2. Adds a "Cheer" state (with a "Cheer" trigger) to your BaseMelee animator controller. NOTE: this edits the controller, commit first.
// 3. Adds the Stats button + Stats screen to the hub of your GameUI prefab and turns the "Duel" button on.
// Safe to run again.
namespace KaijuTools
{
    public static class DuelSetup
    {
        const string ControllerPath = "Assets/Animations/Controllers/Animator/BaseMelee.controller";

        [MenuItem("Tools/Kaiju/Duel/Set Up Duel")]
        public static void Run()
        {
            var report = new List<string>();
            SetUpAnimation(report);
            SetUpUi(report);
            AssetDatabase.SaveAssets();
            Debug.Log("[Duel] Done:\n - " + string.Join("\n - ", report.ToArray()));
        }

        // Looks in every Rig_Medium animation file; the Simulation file first (that is where Sit_Floor_Down and Cheering live).
        static AnimationClip FindClip(string clipName, List<string> report)
        {
            var paths = AssetDatabase.FindAssets("Rig_Medium t:Model").Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.Contains("Simulation") ? 0 : 1).ToList();
            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name == clipName && !c.name.StartsWith("__preview__"));
                if (clip != null) { report.Add("found '" + clipName + "' in " + path); return clip; }
            }
            report.Add("COULD NOT FIND a clip named '" + clipName + "' in any Rig_Medium file (searched " + paths.Count + " files)");
            return null;
        }

        static void SetUpAnimation(List<string> report)
        {
            var packs = AssetDatabase.FindAssets("t:AnimationPack");
            if (packs.Length == 0) { report.Add("no Animation Pack asset found (run Tools > Kaiju > Combat > Setup Animation Pack first)"); return; }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                var g = AssetDatabase.FindAssets("BaseMelee t:AnimatorController");
                if (g.Length > 0) ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(g[0]));
            }

            AnimationClip yield = null, cheer = null;
            foreach (var guid in packs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var pack = AssetDatabase.LoadAssetAtPath<AnimationPack>(path);
                if (pack == null) continue;

                if (pack.yieldClip == null)
                {
                    if (yield == null) yield = FindClip("Sit_Floor_Down", report);
                    pack.yieldClip = yield;
                    if (yield != null) report.Add(path + ": Yield Clip set to " + yield.name + " (the loser of a duel sits down)");
                }
                else report.Add(path + ": Yield Clip already set (" + pack.yieldClip.name + ")");

                if (pack.cheerClip == null)
                {
                    if (cheer == null) cheer = FindClip("Cheering", report);
                    pack.cheerClip = cheer;
                    if (cheer != null) report.Add(path + ": Cheer Clip set to " + cheer.name);
                }
                else report.Add(path + ": Cheer Clip already set (" + pack.cheerClip.name + ")");

                if (pack.cheerClip != null)
                {
                    if (ctrl == null) report.Add("controller 'BaseMelee' not found: the Cheer state was NOT added (winners won't cheer)");
                    else
                    {
                        pack.controllerHasCheer = EnsureTriggerState(ctrl, "Cheer", "Cheer", pack.cheerClip);
                        EditorUtility.SetDirty(ctrl);
                        report.Add("controller '" + ctrl.name + "': 'Cheer' state ready");
                    }
                }
                EditorUtility.SetDirty(pack);
            }
        }

        // Same pattern as Setup Animation Pack uses for Pickup / Spawn / Blocking.
        static bool EnsureTriggerState(AnimatorController ctrl, string stateName, string triggerName, AnimationClip clip)
        {
            var sm = ctrl.layers[0].stateMachine;
            if (!ctrl.parameters.Any(p => p.name == triggerName))
                ctrl.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);

            AnimatorState state = null, idle = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == stateName) state = cs.state;
                if (cs.state.name == "Idle") idle = cs.state;
            }

            if (state == null)
            {
                state = sm.AddState(stateName);

                var any = sm.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
                any.hasExitTime = false;
                any.duration = 0.1f;
                any.canTransitionToSelf = false;

                if (idle != null)
                {
                    var back = state.AddTransition(idle);
                    back.hasExitTime = true;
                    back.exitTime = 0.95f;
                    back.duration = 0.2f;
                }
            }
            state.motion = clip;
            return true;
        }

        static void SetUpUi(List<string> report)
        {
            string path = "Assets/Prefabs/Game/GameUI.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                path = null;
                foreach (var guid in AssetDatabase.FindAssets("GameUI t:Prefab"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.EndsWith("/GameUI.prefab")) { path = p; break; }
                }
            }
            if (path == null) { report.Add("GameUI prefab not found: run Tools > Kaiju > Game > Create Game Prefabs first, then this again"); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui = root.GetComponent<GameUI>();
                if (ui == null) { report.Add("no GameUI component on " + path); return; }
                if (ui.HasDuelStatsUi()) { report.Add("Duel button + Stats screen already in " + path); return; }
                ui.AddDuelAndStatsUi();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Add("Duel button turned on and Stats button + screen added to " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
