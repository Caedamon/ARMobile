// using System.Collections.Generic;
// using UnityEngine;
//
// // Only these match what you said remains:
// using Game;          // Health
// using Characters;    // KaijuBody
//
// namespace Combat
// {
//     public enum Team { Main, Enemy }
//
//     [RequireComponent(typeof(Health))]
//     [RequireComponent(typeof(Animation))] // Legacy Animation (not Animator)
//     public class SimpleMeleeAI : MonoBehaviour
//     {
//         [Header("Team (used only to prefer targets)")]
//         public Team team = Team.Main;
//
//         [Header("Targeting (no layers/masks needed)")]
//         public float sightRadius = 20f;      // meters
//
//         [Header("Movement")]
//         public float moveSpeed = 1.5f;       // raise if your scale is big
//         [Range(0f, 1f)] public float turnLerp = 0.35f;
//
//         [Header("Attack")]
//         public float attackRange = 1.6f;
//         public float attackRate = 0.6f;      // swings per second
//         public float damage = 25f;
//
//         [Header("Animation (Legacy clip names)")]
//         public string idleClip = "Idle_Combat";
//         [Tooltip("e.g. Unarmed_Melee_Attack_Punch_A, _B, _Kick")]
//         public List<string> attackClips = new List<string>();
//         [Tooltip("e.g. Hit_A, Hit_B")]
//         public List<string> hitClips = new List<string>();
//         [Tooltip("e.g. Dodge_Forward, _Backward, _Left, _Right")]
//         public List<string> dodgeClips = new List<string>();
//         public string deathClip = "Death_C_Skeletons";
//
//         [Header("Dodge (optional)")]
//         [Range(0f, 1f)] public float dodgeChanceOnAttack = 0f;
//         public float dodgeStrafeDistance = 0.8f;
//
//         [Header("Debug")]
//         public bool debug;
//
//         // runtime
//         Health _myHealth;
//         Animation _anim;        // Legacy Animation
//         Rigidbody _rb;
//
//         Transform _targetTr;    // target transform with Health
//         Health _targetHp;
//
//         float _nextAttackTime;
//         bool _isDead;
//
//         void Awake()
//         {
//             _myHealth = GetComponent<Health>();
//             _anim     = GetComponent<Animation>();
//             _rb       = GetComponent<Rigidbody>();
//         }
//
//         void Start()
//         {
//             if (_anim && !string.IsNullOrEmpty(idleClip) && _anim.GetClip(idleClip))
//                 _anim.Play(idleClip);
//         }
//
//         void OnEnable()
//         {
//             _isDead = false;
//             _nextAttackTime = 0f;
//             _targetTr = null;
//             _targetHp = null;
//         }
//
//         void Update()
//         {
//             if (_myHealth == null) return;
//
//             if (!_isDead && _myHealth.IsDead)
//             {
//                 Die();
//                 return;
//             }
//             if (_isDead) return;
//
//             if (_targetTr == null || _targetHp == null || !_targetTr.gameObject.activeInHierarchy || _targetHp.IsDead)
//                 AcquireTarget();
//
//             if (_targetTr == null) return;
//
//             Vector3 to = _targetTr.position - transform.position;
//             to.y = 0f;
//
//             if (to.sqrMagnitude > 0.0001f)
//                 transform.forward = Vector3.Lerp(transform.forward, to.normalized, turnLerp);
//
//             float d = to.magnitude;
//
//             if (d > attackRange * 0.98f)
//             {
//                 Advance(transform.forward, moveSpeed);
//
//                 if (_anim && !string.IsNullOrEmpty(idleClip) && _anim.GetClip(idleClip) && !_anim.IsPlaying(idleClip))
//                     _anim.CrossFade(idleClip, 0.15f);
//             }
//             else
//             {
//                 TryAttackOrDodge();
//             }
//
// #if UNITY_EDITOR
//             if (debug)
//                 Debug.DrawLine(transform.position + Vector3.up, _targetTr.position + Vector3.up, Color.green);
// #endif
//         }
//
//         // Moves reliably even if a non-kinematic Rigidbody exists
//         void Advance(Vector3 dir, float speed)
//         {
//             Vector3 step = dir.normalized * speed * Time.deltaTime;
//             if (_rb != null && !_rb.isKinematic) _rb.MovePosition(_rb.position + step);
//             else transform.position += step;
//         }
//
//         void AcquireTarget()
//         {
//             // Pass 1: nearest different-team AI with Health
//             var ais = GetAllAIs();
//             float best = float.MaxValue;
//             Transform bestTr = null;
//             Health bestHp = null;
//
//             foreach (var ai in ais)
//             {
//                 if (ai == this) continue;
//                 if (ai._myHealth == null || ai._myHealth.IsDead) continue;
//                 if (ai.team == this.team) continue;
//
//                 float sd = (ai.transform.position - transform.position).sqrMagnitude;
//                 if (sd <= sightRadius * sightRadius && sd < best)
//                 {
//                     best = sd; bestTr = ai.transform; bestHp = ai._myHealth;
//                 }
//             }
//
//             // Pass 2: nearest ANY Health (covers objects without AI/Team)
//             if (bestTr == null)
//             {
//                 var allHp = GetAllHealths();
//                 best = float.MaxValue;
//                 foreach (var hp in allHp)
//                 {
//                     if (hp == _myHealth || hp.IsDead) continue;
//                     float sd = (hp.transform.position - transform.position).sqrMagnitude;
//                     if (sd <= sightRadius * sightRadius && sd < best)
//                     {
//                         best = sd; bestTr = hp.transform; bestHp = hp;
//                     }
//                 }
//             }
//
//             _targetTr = bestTr;
//             _targetHp = bestHp;
//
//             if (debug)
//             {
//                 if (_targetTr) Debug.Log($"{name} → target = {_targetTr.name}");
//                 else Debug.Log($"{name} → no target in radius");
//             }
//         }
//
//         void TryAttackOrDodge()
//         {
//             if (dodgeChanceOnAttack > 0f && dodgeClips.Count > 0 && Random.value < dodgeChanceOnAttack)
//             {
//                 string dodge = RandomClip(dodgeClips);
//                 if (_anim && _anim.GetClip(dodge)) _anim.CrossFade(dodge, 0.08f);
//                 Vector3 side = (Random.value < 0.5f ? -transform.right : transform.right);
//                 transform.position += side * dodgeStrafeDistance;
//                 return;
//             }
//
//             if (Time.time < _nextAttackTime) return;
//             _nextAttackTime = Time.time + 1f / Mathf.Max(attackRate, 0.01f);
//
//             string attack = RandomClip(attackClips);
//             if (!string.IsNullOrEmpty(attack) && _anim && _anim.GetClip(attack))
//                 _anim.CrossFade(attack, 0.1f);
//
//             if (_targetTr != null && _targetHp != null && !_targetHp.IsDead)
//             {
//                 float d = Vector3.Distance(transform.position, _targetTr.position);
//                 if (d <= attackRange * 1.1f)
//                 {
//                     _targetHp.TakeDamage(damage);
//
//                     var kb = _targetTr.GetComponent<KaijuBody>() ?? _targetTr.GetComponentInChildren<KaijuBody>();
//                     if (kb != null)
//                     {
//                         Vector3 dir = _targetTr.position - transform.position;
//                         kb.ApplyKnockback(dir, 0.35f);
//                     }
//
//                     var victimAnim = _targetTr.GetComponent<Animation>();
//                     string hit = RandomClip(hitClips);
//                     if (victimAnim && !string.IsNullOrEmpty(hit) && victimAnim.GetClip(hit))
//                         victimAnim.CrossFade(hit, 0.1f);
//                 }
//             }
//         }
//
//         string RandomClip(List<string> list)
//         {
//             if (list == null || list.Count == 0) return null;
//             int i = Random.Range(0, list.Count);
//             return list[i];
//         }
//
//         void Die()
//         {
//             _isDead = true;
//             if (_anim && !string.IsNullOrEmpty(deathClip) && _anim.GetClip(deathClip))
//                 _anim.CrossFade(deathClip, 0.15f);
//         }
//
//         void OnDrawGizmosSelected()
//         {
//             Gizmos.color = Color.cyan;
//             Gizmos.DrawWireSphere(transform.position, sightRadius);
//         }
//
//         // ---------- Helpers to avoid deprecation warnings ----------
//         static SimpleMeleeAI[] GetAllAIs()
//         {
// #if UNITY_2023_1_OR_NEWER
//             return Object.FindObjectsByType<SimpleMeleeAI>(FindObjectsSortMode.None);
// #else
//             return Object.FindObjectsOfType<SimpleMeleeAI>();
// #endif
//         }
//
//         static Health[] GetAllHealths()
//         {
// #if UNITY_2023_1_OR_NEWER
//             return Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
// #else
//             return Object.FindObjectsOfType<Health>();
// #endif
//         }
//     }
// }