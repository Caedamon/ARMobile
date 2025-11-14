// Editor/WeaponaryBaseAnimatorFactory.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Weaponary;

namespace editor
{
    public static class WeaponaryBaseAnimatorFactory
    {
        const string Idle = "Base/Idle";
        const string Walk = "Base/Walk";
        const string AtkL = "Base/Attack_Light";
        const string AtkH = "Base/Attack_Heavy";
        const string Block = "Base/Block";
        const string Hit = "Base/Hit";
        const string Die = "Base/Die";

        [MenuItem("Assets/Create/Weaponary/Create Base Melee Animator", priority = 0)]
        public static void CreateBaseAnimator()
        {
            var folder = GetSelectedFolderPath();
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath($"{folder}/BaseMelee.controller");

            // Params
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Heavy", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Block", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            // Placeholder clips (empty, overridden at runtime)
            var idle = CreateClip(folder, Idle);
            var walk = CreateClip(folder, Walk);
            var aL = CreateClip(folder, AtkL);
            var aH = CreateClip(folder, AtkH);
            var blk = CreateClip(folder, Block);
            var hit = CreateClip(folder, Hit);
            var die = CreateClip(folder, Die);

            // Layers & states
            var layer = ctrl.layers[0];
            var sm = layer.stateMachine;
            sm.entryPosition = new Vector3(50, 100);
            sm.anyStatePosition = new Vector3(50, 250);

            var sIdle = sm.AddState("Idle", new Vector3(300, 100));
            sIdle.motion = idle;
            var sWalk = sm.AddState("Walk", new Vector3(550, 100));
            sWalk.motion = walk;
            var sAtkL = sm.AddState("Attack_Light", new Vector3(300, 260));
            sAtkL.motion = aL;
            sAtkL.speed = 1f;
            sAtkL.speedParameterActive = false;
            var sAtkH = sm.AddState("Attack_Heavy", new Vector3(550, 260));
            sAtkH.motion = aH;
            var sBlk = sm.AddState("Block", new Vector3(800, 260));
            sBlk.motion = blk;
            sBlk.writeDefaultValues = true;
            var sHit = sm.AddState("Hit", new Vector3(300, 420));
            sHit.motion = hit;
            var sDie = sm.AddState("Die", new Vector3(550, 420));
            sDie.motion = die;
            sDie.speed = 1f;
            sDie.iKOnFeet = false;

            sm.defaultState = sIdle;

            // Locomotion transitions
            var t1 = sIdle.AddTransition(sWalk);
            t1.hasExitTime = false;
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var t2 = sWalk.AddTransition(sIdle);
            t2.hasExitTime = false;
            t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // AnyState triggers (one-shots)
            AddAnyTo(sm, sAtkL, ctrl, triggerName: "Attack", extraCondBoolName: "Heavy", extraCondBoolFalse: true);
            AddAnyTo(sm, sAtkH, ctrl, triggerName: "Attack", extraCondBoolName: "Heavy", extraCondBoolTrue: true);
            AddAnyTo(sm, sHit, ctrl, triggerName: "Hit");
            AddAnyTo(sm, sDie, ctrl, triggerName: "Die");

            // AnyState block (bool)
            var anyBlk = sm.AddAnyStateTransition(sBlk);
            anyBlk.hasExitTime = false;
            anyBlk.AddCondition(AnimatorConditionMode.If, 0, "Block");
            // Exit block when false
            var blkExitToIdle = sBlk.AddTransition(sIdle);
            blkExitToIdle.hasExitTime = false;
            blkExitToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Block");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Selection.activeObject = ctrl;
            Debug.Log($"Created base melee Animator at: {AssetDatabase.GetAssetPath(ctrl)}");
        }

        [MenuItem("GameObject/Weaponary/Assign Base Controller & Components", priority = 0)]
        public static void AssignToSelected()
        {
            var obj = Selection.activeGameObject;
            if (!obj)
            {
                Debug.LogWarning("Select a character GameObject.");
                return;
            }

            var anim = obj.GetComponent<Animator>() ?? obj.AddComponent<Animator>();
            anim.applyRootMotion = false;

            var ctrl = Selection.activeObject as AnimatorController;
            if (!ctrl)
            {
                // Try find in project
                var guids = AssetDatabase.FindAssets("t:AnimatorController BaseMelee");
                if (guids.Length > 0)
                    ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (!ctrl)
            {
                Debug.LogWarning(
                    "Create the base controller first (Assets/Create/Weaponary/Create Base Melee Animator).");
                return;
            }

            anim.runtimeAnimatorController = ctrl;

            // Remove legacy Animation if present
            var legacy = obj.GetComponent<Animation>();
            if (legacy) Object.DestroyImmediate(legacy);

            var lib = obj.GetComponent<WeaponAnimationLibrary>() ?? obj.AddComponent<WeaponAnimationLibrary>();
            var binder = obj.GetComponent<WeaponAnimatorBinder>() ?? obj.AddComponent<WeaponAnimatorBinder>();
            binder.baseController = ctrl;
            var resolver = obj.GetComponent<WeaponEquipResolver>() ?? obj.AddComponent<WeaponEquipResolver>();

            Debug.Log("Assigned base controller and required components to character.");
        }

        static void AddAnyTo(AnimatorStateMachine sm, AnimatorState target, AnimatorController ctrl,
            string triggerName, string extraCondBoolName = null,
            bool extraCondBoolTrue = false, bool extraCondBoolFalse = false)
        {
            var any = sm.AddAnyStateTransition(target);
            any.hasExitTime = false;
            any.AddCondition(AnimatorConditionMode.If, 0, triggerName);
            if (!string.IsNullOrEmpty(extraCondBoolName))
            {
                if (extraCondBoolTrue) any.AddCondition(AnimatorConditionMode.If, 0, extraCondBoolName);
                if (extraCondBoolFalse) any.AddCondition(AnimatorConditionMode.IfNot, 0, extraCondBoolName);
            }

            any.duration = 0.05f;
        }

        static AnimationClip CreateClip(string folder, string name)
        {
            var clip = new AnimationClip { name = name, legacy = false };
            AssetDatabase.CreateAsset(clip, $"{folder}/{Sanitize(name)}.anim");
            return clip;
        }

        static string GetSelectedFolderPath()
        {
            var obj = Selection.activeObject;
            var path = (obj != null) ? AssetDatabase.GetAssetPath(obj) : "Assets";
            if (string.IsNullOrEmpty(path)) path = "Assets";
            if (!System.IO.Directory.Exists(path)) path = System.IO.Path.GetDirectoryName(path);
            return path;
        }

        static string Sanitize(string n) => n.Replace("/", "_");
    }
}
#endif
