using System.Collections.Generic;
using UnityEngine;

namespace Weaponary
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(WeaponAnimationLibrary))]
    public sealed class WeaponAnimatorBinder : MonoBehaviour
    {
        [Tooltip("Controller with placeholders: Base/Idle, Base/Walk, Base/Attack_Light, Base/Attack_Heavy, Base/Block, Base/Hit, Base/Die")]
        public RuntimeAnimatorController baseController;

        Animator _anim;
        WeaponAnimationLibrary _lib;
        AnimatorOverrideController _aoc;
        readonly List<KeyValuePair<AnimationClip, AnimationClip>> _buf = new();

        static readonly string IdleKey = "Base/Idle";
        static readonly string WalkKey = "Base/Walk";
        static readonly string AttackLightKey = "Base/Attack_Light";
        static readonly string AttackHeavyKey = "Base/Attack_Heavy";
        static readonly string BlockKey = "Base/Block";
        static readonly string HitKey = "Base/Hit";
        static readonly string DieKey = "Base/Die";

        void Awake()
        {
            _anim = GetComponent<Animator>();
            _lib = GetComponent<WeaponAnimationLibrary>();
            EnsureAOC();
        }

        void EnsureAOC()
        {
            if (!_anim.runtimeAnimatorController && baseController)
                _anim.runtimeAnimatorController = baseController;
            if (_aoc == null && _anim.runtimeAnimatorController)
            {
                _aoc = new AnimatorOverrideController(_anim.runtimeAnimatorController);
                _anim.runtimeAnimatorController = _aoc;
            }
        }

        public void ApplyFor(WeaponClass cls)
        {
            EnsureAOC();
            if (_aoc == null || _lib == null) return;

            var set = _lib.GetOrFallback(cls);
            if (set == null) return;

            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            _aoc.GetOverrides(pairs);

            AnimationClip idlePH = Find(pairs, IdleKey);
            AnimationClip walkPH = Find(pairs, WalkKey);
            AnimationClip atkLPH = Find(pairs, AttackLightKey);
            AnimationClip atkHPH = Find(pairs, AttackHeavyKey);
            AnimationClip blockPH = Find(pairs, BlockKey);
            AnimationClip hitPH = Find(pairs, HitKey);
            AnimationClip diePH = Find(pairs, DieKey);

            _buf.Clear();
            TryAdd(idlePH, set.idle);
            TryAdd(walkPH, set.walk);
            TryAdd(atkLPH, set.attackLight);
            TryAdd(atkHPH, set.attackHeavy ? set.attackHeavy : set.attackLight);
            TryAdd(blockPH, set.block ? set.block : set.idle);
            TryAdd(hitPH, set.hit ? set.hit : set.idle);
            TryAdd(diePH, set.die ? set.die : (set.hit ? set.hit : set.idle));

            _aoc.ApplyOverrides(_buf);
        }

        static AnimationClip Find(List<KeyValuePair<AnimationClip, AnimationClip>> pairs, string name)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                var src = pairs[i].Key;
                if (src && src.name == name) return src;
            }
            return null;
        }

        void TryAdd(AnimationClip ph, AnimationClip clip)
        {
            if (ph && clip) _buf.Add(new KeyValuePair<AnimationClip, AnimationClip>(ph, clip));
        }
    }
}
