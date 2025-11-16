using UnityEngine;

namespace Weaponary
{
    [DisallowMultipleComponent]
    public sealed class WeaponDamage : MonoBehaviour
    {
        [Header("Weapon Damage")]
        [Min(1f)] public float baseDamage = 25f;
        [Tooltip("Optional per-weapon scalar (e.g., 2H heavier).")]
        [Min(0.1f)] public float weaponScalar = 1.0f;

        public float GetBaseDamage() => Mathf.Max(1f, baseDamage) * Mathf.Max(0.1f, weaponScalar);
    }
}