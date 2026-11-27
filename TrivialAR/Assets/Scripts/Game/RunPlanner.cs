using System;
using System.Collections.Generic;
using Fighters;

namespace Game
{
    /// <summary>
    /// Rolls the cards for the fight schedule and rates how dangerous they are.
    /// No Unity types here on purpose (plain C#), so it is easy to reason about and to test.
    /// </summary>
    public static class RunPlanner
    {
        // Opponent level compared to the hero's. Mostly even, sometimes easier, sometimes a risky one.
        static readonly int[] LevelOffsets = { -1, 0, 0, 1, 1, 2 };

        /// <summary>
        /// Three cards for one of the regular fights: a mix of Death Matches, Duels and Group Fights.
        /// A Group Fight is the hero + groupAllies teammates against groupFoes opponents.
        /// </summary>
        public static List<FightCard> RollRegular(FighterCatalog catalog, FighterData hero, Random rng, int groupAllies, int groupFoes)
        {
            var offsets = new int[3];
            var modes = new FightMode[3];
            for (int i = 0; i < 3; i++)
            {
                offsets[i] = LevelOffsets[rng.Next(LevelOffsets.Length)];
                double r = rng.NextDouble();
                modes[i] = r < 0.40 ? FightMode.DeathMatch : (r < 0.70 ? FightMode.Duel : FightMode.Group);
            }

            // Always a real choice: not three of the same fight type, and always one opponent that is not tougher than you.
            if (modes[0] == modes[1] && modes[1] == modes[2])
            {
                var other = modes[0] == FightMode.DeathMatch ? FightMode.Group : FightMode.DeathMatch;
                modes[rng.Next(3)] = other;
            }
            if (offsets[0] > 0 && offsets[1] > 0 && offsets[2] > 0)
                offsets[rng.Next(3)] = 0;

            var cards = new List<FightCard>();
            for (int i = 0; i < 3; i++)
            {
                if (modes[i] == FightMode.Group)
                {
                    cards.Add(MakeGroupCard(catalog, hero, rng, FightMode.Group, groupAllies, groupFoes));
                    continue;
                }
                var enemy = FighterGenerator.Generate(catalog, FighterSide.Enemy, rng, hero.level);
                ProgressionRules.ScaleToLevel(enemy, Math.Max(1, hero.level + offsets[i]), rng);
                cards.Add(new FightCard { mode = modes[i], enemy = enemy });
            }
            return cards;
        }

        /// <summary>The finale: group death match, boss fight, last hero standing.</summary>
        public static List<FightCard> RollFinale(FighterCatalog catalog, FighterData hero, int bossLevelBonus, Random rng,
                                                 int finaleAllies, int finaleFoes, int lastHeroRivals)
        {
            var cards = new List<FightCard>();
            cards.Add(MakeGroupCard(catalog, hero, rng, FightMode.GroupDeathMatch, finaleAllies, finaleFoes));
            cards.Add(new FightCard { mode = FightMode.Boss, enemy = RollBoss(catalog, hero, bossLevelBonus, rng) });
            cards.Add(MakeGroupCard(catalog, hero, rng, FightMode.LastHero, 0, lastHeroRivals));
            return cards;
        }

        // Teammates and opponents for a group fight. Last Hero Standing has no teammates: everyone else is a rival.
        static FightCard MakeGroupCard(FighterCatalog catalog, FighterData hero, Random rng, FightMode mode, int allies, int foes)
        {
            var card = new FightCard { mode = mode };

            for (int i = 0; i < Math.Max(0, allies); i++)
            {
                var a = FighterGenerator.Generate(catalog, FighterSide.Player, rng, hero.level);
                ProgressionRules.ScaleToLevel(a, Math.Max(1, hero.level + rng.Next(-1, 1)), rng);   // a level below you, or level with you
                card.allies.Add(a);
            }

            FighterData strongest = null;
            for (int i = 0; i < Math.Max(1, foes); i++)
            {
                var f = FighterGenerator.Generate(catalog, FighterSide.Enemy, rng, hero.level);
                ProgressionRules.ScaleToLevel(f, Math.Max(1, hero.level + rng.Next(-1, 2)), rng);   // -1, 0 or +1
                card.foes.Add(f);
                if (strongest == null || Power(f) > Power(strongest)) strongest = f;
            }
            card.enemy = strongest;
            return card;
        }

