using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    public sealed class MeleeAnimator : MonoBehaviour
    {
        [Header("Mode (auto)")]
        [SerializeField] bool useAnimator; // auto-detected

        [Header("Animator Params")]
        public string speedParam = "Speed";
        public string attackTrigger = "Attack";
        public string heavyBool = "Heavy";
        public string blockBool = "Block";
        public string hitTrigger = "Hit";
        public string dieTrigger = "Die";

        [Header("Legacy Animation Clip Names (only if no Animator)")]
        public string clipIdle = "Idle";
        public string clipWalk = "Walking_A";
        public string clipAttackLight = "1H_Melee_Attack_Slice_Horizontal";
        public string clipAttackHeavy = "2H_Melee_Attack_Spinning";
        public string clipBlockLoop = "Blocking";
        public string clipHit = "Hit_A";
        public string clipDie = "Death_A";

        [Header("Durations (returned to AI)")]
        public float idleDuration = 0.25f;
        public float walkTickDuration = 0.50f;
        public float attackLightDuration = 0.70f;
        public float attackHeavyDuration = 0.95f;
        public float blockEnterExitDuration = 0.20f;
        public float hitDuration = 0.45f;
        public float dieDuration = 1.20f;

        Animator _anim;
        Animation _legacy;
        float _lastSpeed;

        void Awake()
        {
            _anim = GetComponent<Animator>();
            _legacy = GetComponent<Animation>();
            useAnimator = _anim != null; // prefer Animator when present
            if (!useAnimator && _legacy == null)
            {
                // Failsafe: stay silent rather than throw; callers only depend on returned durations.
                Debug.LogWarning($"[{name}] MeleeAnimator: no Animator or Animation found. Calls will no-op.");
            }
        }

        // Locomotion
        public float PlayIdle()
        {
            if (useAnimator && _anim) { SetSpeed(0f); }
            else if (_legacy) { CrossFadeSafe(clipIdle, 0.1f, true); }
            return idleDuration;
        }

        public float PlayWalk(float speed01 = 1f)
        {
            if (useAnimator && _anim) { SetSpeed(Mathf.Clamp01(speed01)); }
            else if (_legacy) { CrossFadeSafe(clipWalk, 0.1f, true); }
            return walkTickDuration;
        }

        public void EnsureIdlePlaying()
        {
            if (useAnimator)
            {
                if (_lastSpeed > 0f) SetSpeed(0f);
            }
            else if (_legacy)
            {
                if (!_legacy.IsPlaying(clipIdle)) CrossFadeSafe(clipIdle, 0.1f, true);
            }
        }

        public void SetSpeed(float v)
        {
            _lastSpeed = v;
            if (useAnimator && _anim) _anim.SetFloat(speedParam, v);
        }

        // Combat
        public float PlayAttack(bool heavy = false)
        {
            if (useAnimator && _anim)
            {
                _anim.SetBool(heavyBool, heavy);
                _anim.ResetTrigger(attackTrigger);
                _anim.SetTrigger(attackTrigger);
            }
            else if (_legacy)
            {
                var clip = heavy ? clipAttackHeavy : clipAttackLight;
                CrossFadeSafe(clip, 0.05f, false);
            }
            return heavy ? attackHeavyDuration : attackLightDuration;
        }

        public float SetBlock(bool value)
        {
            if (useAnimator && _anim)
                _anim.SetBool(blockBool, value);
            else if (_legacy)
            {
                if (value) CrossFadeSafe(clipBlockLoop, 0.05f, true);
                else CrossFadeSafe(clipIdle, 0.1f, true);
            }
            return blockEnterExitDuration;
        }

        public float PlayHit()
        {
            if (useAnimator && _anim)
            {
                _anim.ResetTrigger(hitTrigger);
                _anim.SetTrigger(hitTrigger);
            }
            else if (_legacy) CrossFadeSafe(clipHit, 0.02f, false);
            return hitDuration;
        }

        public float PlayDie()
        {
            if (useAnimator && _anim)
            {
                _anim.ResetTrigger(dieTrigger);
                _anim.SetTrigger(dieTrigger);
            }
            else if (_legacy) CrossFadeSafe(clipDie, 0.02f, false);
            return dieDuration;
        }

        // Legacy helper
        void CrossFadeSafe(string clipName, float fade, bool loop)
        {
            if (!_legacy) return;
            var c = _legacy.GetClip(clipName);
            if (!c)
            {
                if (clipName == clipWalk) c = _legacy.GetClip("Walking_B") ?? _legacy.GetClip("Walking_C");
                if (!c) return;
            }
            c.wrapMode = loop ? WrapMode.Loop : WrapMode.Default;
            _legacy.CrossFade(c.name, Mathf.Clamp(fade, 0f, 0.25f));
        }
    }
}
