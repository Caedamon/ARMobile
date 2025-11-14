using UnityEngine;

namespace Weaponary
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponAnimatorBinder))]
    [RequireComponent(typeof(WeaponAnimationLibrary))]
    public sealed class WeaponEquipResolver : MonoBehaviour
    {
        public WeaponAnimatorBinder binder;

        [Header("Current Hands (read-only)")]
        public GameObject mainHand;   // right hand
        public GameObject offHand;    // left hand

        void Reset() { binder = GetComponent<WeaponAnimatorBinder>(); }

        public void EquipMain(GameObject item)
        {
            mainHand = item;
            Apply();
        }

        public void EquipOff(GameObject item)
        {
            offHand = item;
            Apply();
        }

        public void UnequipMain()
        {
            mainHand = null;
            Apply();
        }

        public void UnequipOff()
        {
            offHand = null;
            Apply();
        }

        public WeaponClass GetCurrentClass() => ResolveClass(mainHand, offHand);

        public void Apply()
        {
            if (!binder) return;
            binder.ApplyFor(GetCurrentClass());
        }

        static WeaponClass ResolveClass(GameObject main, GameObject off)
        {
            var mainC = From(main);
            var offC  = From(off);

            // 2H dominates
            if (mainC == WeaponClass.TwoHand) return WeaponClass.TwoHand;

            // 1H + Shield
            if (mainC == WeaponClass.OneHand && offC == WeaponClass.ShieldOnly)
                return WeaponClass.OneHandAndShield;

            // Dual 1H
            if (mainC == WeaponClass.OneHand && offC == WeaponClass.OneHand)
                return WeaponClass.DualOneHand;

            // Shield only
            if (main == null && offC == WeaponClass.ShieldOnly)
                return WeaponClass.ShieldOnly;

            // Solo 1H
            if (mainC == WeaponClass.OneHand) return WeaponClass.OneHand;

            // Fallbacks
            if (mainC != 0) return mainC;
            if (offC  != 0) return offC;
            return WeaponClass.OneHand;
        }

        static WeaponClass From(GameObject go)
        {
            if (!go) return 0;
            var p = go.GetComponent<IWeaponClassProvider>();
            return p != null ? p.GetWeaponClass() : WeaponClass.OneHand;
        }
    }
}
