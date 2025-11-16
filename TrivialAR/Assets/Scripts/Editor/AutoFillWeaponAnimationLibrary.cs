#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Animation;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    // Fills WeaponAnimationLibrary with one Idle/Walk/Spawn and flat attack lists per weapon type.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponAnimationLibrary))]
    public sealed class AutoFillWeaponAnimationLibrary : MonoBehaviour
    {
        [Tooltip("Drag the clips you want considered (recommended). If empty, scans loaded clips.")]
        public List<AnimationClip> sourceClips = new();

        [Tooltip("Run on Awake in Play Mode.")]
        public bool runAtAwake = false;

        static readonly Regex RxIdle      = new(@"^Idle(_Combat)?$", RegexOptions.IgnoreCase);
        static readonly Regex RxWalk      = new(@"^Walking(_.*)?$",   RegexOptions.IgnoreCase);
        static readonly Regex RxSpawn     = new(@"^Spawn_Air$|^Spawn",RegexOptions.IgnoreCase);
        static readonly Regex RxBlock     = new(@"^Block$",           RegexOptions.IgnoreCase);
        static readonly Regex RxHitVar    = new(@"^Hit(_.*)?$",       RegexOptions.IgnoreCase);
        static readonly Regex RxDodgeVar  = new(@"^Dodge(_.*)?$",     RegexOptions.IgnoreCase);

        static readonly Regex RxUnarmed   = new(@"^Unarmed_.*Attack_.*",    RegexOptions.IgnoreCase);
        static readonly Regex Rx1H        = new(@"^1H_.*Attack_.*",         RegexOptions.IgnoreCase);
        static readonly Regex Rx2H        = new(@"^2H_.*Attack_.*",         RegexOptions.IgnoreCase);
        static readonly Regex RxDual      = new(@"^DualWield_.*Attack_.*",  RegexOptions.IgnoreCase);
        static readonly Regex RxShield    = new(@"^Shield_.*Attack_.*",     RegexOptions.IgnoreCase);

        void Awake()
        {
            if (runAtAwake) FillNow();
        }

        [ContextMenu("Fill Now")]
        public void FillNow()
        {
            var lib = GetComponent<WeaponAnimationLibrary>();
            if (!lib) { Debug.LogWarning("[AutoFill] No WeaponAnimationLibrary on this object.", this); return; }

            var clips = GatherClips();
            if (clips.Count == 0) { Debug.LogWarning("[AutoFill] No clips to process.", this); return; }

            // single set (you don’t want per-character sets)
            var set = lib.Ensure(lib.fallbackClass);

            // singles (seed only if empty)
            SeedOnce(ref set.idle,  PickOne(clips, RxIdle));
            SeedOnce(ref set.walk,  PickOne(clips, RxWalk));
            SeedOnce(ref set.spawn, PickOne(clips, RxSpawn));
            SeedOnce(ref set.block, PickOne(clips, RxBlock));

            // variants
            set.hitVariants   = PickMany(clips, RxHitVar);
            set.dodgeVariants = PickMany(clips, RxDodgeVar);

            // flat attack lists
            set.unarmedAttacks        = PickMany(clips, RxUnarmed);
            set.oneHandAttacks        = PickMany(clips, Rx1H);
            set.twoHandAttacks        = PickMany(clips, Rx2H);
            set.dualWieldAttacks      = PickMany(clips, RxDual);
            set.shieldOnlyAttacks     = PickMany(clips, RxShield);
            set.oneHandShieldAttacks  = set.oneHandShieldAttacks ?? new List<AnimationClip>(); // optional bespoke; else fallback at runtime

            // fallbacks (seed singles for legacy if empty)
            if (!set.attackLight) set.attackLight = FirstNonNull(set.oneHandAttacks, set.twoHandAttacks, set.dualWieldAttacks, set.unarmedAttacks);
            if (!set.attackHeavy) set.attackHeavy = LastNonNull (set.twoHandAttacks, set.oneHandAttacks, set.dualWieldAttacks, set.unarmedAttacks);
            if (!set.dualWield)   set.dualWield   = set.dualWieldAttacks.FirstOrDefault();

            // hits/dodge single fallbacks
            if (!set.hit)   set.hit   = set.hitVariants.FirstOrDefault();
            if (!set.dodge) set.dodge = set.dodgeVariants.FirstOrDefault();

            EditorUtility.SetDirty(lib);
            EditorUtility.SetDirty(this);
            Debug.Log("[AutoFill] Library populated from provided clips.", this);
        }

        List<AnimationClip> GatherClips()
        {
            var list = new List<AnimationClip>();
            if (sourceClips != null && sourceClips.Count > 0)
            {
                for (int i = 0; i < sourceClips.Count; i++) if (sourceClips[i]) list.Add(sourceClips[i]);
                return list;
            }

            // broad fallback
            var all = Resources.FindObjectsOfTypeAll<AnimationClip>();
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c && !c.name.Contains("__preview__")) list.Add(c);
            }
            return list;
        }

        static void SeedOnce(ref AnimationClip field, AnimationClip value)
        {
            if (!field && value) field = value;
        }

        static AnimationClip PickOne(List<AnimationClip> clips, Regex rx)
        {
            for (int i = 0; i < clips.Count; i++) if (rx.IsMatch(clips[i].name)) return clips[i];
            return null;
        }

        static List<AnimationClip> PickMany(List<AnimationClip> clips, Regex rx)
        {
            var set = new HashSet<AnimationClip>();
            var list = new List<AnimationClip>();
            for (int i = 0; i < clips.Count; i++)
            {
                var c = clips[i];
                if (rx.IsMatch(c.name) && set.Add(c)) list.Add(c);
            }
            return list;
        }

        static AnimationClip FirstNonNull(params List<AnimationClip>[] lists)
        {
            for (int i = 0; i < lists.Length; i++) { var l = lists[i]; if (l != null && l.Count > 0) return l[0]; }
            return null;
        }

        static AnimationClip LastNonNull(params List<AnimationClip>[] lists)
        {
            for (int i = 0; i < lists.Length; i++) { var l = lists[i]; if (l != null && l.Count > 0) return l[l.Count - 1]; }
            return null;
        }
    }
}
#endif