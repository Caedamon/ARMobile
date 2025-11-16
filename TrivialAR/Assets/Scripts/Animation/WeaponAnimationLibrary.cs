using System;
using System.Collections.Generic;
using UnityEngine;
using Weaponary;

namespace Animation
{
    [DisallowMultipleComponent]
    public sealed class WeaponAnimationLibrary : MonoBehaviour
    {
        [Serializable]
        public sealed class AnimationSet
        {
            public WeaponClass weaponClass;

            public AnimationClip idle;
            public AnimationClip walk;
            public AnimationClip spawn;
            public AnimationClip block;
            public AnimationClip hit;
            public AnimationClip dodge;

            // seed singles (kept for compatibility; optional now)
            public AnimationClip attackLight;
            public AnimationClip attackHeavy;
            public AnimationClip dualWield;

            // flat lists (use these)
            public List<AnimationClip> unarmedAttacks        = new(); // fists & kicks
            public List<AnimationClip> oneHandAttacks        = new(); // 1H_*
            public List<AnimationClip> twoHandAttacks        = new(); // 2H_*
            public List<AnimationClip> dualWieldAttacks      = new(); // DualWield_*
            public List<AnimationClip> oneHandShieldAttacks  = new(); // 1H+Shield (falls back to 1H)
            public List<AnimationClip> shieldOnlyAttacks     = new(); // shield bash, etc.

            public List<AnimationClip> hitVariants   = new();
            public List<AnimationClip> dodgeVariants = new();

            public bool IsValid => idle && walk;

            public List<AnimationClip> GetAttackListFor(WeaponClass cls)
            {
                switch (cls)
                {
                    case WeaponClass.OneHand:          return oneHandAttacks;
                    case WeaponClass.TwoHand:          return twoHandAttacks;
                    case WeaponClass.DualOneHand:      return dualWieldAttacks;
                    case WeaponClass.OneHandAndShield: return oneHandShieldAttacks.Count>0 ? oneHandShieldAttacks : oneHandAttacks;
                    case WeaponClass.ShieldOnly:       return shieldOnlyAttacks.Count>0 ? shieldOnlyAttacks : oneHandAttacks;
                    default:                           return unarmedAttacks; // no weapon → fists/kicks
                }
            }

            public AnimationClip GetAnyAttackFallback(WeaponClass cls)
            {
                // used only if lists are empty; tries old singles
                if (cls == WeaponClass.DualOneHand && dualWield) return dualWield;
                if (attackLight) return attackLight;
                if (attackHeavy) return attackHeavy;
                return null;
            }
        }

        public List<AnimationSet> sets = new();
        public WeaponClass fallbackClass = WeaponClass.OneHand;

        public AnimationSet Get(WeaponClass cls)
        {
            for (int i = 0; i < sets.Count; i++)
            {
                var s = sets[i];
                if (s != null && s.weaponClass == cls) return s;
            }
            return null;
        }

        public AnimationSet GetOrFallback(WeaponClass cls)
        {
            var exact = Get(cls);
            if (exact != null && exact.IsValid) return exact;
            var fb = Get(fallbackClass);
            if (fb != null && fb.IsValid) return fb;
            for (int i = 0; i < sets.Count; i++)
            {
                var s = sets[i];
                if (s != null && s.IsValid) return s;
            }
            return null;
        }

        public AnimationSet Ensure(WeaponClass cls)
        {
            var s = Get(cls);
            if (s != null) return s;
            s = new AnimationSet { weaponClass = cls };
            sets.Add(s);
            return s;
        }
    }
}
