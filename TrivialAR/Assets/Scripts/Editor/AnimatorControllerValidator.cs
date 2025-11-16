#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace editor
{
    public static class AnimatorControllerValidator
    {
        // Menu: select a controller asset, then run this
        [MenuItem("Tools/Animator/Validate Base Melee Controller")]
        public static void ValidateSelected()
        {
            var ctrl = Selection.activeObject as AnimatorController;
            if (!ctrl)
            {
                Debug.LogWarning("Select an AnimatorController asset.");
                return;
            }

            Validate(ctrl);
        }

        // Menu: checks all controllers in project (optional)
        [MenuItem("Tools/Animator/Validate All Controllers")]
        public static void ValidateAll()
        {
            var guids = AssetDatabase.FindAssets("t:AnimatorController");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ctrl) Validate(ctrl);
            }
        }

        static void Validate(AnimatorController ctrl)
        {
            var mustParams = new (string, AnimatorControllerParameterType)[]
            {
                ("Speed", AnimatorControllerParameterType.Float),
                ("Attack", AnimatorControllerParameterType.Trigger),
                ("Heavy", AnimatorControllerParameterType.Bool),
                ("Hit", AnimatorControllerParameterType.Trigger),
                ("Die", AnimatorControllerParameterType.Trigger),
                ("Block", AnimatorControllerParameterType.Bool), // optional but recommended
                ("Dodge", AnimatorControllerParameterType.Trigger) // optional but recommended
            };

            var mustStates = new[]
            {
                "Base/Idle", "Base/Walk", "Base/Attack_Light", "Base/Attack_Heavy",
                "Base/Block", "Base/Dodge", "Base/Hit", "Base/Die", "Base/Spawn"
            };

            var layer = ctrl.layers.FirstOrDefault();
            var sm = layer.stateMachine;

            // params
            foreach (var (name, type) in mustParams)
            {
                var p = ctrl.parameters.FirstOrDefault(x => x.name == name);
                if (p == null || p.type != type)
                    Debug.LogWarning($"[AnimatorValidator] Missing/typed wrong param '{name}' ({type}) in {ctrl.name}");
            }

            // states
            foreach (var s in mustStates)
            {
                if (!FindState(sm, s))
                    Debug.LogWarning($"[AnimatorValidator] Missing state '{s}' in {ctrl.name}");
            }

            // AnyState transitions
            bool HasAnyTo(string stateName) =>
                sm.anyStateTransitions.Any(t => t.destinationState != null && t.destinationState.name == stateName);

            if (!HasAnyTo("Attack_Light"))
                Debug.LogWarning($"[AnimatorValidator] Missing AnyState→Attack_Light in {ctrl.name}");
            if (!HasAnyTo("Attack_Heavy"))
                Debug.LogWarning($"[AnimatorValidator] Missing AnyState→Attack_Heavy in {ctrl.name}");
            if (!HasAnyTo("Hit")) Debug.LogWarning($"[AnimatorValidator] Missing AnyState→Hit in {ctrl.name}");
            if (!HasAnyTo("Die")) Debug.LogWarning($"[AnimatorValidator] Missing AnyState→Die in {ctrl.name}");
            if (!HasAnyTo("Dodge")) Debug.LogWarning($"[AnimatorValidator] Missing AnyState→Dodge in {ctrl.name}");

            Debug.Log($"[AnimatorValidator] Checked '{ctrl.name}'");
        }

        static bool FindState(AnimatorStateMachine sm, string path)
        {
            var parts = path.Split('/');
            var cur = sm;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (i == parts.Length - 1)
                    return cur.states.Any(s => s.state.name == part);
                var sub = cur.stateMachines.FirstOrDefault(s => s.stateMachine.name == part).stateMachine;
                if (!sub) return false;
                cur = sub;
            }

            return false;
        }
    }
}
#endif
