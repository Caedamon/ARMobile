using System.Collections.Generic;
using UnityEngine;
using Game;          // for Health
using Characters;    // for KaijuBody
using GameCamera;    // for CameraShaker
// https://www.youtube.com/watch?v=gpaq5bAjya8

namespace Combat
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Animation))]
    public class SimpleMeleeAI : MonoBehaviour
    {
        [Header("Targeting")] public LayerMask enemyMask;
        public float sightRadius = 6f;

        [Header("Movement")] public float moveSpeed = 0.9f;

        [Header("Attack")] public float attackRange = 1.6f;
        public float attackRate = 0.6f;   // swings per second
        public float damage = 25f;

        [Header("Animation Clips")]
        public string idleClip = "Idle_Combat";
        public string attackClip = "Unarmed_Melee_Attack_Punch_A";
        public string hitClip = "Hit_A";
        public string deathClip = "Death_C_Skeletons";

        private Transform _target;
        private Health _myHealth;
        private Animation _anim;
        private float _nextAttackTime;
        private bool _isDead;

        void Awake()
        {
            _myHealth = GetComponent<Health>();
            _anim = GetComponent<Animation>();
        }

        void Start()
        {
            // make sure idle is playing initially
            if (_anim && _anim.GetClip(idleClip))
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

            float d = Vector3.Distance(transform.position, _target.position);
            if (d > attackRange)
            {
                MoveTowardsTarget();
            }
            else
            {
                TryAttack();
            }
        }

        void MoveTowardsTarget()
        {
            Vector3 to = (_target.position - transform.position);
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
                transform.forward = Vector3.Lerp(transform.forward, to.normalized, 0.35f);

            transform.position += transform.forward * moveSpeed * Time.deltaTime;

            if (_anim && !_anim.IsPlaying(idleClip))
                _anim.CrossFade(idleClip, 0.2f);
        }

        Transform FindClosestEnemy()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, sightRadius, enemyMask);
            float best = float.MaxValue;
            Transform bestT = null;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                var hp = h.GetComponent<Health>() ?? h.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead) continue;
                float sd = (h.transform.position - transform.position).sqrMagnitude;
                if (sd < best) { best = sd; bestT = h.transform; }
            }
            return bestT;
        }

        void TryAttack()
        {
            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + 1f / Mathf.Max(attackRate, 0.01f);

            // play attack animation
            if (_anim && _anim.GetClip(attackClip))
                _anim.CrossFade(attackClip, 0.1f);

            Vector3 p = transform.position + transform.forward * (attackRange * 0.6f);
            float r = attackRange * 0.6f;
            var hits = Physics.OverlapSphere(p, r, enemyMask);

            HashSet<Health> unique = new HashSet<Health>();
            foreach (var h in hits)
            {
                var hp = h.GetComponent<Health>() ?? h.GetComponentInParent<Health>();
                if (hp != null) unique.Add(hp);
            }

            foreach (var hp in unique)
            {
                hp.TakeDamage(damage);

                var tr = (hp as Component).transform;
                var kb = tr.GetComponent<KaijuBody>() ?? tr.GetComponentInParent<KaijuBody>();
                if (kb != null)
                {
                    Vector3 dir = tr.position - transform.position;
                    kb.ApplyKnockback(dir, 0.35f);
                }

                // trigger their hit reaction if they have Animation
                var anim = tr.GetComponent<Animation>();
                if (anim && anim.GetClip(hitClip))
                    anim.CrossFade(hitClip, 0.1f);
            }

            var shaker = Object.FindAnyObjectByType<CameraShaker>();
            if (shaker != null) shaker.Kick();
        }

        void Die()
        {
            _isDead = true;
            if (_anim && _anim.GetClip(deathClip))
                _anim.CrossFade(deathClip, 0.2f);
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
