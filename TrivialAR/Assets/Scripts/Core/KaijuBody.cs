// File: Scripts/Core/KaijuBody.cs
using UnityEngine;

namespace Characters
{
    /// <summary>
    /// Tiny knockback helper. Horizontal-only nudge; works with kinematic bodies.
    /// </summary>
    public class KaijuBody : MonoBehaviour
    {
        [Header("Knockback")]
        [Tooltip("Multiplier applied to knockback distance.")]
        public float knockbackMultiplier = 1f;

        [Tooltip("Optional max horizontal displacement per hit.")]
        public float maxStep = 0.6f;

        [Tooltip("If present, this Rigidbody can be briefly toggled for physics-friendly nudges.")]
        public Rigidbody optionalRb;

        /// <summary>
        /// Applies a horizontal knockback from the attacker towards this unit.
        /// </summary>
        public void ApplyKnockback(Vector3 fromTargetDirection, float baseForce = 0.35f)
        {
            Vector3 dir = fromTargetDirection; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;

            float force = Mathf.Max(0f, baseForce) * Mathf.Max(0.1f, knockbackMultiplier);
            if (maxStep > 0f) force = Mathf.Min(force, maxStep);

            Vector3 delta = dir.normalized * force;

            if (optionalRb == null || optionalRb.isKinematic)
            {
                // deterministic nudge
                transform.position += delta;
            }
            else
            {
                // physics-friendly push
                optionalRb.AddForce(delta / Mathf.Max(Time.fixedDeltaTime, 1e-3f), ForceMode.Acceleration);
            }
        }
    }
}