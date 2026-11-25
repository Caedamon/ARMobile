using System;
using System.Collections.Generic;
using Weaponary;

namespace Fighters
{
    public sealed class LevelUpResult
    {
        public int xpGained;
        public int levelsGained;
        public int pointsGained;                              // free stat points earned (spent under Stats on the hub)
        public List<string> statGains = new List<string>();   // e.g. "MIGHT +1"
    }

    /// <summary>
    /// XP, levels and stat growth in one place. All numbers here are PROPOSALS: tune freely.
    ///  - XP reward = BaseXp x enemy tier x level difference x (player EDUCATION bonus)
    ///  - Each level-up: +1 to one random stat below the cap
    /// </summary>
    public static class ProgressionRules
    {
        public const int StatCap = 15;
        public const int BaseXp = 40;

        /// <summary>Chance that a defeated fighter survives. 0 = permadeath always (design doc proposes ~0.15).</summary>
        public const float DefeatSurvivalChance = 0f;

        /// <summary>Enemy level for a given player level: usually within +/-1; from level 6 on, sometimes a tougher +2/+3.</summary>
        public static int RollEnemyLevel(int playerLevel, Random rng)
        {
            int offset = rng.Next(-1, 2);
            if (playerLevel >= 6 && rng.NextDouble() < 0.15) offset = rng.Next(2, 4);
            return Math.Max(1, playerLevel + offset);
        }

        public static int XpToNext(int level) { return 100 + 50 * (level - 1); }

        static float TierMultiplier(FighterTier t)
        {
            switch (t)
            {
                case FighterTier.Pathetic:    return 0.6f;
                case FighterTier.HighQuality: return 1.5f;
                case FighterTier.Elite:       return 2.5f;
                default:                      return 1.0f;
            }
        }

        public static int XpReward(FighterData player, FighterData enemy)
        {
            float levelMod = Math.Max(0.5f, Math.Min(2f, 1f + 0.15f * (enemy.level - player.level)));
            float eduMod = Math.Max(0.7f, Math.Min(1.5f, 1f + 0.05f * (player.education - 5)));
            return Math.Max(1, (int)Math.Round(BaseXp * TierMultiplier(enemy.tier) * levelMod * eduMod));
        }

        public static LevelUpResult AddXp(FighterData d, int xp, Random rng)
        {
            var result = new LevelUpResult { xpGained = xp };
            d.xp += xp;
            while (d.xp >= XpToNext(d.level))
            {
                d.xp -= XpToNext(d.level);
                d.level++;
                result.levelsGained++;
                d.statPoints++;          // the player chooses where it goes (enemies still grow at random, see ScaleToLevel)
                result.pointsGained++;
            }
            return result;
        }

        /// <summary>Gives an enemy the levels (and stat growth) it needs to match a target level.</summary>
        public static void ScaleToLevel(FighterData d, int level, Random rng)
        {
            while (d.level < level)
            {
                d.level++;
                GrowStat(d, rng);
            }
            if (d.hasPreferredClass)
                GrantProfLevel(d, StyleKey(d.preferredClass), Math.Max(StartProfLevel(d.tier), d.level / 2));
        }

        static readonly string[] StatNames = { "MIGHT", "EGO", "EDUCATION", "INSIGHT" };

        static string GrowStat(FighterData d, Random rng)
        {
            var open = new List<int>();
            int[] v = { d.might, d.ego, d.education, d.insight };
            for (int i = 0; i < 4; i++) if (v[i] < StatCap) open.Add(i);
            if (open.Count == 0) return null;

            int pick = open[rng.Next(open.Count)];
            switch (pick)
            {
                case 0: d.might++; break;
                case 1: d.ego++; break;
                case 2: d.education++; break;
                default: d.insight++; break;
            }
            return StatNames[pick] + " +1";
        }

        // ------------------------------------------------------------------ weapon proficiency
        // Styles: Unarmed, OneHand, TwoHand, Dual. Level = floor(sqrt(xp / 12)), max 10.
        // Gain: ~3 XP per landed hit (scaled by EDUCATION) + 10 for a kill.
        // Effects: +4% damage per level with that style; weapon choice favours known styles.
        public const string Unarmed = "Unarmed";
        public const string OneHand = "OneHand";
        public const string Shield = "Shield";
        public const string TwoHand = "TwoHand";
        public const string Dual = "Dual";
        public const int ProfMaxLevel = 10;
        public static readonly string[] ProfKeys = { Unarmed, OneHand, Shield, TwoHand, Dual };

