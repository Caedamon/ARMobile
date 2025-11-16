// AttackVariantPicker: adds "Unarmed-until-pickup" support
using System.Collections.Generic;
using Animation;
using UnityEngine;

namespace Weaponary
{
    public enum VariantMode { Random, Sequential, NoImmediateRepeat }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponAnimatorBinder))]
    [RequireComponent(typeof(WeaponAnimationLibrary))]
    public sealed class AttackVariantPicker : MonoBehaviour
    {
        public VariantMode mode = VariantMode.NoImmediateRepeat;

        // New: force unarmed until pickup
        public bool forceUnarmedUntilEquipped = true;
        bool _equipped;

        WeaponAnimatorBinder _binder;
        WeaponAnimationLibrary _lib;
        WeaponClass _current;

        int _iAtk=-1,_iHit=-1,_iDodge=-1;
        AnimationClip _lastAtk,_lastHit,_lastDodge;

        void Awake()
        {
            _binder = GetComponent<WeaponAnimatorBinder>();
            _lib    = GetComponent<WeaponAnimationLibrary>();
        }

        public void SetCurrentClass(WeaponClass cls) { _current = cls; }
        public void MarkUnequipped() { _equipped = false; }
        public void MarkEquipped()   { _equipped = true;  }

        public void PrepareAttack(bool _heavyIgnored)
        {
            var set = _lib.GetOrFallback(_current);
            if (set == null) return;

            List<AnimationClip> pool = null;

            if (forceUnarmedUntilEquipped && !_equipped)
            {
                pool = set.unarmedAttacks; // strictly unarmed before pickup
            }
            else
            {
                pool = set.GetAttackListFor(_current);
                var chosenTmp = Pick(pool, ref _iAtk, ref _lastAtk);
                if (!chosenTmp && set.unarmedAttacks != null && set.unarmedAttacks.Count > 0)
                    pool = set.unarmedAttacks; // fallback to unarmed if weapon pool empty
            }

            var chosen = Pick(pool, ref _iAtk, ref _lastAtk);
            if (!chosen) chosen = set.GetAnyAttackFallback(_current);
            if (!chosen) chosen = set.idle;
            if (!chosen) return;

            _binder.OverrideAttackPlaceholder(false, chosen);
            _binder.OverrideAttackPlaceholder(true,  chosen);
        }

        public AnimationClip PrepareHit()
        {
            var set = _lib.GetOrFallback(_current);
            if (set == null) return null;
            var c = Pick(set.hitVariants, ref _iHit, ref _lastHit);
            return c ? c : set.hit;
        }

        public AnimationClip PrepareDodge()
        {
            var set = _lib.GetOrFallback(_current);
            if (set == null) return null;
            var c = Pick(set.dodgeVariants, ref _iDodge, ref _lastDodge);
            return c ? c : set.dodge;
        }

        AnimationClip Pick(List<AnimationClip> list, ref int idx, ref AnimationClip last)
        {
            if (list == null || list.Count == 0) return null;

            if (mode == VariantMode.Sequential)
            { idx = (idx + 1) % list.Count; last = list[idx]; return last; }

            if (mode == VariantMode.Random)
            { last = list[Random.Range(0, list.Count)]; return last; }

            if (list.Count == 1) { last = list[0]; return last; }

            int tries = 6; AnimationClip c;
            do { c = list[Random.Range(0, list.Count)]; } while (c == last && --tries > 0);
            last = c; return last;
        }
    }
}
