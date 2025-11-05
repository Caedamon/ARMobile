using UnityEngine;

namespace Characters
{
    public class KaijuBody : MonoBehaviour
    {
        [Header("Knockback")] [Tooltip("Multiplier applied to knockback distance.")]
        public float knockbackMultiplier = 1f;

        [Tooltip("Optional max horizontal displacement per hit.")]
        public float maxStep = 0.6f;

        [Tooltip("If present, this Rigidbody can be briefly toggled for physics-friendly nudges.")]
        public Rigidbody optionalRb;

        /// <summary>
        /// Applies a simple horizontal knockback by moving the transform.
        /// Works with kinematic bodies; ignores vertical displacement.
        /// </summary>
        public void ApplyKnockback(Vector3 fromTargetDirection, float baseForce = 0.35f)
        {
            Vector3 dir = fromTargetDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            float force = Mathf.Max(0f, baseForce) * Mathf.Max(0.1f, knockbackMultiplier);
            if (maxStep > 0f) force = Mathf.Min(force, maxStep);

            Vector3 delta = dir.normalized * force;

            // Prefer transform move for deterministic nudge
            if (optionalRb == null || optionalRb.isKinematic)
            {
                transform.position += delta;
            }
            else
            {
                // For non-kinematic bodies, add an impulse in the horizontal plane
                optionalRb.AddForce(delta / Time.fixedDeltaTime, ForceMode.Acceleration);
            }
        }
    }
}