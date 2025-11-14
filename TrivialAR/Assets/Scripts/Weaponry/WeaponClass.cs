using UnityEngine;

namespace Weaponary
{
    public enum WeaponClass
    {
        OneHand = 1,
        TwoHand = 2,
        ShieldOnly = 3,
        OneHandAndShield = 4,
        DualOneHand = 5
    }

    public interface IWeaponClassProvider
    {
        WeaponClass GetWeaponClass();
    }
}