using System;
using Weaponary;

namespace Fighters
{
    /// <summary>
    /// Rolls a random fighter. The tier is rolled FIRST and then decides the stat budget,
    /// so the tier label always matches the stats (an "Elite" is never secretly weak).
    /// LUCK is rolled separately, so luck can be good or bad at any tier.
    /// </summary>
    public static class FighterGenerator
    {
        public const int StatMin = 1;
        public const int StatMax = 10;

        static readonly string[] FirstNames =
        {
            "Bjorn", "Hrolf", "Sigrid", "Ulf", "Astrid", "Gunnar", "Freya", "Torvald",
            "Ingrid", "Leif", "Brynja", "Haldor", "Runa", "Eirik", "Solveig", "Knut",
            "Maren", "Orm", "Thyra", "Vidar", "Yrsa", "Dag", "Hild", "Sten"
        };

        static readonly string[] Epithets =
        {
            "Ironjaw", "the Reckless", "Skullsplitter", "the Unlucky", "Bonecrusher", "the Quiet",
            "Stormborn", "the Hungry", "Redhand", "the Stubborn", "Gravedigger", "the Bold",
            "Wolfbane", "the Lame", "Coldeye", "the Loud", "Blackbeard", "the Younger",
            "Oathbreaker", "the Fortunate", "Hammerfist", "the Pale", "Nightwalker", "the Brave"
        };

        public static FighterData Generate(FighterCatalog catalog, FighterSide side, Random rng = null, int levelHint = 1)
        {
            rng = rng ?? new Random();

            var d = new FighterData();
            d.tier = RollTier(rng, levelHint);
            RollStats(d, rng);
            d.luck = (rng.Next(0, 11) + rng.Next(0, 11)) / 2;   // 0-10, bell-shaped
            RollPreferred(d, rng);
            ProgressionRules.GrantProfLevel(d, ProgressionRules.StyleKey(d.preferredClass), ProgressionRules.StartProfLevel(d.tier));
            d.displayName = FirstNames[rng.Next(FirstNames.Length)] + " " + Epithets[rng.Next(Epithets.Length)];
            d.modelId = PickModel(catalog, side, rng);
            return d;
        }

        // From level 4 on, better opponents become more common (shift goes 0..1).
        static float LevelWeight(FighterTier t, int level)
        {
            float s = Math.Max(0f, Math.Min(1f, (level - 3) * 0.1f));
            float w = FighterTiers.Weight(t);
            switch (t)
            {
                case FighterTier.Pathetic:    return w * (1f - s);
                case FighterTier.Normal:      return w * (1f - 0.5f * s);
                case FighterTier.HighQuality: return w * (1f + s);
                case FighterTier.Elite:       return w * (1f + 3f * s);
                default:                      return w;
            }
        }

        static FighterTier RollTier(Random rng, int levelHint)
        {
            var tiers = (FighterTier[])Enum.GetValues(typeof(FighterTier));
            float total = 0f;
            foreach (var t in tiers) total += LevelWeight(t, levelHint);

            double r = rng.NextDouble() * total;
            foreach (var t in tiers)
            {
                r -= LevelWeight(t, levelHint);
                if (r <= 0.0) return t;
            }
            return FighterTier.Normal;
        }

        static void RollStats(FighterData d, Random rng)
        {
            FighterTiers.StatBudget(d.tier, out int lo, out int hi);
            int budget = rng.Next(lo, hi + 1);

            var s = new[] { StatMin, StatMin, StatMin, StatMin };
            var w = new double[4];
            for (int i = 0; i < 4; i++) w[i] = 0.4 + rng.NextDouble() * 1.6;   // random specialisation

            int remaining = budget - 4 * StatMin;
            while (remaining > 0)
            {
                double total = 0;
                int lastOpen = -1;
                for (int i = 0; i < 4; i++)
                    if (s[i] < StatMax) { total += w[i]; lastOpen = i; }
                if (lastOpen < 0) break;

                double r = rng.NextDouble() * total;
                int pick = lastOpen;
                for (int i = 0; i < 4; i++)
                {
                    if (s[i] >= StatMax) continue;
                    r -= w[i];
                    if (r <= 0.0) { pick = i; break; }
                }
                s[pick]++;
                remaining--;
            }

            d.might = s[0];
            d.ego = s[1];
            d.education = s[2];
            d.insight = s[3];
        }

        static void RollPreferred(FighterData d, Random rng)
        {
            // Strong fighters lean toward big weapons, perceptive ones toward dual wield.
            double wOne  = 3.0;
            double wTwo  = 2.0 + d.might / 5.0;
            double wDual = 2.0 + d.insight / 5.0;
            double wShield = 2.0 + d.ego / 5.0;   // stubborn fighters like a shield

            double r = rng.NextDouble() * (wOne + wTwo + wDual + wShield);
            d.hasPreferredClass = true;
            if ((r -= wOne) <= 0) d.preferredClass = WeaponClass.OneHand;
            else if ((r -= wTwo) <= 0) d.preferredClass = WeaponClass.TwoHand;
            else if ((r -= wDual) <= 0) d.preferredClass = WeaponClass.DualOneHand;
            else d.preferredClass = WeaponClass.OneHandAndShield;
        }

        static string PickModel(FighterCatalog catalog, FighterSide side, Random rng)
        {
            if (catalog == null) return null;
            var models = catalog.ForSide(side);
            if (models.Count == 0) return null;
            return models[rng.Next(models.Count)].id;
        }
    }
}
