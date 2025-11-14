using System;
using System.Collections.Generic;
using UnityEngine;

namespace Weaponary
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
            public AnimationClip attackLight;
            public AnimationClip attackHeavy;
            public AnimationClip block;
            public AnimationClip hit;
            public AnimationClip die;
            public bool IsValid => idle && walk && attackLight;
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
            for (int i = 0; i < sets.Count; i++) if (sets[i] != null && sets[i].IsValid) return sets[i];
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