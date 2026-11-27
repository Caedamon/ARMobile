using System;
using System.Collections.Generic;
using Fighters;

namespace Game
{
    /// <summary>
    /// One choice on the fight schedule screen: a fight type against a specific opponent. The opponent is rolled
    /// when the card is made and saved with it, so closing the game never re-rolls your choices.
    /// </summary>
    [Serializable]
    public sealed class FightCard
    {
        public FightMode mode = FightMode.DeathMatch;

        /// <summary>The opponent (1 against 1 fights). In group fights: the strongest foe, shown on the card.</summary>
        public FighterData enemy = new FighterData();

        /// <summary>Group fights only: the hero's teammates and everyone on the other side. Last Hero Standing: foes = all the other fighters.</summary>
        public List<FighterData> allies = new List<FighterData>();
        public List<FighterData> foes = new List<FighterData>();

        public bool IsGroup
        {
            get { return mode == FightMode.Group || mode == FightMode.GroupDeathMatch || mode == FightMode.LastHero; }
        }

        /// <summary>Winning this finishes the schedule.</summary>
        public bool IsFinale
        {
            get { return mode == FightMode.Boss || mode == FightMode.GroupDeathMatch || mode == FightMode.LastHero; }
        }

        /// <summary>A card that can't be picked yet (fight types that are not built). Shows lockedTitle and "Coming soon".</summary>
        public bool locked;
        public string lockedTitle = "";
    }

    /// <summary>
    /// The schedule in progress: 3 fights (pick 1 of 3 each time), then a finale (pick 1 of 3). Saved in the save file.
    /// </summary>
    [Serializable]
    public sealed class RunState
    {
        public bool active;                 // false until the first time the schedule is opened
        public int fightsDone;              // regular fights finished in this run
        public List<FightCard> cards = new List<FightCard>();   // the 3 cards on offer right now
    }
}