        // The best tier out of many rolls (Elite if one turns up), scaled above the hero's level.
        static FighterData RollBoss(FighterCatalog catalog, FighterData hero, int levelBonus, Random rng)
        {
            FighterData best = null;
            for (int i = 0; i < 300; i++)
            {
                var e = FighterGenerator.Generate(catalog, FighterSide.Enemy, rng, hero.level);
                if (best == null || e.tier > best.tier) best = e;
                if (best.tier == FighterTier.Elite) break;
            }
            ProgressionRules.ScaleToLevel(best, Math.Max(1, hero.level + levelBonus), rng);
            best.displayName = "Boss " + best.displayName;
            return best;
        }

        // ------------------------------------------------------------------ how dangerous is this fight
        static float Power(FighterData f) { return f.level * 2f + f.VisibleTotal; }

        /// <summary>0 = Easy, 1 = Even, 2 = Tough, 3 = Deadly (compared to the hero).</summary>
        public static int Danger(FighterData hero, FighterData enemy)
        {
            return FromRatio(Power(enemy) / Math.Max(1f, Power(hero)));
        }

        /// <summary>Same for a whole card: your team (hero + allies) against theirs. Last Hero Standing is always Deadly.</summary>
        public static int Danger(FighterData hero, FightCard card)
        {
            if (card == null || card.enemy == null) return 1;
            if (card.mode == FightMode.LastHero) return 3;
            if (!card.IsGroup) return Danger(hero, card.enemy);

            float mine = Power(hero), theirs = 0f;
            foreach (var a in card.allies) mine += Power(a);
            foreach (var f in card.foes) theirs += Power(f);
            return FromRatio(theirs / Math.Max(1f, mine));
        }

        static int FromRatio(float ratio)
        {
            if (ratio < 0.85f) return 0;
            if (ratio < 1.15f) return 1;
            if (ratio < 1.45f) return 2;
            return 3;
        }

        public static string DangerLabel(int danger)
        {
            switch (danger)
            {
                case 0:  return "EASY";
                case 1:  return "EVEN";
                case 2:  return "TOUGH";
                default: return "DEADLY";
            }
        }

        // ------------------------------------------------------------------ words on a card
        public static string ModeLabel(FightMode mode)
        {
            switch (mode)
            {
                case FightMode.Duel:            return "DUEL";
                case FightMode.Boss:            return "BOSS FIGHT";
                case FightMode.Group:           return "GROUP FIGHT";
                case FightMode.GroupDeathMatch: return "GROUP DEATH MATCH";
                case FightMode.LastHero:        return "LAST HERO STANDING";
                default:                        return "DEATH MATCH";
            }
        }

        public static string RulesLine(FightCard c)
        {
            switch (c.mode)
            {
                case FightMode.Duel:            return "Nobody dies";
                case FightMode.Boss:            return "Loser dies  -  big reward";
                case FightMode.Group:           return "Your team fights on if you fall";
                case FightMode.GroupDeathMatch: return "Team fights on if you fall  -  big reward";
                case FightMode.LastHero:        return "Only one survives  -  big reward";
                default:                        return "Loser dies";
            }
        }

        /// <summary>The big name line: the opponent, or who you are up against.</summary>
        public static string CardName(FightCard c)
        {
            if (c.enemy == null) return "";
            if (c.mode == FightMode.LastHero) return (c.foes.Count + 1) + " fighters, one survivor";
            if (c.IsGroup)
            {
                int more = c.foes.Count - 1;
                return c.enemy.displayName + (more > 0 ? "  and " + more + " more" : "");
            }
            return c.enemy.displayName;
        }

        /// <summary>The small line under the name.</summary>
        public static string CardInfo(FightCard c)
        {
            if (c.enemy == null) return "";
            var e = c.enemy;
            if (c.mode == FightMode.LastHero)
                return "Toughest rival:  Lv " + e.level + "  -  " + FighterTiers.Label(e.tier);
            if (c.IsGroup)
                return "Your team of " + (1 + c.allies.Count) + " vs " + c.foes.Count + "  -  toughest: Lv " + e.level + " " + FighterTiers.Label(e.tier);
            return "Lv " + e.level + "  -  " + FighterTiers.Label(e.tier) + "  -  " + e.PreferredStyleLabel;
        }
    }
}
