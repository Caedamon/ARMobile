using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeAnimator))]
    public sealed class MeleeAnimatorCompat : MonoBehaviour
    {
        MeleeAnimator _a;

        void Awake() { _a = GetComponent<MeleeAnimator>(); }

        public float PickDodge()
        {
            if (_a != null) return Mathf.Max(0.3f, _a.dodgeDuration);
            return 0.6f;
        }

        public float PlayDodge()
        {
            if (_a != null) return _a.PlayDodge("Dodge"); // default key
            return 0.6f;
        }

        public float PickAttack()
        {
            if (_a == null) return 0.7f;
            return (_a.attackHeavyDuration > _a.attackLightDuration)
                ? _a.attackHeavyDuration
                : _a.attackLightDuration;
        }

        public float PickHit()
        {
            if (_a != null) return Mathf.Max(0.3f, _a.hitDuration);
            return 0.45f;
        }

        public float PlayDeath()
        {
            if (_a != null) return _a.PlayDie();
            return 1.2f;
        }
    }
}