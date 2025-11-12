using UnityEngine;

namespace Combat
{
    public sealed class PersonalSpace : MonoBehaviour
    {
        [Tooltip("Collider used to compute personal radius (drag your Sphere/Capsule/CC here).")]
        public Collider sourceCollider;

        [Tooltip("Extra padding added to the computed radius.")]
        public float padding = 0.01f;

        // Horizontal radius in meters (XZ plane) plus padding.
        public float Radius2D
        {
            get
            {
                float r = ComputeRadiusXZ(sourceCollider);
                return Mathf.Max(0f, r + Mathf.Max(0f, padding));
            }
        }

        // Compute flat (XZ) radius of given collider under transform scale.
        static float ComputeRadiusXZ(Collider col)
        {
            if (!col) return 0f;

            var t = col.transform;
            // uniform-ish XZ scale for radius
            float xzScale = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z));

            if (col is SphereCollider sc)
            {
                return Mathf.Abs(sc.radius) * xzScale;
            }
            if (col is CapsuleCollider cc)
            {
                // Capsule radius applies on the 2 axes perpendicular to 'direction'
                float baseR = Mathf.Abs(cc.radius);
                return baseR * xzScale;
            }
#if UNITY_2021_2_OR_NEWER
            if (col is CharacterController chr)
            {
                return Mathf.Abs(chr.radius) * xzScale;
            }
#endif
            // Fallback: approximate from bounds (half of diagonal in XZ)
            var b = col.bounds;
            var xz = new Vector2(b.extents.x, b.extents.z);
            return xz.magnitude;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!sourceCollider) return;
            float r = Radius2D;
            if (r <= 0f) return;
            var p = transform.position;
            UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.35f);
            UnityEditor.Handles.DrawWireDisc(p, Vector3.up, r);
        }
#endif
    }
}