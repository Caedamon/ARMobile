namespace Fighters
{
    /// <summary>
    /// The tuning sheet: how stats turn into gameplay numbers. Change numbers here, nowhere else.
    /// Already handled elsewhere (CharacterStats / DamageCalculator):
    ///   MIGHT   -> +5% melee damage per point
    ///   INSIGHT -> crit chance and crit multiplier
    ///   LUCK    -> crit chance (hidden)
    /// EGO and EDUCATION have no effect yet.
    /// </summary>
    public static class StatRules
    {
        // MIGHT -> max HP
        public const float HpBase = 120f;
        public const float HpPerMight = 16f;
        public const float HpPerLevel = 8f;

        // INSIGHT -> how far (world meters) a fighter notices weapons
        public const float SearchBase = 0.5f;
        public const float SearchPerInsight = 0.1f;

        public static float MaxHP(FighterData d)
        {
            return HpBase + HpPerMight * d.might + HpPerLevel * (d.level - 1);
        }

        public static float SearchRadius(FighterData d)
        {
            return SearchBase + SearchPerInsight * d.insight;
        }
    }
}
