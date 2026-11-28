using System.Collections.Generic;
using UnityEngine;

namespace Fighters
{
    /// <summary>
    /// Put on a fighter by FighterIdentity when its archetype has modifiers. The combat code reads these numbers
    /// (FighterBrain.Archetype.cs). A fighter without this component fights exactly as it always did.
    /// The same code path runs for the hero, allies and generated opponents, because they all come through FighterIdentity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchetypeEffects : MonoBehaviour
    {
        public ArchetypeDefinition Archetype { get; private set; }

        public float HealthMul = 1f;
        public float DamageMul = 1f;
        public float DodgeAdd = 0f;
        public float MoveMul = 1f;
        public float AttackSpeedMul = 1f;
        public float PoiseAdd = 0f;

        /// <summary>Unique abilities, for later. Nothing reads these yet.</summary>
        public readonly List<ArchetypeAbility> Abilities = new List<ArchetypeAbility>();

        /// <summary>Returns the component, or null (and removes any old one) when the archetype changes nothing.</summary>
        public static ArchetypeEffects Attach(GameObject fighter, ArchetypeDefinition archetype)
        {
            var fx = fighter.GetComponent<ArchetypeEffects>();
            bool hasAbilities = archetype != null && archetype.abilities != null && archetype.abilities.Count > 0;
            if (archetype == null || (!archetype.HasModifiers && !hasAbilities))
            {
                if (fx != null) Destroy(fx);
                return null;
            }

            if (fx == null) fx = fighter.AddComponent<ArchetypeEffects>();
            fx.Archetype = archetype;
            fx.HealthMul = 1f;
            fx.DamageMul = 1f;
            fx.DodgeAdd = 0f;
            fx.MoveMul = 1f;
            fx.AttackSpeedMul = 1f;
            fx.PoiseAdd = 0f;
            fx.Abilities.Clear();
            if (archetype.modifiers != null)
            {
                foreach (var m in archetype.modifiers)
                {
                    float p = m.points;
                    switch (m.kind)
                    {
                        case ArchetypeModifierKind.Health: fx.HealthMul += p * ArchetypeRules.HealthPerPoint; break;
                        case ArchetypeModifierKind.Damage: fx.DamageMul += p * ArchetypeRules.DamagePerPoint; break;
                        case ArchetypeModifierKind.Dodge: fx.DodgeAdd += p * ArchetypeRules.DodgePerPoint; break;
                        case ArchetypeModifierKind.Movement: fx.MoveMul += p * ArchetypeRules.MovementPerPoint; break;
                        case ArchetypeModifierKind.AttackSpeed: fx.AttackSpeedMul += p * ArchetypeRules.AttackSpeedPerPoint; break;
                        case ArchetypeModifierKind.Poise: fx.PoiseAdd += p * ArchetypeRules.PoisePerPoint; break;
                    }
                }
            }
            // never below a sensible floor, so a stack of penalties cannot freeze or one-shot a fighter
            fx.HealthMul = Mathf.Max(0.3f, fx.HealthMul);
            fx.DamageMul = Mathf.Max(0.3f, fx.DamageMul);
            fx.MoveMul = Mathf.Max(0.4f, fx.MoveMul);
            fx.AttackSpeedMul = Mathf.Max(0.4f, fx.AttackSpeedMul);
            if (hasAbilities) fx.Abilities.AddRange(archetype.abilities);
            return fx;
        }
    }
}
