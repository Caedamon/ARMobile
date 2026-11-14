using UnityEngine;

namespace Fighters
{
    public enum FighterTier { Pathetic = 0, Normal = 1, HighQuality = 2, Elite = 3 }

    /// <summary>Everything about tiers in one place: label, colour, spawn chance and stat budget.</summary>
    public static class FighterTiers
    {
        public static string Label(FighterTier t)
        {
            switch (t)
            {
                case FighterTier.Pathetic:    return "Pathetic";
                case FighterTier.Normal:      return "Normal";
                case FighterTier.HighQuality: return "High quality";
                case FighterTier.Elite:       return "Elite";
                default:                      return t.ToString();
            }
        }

        public static Color Tint(FighterTier t)
        {
            switch (t)
            {
                case FighterTier.Pathetic:    return new Color(0.65f, 0.65f, 0.65f);
                case FighterTier.Normal:      return new Color(1.00f, 1.00f, 1.00f);
                case FighterTier.HighQuality: return new Color(0.40f, 0.80f, 1.00f);
                case FighterTier.Elite:       return new Color(1.00f, 0.80f, 0.20f);
                default:                      return Color.white;
            }
        }

        /// <summary>Relative chance of rolling this tier.</summary>
        public static float Weight(FighterTier t)
        {
            switch (t)
            {
                case FighterTier.Pathetic:    return 15f;
                case FighterTier.Normal:      return 50f;
                case FighterTier.HighQuality: return 27f;
                case FighterTier.Elite:       return 8f;
                default:                      return 0f;
            }
        }

        /// <summary>Total points spread over the four visible stats (each stat is 1-10). Inclusive range.</summary>
        public static void StatBudget(FighterTier t, out int min, out int max)
        {
            switch (t)
            {
                case FighterTier.Pathetic:    min = 8;  max = 12; break;
                case FighterTier.Normal:      min = 13; max = 19; break;
                case FighterTier.HighQuality: min = 20; max = 27; break;
                case FighterTier.Elite:       min = 28; max = 34; break;
                default:                      min = 13; max = 19; break;
            }
        }
    }
}
