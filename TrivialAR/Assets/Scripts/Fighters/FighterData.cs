using System;
using Weaponary;

namespace Fighters
{
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
