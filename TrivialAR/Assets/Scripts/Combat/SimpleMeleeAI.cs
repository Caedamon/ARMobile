using System.Collections.Generic;
using UnityEngine;
using Game;          // Health
using Characters;    // KaijuBody
using GameCamera;    // CameraShaker

namespace Combat
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Animation))]
    public class SimpleMeleeAI : MonoBehaviour
    {
        [Header("Targeting")]
        public LayerMask enemyMask;       // leave empty to auto-fallback
        public float sightRadius = 8f;

        [Header("Movement")]
        public float moveSpeed = 0.9f;
        public float turnLerp = 0.35f;

        [Header("Attack")]
        public float attackRange = 1.6f;
        public float attackRate = 0.6f;   // swings per second
        public float damage = 25f;

        [Header("Animation Lists (Legacy Animation clip names)")]
        public string idleClip = "Idle_Combat";

        [Tooltip("e.g. Unarmed_Melee_Attack_Punch_A, Unarmed_Melee_Attack_Punch_B, Unarmed_Melee_Attack_Kick")]
        public List<string> attackClips = new List<string>();

        [Tooltip("e.g. Hit_A, Hit_B")]
        public List<string> hitClips = new List<string>();

        [Tooltip("e.g. Dodge_Forward, Dodge_Backward, Dodge_Left, Dodge_Right")]
        public List<string> dodgeClips = new List<string>();

        [Tooltip("e.g. Death_C_Skeletons")]
        public string deathClip = "Death_C_Skeletons";

        [Header("Dodge Settings")]
        [Range(0f, 1f)] public float dodgeChanceOnAttack = 0.0f; // set >0 later if you want dodges
        public float dodgeStrafeDistance = 0.8f;                  // small sidestep

        Transform _target;
        Health _myHealth;
        Animation _anim;
        float _nextAttackTime;
        bool _isDead;

        static readonly Collider[] s_temp = new Collider[32];

        void Awake()
        {
            _myHealth = GetComponent<Health>();
            _anim = GetComponent<Animation>();
        }

        void Start()
        {
            if (_anim && !string.IsNullOrEmpty(idleClip) && _anim.GetClip(idleClip))
                _anim.Play(idleClip);
        }

        void OnEnable()
        {
            _target = null;
            _nextAttackTime = 0f;
            _isDead = false;
        }

        void Update()
        {
            if (_myHealth == null) return;

            if (!_isDead && _myHealth.IsDead)
            {
                Die();
                return;
            }
            if (_isDead) return;

            if (_target == null || !_target.gameObject.activeInHierarchy)
                _target = FindClosestEnemy();

            if (_target == null) return;

            var to = _target.position - transform.position;
            float d = to.magnitude;

            if (d > attackRange * 0.98f)
            {
                MoveTowards(to);
            }
            else
            {
                TryAttackOrDodge();
            }
        }

        void MoveTowards(Vector3 to)
        {
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
                transform.forward = Vector3.Lerp(transform.forward, to.normalized, turnLerp);

            transform.position += transform.forward * moveSpeed * Time.deltaTime;

            if (_anim && !string.IsNullOrEmpty(idleClip) && !_anim.IsPlaying(idleClip) && _anim.GetClip(idleClip))
                _anim.CrossFade(idleClip, 0.15f);
        }

        Transform FindClosestEnemy()
        {
            int count;
            if (enemyMask.value != 0)
                count = Physics.OverlapSphereNonAlloc(transform.position, sightRadius, s_temp, enemyMask);
            else
                count = Physics.OverlapSphereNonAlloc(transform.position, sightRadius, s_temp); // fallback: any layer

            float best = float.MaxValue;
            Transform bestT = null;

            for (int i = 0; i < count; i++)
            {
                var col = s_temp[i];
                if (!col || col.transform == transform) continue;

                var hp = col.GetComponent<Health>() ?? col.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead || hp == _myHealth) continue;

                float sd = (col.transform.position - transform.position).sqrMagnitude;
                if (sd < best) { best = sd; bestT = col.transform; }
            }
            return bestT;
        }

        void TryAttackOrDodge()
        {
            // Optional dodge behavior
            if (dodgeChanceOnAttack > 0f && dodgeClips.Count > 0 && Random.value < dodgeChanceOnAttack)
            {
                string dodge = RandomClip(dodgeClips);
                if (_anim && _anim.GetClip(dodge))
                    _anim.CrossFade(dodge, 0.08f);

                // small sidestep left/right
                Vector3 side = (Random.value < 0.5f ? -transform.right : transform.right);
                transform.position += side * dodgeStrafeDistance;
                return;
            }

            // Attack cadence
            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + 1f / Mathf.Max(attackRate, 0.01f);

            // Play random attack
            string attack = RandomClip(attackClips);
            if (!string.IsNullOrEmpty(attack) && _anim && _anim.GetClip(attack))
                _anim.CrossFade(attack, 0.1f);

            // Hit test
            Vector3 p = transform.position + transform.forward * (attackRange * 0.6f);
            float r = attackRange * 0.6f;
            int count = (enemyMask.value != 0)
                ? Physics.OverlapSphereNonAlloc(p, r, s_temp, enemyMask)
                : Physics.OverlapSphereNonAlloc(p, r, s_temp);

            // Unique victims with Health
            var seen = new HashSet<Health>();
            for (int i = 0; i < count; i++)
            {
                var hp = s_temp[i] ? (s_temp[i].GetComponent<Health>() ?? s_temp[i].GetComponentInParent<Health>()) : null;
                if (hp != null && hp != _myHealth && !hp.IsDead) seen.Add(hp);
            }

            foreach (var hp in seen)
            {
                hp.TakeDamage(damage);

                var tr = (hp as Component).transform;
                var kb = tr.GetComponent<KaijuBody>() ?? tr.GetComponentInParent<KaijuBody>();
                if (kb != null)
                {
                    Vector3 dir = tr.position - transform.position;
                    kb.ApplyKnockback(dir, 0.35f);
                }

                // Random hit reaction on victim
                var anim = tr.GetComponent<Animation>();
                string hit = RandomClip(hitClips);
                if (anim && !string.IsNullOrEmpty(hit) && anim.GetClip(hit))
                    anim.CrossFade(hit, 0.1f);
            }

            var shaker = Object.FindAnyObjectByType<CameraShaker>();
            if (shaker != null) shaker.Kick();
        }

        string RandomClip(List<string> list)
        {
            if (list == null || list.Count == 0) return null;
            int i = Random.Range(0, list.Count);
            return list[i];
        }

        void Die()
        {
            _isDead = true;
            if (_anim && !string.IsNullOrEmpty(deathClip) && _anim.GetClip(deathClip))
                _anim.CrossFade(deathClip, 0.15f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 p = transform.position + transform.forward * (attackRange * 0.6f);
            Gizmos.DrawWireSphere(p, attackRange * 0.6f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, sightRadius);
        }
    }
}
