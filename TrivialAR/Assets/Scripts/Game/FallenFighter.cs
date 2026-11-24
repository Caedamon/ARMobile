using System;
using Fighters;

namespace Game
{
    /// <summary>A fighter who died. Stored in the save file and shown on the graveyard pages of the Book.</summary>
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
    }
}
