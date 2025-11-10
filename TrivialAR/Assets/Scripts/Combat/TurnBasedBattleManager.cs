using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Combat
{
    public class TurnBasedBattleManager : MonoBehaviour
    {
        [Header("Timing")]
        public float thinkTimeSeconds = 2f;
        public float resolveDelaySeconds = 0.1f;

        [Header("Discovery")]
        public bool autoDiscoverOnStart = true;
        public bool rescanEachRound = true;   // AR-spawned arenas

        [Header("Debug")]
        public bool logRounds = true;
        public bool kickstartFirstRound = true;

        private readonly List<TurnBasedMeleeAI> _units = new();
        private int _roundIndex = 0;
        private bool _didKickstart = false;   // <-- MISSING BEFORE

        void Start()
        {
            if (autoDiscoverOnStart) DiscoverUnits();
            StartCoroutine(BattleLoop());
        }

        public void DiscoverUnits()
        {
            _units.Clear();
            _units.AddRange(FindObjectsByType<TurnBasedMeleeAI>(FindObjectsSortMode.None));
            if (logRounds) Debug.Log($"[TB] DiscoverUnits → found: {_units.Count}");
        }

        private IEnumerator BattleLoop()
        {
            while (true)
            {
                if (rescanEachRound) DiscoverUnits();

                // prune dead/missing
                for (int i = _units.Count - 1; i >= 0; i--)
                {
                    var u = _units[i];
                    if (!u || u.IsDead) _units.RemoveAt(i);
                }
                if (_units.Count == 0)
                {
                    // AR: units may appear later; keep coroutine alive and rescan next frame
                    yield return null;
                    continue;
                }

                _roundIndex++;
                if (logRounds) Debug.Log($"[TB] Round #{_roundIndex} — units={_units.Count}");

                // ----- KICKSTART FIRST ROUND (no initial wait) -----
                if (kickstartFirstRound && !_didKickstart && _units.Count >= 2)
                {
                    foreach (var u in _units) { if (!u || u.IsDead) continue; u.CurrentInitiative = u.RollInitiative(); }
                    foreach (var u in _units) { if (!u || u.IsDead) continue; u.FinalizeIntent(); }

                    var alive = _units.Where(u => u && !u.IsDead).ToList();
                    foreach (var u in alive) u.CacheTarget(alive);

                    if (logRounds) Debug.Log("[TB] Kickstart resolve");
                    ResolveRound(alive);

                    _didKickstart = true;
                    yield return null;
                    continue; // next loop runs normal timing
                }

                // initiative + thinking
                foreach (var u in _units)
                {
                    if (!u || u.IsDead) continue;
                    u.CurrentInitiative = u.RollInitiative();
                    u.BeginThinking(thinkTimeSeconds);
                }

                yield return new WaitForSeconds(thinkTimeSeconds + resolveDelaySeconds);

                // choose intents
                foreach (var u in _units)
                {
                    if (!u || u.IsDead) continue;
                    u.FinalizeIntent();
                }

                // cache targets
                var aliveNow = _units.Where(u => u && !u.IsDead).ToList();
                foreach (var u in aliveNow) u.CacheTarget(aliveNow);

                // resolve
                ResolveRound(aliveNow);

                yield return null;
            }
        }

        private void ResolveRound(List<TurnBasedMeleeAI> alive)
        {
            var resolved = new HashSet<TurnBasedMeleeAI>();

            foreach (var a in alive)
            {
                if (resolved.Contains(a)) continue;

                var b = a.CachedTarget;

                if (b == null || b.IsDead)
                {
                    a.ResolveSoloIntent(a.moveStepMeters);
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
                    a.ResolveVsOpponent(b, a.moveStepMeters);
                    resolved.Add(a);
                }
            }
        }

        private void ResolvePair(TurnBasedMeleeAI a, TurnBasedMeleeAI b)
        {
            if (a.IsDead && b.IsDead) return;
            if (a.IsDead) { b.ResolveSoloIntent(b.moveStepMeters); return; }
            if (b.IsDead) { a.ResolveSoloIntent(a.moveStepMeters); return; }

            var ai = a.Intent;
            var bi = b.Intent;

            // both move → walk without overshooting into attack range
            if (ai == TBIntent.Move && bi == TBIntent.Move)
            {
                float dist = Vector3.Distance(a.transform.position, b.transform.position);
                float targetDist = Mathf.Max(a.attackRange, b.attackRange);
                float available = Mathf.Max(0f, dist - targetDist + 1e-3f);
                float stepEach = Mathf.Min(a.moveStepMeters, b.moveStepMeters, available * 0.5f);
                a.ExecuteMoveStepTowards(b.transform.position, stepEach);
                b.ExecuteMoveStepTowards(a.transform.position, stepEach);
                return;
            }

            // Dodge beats Attack
            if ((ai == TBIntent.Dodge && bi == TBIntent.Attack) ||
                (bi == TBIntent.Dodge && ai == TBIntent.Attack))
            {
                if (ai == TBIntent.Dodge) a.ExecuteDodge();
                if (bi == TBIntent.Dodge) b.ExecuteDodge();
                return;
            }

            // Attack vs Attack → higher initiative hits
            if (ai == TBIntent.Attack && bi == TBIntent.Attack)
            {
                int ia = a.CurrentInitiative, ib = b.CurrentInitiative;
                if (ia == ib) { if (Random.value < 0.5f) ia++; else ib++; }
                if (ia > ib) a.ExecuteAttack(b); else b.ExecuteAttack(a);
                return;
            }

            // otherwise resolve individually
            a.ResolveVsOpponent(b, a.moveStepMeters);
            b.ResolveVsOpponent(a, b.moveStepMeters);
        }
    }
}