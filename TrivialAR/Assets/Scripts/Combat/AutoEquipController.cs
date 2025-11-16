using UnityEngine;
using Combat;
using Items;
using Weaponary;

namespace Combat
{
    [DisallowMultipleComponent]
    public sealed class AutoEquipController : MonoBehaviour
    {
        public float searchRadius = 5f;
        public float pickupRange = 0.8f;

        MeleeAI _ai;
        MeleeMotor _motor;
        MeleeAnimator _anim;
        AttackVariantPicker _picker;
        bool _equipped;
        WeaponPickup _target;

        void Awake()
        {
            _ai     = GetComponent<MeleeAI>();
            _motor  = GetComponent<MeleeMotor>();
            _anim   = GetComponent<MeleeAnimator>();
            _picker = GetComponent<AttackVariantPicker>();
        }

        void OnEnable()
        {
            _equipped = false;
            _target = null;
            if (_picker) _picker.MarkUnequipped(); // ensure Unarmed-only at start
        }

        void Update()
        {
            if (_equipped) return;

            if (_target == null) _target = FindClosestPickup();
            if (_target == null) { _equipped = true; return; }

            var pos = _target.transform.position;
            var d = (pos - transform.position); d.y = 0f;
            if (d.magnitude > pickupRange)
            {
                var step = Mathf.Clamp01(Time.deltaTime * 3f);
                _motor.StepTowards(pos, step, 1f, _anim);
                return;
            }

            if (!_target.TryReserve(gameObject)) { _target = null; return; }

            _target.Pick(gameObject);
            _equipped = true;
        }

        WeaponPickup FindClosestPickup()
        {
            var all = FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
            WeaponPickup best = null; float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < all.Length; i++)
            {
                var w = all[i]; if (!w) continue;
                var sqr = (w.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr && sqr <= searchRadius * searchRadius) { bestSqr = sqr; best = w; }
            }
            return best;
        }
    }
}
