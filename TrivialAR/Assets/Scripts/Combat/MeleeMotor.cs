using System;
using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    public sealed class MeleeMotor : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Max distance this unit may move per 'round' when AI asks to Move.")]
        public float moveStepMeters = 0.05f;

        [Tooltip("Fallback stop distance if no PersonalSpace colliders are present.")]
        public float attackRange = 0.10f;

        [Tooltip("How fast the unit walks while moving (meters/sec).")]
        public float moveSpeedMetersPerSec = 0.6f;

        [Tooltip("AI sight radius (flat).")]
        public float sightRadius = 20f;

        // Runtime
        public bool IsMoving { get; private set; }

        Transform _t;
        Vector3 _targetFlat;
        float _explicitStop;
        float _speed;
        float _maxThisTurn;
        float _traveled;
        Action _onDone;

        PersonalSpace _selfSpace;
        PersonalSpace _targetSpace;
        MeleeAnimator _anim;

        void Awake()
        {
            _t = transform;
            _selfSpace  = GetComponent<PersonalSpace>(); // optional
            _anim       = GetComponent<MeleeAnimator>(); // optional
        }

        public void StepTowards(Vector3 worldTarget, float moveBudget, float stopAtDistance, MeleeAnimator animator = null)
        {
            _targetSpace = null;
            StartMoveInternal(worldTarget, stopAtDistance, moveBudget, animator);
        }

        // Target-aware smooth move.
        public void StartMoveTo(MeleeAI target, float moveBudget, float fallbackStopAtDistance, MeleeAnimator animator = null)
        {
            _targetSpace = target ? target.GetComponent<PersonalSpace>() : null;
            var pos = target ? target.transform.position : _t.position;
            StartMoveInternal(pos, fallbackStopAtDistance, moveBudget, animator);
        }

        public void CancelMove(Action onDone = null)
        {
            if (!IsMoving) { onDone?.Invoke(); return; }
            IsMoving = false;
            _onDone?.Invoke();
            onDone?.Invoke();
            _onDone = null;
            (_anim ?? GetComponent<MeleeAnimator>())?.PlayIdle();
        }

        void StartMoveInternal(Vector3 worldTarget, float explicitStopAtDistance, float moveBudget, MeleeAnimator animator)
        {
            _anim = _anim ?? animator ?? GetComponent<MeleeAnimator>();

            _targetFlat     = worldTarget; _targetFlat.y = 0f;
            _explicitStop   = Mathf.Max(0f, explicitStopAtDistance);
            _maxThisTurn    = Mathf.Max(0f, moveBudget);
            _traveled       = 0f;
            _speed          = Mathf.Max(1e-4f, moveSpeedMetersPerSec);
            _onDone         = () => { (_anim ?? GetComponent<MeleeAnimator>())?.PlayIdle(); };

            IsMoving = true;

            // Face and start walk loop immediately
            var pos = _t.position; pos.y = 0f;
            var to  = _targetFlat - pos;
            if (to.sqrMagnitude > 1e-8f)
                _t.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            (_anim ?? GetComponent<MeleeAnimator>())?.PlayWalk();
        }

        void Update()
        {
            if (!IsMoving) return;

            var posFlat = _t.position; posFlat.y = 0f;
            var to = _targetFlat - posFlat;
            var dist = to.magnitude;
            var dir = dist > 1e-6f ? to / dist : _t.forward;

            // Stop distance: sum of personal-space radii (if any) OR explicit fallback.
            float stopAt = 0f;
            if (_selfSpace)  stopAt += _selfSpace.Radius2D;
            if (_targetSpace) stopAt += _targetSpace.Radius2D;
            stopAt = Mathf.Max(stopAt, _explicitStop);

            float remainToStop = Mathf.Max(0f, dist - stopAt);
            float remainBudget = Mathf.Max(0f, _maxThisTurn - _traveled);

            if (remainToStop <= 1e-5f || remainBudget <= 1e-5f)
            {
                IsMoving = false;
                var cb = _onDone; _onDone = null;
                cb?.Invoke();
                return;
            }

            float step = Mathf.Min(_speed * Time.deltaTime, remainToStop, remainBudget);
            _t.rotation = Quaternion.LookRotation(dir, Vector3.up);
            _t.position += dir * step;
            _traveled   += step;
        }

        public float FlatDistanceTo(Vector3 worldPos)
        {
            var a = _t.position; a.y = 0f; worldPos.y = 0f;
            return Vector3.Distance(a, worldPos);
        }

        // Unified stop distance used by both decision and movement.
        public float StopDistanceTo(MeleeAI target, float minFallback = 0f)
        {
            float stop = 0f;
            var ts = target ? target.GetComponent<PersonalSpace>() : null;
            if (_selfSpace) stop += _selfSpace.Radius2D;
            if (ts)         stop += ts.Radius2D;
            float fb = (minFallback > 0f ? minFallback : attackRange);
            return Mathf.Max(stop, fb);
        }

        // Uses same rule as movement so AI doesn’t loop “Move” forever.
        public bool InMeleeRange(MeleeAI target, float extra = 1e-4f)
        {
            if (!target) return false;
            var dist = FlatDistanceTo(target.transform.position);
            return dist <= StopDistanceTo(target) + extra;
        }
    }
}