using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fighters
{
    /// <summary>What a modifier changes. Each one is worth a small, fixed amount per point (see ArchetypeRules).</summary>
    public enum ArchetypeModifierKind
    {
        Health,        // maximum health
        Damage,        // damage of every hit
        Dodge,         // chance to dodge an incoming swing
        Movement,      // walking and running speed
        AttackSpeed,   // swing speed
        Poise          // chance to shrug off a flinch while attacking
    }

    /// <summary>Where the archetype is listed in the Codex, and how it is described.</summary>
    public enum ArchetypeClass
    {
        Playable,     // a class you can pick (it can also appear as an opponent)
        EnemyOnly,    // only met as an opponent
        Large         // the big ones
    }

    /// <summary>One bonus (positive points) or penalty (negative points).</summary>
    [Serializable]
    public struct ArchetypeModifier
    {
        public ArchetypeModifierKind kind;
        [Tooltip("Points: +1 is a small bonus, -1 a small penalty. See ArchetypeRules for what one point is worth.")]
        [Range(-5, 5)] public int points;

        public ArchetypeModifier(ArchetypeModifierKind kind, int points) { this.kind = kind; this.points = points; }
    }

    /// <summary>
    /// Room for the unique innate abilities that archetypes will get later (a Vampire draining life, a Paladin smiting the undead...).
    /// Nothing implements this yet: make an asset type that derives from it, add it to an archetype's "abilities" list, and let
    /// ArchetypeEffects hand it to the fighter. Until then the list stays empty and nothing happens.
    /// </summary>
    public abstract class ArchetypeAbility : ScriptableObject
    {
        public string displayName;
        [TextArea(1, 4)] public string description;
    }

    /// <summary>
    /// One archetype (class) as data: its lore, its listing in the Codex, and its modifiers. It applies to catalog model ids
    /// (the same ids the FighterCatalog already has), so no fighter is defined twice. It knows nothing about quality tiers,
    /// individual names or gender: those stay on the fighter. Create them with Tools > Kaiju > Book > Set Up Codex, edit in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "Archetype", menuName = "Kaiju/Archetype")]
    public sealed class ArchetypeDefinition : ScriptableObject
    {
        [Tooltip("Shown as the title of the Codex page.")]
        public string displayName = "Archetype";
        public ArchetypeClass classification = ArchetypeClass.Playable;

        [Tooltip("The FighterCatalog model ids this archetype applies to (the prefab file names, like Paladin_with_Helmet). Usually one.")]
        public List<string> modelIds = new List<string>();

        [Tooltip("The fixed lore text on the Codex page.")]
        [TextArea(4, 12)] public string description = "";

        [Tooltip("Bonuses and penalties. Leave empty and the fighter fights exactly as before.")]
        public List<ArchetypeModifier> modifiers = new List<ArchetypeModifier>();

        [Tooltip("Reserved for unique abilities (not used yet).")]
        public List<ArchetypeAbility> abilities = new List<ArchetypeAbility>();

        public bool HasModifiers
        {
            get
            {
                if (modifiers == null) return false;
                foreach (var m in modifiers) if (m.points != 0) return true;
                return false;
            }
        }

        public bool AppliesTo(string modelId)
        {
            return !string.IsNullOrEmpty(modelId) && modelIds != null && modelIds.Contains(modelId);
        }
    }
}
