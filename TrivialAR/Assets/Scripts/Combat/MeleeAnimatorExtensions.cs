using UnityEngine;

namespace Combat
{
    public static class MeleeAnimatorExtensions
    {
        public static string PickDodge(this MeleeAnimator anim)
        {
            return "Dodge";
        }
        
        public static string PickAttack(this MeleeAnimator anim)
        {
            if (anim == null) return "Attack_Light";
            // bias to heavy if it's clearly longer
            if (anim.attackHeavyDuration > anim.attackLightDuration + 0.05f)
                return "Attack_Heavy";
            return "Attack_Light";
        }
        public static string PickHit(this MeleeAnimator anim)
        {
            return "Hit";
        }
    }
}