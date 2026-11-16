using System.Collections.Generic;
using UnityEngine;

namespace Weaponary
{
    /// <summary>
    /// One weapon type. Owns its model, damage, reach and its own list of attack animations.
    /// Create via: Assets > Create > Kaiju > Weapon Definition (or use Tools > Kaiju > Weapons > Setup).
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Kaiju/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Weapon";
        public WeaponClass weaponClass = WeaponClass.OneHand;

        [Header("Visuals")]
        [Tooltip("Model shown on the floor and in the right hand.")]
        public GameObject visualPrefab;
        [Tooltip("Optional second model for the left hand (dual wield).")]
        public GameObject offhandVisualPrefab;

        [Header("Combat")]
        [Min(1f)] public float baseDamage = 25f;
        [Min(0.1f)] public float damageScalar = 1f;
        [Tooltip("How close (world meters) the wielder stands to hit. Replaces MeleeMotor.attackRange while equipped.")]
        [Min(0.01f)] public float reach = 0.10f;

        [Header("Attacks (owned by the weapon)")]
        [Tooltip("One of these is picked per attack (never the same twice in a row).")]
        public List<AnimationClip> attackClips = new List<AnimationClip>();
        
        [Header("Real-time combat")]
        [Tooltip("Attack set used by the real-time FighterBrain. Filled in by Tools > Kaiju > Combat > Create Starter Movesets.")]
        public Combat.Moveset moveset;

        [Header("Off-hand (shield or second weapon)")]
        [Tooltip("Rotation of the off-hand model in the hand socket, in degrees. Use this if a shield sits wrong.")]
        public Vector3 offhandEuler;
        [Tooltip("Position offset of the off-hand model in the hand socket (socket-local units).")]
        public Vector3 offhandOffset;
    }
}
