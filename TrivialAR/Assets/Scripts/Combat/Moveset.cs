using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>One attack a fighter can perform. Timings are fractions (0-1) of the clip.</summary>
    [Serializable]
    public sealed class AttackMove
    {
        public string label = "Attack";
        public AnimationClip clip;
        [Tooltip("Playback speed multiplier for this move.")]
        [Min(0.2f)] public float speed = 1f;
        [Tooltip("Fraction of the clip at which the strike lands (damage is applied here).")]
        [Range(0.05f, 0.95f)] public float hitAt = 0.45f;
        [Tooltip("Fraction of the clip at which the fighter may act again.")]
        [Range(0.3f, 1f)] public float endAt = 0.92f;
        [Min(0.1f)] public float damageMul = 1f;
        [Tooltip("Multiplier on the weapon's reach for this move.")]
        [Min(0.5f)] public float reachMul = 1f;
        [Tooltip("Step forward during the wind-up, in model meters (scaled by the fighter's size).")]
        [Min(0f)] public float lungeModelMeters = 0.25f;
        [Tooltip("Knockback on a hit, in model meters (scaled by the fighter's size).")]
        [Min(0f)] public float knockbackModelMeters = 0.35f;
        [Tooltip("Multiplies the victim's hit-stun time.")]
        [Min(0f)] public float stagger = 1f;
        [Tooltip("Relative chance of being picked.")]
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>All attacks for one weapon class (or unarmed), plus pacing.</summary>
    [CreateAssetMenu(fileName = "NewMoveset", menuName = "Kaiju/Moveset")]
    public sealed class Moveset : ScriptableObject
    {
        public string displayName = "Moveset";
        public List<AttackMove> attacks = new List<AttackMove>();
        [Tooltip("Seconds between attacks (random in this range).")]
        public Vector2 cooldownRange = new Vector2(0.3f, 0.6f);

        /// <summary>Weighted random pick that avoids repeating 'avoid' when there is a choice.</summary>
        public AttackMove Pick(AttackMove avoid)
        {
            float total = 0f;
            foreach (var a in attacks)
                if (Eligible(a, avoid)) total += a.weight;
            if (total <= 0f) return null;

            float r = UnityEngine.Random.value * total;
            AttackMove last = null;
            foreach (var a in attacks)
            {
                if (!Eligible(a, avoid)) continue;
                last = a;
                r -= a.weight;
                if (r <= 0f) return a;
            }
            return last;
        }

        bool Eligible(AttackMove a, AttackMove avoid)
        {
            return a != null && a.weight > 0f && !(a == avoid && attacks.Count > 1);
        }
    }
}