using System.Collections.Generic;
using UnityEngine;
using Game;          // for Health
using Characters;    // for KaijuBody
using GameCamera;    // for CameraShaker

namespace Combat
{
    [RequireComponent(typeof(Health))]
    public class SimpleMeleeAI : MonoBehaviour
    {
        [Header("Targeting")] public LayerMask enemyMask;
        public float sightRadius = 6f;

        [Header("Movement")] public float moveSpeed = 0.9f;

        [Header("Attack")] public float attackRange = 1.6f;
        public float attackRate = 0.6f;   // swings per second
        public float damage = 25f;

        private Transform _target;
        private Health _myHealth;
        private float _nextAttackTime;

        void Awake()
        {
            _myHealth = GetComponent<Health>();
        }

        void OnEnable()
        {
            _target = null;
            _nextAttackTime = 0f;
        }

        void Update()
        {
            if (_myHealth != null && _myHealth.IsDead) return;

            if (_target == null || !_target.gameObject.activeInHierarchy)
                _target = FindClosestEnemy();

            if (_target == null) return;

            float d = Vector3.Distance(transform.position, _target.position);
            if (d > attackRange)
            {
                Vector3 to = (_target.position - transform.position);
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                    transform.forward = Vector3.Lerp(transform.forward, to.normalized, 0.35f);

                transform.position += transform.forward * moveSpeed * Time.deltaTime;
            }
            else
            {
                TryAttack();
            }
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

            Vector3 p = transform.position + transform.forward * (attackRange * 0.6f);
            float r = attackRange * 0.6f;
            var hits = Physics.OverlapSphere(p, r, enemyMask);

            HashSet<Health> unique = new HashSet<Health>();
            for (int i = 0; i < hits.Length; i++)
            {
                var hp = hits[i].GetComponent<Health>() ?? hits[i].GetComponentInParent<Health>();
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
            }

            var shaker = Object.FindAnyObjectByType<CameraShaker>();
            if (shaker != null) shaker.Kick();
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
