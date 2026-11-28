using System;
using UnityEngine;

namespace Combat
{
    /// <summary>How a fighter thinks. Basic = grab a weapon and rush. Smart = circles, punishes mistakes, defends when it matters. Boss = Smart plus phases.</summary>
    public enum AiTier { Basic = 0, Smart = 1, Boss = 2 }

    /// <summary>
    /// All the numbers behind the Smart and Boss behaviour, in one place. It sits on the ArenaFighterSpawner (Inspector: "Ai Tuning"),
    /// so you tune it without touching code. Basic fighters ignore all of it.
    /// </summary>
    [Serializable]
    public sealed class AiTuning
    {
        [Header("Circling (Smart + Boss): instead of standing still between swings, walk an arc around the enemy")]
        [Tooltip("Walking speed while circling, as a fraction of the normal walk.")]
        public float circleSpeedMul = 0.7f;
        [Tooltip("The ring it circles on, outside its own weapon reach, in body heights.")]
        public float ringExtraHeights = 0.10f;
        [Tooltip("It changes direction every so many seconds (random between these).")]
        public float circleFlipMin = 1.2f;
        public float circleFlipMax = 2.6f;
        [Tooltip("It stops circling and squares up this long before the next swing (seconds).")]
        public float circleUntil = 0.25f;
        [Tooltip("It only swings when within this extra distance of its reach (body heights); further away it steps in first.")]
        public float strikeSlackHeights = 0.10f;

        [Header("Punishing (Smart + Boss): hit the enemy right away while it is recovering")]
        public bool punishVulnerable = true;

        [Header("Hit and run (Smart + Boss): after a swing, sometimes a quick step away (no invulnerability, unlike a real dodge)")]
        [Range(0f, 1f)] public float hitAndRunChance = 0.35f;
        [Tooltip("At least this many seconds between two such steps.")]
        public float hitAndRunGap = 2.2f;

        [Header("Defence (Smart + Boss)")]
        [Tooltip("Dodge / block chance is multiplied by this.")]
        public float defenceMul = 1.3f;
        [Tooltip("Do not waste a dodge or block on a swing that cannot reach anyway.")]
        public bool defendOnlyIfReachable = true;
        [Tooltip("After a successful dodge or block, strike back at once.")]
        public bool counterAfterDefence = true;

        [Header("Mood (Smart + Boss), from health")]
        [Tooltip("Below this fraction of its own health it gets careful: longer between swings, more stepping away.")]
        public float cautiousBelow = 0.35f;
        public float cautiousCooldownMul = 1.35f;
        [Tooltip("When the enemy is below this fraction of health it goes for the kill: shorter between swings.")]
        public float killBelow = 0.30f;
        public float killCooldownMul = 0.7f;

        [Header("Targets in group fights (Smart + Boss)")]
        [Tooltip("0 = nearest enemy. Higher = prefers enemies with less health. (score = distance x (1 + this x health fraction))")]
        public float weakBias = 0.8f;
        [Tooltip("It only changes target when the new one scores below this fraction of the old one.")]
        [Range(0.3f, 1f)] public float keepTargetBias = 0.75f;

        [Header("Choosing the attack (Smart + Boss)")]
        [Tooltip("Weight left for a move that cannot reach the enemy from here (1 = no change, 0 = never).")]
        [Range(0f, 1f)] public float outOfReachWeight = 0.2f;

        [Header("Weapon race (Smart + Boss)")]
        [Tooltip("Lower = keeps away from weapons the enemy will probably get first.")]
        public float seekerContestedPenalty = 0.15f;
        public float seekerRaceMargin = 0.9f;

        [Header("Boss phases (Boss only). Health fractions where it changes.")]
        public float phase1At = 0.66f;
        public float phase2At = 0.33f;
        [Tooltip("The roar when a phase starts. It is stuck in place this long, a chance to hit it.")]
        public float roarMaxSeconds = 1.2f;
        [Header("   Phase 1 / Phase 2 changes")]
        public float phase1CooldownMul = 0.8f;
        public float phase2CooldownMul = 0.6f;
        public float phase1SpeedMul = 1.1f;
        public float phase2SpeedMul = 1.25f;
        public float phase1DamageMul = 1.0f;
        public float phase2DamageMul = 1.15f;
        [Tooltip("Extra chance to shrug off a hit while swinging.")]
        public float phase1Poise = 0.25f;
        public float phase2Poise = 0.5f;
        [Header("   Combos: after a swing that hit, the chance to swing again at once")]
        public float phase0ComboChance = 0.15f;
        public float phase1ComboChance = 0.3f;
        public float phase2ComboChance = 0.45f;
        public float comboCooldown = 0.08f;
    }
}
