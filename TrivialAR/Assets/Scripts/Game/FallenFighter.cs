using System;
using Fighters;

namespace Game
{
    public enum Pronoun { He, She, It }
    public enum FallenKind { Human, Undead, Robot }

    /// <summary>A fighter who died. Stored in the save file and shown on the graveyard pages of the Book (two pages each).</summary>
    [Serializable]
    public sealed class FallenFighter
    {
        public string displayName = "Unnamed";
        public string modelId;
        public FighterTier tier = FighterTier.Normal;
        public int level = 1;
        public int wins;
        public int fights;            // total fights, the last one being the one that killed them
        public string killerName = "";
        public int killerLevel;

        // Added with the two-page graves. Heroes who died before this have detailed = false and get a shorter text.
        public bool detailed;
        public FightMode mode = FightMode.DeathMatch;   // the kind of fight they died in
        public Pronoun pronoun = Pronoun.He;
        public FallenKind heroKind = FallenKind.Human;
        public FallenKind killerKind = FallenKind.Human;
        public bool killerIsBoss;
        public int bossesDefeated;
        public int bestStreak;
        public string bestStyle = "";    // e.g. "One-handed"
        public int bestStyleLevel;

        public FallenFighter() { }

        public FallenFighter(FighterData d, FighterData killer)
        {
            displayName = d.displayName;
            modelId = d.modelId;
            tier = d.tier;
            level = d.level;
            wins = d.wins;
            fights = d.fights;
            killerName = killer != null ? killer.displayName : "";
            killerLevel = killer != null ? killer.level : 0;
        }

        /// <summary>The full record made when a hero dies: how, against whom, and what they achieved.</summary>
        public static FallenFighter Record(FighterCatalog catalog, FighterData hero, FighterData killer, FightMode mode)
        {
            var f = new FallenFighter(hero, killer);
            f.detailed = true;
            f.mode = mode;

            var hg = catalog != null ? catalog.GenderOf(hero.modelId, FighterSide.Player) : FighterGender.NotSet;
            f.pronoun = PronounFor(hg, hero.displayName);
            f.heroKind = KindOf(hg);
            if (killer != null)
            {
                var kg = catalog != null ? catalog.GenderOf(killer.modelId, FighterSide.Enemy) : FighterGender.NotSet;
                f.killerKind = KindOf(kg);
            }
            f.killerIsBoss = mode == FightMode.Boss && killer != null;

            f.bossesDefeated = hero.bossesDefeated;
            f.bestStreak = hero.bestStreak;

            int best = 0;
            foreach (var key in ProgressionRules.ProfKeys)
            {
                int lv = ProgressionRules.ProfLevel(hero, key);
                if (lv > best) { best = lv; f.bestStyle = ProgressionRules.StyleLabel(key); }
            }
            f.bestStyleLevel = best;
            return f;
        }

        /// <summary>The pronoun to use. Old graves (no record) guess from the first name.</summary>
        public Pronoun Pron()
        {
            if (detailed) return pronoun;
            return FighterGenerator.IsFemaleName(displayName) ? Pronoun.She : Pronoun.He;
        }

        static Pronoun PronounFor(FighterGender g, string name)
        {
            switch (g)
            {
                case FighterGender.Male: return Pronoun.He;
                case FighterGender.Female: return Pronoun.She;
                case FighterGender.Robot: return Pronoun.It;
                default: return FighterGenerator.IsFemaleName(name) ? Pronoun.She : Pronoun.He;   // Either / Undead / Not Set: go by the first name
            }
        }

        static FallenKind KindOf(FighterGender g)
        {
            if (g == FighterGender.Undead) return FallenKind.Undead;
            if (g == FighterGender.Robot) return FallenKind.Robot;
            return FallenKind.Human;
        }
    }
}
