using UnityEngine;

namespace Weaponary
{
    [DisallowMultipleComponent]
    public sealed class OnEquip_ApplyAnimations : MonoBehaviour
    {
        public WeaponAnimatorBinder binder;

        void Reset() { binder = GetComponentInParent<WeaponAnimatorBinder>(); }

        public void OnEquipped(GameObject equippedWeapon)
        {
            if (!binder || !equippedWeapon) return;

            var provider = equippedWeapon.GetComponent<IWeaponClassProvider>();
            var cls = provider != null ? provider.GetWeaponClass() : WeaponClass.OneHand;

            // Shield combo: if equipping a shield while holding 1H, prefer OneHandAndShield if available.
            if (cls == WeaponClass.ShieldOnly)
                binder.ApplyFor(WeaponClass.OneHandAndShield);
            else
                binder.ApplyFor(cls);
        }
    }
}
