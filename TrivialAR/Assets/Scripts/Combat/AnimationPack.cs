using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// The extra animations the real-time fighters use: class stances, directional dodges, hit/death variants,
    /// pickup and spawn. Filled in by Tools > Kaiju > Combat > Setup Animation Pack.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationPack", menuName = "Kaiju/Animation Pack")]
    public sealed class AnimationPack : ScriptableObject
    {
        [Header("Clips the controller states play by default (read from BaseMelee)")]
        public AnimationClip idleOriginal;
        public AnimationClip dodgeOriginal;
        public AnimationClip hitOriginal;
        public AnimationClip deathOriginal;

        [Header("Combat stances (null = keep the default idle)")]
        public AnimationClip idleUnarmed;
        public AnimationClip idleTwoHand;

        [Header("Variants")]
        public List<AnimationClip> hitClips = new List<AnimationClip>();
        public List<AnimationClip> deathClips = new List<AnimationClip>();
        public AnimationClip dodgeBack;
        public AnimationClip dodgeLeft;
        public AnimationClip dodgeRight;

        [Header("Extra states (added to the controller by the setup tool)")]
        public AnimationClip pickupClip;
        public AnimationClip spawnClip;
        public bool controllerHasPickup;
        public bool controllerHasSpawn;

        [Header("Shield (a state is added to the controller by the setup tool)")]
        public AnimationClip blockClip;
        public AnimationClip blockHitClip;
        public bool controllerHasBlock;
    }
}
