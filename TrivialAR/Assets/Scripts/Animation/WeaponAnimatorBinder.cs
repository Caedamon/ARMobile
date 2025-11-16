using System.Collections.Generic;
using UnityEngine;
using Weaponary;

namespace Animation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(WeaponAnimationLibrary))]
    public sealed class WeaponAnimatorBinder : MonoBehaviour
    {
        public RuntimeAnimatorController baseController;

        Animator _anim;
        WeaponAnimationLibrary _lib;
        AnimatorOverrideController _aoc;
        readonly List<KeyValuePair<AnimationClip, AnimationClip>> _pairs = new();

        static readonly string IdleKey  = "Base/Idle";
        static readonly string WalkKey  = "Base/Walk";
        static readonly string AtkLKey  = "Base/Attack_Light";
        static readonly string AtkHKey  = "Base/Attack_Heavy";
        static readonly string BlockKey = "Base/Block";
        static readonly string DodgeKey = "Base/Dodge";
        static readonly string HitKey   = "Base/Hit";
        static readonly string DieKey   = "Base/Die";
        static readonly string SpawnKey = "Base/Spawn";

        AnimationClip _atkLPH, _atkHPH;

        void Awake()
        {
            _anim = GetComponent<Animator>();
            _lib  = GetComponent<WeaponAnimationLibrary>();
            EnsureAOC();
        }

        void EnsureAOC()
        {
            if (!_anim.runtimeAnimatorController && baseController)
                _anim.runtimeAnimatorController = baseController;

            if (_anim.runtimeAnimatorController is AnimatorOverrideController exist)
            {
                _aoc = exist;
                return;
            }

            if (_anim.runtimeAnimatorController)
            {
                _aoc = new AnimatorOverrideController(_anim.runtimeAnimatorController);
                _anim.runtimeAnimatorController = _aoc;
            }
            else if (baseController)
            {
                _aoc = new AnimatorOverrideController(baseController);
                _anim.runtimeAnimatorController = _aoc;
            }
        }

        public void ApplyFor(IWeaponClassProvider provider)
        {
            if (provider == null) return;
            ApplyFor(provider.GetWeaponClass());
        }

        public void ApplyFor(WeaponClass cls)
        {
            EnsureAOC();
            if (_aoc == null || _lib == null) return;

            var set = _lib.GetOrFallback(cls);
            if (set == null) return;

            _pairs.Clear();
            _aoc.GetOverrides(_pairs);

            var idlePH  = FindPH(IdleKey);
            var walkPH  = FindPH(WalkKey);
            var atkLPH  = FindPH(AtkLKey);
            var atkHPH  = FindPH(AtkHKey);
            var blockPH = FindPH(BlockKey);
            var dodgePH = FindPH(DodgeKey);
            var hitPH   = FindPH(HitKey);
            var diePH   = FindPH(DieKey);
            var spawnPH = FindPH(SpawnKey);

            Try(idlePH,  set.idle);
            Try(walkPH,  set.walk);

            bool isDual = (cls == WeaponClass.DualOneHand);
            var light = isDual && set.dualWield ? set.dualWield : (set.attackLight ?? set.dualWield);
            var heavy = isDual && set.dualWield ? set.dualWield : (set.attackHeavy ?? set.attackLight);

            Try(atkLPH,  light);
            Try(atkHPH,  heavy);

            Try(blockPH, set.block ? set.block : set.idle);
            Try(dodgePH, set.dodge ? set.dodge : (set.hit ? set.hit : set.idle));
            Try(hitPH,   set.hit   ? set.hit   : set.idle);
            Try(diePH,   set.die   ? set.die   : (set.hit ? set.hit : set.idle));
            Try(spawnPH, set.spawn ? set.spawn : set.idle);

            _aoc.ApplyOverrides(_pairs);

            _atkLPH = atkLPH; _atkHPH = atkHPH;

            var picker = GetComponent<AttackVariantPicker>();
            if (picker) picker.SetCurrentClass(cls);

            var ma = GetComponent<Combat.MeleeAnimator>();
            if (ma) ma.RecalculateDurationsFromController();
        }

        public void OverrideAttackPlaceholder(bool heavy, AnimationClip clip)
        {
            if (_aoc == null || clip == null) return;
            if (heavy && _atkHPH) _aoc[_atkHPH] = clip;
            else if (!heavy && _atkLPH) _aoc[_atkLPH] = clip;
        }

        AnimationClip FindPH(string name)
        {
            for (int i = 0; i < _pairs.Count; i++)
            {
                var src = _pairs[i].Key;
                if (src && src.name == name) return src;
            }
            return null;
        }

        void Try(AnimationClip ph, AnimationClip clip)
        {
            if (ph && clip) _pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(ph, clip));
        }
    }
}
