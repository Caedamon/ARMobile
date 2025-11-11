
using System.Collections.Generic;
using System.Linq;
using Characters;
using Game;
using UnityEngine;

namespace Combat
{
    [RequireComponent(typeof(Animation))]
    [RequireComponent(typeof(Health))] // adjust namespace if your Health lives elsewhere
    public class TurnBasedMeleeAI : MonoBehaviour
    {
        [Header("Team")]
        public Team team = Team.Main;

        [Header("Stats")]
        public float moveStepMeters = 0.05f;
        public float attackRange = 0.10f;
        public float damage = 25f;

        [Header("AI")]
        public float sightRadius = 20f;
        [Range(0f, 1f)] public float dodgeProbabilityInRange = 0.0f;

        [Header("Animation Clips (legacy Animation on THIS object)")]
        public string idleClip = "Idle_Combat";
        public string walkClip = "Walking_D_Skeletons";
        public List<string> attackClips = new()
        {
            "Unarmed_Melee_Attack_Punch_A",
            "Unarmed_Melee_Attack_Punch_B",
            "Unarmed_Melee_Attack_Kick"
        };
        public List<string> dodgeClips = new() { "Dodge_Left", "Dodge_Right", "Dodge_Forward", "Dodge_Backward" };
        public List<string> hitClips   = new() { "Hit_A", "Hit_B" };
        public string deathClip = "Death_C_Skeletons";

        [Header("FX")]
        public float knockbackStrength = 0.02f; // set 0 to disable

        // Runtime state
        public int CurrentInitiative { get; set; }
        public TBIntent Intent { get; private set; } = TBIntent.Idle;
        public TurnBasedMeleeAI CachedTarget { get; private set; }

        public bool IsDead => _hp != null && _hp.IsDead;

        Animation _anim;
        Health _hp;
        bool _playedDeath;

        void Awake()
        {
            _anim = GetComponent<Animation>();
            _hp   = GetComponent<Health>();

            if (_anim)
            {
                _anim.playAutomatically = false;
                _anim.cullingType = AnimationCullingType.AlwaysAnimate;

                // Loopers
                SetWrapMode(idleClip, WrapMode.Loop);
                SetWrapMode(walkClip, WrapMode.Loop);
                // One-shots
                foreach (var n in attackClips) SetWrapMode(n, WrapMode.Once);
                foreach (var n in dodgeClips)  SetWrapMode(n, WrapMode.Once);
                foreach (var n in hitClips)    SetWrapMode(n, WrapMode.Once);
                SetWrapMode(deathClip, WrapMode.Once);
            }
            PlayIdle();
        }

        // -------- Decision phase (called by manager) --------
        public int RollInitiative() => Random.Range(1, 21) + Random.Range(0, 3);

        public void BeginThinking(float _thinkSeconds)
        {
            Intent = TBIntent.Idle;
        }

        public void FinalizeIntent()
        {
            if (IsDead) { Intent = TBIntent.Idle; return; }

            var opp = FindClosestEnemy();
            CachedTarget = opp;

            if (!opp || opp.IsDead) { Intent = TBIntent.Idle; return; }

            float dist = FlatDistanceTo(opp.transform.position);
            if (dist > attackRange) Intent = TBIntent.Move;
            else Intent = (Random.value < dodgeProbabilityInRange) ? TBIntent.Dodge : TBIntent.Attack;
        }

        public void CacheTarget(List<TurnBasedMeleeAI> all)
        {
            if (IsDead) { CachedTarget = null; return; }

            bool need = CachedTarget == null || CachedTarget.IsDead ||
                        (CachedTarget.transform.position - transform.position).sqrMagnitude > sightRadius * sightRadius;

            if (need)
            {
                CachedTarget = all
                    .Where(u => u && u != this && !u.IsDead && u.team != team)
                    .OrderBy(u => (u.transform.position - transform.position).sqrMagnitude)
                    .FirstOrDefault();
            }
        }

        // -------- Resolution helpers (used by manager) --------
        public void ResolveSoloIntent(float moveStepOverride)
        {
            if (IsDead) return;
            float step = (moveStepOverride > 0f) ? moveStepOverride : moveStepMeters;

            switch (Intent)
            {
                case TBIntent.Move:
                    if (CachedTarget) ExecuteMoveStepTowards(CachedTarget.transform.position, step, attackRange);
                    else PlayIdle();
                    break;

                case TBIntent.Attack:
                    if (CachedTarget && InRangeOf(CachedTarget)) ExecuteAttack(CachedTarget);
                    else PlayIdle();
                    break;

                case TBIntent.Dodge:
                    ExecuteDodge();
                    break;

                default:
                    PlayIdle();
                    break;
            }
        }

        public void ResolveVsOpponent(TurnBasedMeleeAI opp, float moveStepOverride)
        {
            if (IsDead) return;

            if (Intent == TBIntent.Move)
            {
                if (!InRangeOf(opp)) ExecuteMoveStepTowards(opp.transform.position, moveStepOverride, attackRange);
                else PlayIdle();
                return;
            }

            if (Intent == TBIntent.Dodge) { ExecuteDodge(); return; }

            if (Intent == TBIntent.Attack)
            {
                if (InRangeOf(opp)) ExecuteAttack(opp);
                else PlayIdle();
                return;
            }

            PlayIdle();
        }

        // -------- Actions --------
        // Preferred: stop at 'stopAtDistance' so they don't merge
        public void ExecuteMoveStepTowards(Vector3 targetPos, float moveStep, float stopAtDistance)
        {
            var to = targetPos - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist <= Mathf.Max(1e-4f, stopAtDistance)) { PlayIdle(); return; }

            var dir = to / dist;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            CrossFadeIf(_anim, walkClip, 0.1f);

