using UnityEngine;

namespace Weaponary
{
    public sealed class WeaponClassFromWeaponBase : MonoBehaviour, IWeaponClassProvider
    {
        public WeaponClass weaponClass = WeaponClass.OneHand;
        public WeaponClass GetWeaponClass() => weaponClass;
    }
}