using System;
using System.Collections.Generic;
using Weaponary;

namespace Fighters
{
    /// <summary>XP in one fighting style (Unarmed, OneHand, TwoHand, Dual). See ProgressionRules.</summary>
    [Serializable]
    public sealed class ProficiencyEntry
    {
        public string style;
        public int xp;
    }

    /// <summary>
    /// Everything that defines one fighter. Plain serializable class on purpose:
    /// the roster UI, the spawner and the save file (JsonUtility) all use it.
    /// </summary>
    [Serializable]
    public sealed class FighterData
    {
        public string id = Guid.NewGuid().ToString("N");
        public string displayName = "Unnamed";
        public string modelId;                 // key into FighterCatalog

        public FighterTier tier = FighterTier.Normal;

        // Visible stats (1-10 at level 1)
        public int might = 1;
        public int ego = 1;
        public int education = 1;
        public int insight = 1;
        // Hidden
        public int luck;

        public bool hasPreferredClass;
        public WeaponClass preferredClass = WeaponClass.OneHand;

        public int level = 1;
        public int xp;
        public int wins;
        public int fights;   // fights fought, wins and losses (shown on the tombstone)
        public int statPoints;   // unspent level-up points (spent under Stats on the hub)

        // Legacy (shown on the hero's pages in the Memento Mori section of the Book). Old saves simply start at 0.
        public int bossesDefeated;
        public int streak;        // victories in a row, back to 0 after any defeat
        public int bestStreak;

        // Weapon proficiency (XP per fighting style). Old saves without this simply start empty.
        public List<ProficiencyEntry> proficiency = new List<ProficiencyEntry>();

        public int VisibleTotal => might + ego + education + insight;

        public string PreferredStyleLabel
        {
            get
            {
                if (!hasPreferredClass) return "None";
                switch (preferredClass)
                {
                    case WeaponClass.OneHand:          return "One-handed";
                    case WeaponClass.TwoHand:          return "Two-handed";
                    case WeaponClass.DualOneHand:      return "Dual wield";
                    case WeaponClass.OneHandAndShield: return "Sword and shield";
                    case WeaponClass.ShieldOnly:       return "Shield";
                    default:                           return preferredClass.ToString();
                }
            }
        }

        public string Summary()
        {
            return $"{displayName} [{FighterTiers.Label(tier)}] Lv{level}  " +
                   $"MIGHT {might}  EGO {ego}  EDU {education}  INSIGHT {insight}  (LUCK {luck})  " +
                   $"prefers {PreferredStyleLabel}  model {modelId}";
        }
    }
}