            float maxAdvance = Mathf.Max(0f, dist - stopAtDistance);
            float step = Mathf.Min(moveStep, maxAdvance);
            if (step <= 1e-6f) { PlayIdle(); return; }
            transform.position += dir * step;

            // Post-guard: keep thin shell
            float min = Mathf.Max(1e-4f, stopAtDistance);
            var flatSelf = transform.position; flatSelf.y = 0f;
            var flatOther = targetPos; flatOther.y = 0f;
            var d = Vector3.Distance(flatSelf, flatOther);
            if (d < min) transform.position += (flatSelf - flatOther).normalized * (min - d + 1e-4f);
        }

        public void ExecuteDodge()
        {
            string dc = PickClip(dodgeClips);
            PlayOnceThenIdle(dc, 0.05f);                // <-- queue idle
            TurnBasedEvents.AnnounceAction(this, "Dodge!"); // UI
        }

        public void ExecuteAttack(TurnBasedMeleeAI target)
        {
            if (!target || target.IsDead) { PlayIdle(); return; }

            var to = target.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

            string ac = PickClip(attackClips);
            PlayOnceThenIdle(ac, 0.1f); // <-- queue idle after attack

            // UI: announce which style
            string action = (ac != null && ac.Contains("Kick")) ? "Kick!" :
                            (ac != null && ac.Contains("Punch")) ? "Punch!" : "Attack!";
            TurnBasedEvents.AnnounceAction(this, action);

            // Apply damage
            target._hp?.TakeDamage(damage);
            TurnBasedEvents.AnnounceDamage(target, damage); // UI

            // Small knockback (optional KaijuBody)
            if (knockbackStrength > 0f)
            {
                var kb = target.GetComponent<KaijuBody>() ?? target.GetComponentInChildren<KaijuBody>();
                if (kb != null && to.sqrMagnitude > 1e-6f)
                    kb.ApplyKnockback(to.normalized, knockbackStrength);
            }

            // Victim hit -> then idle, unless dead
            var tAnim = target.GetComponent<Animation>() ?? target.GetComponentInChildren<Animation>();
            string hc = PickClip(hitClips);
            if (tAnim && !string.IsNullOrEmpty(hc) && !target.IsDead)
            {
                var clip = tAnim.GetClip(hc);
                if (clip) clip.wrapMode = WrapMode.Once;
                tAnim.CrossFade(hc, 0.05f);
                // queue victim idle so they don't freeze
                tAnim.CrossFadeQueued(target.idleClip, 0.1f, QueueMode.CompleteOthers);
            }

            if (target.IsDead) target.PlayDeathIfNeeded();
        }

        // -------- Utility --------
        void PlayDeathIfNeeded()
        {
            if (_playedDeath) return;
            var tAnim = GetComponent<Animation>() ?? GetComponentInChildren<Animation>();
            if (tAnim && !string.IsNullOrEmpty(deathClip))
            {
                var c = tAnim.GetClip(deathClip);
                if (c) c.wrapMode = WrapMode.Once;
                tAnim.CrossFade(deathClip, 0.15f);
            }
            _playedDeath = true; // Health should delay disable for a short time if needed
        }

        bool InRangeOf(TurnBasedMeleeAI other)
        {
            if (!other) return false;
            return FlatDistanceTo(other.transform.position) <= attackRange + 1e-4f;
        }

        float FlatDistanceTo(Vector3 worldPos)
        {
            var a = transform.position; a.y = 0f;
            worldPos.y = 0f;
            return Vector3.Distance(a, worldPos);
        }

        TurnBasedMeleeAI FindClosestEnemy()
        {
            var all = FindObjectsByType<TurnBasedMeleeAI>(FindObjectsSortMode.None);
            float best = float.MaxValue; TurnBasedMeleeAI bestU = null;

            foreach (var u in all)
            {
                if (!u || u == this || u.IsDead) continue;
                if (u.team == team) continue;

                float sq = (u.transform.position - transform.position).sqrMagnitude;
                if (sq < best && sq <= sightRadius * sightRadius)
                { best = sq; bestU = u; }
            }
            return bestU;
        }

        void PlayIdle() => CrossFadeIf(_anim, idleClip, 0.1f);

        void SetWrapMode(string clip, WrapMode mode)
        {
            if (!_anim || string.IsNullOrEmpty(clip)) return;
            var c = _anim.GetClip(clip);
            if (c) c.wrapMode = mode;
        }

        void PlayOnceThenIdle(string clip, float fade)
        {
            if (!_anim || string.IsNullOrEmpty(clip)) { PlayIdle(); return; }
            var c = _anim.GetClip(clip);
            if (c == null) { PlayIdle(); return; }

            c.wrapMode = WrapMode.Once;
            SetWrapMode(idleClip, WrapMode.Loop);

            _anim.CrossFade(clip, fade);
            _anim.CrossFadeQueued(idleClip, 0.1f, QueueMode.CompleteOthers); // never freeze at end
        }

        static void CrossFadeIf(Animation a, string clip, float fade)
        {
            if (!a || string.IsNullOrEmpty(clip)) return;
            var c = a.GetClip(clip);
            if (c != null) a.CrossFade(clip, fade);
        }

        static string PickClip(List<string> list)
        {
            if (list == null || list.Count == 0) return null;
            var valid = list.Where(n => !string.IsNullOrEmpty(n)).ToList();
            if (valid.Count == 0) return null;
            return valid[Random.Range(0, valid.Count)];
        }

        // Safety: if we intend to be idle and the animator isn't playing it, keep idle looping
        void LateUpdate()
        {
            if (IsDead || _anim == null) return;
            if (Intent == TBIntent.Idle && !_anim.IsPlaying(idleClip))
                PlayIdle();
        }
    }
}
