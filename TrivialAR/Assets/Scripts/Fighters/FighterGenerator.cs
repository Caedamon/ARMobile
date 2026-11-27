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

        static readonly string[] MaleNames =
        {
            "Bjorn", "Hrolf", "Ulf", "Gunnar", "Torvald", "Leif", "Haldor", "Eirik",
            "Knut", "Orm", "Vidar", "Dag", "Sten", "Ragnar", "Sven", "Arvid"
        };

        static readonly string[] FemaleNames =
        {
            "Sigrid", "Astrid", "Freya", "Ingrid", "Brynja", "Runa", "Solveig", "Maren",
            "Thyra", "Yrsa", "Hild", "Gudrun", "Ragna", "Sunniva", "Helga", "Liv"
        };

        // Epithets that only make sense for men (the rest fit anyone).
        static readonly string[] MaleOnlyEpithets = { "Blackbeard" };

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
            d.modelId = PickModel(catalog, side, rng);   // the model comes first: its gender decides which names fit
            var gender = catalog != null ? catalog.GenderOf(d.modelId, side) : FighterGender.NotSet;
            if (gender == FighterGender.NotSet) WarnNotSet(d.modelId);
            d.displayName = RollName(gender, rng);
            return d;
        }

        static readonly System.Collections.Generic.HashSet<string> Warned = new System.Collections.Generic.HashSet<string>();

        static void WarnNotSet(string modelId)
        {
            if (!Warned.Add(modelId ?? "(none)")) return;
            UnityEngine.Debug.LogWarning("[Names] Model '" + modelId + "' has no gender set on its prefab (Fighter Gender Tag), so it can get any name. Run Tools > Kaiju > Fighters > Set Up Gender Tags.");
        }

        // Undead and robots get their own kinds of names (undead can still get a normal one like "Ingrid the Hollow").
        static readonly string[] UndeadTitles =
        {
            "Soul-Eater", "the Devourer", "Grave-Warden", "the Unburied", "Bone-Gnawer", "the Hollow One",
            "Rotmaw", "Dusk-Walker", "the Withered", "Marrowfang", "Cryptkeeper", "the Restless",
            "Ashen Jaw", "the Forsaken", "Gravewhisper", "the Hungering Dark"
        };

        static readonly string[] UndeadEpithets =
        {
            "the Hollow", "the Unburied", "the Restless", "Gravebound", "the Withered",
            "Soulless", "Bonechill", "the Risen", "Ashenhand", "the Forsaken"
        };

        static readonly string[] RobotNames =
        {
            "Unit-7", "Clank", "Bolt-9", "Gizmo", "RX-78", "Servo", "Cog", "Vex-3",
            "Tin", "Axle", "Rivet", "Mk-IV", "Zeta-9", "Piston", "Sprocket", "Beta-12"
        };

        static readonly string[] RobotEpithets =
        {
            "the Rusty", "Overclocked", "the Obsolete", "Ironclad", "Steelheart", "the Tireless",
            "Gearbreaker", "the Calculated", "Scrapheap", "the Unfeeling", "Sparkplug", "Deadbolt"
        };

        /// <summary>
        /// A name that fits the model. Male / Female pick a matching first name; Either (and Not Set) any human name;
        /// Undead sometimes a human name and sometimes a grim title of its own; Robot a machine name.
        /// </summary>
        public static string RollName(FighterGender gender, Random rng)
        {
            if (gender == FighterGender.Robot)
                return RobotNames[rng.Next(RobotNames.Length)] + " " + RobotEpithets[rng.Next(RobotEpithets.Length)];

            if (gender == FighterGender.Undead)
            {
                double r = rng.NextDouble();
                if (r < 0.40) return UndeadTitles[rng.Next(UndeadTitles.Length)];
                bool grim = r < 0.75;   // "Ingrid the Hollow" (otherwise a normal epithet: "Ingrid Ironjaw")
                return HumanName(FighterGender.Either, rng, grim);
            }

            return HumanName(gender, rng, false);
        }

        /// <summary>True if the name starts with one of the female first names (used to pick a voice for "Either" fighters).</summary>
        public static bool IsFemaleName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            string first = displayName.Split(' ')[0];
            return Array.IndexOf(FemaleNames, first) >= 0;
        }

        static string HumanName(FighterGender gender, Random rng, bool undeadEpithet)
        {
            bool female;
            string first;
            if (gender == FighterGender.Male) { female = false; first = MaleNames[rng.Next(MaleNames.Length)]; }
            else if (gender == FighterGender.Female) { female = true; first = FemaleNames[rng.Next(FemaleNames.Length)]; }
            else
            {
                int i = rng.Next(MaleNames.Length + FemaleNames.Length);
                female = i >= MaleNames.Length;
                first = female ? FemaleNames[i - MaleNames.Length] : MaleNames[i];
            }

            if (undeadEpithet) return first + " " + UndeadEpithets[rng.Next(UndeadEpithets.Length)];

            string epithet;
            do { epithet = Epithets[rng.Next(Epithets.Length)]; }
            while (female && Array.IndexOf(MaleOnlyEpithets, epithet) >= 0);
            return first + " " + epithet;
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
