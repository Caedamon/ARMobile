using UnityEngine;

namespace Fighters
{
    /// <summary>DeathMatch: loser dies. Duel: nobody dies, the loser kneels. Boss: a death match against a much tougher fighter (the schedule's finale).</summary>
    public enum FightMode { DeathMatch, Duel, Boss, Group, GroupDeathMatch, LastHero }

    /// <summary>
    /// Which fighters the next arena should use. Temporary home until the roster and save system exist:
    /// if PlayerFighter is null the arena rolls one and keeps it for the rest of the play session.
    /// </summary>
    public static class FighterSession
    {
        /// <summary>Set by the GameController before an arena spawns. Duel = nobody dies, the loser kneels.</summary>
        public static FightMode Mode = FightMode.DeathMatch;

        // Group fights (set by the GameController before the arena spawns; the arena spawner reads and the controller clears them).
        // Allies fight on the hero's side. Foes are the other side. FreeForAll (Last Hero Standing): everybody is everybody's enemy.
        public static readonly System.Collections.Generic.List<FighterData> Allies = new System.Collections.Generic.List<FighterData>();
        public static readonly System.Collections.Generic.List<FighterData> Foes = new System.Collections.Generic.List<FighterData>();
        public static bool FreeForAll;

        public static void ClearGroup()
        {
            Allies.Clear();
            Foes.Clear();
            FreeForAll = false;
        }

        public static FighterData PlayerFighter;
        public static FighterData EnemyFighter;   // optional: force a specific enemy

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PlayerFighter = null;
            EnemyFighter = null;
            Mode = FightMode.DeathMatch;
            ClearGroup();
        }
    }
}
