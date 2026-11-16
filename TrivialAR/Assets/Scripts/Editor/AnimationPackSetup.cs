#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Put this file in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Combat > Setup Animation Pack
// 1) Creates Assets/Data/AnimationPack.asset and fills it from the KayKit packs.
// 2) Adds two states to BaseMelee.controller: "Pickup" and "Spawn" (each with a trigger of the same name).
// 3) Assigns the pack to the arena's ArenaFighterSpawner.
// Safe to run again. NOTE: it edits BaseMelee.controller - commit first.
namespace KaijuTools
{
    public static class AnimationPackSetup
    {
        const string PackPath = "Assets/Data/AnimationPack.asset";
        const string ControllerPath = "Assets/Animations/Controllers/Animator/BaseMelee.controller";
        const string Dir = "Assets/Animations/Animations/Rig_Medium/";
        const string ArenaPrefab = "Assets/Prefabs/AR/KaijuArenaAR.prefab";

        [MenuItem("Tools/Kaiju/Combat/Setup Animation Pack")]
        public static void Run()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) { Debug.LogError("[AnimPack] Controller not found at " + ControllerPath); return; }

            var general = Clips(Dir + "Rig_Medium_General.fbx");
            var moveAdv = Clips(Dir + "Rig_Medium_MovementAdvanced.fbx");
            var melee   = Clips(Dir + "Rig_Medium_CombatMelee.fbx");

            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            var pack = AssetDatabase.LoadAssetAtPath<AnimationPack>(PackPath);
            if (pack == null)
            {
                pack = ScriptableObject.CreateInstance<AnimationPack>();
                AssetDatabase.CreateAsset(pack, PackPath);
            }

            var sm = ctrl.layers[0].stateMachine;
            pack.idleOriginal  = StateClip(sm, "Idle");
            pack.dodgeOriginal = StateClip(sm, "Dodge");
            pack.hitOriginal   = StateClip(sm, "Hit");
            pack.deathOriginal = StateClip(sm, "Die");

            pack.idleUnarmed = Find(melee, "Melee_Unarmed_Idle");
            pack.idleTwoHand = Find(melee, "Melee_2H_Idle");
            pack.hitClips    = new[] { Find(general, "Hit_A"), Find(general, "Hit_B") }.Where(c => c != null).ToList();
            pack.deathClips  = new[] { Find(general, "Death_A"), Find(general, "Death_B") }.Where(c => c != null).ToList();
            pack.dodgeBack   = Find(moveAdv, "Dodge_Backward");
            pack.dodgeLeft   = Find(moveAdv, "Dodge_Left");
            pack.dodgeRight  = Find(moveAdv, "Dodge_Right");
            pack.pickupClip  = Find(general, "PickUp");
            pack.spawnClip   = Find(general, "Spawn_Ground");
            pack.blockClip    = Find(melee, "Melee_Block");
            pack.blockHitClip = Find(melee, "Melee_Block_Hit");

            pack.controllerHasPickup = pack.pickupClip != null && EnsureTriggerState(ctrl, sm, "Pickup", "Pickup", pack.pickupClip);
            pack.controllerHasSpawn  = pack.spawnClip != null  && EnsureTriggerState(ctrl, sm, "Spawn", "Spawn", pack.spawnClip);
            // The controller already has a "Block" parameter, so the new trigger is called "BlockStart".
            pack.controllerHasBlock  = pack.blockClip != null  && EnsureTriggerState(ctrl, sm, "Blocking", "BlockStart", pack.blockClip);

            EditorUtility.SetDirty(pack);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            bool arena = AssignToArena(pack);
            Debug.Log("[AnimPack] Done. idle originals found: " + (pack.idleOriginal != null) +
                      ", stances: " + (pack.idleUnarmed != null) + "/" + (pack.idleTwoHand != null) +
                      ", hit/death variants: " + pack.hitClips.Count + "/" + pack.deathClips.Count +
                      ", dodges: " + (pack.dodgeBack != null) + "/" + (pack.dodgeLeft != null) + "/" + (pack.dodgeRight != null) +
                      ", pickup state: " + pack.controllerHasPickup + ", spawn state: " + pack.controllerHasSpawn + ", block state: " + pack.controllerHasBlock +
                      ", arena: " + (arena ? "OK" : "FAILED"));
        }

        static List<AnimationClip> Clips(string fbx)
        {
            var list = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToList();
            if (list.Count == 0) Debug.LogWarning("[AnimPack] No clips found in " + fbx);
            return list;
        }

        static AnimationClip Find(List<AnimationClip> clips, string name)
        {
            var c = clips.FirstOrDefault(x => x.name == name);
            if (c == null) Debug.LogWarning("[AnimPack] Clip not found: " + name);
            return c;
        }

        static AnimationClip StateClip(AnimatorStateMachine sm, string stateName)
        {
            foreach (var cs in sm.states)
                if (cs.state.name == stateName) return cs.state.motion as AnimationClip;
            return null;
        }

        static bool EnsureTriggerState(AnimatorController ctrl, AnimatorStateMachine sm, string stateName, string triggerName, AnimationClip clip)
        {
            if (!ctrl.parameters.Any(p => p.name == triggerName))
                ctrl.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);

            AnimatorState state = null;
            AnimatorState idle = null;
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
                    back.exitTime = 0.9f;
                    back.duration = 0.15f;
                }
            }
            state.motion = clip;
            return true;
        }

        static bool AssignToArena(AnimationPack pack)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefab) == null) return false;
            var root = PrefabUtility.LoadPrefabContents(ArenaPrefab);
            try
            {
                var spawner = root.GetComponent<Fighters.ArenaFighterSpawner>();
                if (spawner == null) return false;
                var so = new SerializedObject(spawner);
                var prop = so.FindProperty("animationPack");
                if (prop == null) return false;
                prop.objectReferenceValue = pack;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefab);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
