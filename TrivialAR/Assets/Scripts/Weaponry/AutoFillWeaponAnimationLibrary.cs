using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Weaponary
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponAnimationLibrary))]
    public sealed class AutoFillWeaponAnimationLibrary : MonoBehaviour
    {
        [Tooltip("Optional explicit clips source. Leave empty to scan all loaded clips.")]
        public AnimationClip[] sourceClips;

        [Tooltip("Run on Awake in play mode.")]
        public bool runAtAwake = false;

        WeaponAnimationLibrary _lib;

        static readonly Regex RxIdle = new(@"^Idle$", RegexOptions.IgnoreCase);
        static readonly Regex RxWalk = new(@"^Walking(_.*)?$", RegexOptions.IgnoreCase);
        static readonly Regex RxHit = new(@"^Hit(_.*)?$", RegexOptions.IgnoreCase);
        static readonly Regex RxDeath = new(@"^Death(_.*)?$", RegexOptions.IgnoreCase);
        static readonly Regex RxBlock = new(@"^Block(ing|Hit)?$", RegexOptions.IgnoreCase);

        static readonly Regex Rx1H = new(@"^1H_.*Melee_Attack_(?<type>Chop|Slice(_.*)?|Stab|Punch|Kick)", RegexOptions.IgnoreCase);
        static readonly Regex Rx2H = new(@"^2H_.*Melee_Attack_(?<type>Chop|Slice(_.*)?|Stab|Spinning?)", RegexOptions.IgnoreCase);
        static readonly Regex RxDual = new(@"^DualWield_.*Melee_Attack_(?<type>Chop|Slice(_.*)?|Stab|Spinning?)", RegexOptions.IgnoreCase);

        void Awake()
        {
            if (runAtAwake) AutoFill();
        }

        [ContextMenu("Auto-Fill From Names")]
        public void AutoFill()
        {
            _lib = GetComponent<WeaponAnimationLibrary>();
            if (_lib == null) return;

            var clips = GatherClips();
            if (clips.Count == 0) return;

            var idle = Pick(clips, RxIdle);
            var walk = Pick(clips, RxWalk);
            var hit = Pick(clips, RxHit);
            var death = Pick(clips, RxDeath);
            var block = Pick(clips, RxBlock);

            // OneHand
            var one = _lib.Ensure(WeaponClass.OneHand);
            one.idle = idle ?? one.idle;
            one.walk = walk ?? one.walk;
            one.hit = hit ?? one.hit;
            one.die = death ?? one.die;
            one.block = block ?? one.block;
            one.attackLight = PickAttack(clips, Rx1H, prefer: new[] { "Slice_Horizontal", "Chop", "Stab" });
            one.attackHeavy = PickAttack(clips, Rx1H, prefer: new[] { "Chop", "Slice_Diagonal", "Stab" });

            // TwoHand
            var two = _lib.Ensure(WeaponClass.TwoHand);
            two.idle = idle ?? two.idle;
            two.walk = walk ?? two.walk;
            two.hit = hit ?? two.hit;
            two.die = death ?? two.die;
            two.block = block ?? two.block;
            two.attackLight = PickAttack(clips, Rx2H, prefer: new[] { "Slice", "Chop", "Stab" });
            two.attackHeavy = PickAttack(clips, Rx2H, prefer: new[] { "Spinning", "Chop", "Slice" });

            // OneHand + Shield (reuse 1H attacks + ensure block)
            var oneShield = _lib.Ensure(WeaponClass.OneHandAndShield);
            oneShield.idle = idle ?? oneShield.idle;
            oneShield.walk = walk ?? oneShield.walk;
            oneShield.hit = hit ?? oneShield.hit;
            oneShield.die = death ?? oneShield.die;
            oneShield.block = block ?? oneShield.block;
            oneShield.attackLight = one.attackLight;
            oneShield.attackHeavy = one.attackHeavy ? one.attackHeavy : one.attackLight;

            // Dual OneHand
            var dual = _lib.Ensure(WeaponClass.DualOneHand);
            dual.idle = idle ?? dual.idle;
            dual.walk = walk ?? dual.walk;
            dual.hit = hit ?? dual.hit;
            dual.die = death ?? dual.die;
            dual.block = block ?? dual.block;
            dual.attackLight = PickAttack(clips, RxDual, prefer: new[] { "Slice", "Chop", "Stab" });
            dual.attackHeavy = PickAttack(clips, RxDual, prefer: new[] { "Spinning", "Chop", "Slice" });

#if UNITY_EDITOR
            EditorUtility.SetDirty(_lib);
#endif
        }

        List<AnimationClip> GatherClips()
        {
            var list = new List<AnimationClip>();
            if (sourceClips != null && sourceClips.Length > 0)
            {
                list.AddRange(sourceClips.Where(c => c));
                return list;
            }

#if UNITY_EDITOR
            // Editor-friendly: fetch all clips from the asset this component sits under.
            var path = UnityEditor.AssetDatabase.GetAssetPath(gameObject);
            if (!string.IsNullOrEmpty(path))
            {
                var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var a in all) if (a is AnimationClip c && !c.name.Contains("__preview__")) list.Add(c);
            }
#endif
            // Fallback: all loaded clips (broad)
            if (list.Count == 0)
                list.AddRange(Resources.FindObjectsOfTypeAll<AnimationClip>().Where(c => c && !c.name.Contains("__preview__")));
            return list;
        }

        static AnimationClip Pick(IEnumerable<AnimationClip> clips, Regex rx)
        {
            foreach (var c in clips) if (rx.IsMatch(c.name)) return c;
            return null;
        }

        static AnimationClip PickAttack(IEnumerable<AnimationClip> clips, Regex family, string[] prefer)
        {
            var candidates = clips.Where(c => family.IsMatch(c.name)).ToList();
            if (candidates.Count == 0) return null;

            foreach (var p in prefer)
            {
                var m = candidates.FirstOrDefault(c => c.name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (m) return m;
            }
            return candidates[0];
        }
    }
}
