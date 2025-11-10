using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Combat;        // Team, TBIntent
using Game;          // Health
using Characters;    // KaijuBody

namespace Combat
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Animation))]  // legacy Animation on the SAME GameObject
    public class TurnBasedMeleeAI : MonoBehaviour
    {
        [Header("Team")]
        public Team team = Team.Main;

        [Header("Stats")]
        [Tooltip("How far to walk per round when closing distance.")]
        public float moveStepMeters = 0.45f;
        public float attackRange = 0.55f;
        public float damage = 25f;

        [Header("AI")]
        public float sightRadius = 20f;
        [Range(0f, 1f)] public float dodgeProbabilityInRange = 0.35f;

        [Header("Animation Clips (legacy, on this object)")]
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

        // Runtime (read-only externally)
        public int CurrentInitiative { get; set; }
        public TBIntent Intent { get; private set; } = TBIntent.Idle;
        public TurnBasedMeleeAI CachedTarget { get; private set; }
        public bool IsDead => _hp != null && _hp.IsDead;

        private Health _hp;
        private Animation _anim;

        void Awake()
        {
            _hp = GetComponent<Health>();
            _anim = GetComponent<Animation>();
            if (_anim)
            {
                _anim.playAutomatically = false;
                _anim.cullingType = AnimationCullingType.AlwaysAnimate;
            }
            PlayIdle();
        }

        // ===== Decision phase =====
        public int RollInitiative() => Random.Range(1, 21) + Random.Range(0, 3);

        public void BeginThinking(float thinkSeconds) { Intent = TBIntent.Idle; }

        public void FinalizeIntent()
        {
            if (IsDead) { Intent = TBIntent.Idle; return; }

            var opp = FindClosestEnemy();
            CachedTarget = opp;

            if (!opp || opp.IsDead) { Intent = TBIntent.Idle; return; }

            float dist = FlatDistanceTo(opp.transform.position);
            Intent = (dist > attackRange)
                ? TBIntent.Move
                : (Random.value < dodgeProbabilityInRange ? TBIntent.Dodge : TBIntent.Attack);
        }

        public void CacheTarget(List<TurnBasedMeleeAI> all)
        {
            if (IsDead) { CachedTarget = null; return; }

            if (CachedTarget == null || CachedTarget.IsDead ||
                (CachedTarget.transform.position - transform.position).sqrMagnitude > sightRadius * sightRadius)
            {
                CachedTarget = all
                    .Where(u => u != this && !u.IsDead && u.team != team)
                    .OrderBy(u => (u.transform.position - transform.position).sqrMagnitude)
                    .FirstOrDefault();
            }
        }

        // ===== Resolution helpers =====
        public void ResolveSoloIntent(float moveStepOverride)
        {
            if (IsDead) return;
            float step = (moveStepOverride > 0f) ? moveStepOverride : moveStepMeters;

            switch (Intent)
            {
                case TBIntent.Move:
                    if (CachedTarget) ExecuteMoveStepTowards(CachedTarget.transform.position, step);
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
                if (!InRangeOf(opp)) ExecuteMoveStepTowards(opp.transform.position, moveStepOverride);
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

        // ===== Actions =====
        public void ExecuteMoveStepTowards(Vector3 targetPos, float moveStep)
        {
            var to = targetPos - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 1e-6f) { PlayIdle(); return; }

            var dir = to.normalized;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            if (_anim && !string.IsNullOrEmpty(walkClip) && _anim.GetClip(walkClip))
                _anim.CrossFade(walkClip, 0.1f);

            transform.position += dir * Mathf.Min(moveStep, to.magnitude);
        }

        public void ExecuteDodge()
        {
            string dc = PickClip(dodgeClips);
            if (_anim && !string.IsNullOrEmpty(dc) && _anim.GetClip(dc))
                _anim.CrossFade(dc, 0.05f);
            else
                PlayIdle();
        }

        public void ExecuteAttack(TurnBasedMeleeAI target)
        {
            if (!target || target.IsDead) { PlayIdle(); return; }

            var to = target.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

            string ac = PickClip(attackClips);
            if (_anim && !string.IsNullOrEmpty(ac) && _anim.GetClip(ac))
                _anim.CrossFade(ac, 0.1f);

            target._hp?.TakeDamage(damage);

            var kb = target.GetComponent<KaijuBody>() ?? target.GetComponentInChildren<KaijuBody>();
            if (kb != null) kb.ApplyKnockback(to.normalized, 0.3f);

            var tAnim = target.GetComponent<Animation>() ?? target.GetComponentInChildren<Animation>();
            string hc = PickClip(hitClips);
            if (tAnim && !string.IsNullOrEmpty(hc) && tAnim.GetClip(hc))
                tAnim.CrossFade(hc, 0.05f);

            if (target.IsDead && tAnim && !string.IsNullOrEmpty(deathClip) && tAnim.GetClip(deathClip))
                tAnim.CrossFade(deathClip, 0.15f);
        }

        // ===== Utility =====
        private bool InRangeOf(TurnBasedMeleeAI other)
        {
            if (!other) return false;
            return FlatDistanceTo(other.transform.position) <= attackRange + 1e-4f;
        }

        private float FlatDistanceTo(Vector3 worldPos)
        {
            var a = transform.position; a.y = 0f;
            worldPos.y = 0f;
            return Vector3.Distance(a, worldPos);
        }

        private TurnBasedMeleeAI FindClosestEnemy()
        {
            var all = FindObjectsByType<TurnBasedMeleeAI>(FindObjectsSortMode.None);
            float best = float.MaxValue; TurnBasedMeleeAI bestU = null;

            foreach (var u in all)
            {
                if (u == this || u.IsDead) continue;
                if (u.team == team) continue;

                float sq = (u.transform.position - transform.position).sqrMagnitude;
                if (sq < best && sq <= sightRadius * sightRadius)
                { best = sq; bestU = u; }
            }
            return bestU;
        }

        private void PlayIdle()
        {
            if (_anim && !string.IsNullOrEmpty(idleClip) && _anim.GetClip(idleClip) && !_anim.IsPlaying(idleClip))
                _anim.CrossFade(idleClip, 0.1f);
        }

        private static string PickClip(List<string> list)
        {
            if (list == null || list.Count == 0) return null;
            foreach (var name in list)
                if (!string.IsNullOrEmpty(name)) return name;
            return null;
        }
    }
}