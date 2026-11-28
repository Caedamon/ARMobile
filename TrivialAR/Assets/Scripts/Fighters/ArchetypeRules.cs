using UnityEngine;

namespace Fighters
{
    /// <summary>
    /// The tuning sheet for archetype points: what one point of each modifier is worth. Change numbers here, nowhere else
    /// (like StatRules). A fighter with no modifiers is not touched at all.
    /// </summary>
    public static class ArchetypeRules
    {
        public const float HealthPerPoint = 0.10f;        // +10% max health per point
        public const float DamagePerPoint = 0.05f;        // +5% damage per point
        public const float DodgePerPoint = 0.03f;         // +3 percentage points of dodge chance per point
        public const float MovementPerPoint = 0.06f;      // +6% walking/running speed per point
        public const float AttackSpeedPerPoint = 0.04f;   // +4% swing speed per point
        public const float PoisePerPoint = 0.04f;         // +4 percentage points of poise per point

        public static string Label(ArchetypeModifierKind kind)
        {
            switch (kind)
            {
                case ArchetypeModifierKind.Health: return "Health";
                case ArchetypeModifierKind.Damage: return "Damage";
                case ArchetypeModifierKind.Dodge: return "Dodge";
                case ArchetypeModifierKind.Movement: return "Movement";
                case ArchetypeModifierKind.AttackSpeed: return "Attack speed";
                case ArchetypeModifierKind.Poise: return "Poise";
                default: return kind.ToString();
            }
        }

        public static string ClassLabel(ArchetypeClass c)
        {
            switch (c)
            {
                case ArchetypeClass.Playable: return "Playable class";
                case ArchetypeClass.EnemyOnly: return "Enemy class";
                default: return "Large enemy";
            }
        }
    }
}
