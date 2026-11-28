using Fighters;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Reads the archetype modifiers (ArchetypeEffects, put on the fighter by FighterIdentity). Every value is neutral
    /// (x1 or +0) when the fighter has none, so fighters without a configured archetype fight exactly as before.
    /// </summary>
    public sealed partial class FighterBrain
    {
        ArchetypeEffects _archFx;
        bool _archLooked;

        ArchetypeEffects ArchFx
        {
            get
            {
                if (!_archLooked) { _archFx = GetComponent<ArchetypeEffects>(); _archLooked = true; }
                return _archFx;
            }
        }

        float ArchDamageMul { get { var f = ArchFx; return f != null ? f.DamageMul : 1f; } }
        float ArchAttackSpeedMul { get { var f = ArchFx; return f != null ? f.AttackSpeedMul : 1f; } }
        float ArchMoveMul { get { var f = ArchFx; return f != null ? f.MoveMul : 1f; } }
        float ArchDodgeAdd { get { var f = ArchFx; return f != null ? f.DodgeAdd : 0f; } }
        float ArchPoiseAdd { get { var f = ArchFx; return f != null ? f.PoiseAdd : 0f; } }
    }
}