        public static string StyleKey(WeaponDefinition def)
        {
            return def == null ? Unarmed : StyleKey(def.weaponClass);
        }

        public static string StyleKey(WeaponClass cls)
        {
            switch (cls)
            {
                case WeaponClass.TwoHand:          return TwoHand;
                case WeaponClass.DualOneHand:      return Dual;
                case WeaponClass.OneHandAndShield:
                case WeaponClass.ShieldOnly:       return Shield;
                default:                           return OneHand;
            }
        }

        public static string StyleLabel(string key)
        {
            switch (key)
            {
                case Unarmed: return "Unarmed";
                case OneHand: return "One-handed";
                case Shield:  return "Sword & shield";
                case TwoHand: return "Two-handed";
                case Dual:    return "Dual wield";
                default:      return key;
            }
        }

        public static int ProfXp(FighterData d, string key)
        {
            foreach (var e in d.proficiency) if (e.style == key) return e.xp;
            return 0;
        }

        public static int XpForProfLevel(int level) { return 12 * level * level; }

        public static int ProfLevel(FighterData d, string key)
        {
            int lvl = (int)Math.Floor(Math.Sqrt(ProfXp(d, key) / 12.0));
            return Math.Min(ProfMaxLevel, Math.Max(0, lvl));
        }

        public static void AddProfXp(FighterData d, string key, int amount)
        {
            if (amount <= 0) return;
            foreach (var e in d.proficiency)
            {
                if (e.style == key) { e.xp += amount; return; }
            }
            d.proficiency.Add(new ProficiencyEntry { style = key, xp = amount });
        }

        public static void GrantProfLevel(FighterData d, string key, int level)
        {
            int target = XpForProfLevel(level);
            int cur = ProfXp(d, key);
            if (target > cur) AddProfXp(d, key, target - cur);
        }

        public const int ProfXpPerHit = 6;
        public const int ProfXpPerKill = 20;

        public static int ProfGain(FighterData d, bool kill)
        {
            float edu = Math.Max(0.7f, Math.Min(1.5f, 1f + 0.06f * (d.education - 5)));
            return (int)Math.Round(ProfXpPerHit * edu) + (kill ? ProfXpPerKill : 0);
        }

        // The fights use CombatTuning (Prof Damage Per Level) instead; this is only the fallback.
        public static float ProfDamageBonus(int level) { return 0.05f * level; }

        /// <summary>Weapon-choice affinity: preferred style counts double, and every proficiency level adds 15%.</summary>
        public static float Affinity(FighterData d, WeaponClass cls)
        {
            bool preferred = d.hasPreferredClass && d.preferredClass == cls;
            return (preferred ? 2f : 1f) + 0.15f * ProfLevel(d, StyleKey(cls));
        }

        public static int StartProfLevel(FighterTier t)
        {
            switch (t)
            {
                case FighterTier.HighQuality: return 1;
                case FighterTier.Elite:       return 2;
                default:                      return 0;
            }
        }

        public static int[] ProfSnapshot(FighterData d)
        {
            var a = new int[ProfKeys.Length];
            for (int i = 0; i < a.Length; i++) a[i] = ProfLevel(d, ProfKeys[i]);
            return a;
        }

        public static string ProfDiffText(int[] before, FighterData d)
        {
            if (before == null) return "";
            var parts = new List<string>();
            for (int i = 0; i < ProfKeys.Length && i < before.Length; i++)
            {
                int now = ProfLevel(d, ProfKeys[i]);
                if (now > before[i]) parts.Add(StyleLabel(ProfKeys[i]) + " proficiency Lv " + now);
            }
            return string.Join("   ", parts.ToArray());
        }

        public static string ProfLine(FighterData d)
        {
            var parts = new List<string>();
            foreach (var key in ProfKeys) parts.Add(StyleLabel(key) + " " + ProfLevel(d, key));
            return "Proficiency:  " + string.Join("   ", parts.ToArray());
        }
    }
}
