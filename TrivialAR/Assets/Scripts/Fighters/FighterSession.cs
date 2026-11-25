using UnityEngine;

namespace Fighters
{
    public enum FightMode { DeathMatch, Duel }

    /// <summary>
    /// Which fighters the next arena should use. Temporary home until the roster and save system exist:
    /// if PlayerFighter is null the arena rolls one and keeps it for the rest of the play session.
    /// </summary>
    public static class FighterSession
    {
        /// <summary>Set by the GameController before an arena spawns. Duel = nobody dies, the loser kneels.</summary>
        public static FightMode Mode = FightMode.DeathMatch;

        public static FighterData PlayerFighter;
        public static FighterData EnemyFighter;   // optional: force a specific enemy

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PlayerFighter = null;
            EnemyFighter = null;
            Mode = FightMode.DeathMatch;
        }
    }
}
