// ============================================================================
// File: Assets/Scripts/Combat/BattleManager.cs
// Purpose: Instant decisions; round waits for the longest action duration.
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Combat
{
    public sealed class BattleManager : MonoBehaviour
    {
        [Header("Cadence")]
        [Tooltip("Minimum time to wait per round if actions are shorter.")]
        public float minRoundSeconds = 0.5f;   // can set to 0 if you want pure clip-driven timing

        [Header("Discovery")]
        public bool autoDiscoverOnStart = true;
        public bool rescanEachRound = true;

        [Header("Debug")]
        public bool log = true;

        readonly List<MeleeAI> _units = new();
        int _roundIdx = 0;

        void Start()
        {
            if (autoDiscoverOnStart) DiscoverUnits();
            StartCoroutine(BattleLoop());
        }

        public void DiscoverUnits()
        {
            _units.Clear();
            _units.AddRange(FindObjectsByType<MeleeAI>(FindObjectsSortMode.None));
            if (log) Debug.Log($"[Battle] DiscoverUnits → {_units.Count}");
        }

        IEnumerator BattleLoop()
        {
            while (true)
            {
                if (rescanEachRound) DiscoverUnits();

                // prune
                for (int i = _units.Count - 1; i >= 0; i--)
                    if (!_units[i] || _units[i].IsDead) _units.RemoveAt(i);

                if (_units.Count == 0) { yield return null; continue; }

                _roundIdx++;
                if (log) Debug.Log($"[Battle] Round #{_roundIdx}");

                // Instant decisions
                foreach (var u in _units) { if (!u || u.IsDead) continue; u.CurrentInitiative = u.RollInitiative(); u.BeginThinking(0f); }
                foreach (var u in _units) { if (!u || u.IsDead) continue; u.FinalizeIntent(); }

                // cache targets
                var alive = _units.Where(u => u && !u.IsDead).ToList();
                foreach (var u in alive) u.CacheTarget(alive);

                // Resolve; units will fill LastActionDuration
                ResolveRound(alive);

                // Wait for the longest action to finish (plus a tiny buffer)
                float longest = Mathf.Max(minRoundSeconds, alive.Max(u => u.LastActionDuration));
                yield return new WaitForSeconds(longest);
            }
        }

        void ResolveRound(List<MeleeAI> alive)
        {
            var resolved = new HashSet<MeleeAI>();

            foreach (var a in alive)
            {
                if (resolved.Contains(a)) continue;
                var b = a.CachedTarget;

                if (b == null || b.IsDead)
                {
                    a.ResolveSoloIntent(a.MoveStepMeters);
                    resolved.Add(a);
                    continue;
                }

                if (b.CachedTarget == a && !resolved.Contains(b))
                {
                    ResolvePair(a, b);
                    resolved.Add(a); resolved.Add(b);
                }
                else
                {
                    a.ResolveVsOpponent(b, a.MoveStepMeters);
                    resolved.Add(a);
                }
            }
        }

        void ResolvePair(MeleeAI a, MeleeAI b)
        {
            if (a.IsDead && b.IsDead) return;
            if (a.IsDead) { b.ResolveSoloIntent(b.MoveStepMeters); return; }
            if (b.IsDead) { a.ResolveSoloIntent(a.MoveStepMeters); return; }

            var ai = a.Intent;
            var bi = b.Intent;

            if (ai == Intent.Move && bi == Intent.Move)
            {
                float dist = a.GetComponent<MeleeMotor>().FlatDistanceTo(b.transform.position);
                float targetDist = Mathf.Max(a.AttackRange, b.AttackRange);
                float available = Mathf.Max(0f, dist - targetDist + 1e-3f);
                float each = Mathf.Min(a.MoveStepMeters, b.MoveStepMeters, available * 0.5f);
                a.ExecuteMoveStepTowards(b.transform.position, each, a.AttackRange);
                b.ExecuteMoveStepTowards(a.transform.position, each, b.AttackRange);
                // both will play walk; durations already covered via ResolveSoloIntent in other paths,
                // here we can approximate by forcing walk once (optional).
                a.GetComponent<MeleeAnimator>()?.PlayWalk();
                b.GetComponent<MeleeAnimator>()?.PlayWalk();
                return;
            }

            if ((ai == Intent.Dodge && bi == Intent.Attack) || (bi == Intent.Dodge && ai == Intent.Attack))
            { a.ExecuteDodge(); b.ExecuteDodge(); return; }

            if (ai == Intent.Attack && bi == Intent.Attack)
            {
                int ia = a.CurrentInitiative, ib = b.CurrentInitiative;
                if (ia == ib) { if (Random.value < 0.5f) ia++; else ib++; }
                if (ia > ib) a.ExecuteAttack(b); else b.ExecuteAttack(a);
                return;
            }

            a.ResolveVsOpponent(b, a.MoveStepMeters);
            b.ResolveVsOpponent(a, b.MoveStepMeters);
        }
    }
}
