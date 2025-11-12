// ============================================================================
// File: Assets/Scripts/Combat/MeleeAnimator.cs
// Purpose: Legacy Animation wrapper that returns real playback duration per action.
// ============================================================================
using UnityEngine;

namespace Combat
{
    [RequireComponent(typeof(Animation))]
    public sealed class MeleeAnimator : MonoBehaviour
    {
        [Header("Clip Names")]
        public string idleClip = "Idle_Combat";
        public string walkClip = "Walking_D_Skeletons";
        public string[] attackClips = { "Unarmed_Melee_Attack_Punch_A", "Unarmed_Melee_Attack_Punch_B", "Unarmed_Melee_Attack_Kick" };
        public string[] dodgeClips  = { "Dodge_Left", "Dodge_Right", "Dodge_Forward", "Dodge_Backward" };
        public string[] hitClips    = { "Hit_A", "Hit_B" };
        public string deathClip = "Death_C_Skeletons";

        [Header("Speeds (1 = import speed)")]
        public float idleSpeed   = 1f;
        public float walkSpeed   = 1f;
        public float attackSpeed = 1f;
        public float dodgeSpeed  = 1f;
        public float hitSpeed    = 1f;
        public float deathSpeed  = 1f;

        Animation _anim;

        void Awake()
        {
            _anim = GetComponent<Animation>();
            _anim.playAutomatically = false;
            _anim.cullingType = AnimationCullingType.AlwaysAnimate;

            SetLoop(idleClip, true);
            SetLoop(walkClip, true);
            foreach (var n in attackClips) SetLoop(n, false);
            foreach (var n in dodgeClips)  SetLoop(n, false);
            foreach (var n in hitClips)    SetLoop(n, false);
            SetLoop(deathClip, false);
        }

        void SetLoop(string clip, bool loop)
        {
            if (string.IsNullOrEmpty(clip)) return;
            var c = _anim.GetClip(clip);
            if (c) c.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        }

        // --- Helpers ---
        float DurationSeconds(string clip, float speed)
        {
            if (string.IsNullOrEmpty(clip) || _anim[clip] == null) return 0f;
            speed = Mathf.Max(0.05f, speed);
            return _anim[clip].length / speed;
        }
        void SetSpeed(string clip, float speed)
        {
            if (string.IsNullOrEmpty(clip) || _anim[clip] == null) return;
            _anim[clip].speed = Mathf.Max(0.05f, speed);
        }
        void CrossFadeIf(string clip, float fade) { if (!string.IsNullOrEmpty(clip) && _anim.GetClip(clip) != null) _anim.CrossFade(clip, fade); }

        // --- Public control (all return their real playback time in seconds) ---
        public float PlayIdle()
        {
            SetSpeed(idleClip, idleSpeed);
            CrossFadeIf(idleClip, 0.1f);
            return DurationSeconds(idleClip, idleSpeed);
        }
        public void EnsureIdlePlaying()
        {
            if (!_anim.IsPlaying(idleClip)) PlayIdle();
        }
        public float PlayWalk()
        {
            SetSpeed(walkClip, walkSpeed);
            CrossFadeIf(walkClip, 0.1f);
            return DurationSeconds(walkClip, walkSpeed);
        }
        public float PlayAttack(string clip)
        {
            if (string.IsNullOrEmpty(clip) || _anim.GetClip(clip) == null) return PlayIdle();
            SetSpeed(clip, attackSpeed);
            _anim.CrossFade(clip, 0.1f);
            _anim.CrossFadeQueued(idleClip, 0.1f, QueueMode.CompleteOthers);
            return DurationSeconds(clip, attackSpeed);
        }
        public float PlayDodge(string clip)
        {
            if (string.IsNullOrEmpty(clip) || _anim.GetClip(clip) == null) return PlayIdle();
            SetSpeed(clip, dodgeSpeed);
            _anim.CrossFade(clip, 0.05f);
            _anim.CrossFadeQueued(idleClip, 0.1f, QueueMode.CompleteOthers);
            return DurationSeconds(clip, dodgeSpeed);
        }
        public float PlayHit(string clip)
        {
            if (string.IsNullOrEmpty(clip) || _anim.GetClip(clip) == null) return 0f;
            SetSpeed(clip, hitSpeed);
            _anim.CrossFade(clip, 0.05f);
            _anim.CrossFadeQueued(idleClip, 0.1f, QueueMode.CompleteOthers);
            return DurationSeconds(clip, hitSpeed);
        }
        public float PlayDeath()
        {
            if (string.IsNullOrEmpty(deathClip) || _anim.GetClip(deathClip) == null) return 0f;
            SetSpeed(deathClip, deathSpeed);
            CrossFadeIf(deathClip, 0.15f);
            return DurationSeconds(deathClip, deathSpeed);
        }

        // Random pick helpers
        public string PickAttack() => Pick(attackClips);
        public string PickDodge()  => Pick(dodgeClips);
        public string PickHit()    => Pick(hitClips);
        static string Pick(string[] arr) => (arr == null || arr.Length == 0) ? null : arr[Random.Range(0, arr.Length)];
    }
}
