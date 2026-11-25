using System;
using UnityEngine;

namespace Combat
{
    /// <summary>Per-fighting-style multipliers, applied on top of each weapon's own damage and its moveset.</summary>
    [Serializable]
    public struct StyleTuning
    {
        [Tooltip("Multiplies the damage of every hit with this style.")]
        public float damageMul;
        [Tooltip("Multiplies attack animation speed (faster swings AND a sooner strike).")]
        public float speedMul;
        [Tooltip("Multiplies the pause between attacks. Below 1 = recovers faster.")]
        public float cooldownMul;

        public StyleTuning(float damage, float speed, float cooldown) { damageMul = damage; speedMul = speed; cooldownMul = cooldown; }
    }

    /// <summary>
    /// All the combat balance numbers in one asset you can edit in the Inspector
    /// (Tools > Kaiju > Combat > Create Combat Tuning makes it and plugs it into the arena).
    /// Per-weapon damage and swing speed still live on the weapon and moveset assets; this adds the shield rules,
    /// dodge, and a multiplier per fighting style. Use Tools > Kaiju > Combat > Balance Simulator to check the effect.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatTuning", menuName = "Kaiju/Combat Tuning")]
    public sealed class CombatTuning : ScriptableObject
    {
        [Header("Shield and blocking")]
        [Tooltip("Chance a shield carrier raises the shield against an incoming swing.")]
        [Range(0f, 1f)] public float baseBlockChance = 0.22f;
        [Tooltip("Extra block chance per INSIGHT point.")]
        [Range(0f, 0.1f)] public float blockPerInsight = 0.02f;
        [Tooltip("Seconds after a block before the shield can be raised again.")]
        public float blockCooldown = 1.0f;
        [Tooltip("Damage taken from a blocked hit (0.40 = 40%).")]
        [Range(0f, 1f)] public float blockDamageFactor = 0.40f;
        [Tooltip("Damage reduction just for carrying a shield (0.05 = 5%).")]
        [Range(0f, 0.5f)] public float shieldPassiveReduction = 0.05f;

        [Header("Dodge")]
        [Range(0f, 1f)] public float baseDodgeChance = 0.08f;
        [Range(0f, 0.1f)] public float dodgePerInsight = 0.025f;
        public float dodgeCooldown = 1.6f;

        [Header("Stats")]
        [Tooltip("Chance per EGO point to shrug off a flinch while attacking.")]
        public float egoPoisePerPoint = 0.04f;
        [Tooltip("Attack speed bonus per INSIGHT point above 5.")]
        public float attackSpeedPerInsight = 0.02f;

        [Header("Weapon proficiency (per level, max level 10). Damage applies to every style; the perk only while fighting with that style.")]
        [Tooltip("Extra damage per proficiency level (0.05 = +5%).")]
        public float profDamagePerLevel = 0.05f;
        [Tooltip("One-handed: extra crit chance per level (0.015 = +1.5%).")]
        public float profCritPerLevel = 0.015f;
        [Tooltip("Sword & shield: extra block chance per level.")]
        public float profBlockPerLevel = 0.02f;
        [Tooltip("Dual wield: extra attack speed per level.")]
        public float profAttackSpeedPerLevel = 0.02f;
        [Tooltip("Two-handed: extra chance per level to shrug off a flinch while attacking.")]
        public float profPoisePerLevel = 0.03f;
        [Tooltip("Unarmed: extra dodge chance per level.")]
        public float profDodgePerLevel = 0.03f;
        [Tooltip("Proficiency XP in a Duel is multiplied by this (duels are for training).")]
        public float duelProfXpMultiplier = 1.5f;

        [Header("Per fighting style (1 = no change)")]
        public StyleTuning unarmed = new StyleTuning(1f, 1f, 1f);
        public StyleTuning oneHand = new StyleTuning(1f, 1f, 1f);
        public StyleTuning shield  = new StyleTuning(1f, 1f, 1f);
        [Tooltip("Two-handed weapons hit harder.")]
        public StyleTuning twoHand = new StyleTuning(1.10f, 1f, 1f);
        [Tooltip("Dual wielding swings faster and recovers faster.")]
        public StyleTuning dual    = new StyleTuning(1f, 1.10f, 0.90f);

        // ------------------------------------------------------------------ shared formulas (the fights and the simulator both use these)
        public StyleTuning For(string styleKey)
        {
            switch (styleKey)
            {
                case Fighters.ProgressionRules.Unarmed: return unarmed;
                case Fighters.ProgressionRules.Shield:  return shield;
                case Fighters.ProgressionRules.TwoHand: return twoHand;
                case Fighters.ProgressionRules.Dual:    return dual;
                default:                                return oneHand;
            }
        }

        public float BlockChance(float insight) { return Mathf.Clamp01(baseBlockChance + blockPerInsight * insight); }
        public float DodgeChance(float insight) { return Mathf.Clamp01(baseDodgeChance + dodgePerInsight * insight); }

        public float IncomingFactor(bool blocking, bool hasShield)
        {
            if (blocking) return blockDamageFactor;
            return hasShield ? 1f - shieldPassiveReduction : 1f;
        }

        // The built-in defaults, used when no asset is assigned.
        static CombatTuning _default;
        public static CombatTuning Default()
        {
            if (_default == null)
            {
                _default = CreateInstance<CombatTuning>();
                _default.hideFlags = HideFlags.HideAndDontSave;
            }
            return _default;
        }
    }
}
