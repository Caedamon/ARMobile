using Core;
using Fighters;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// The Smart and Boss behaviour of the FighterBrain (the Basic fighter ignores all of this). See AiTuning for every number.
    /// Smart: circles the enemy between swings, hits it while it recovers, defends only swings that can reach, strikes back after a defence,
    /// steps away after some swings, picks weak targets in group fights and attacks that can reach.
    /// Boss: all of that, and phases at fixed health (a roar, faster, harder, harder to stagger, combos).
    /// </summary>
    public sealed partial class FighterBrain
    {
        [Header("AI (set by the ArenaFighterSpawner)")]
        public AiTier aiTier = AiTier.Basic;
        public AiTuning aiTuning;

        AiTuning _tune;
        AiTuning Tune { get { if (_tune == null) _tune = aiTuning != null ? aiTuning : new AiTuning(); return _tune; } }

        bool Smart { get { return aiTier != AiTier.Basic; } }
        bool IsBoss { get { return aiTier == AiTier.Boss; } }

        int _phase;                 // boss: 0, 1, 2
        float _roarSeconds;
        float _nextTacticalAt;
        float _circleDir = 1f;
        float _circleFlipAt;
        bool _lastHitLanded;
        float _dodgeInvuln;
        bool _dodgeTactical;

        /// <summary>True while this fighter is a good target to hit right now: stunned, grabbing a weapon, roaring, or recovering from a swing or a dodge.</summary>
        public bool Vulnerable
        {
            get
            {
                switch (_state)
                {
                    case State.Hit:
                    case State.Pickup:
                    case State.Roar:
                        return true;
                    case State.Attack:
                        return _hitDone;
                    case State.Dodge:
                        return _stateTime > _dodgeInvuln * 0.9f;
                    default:
                        return false;
                }
            }
        }

        void TacticsStart()
        {
            _circleDir = Random.value < 0.5f ? 1f : -1f;
            _circleFlipAt = Time.time + Random.Range(Tune.circleFlipMin, Tune.circleFlipMax);
            if (Smart && _seeker != null)
            {
                _seeker.contestedPenalty = Mathf.Min(_seeker.contestedPenalty, Tune.seekerContestedPenalty);
                _seeker.raceMargin = Mathf.Max(_seeker.raceMargin, Tune.seekerRaceMargin);
            }
            Log("AI tier: " + aiTier);
        }

        static float HpFrac(FighterBrain b)
        {
            return b != null && b._hp != null && b._hp.MaxHP > 0f ? Mathf.Clamp01(b._hp.CurrentHP / b._hp.MaxHP) : 1f;
        }

        // ------------------------------------------------------------------ mood and phase numbers
        float CooldownFactor()
        {
            float f = 1f;
            if (HpFrac(this) < Tune.cautiousBelow) f *= Tune.cautiousCooldownMul;
            if (_targetBrain != null && HpFrac(_targetBrain) < Tune.killBelow) f *= Tune.killCooldownMul;
            if (_phase == 1) f *= Tune.phase1CooldownMul; else if (_phase >= 2) f *= Tune.phase2CooldownMul;
            return f;
        }

        float TierSpeedMul { get { return _phase == 0 ? 1f : (_phase == 1 ? Tune.phase1SpeedMul : Tune.phase2SpeedMul); } }
        float TierDamageMul { get { return _phase == 0 ? 1f : (_phase == 1 ? Tune.phase1DamageMul : Tune.phase2DamageMul); } }
        float TierPoise { get { return _phase == 0 ? 0f : (_phase == 1 ? Tune.phase1Poise : Tune.phase2Poise); } }
        float ComboChance { get { return _phase == 0 ? Tune.phase0ComboChance : (_phase == 1 ? Tune.phase1ComboChance : Tune.phase2ComboChance); } }

        // ------------------------------------------------------------------ boss phases
        void BossPhaseCheck()
        {
            if (_state != State.Engage && _state != State.Approach) return;
            float f = HpFrac(this);
            int want = f <= Tune.phase2At ? 2 : (f <= Tune.phase1At ? 1 : 0);
            if (want > _phase) StartRoar(want);
        }

        void StartRoar(int phase)
        {
            _phase = phase;
            float len = _fa != null ? _fa.PlayCheer() : 0f;
            _roarSeconds = Mathf.Clamp(len > 0.1f ? len : 0.9f, 0.6f, Mathf.Max(0.6f, Tune.roarMaxSeconds));
            if (sounds != null) sounds.PlayHurt(_voicePitch * 0.75f, _voiceStyle);   // a growl
            BattleEvents.AnnounceAction(_ai, phase == 1 ? "Enraged!" : "Furious!");
            EnterState(State.Roar);
            Log("boss phase " + phase);
        }

        void TickRoar(float dt)
        {
            if (_target != null) FaceDirection(_target.transform.position - transform.position, dt);
            if (_stateTime >= _roarSeconds) { _cooldown = Mathf.Min(_cooldown, 0.2f); EnterState(State.Engage); }
        }

        // ------------------------------------------------------------------ targets
        MeleeAI FindSmartTarget()
        {
            MeleeAI best = null;
            float bestScore = float.MaxValue, curScore = float.MaxValue;
            foreach (var u in FindObjectsByType<MeleeAI>(FindObjectsSortMode.None))
            {
                if (u == null || u == _ai || !u.isActiveAndEnabled || u.IsDead || !MeleeAI.IsHostile(_ai, u)) continue;
                float dist = (u.transform.position - transform.position).magnitude;
                float score = dist * (1f + Tune.weakBias * HpFrac(u.GetComponent<FighterBrain>()));
                if (u == _target) curScore = score;
                if (score < bestScore) { bestScore = score; best = u; }
            }
            // do not dance between two targets: change only when the new one is clearly better
            if (best != _target && _target != null && curScore < float.MaxValue && bestScore > curScore * Tune.keepTargetBias) return _target;
            return best;
        }

        // ------------------------------------------------------------------ choosing an attack
        AttackMove PickMove(Moveset set)
        {
            if (set == null) return null;
            if (!Smart || set.attacks == null || set.attacks.Count == 0) return set.Pick(_lastMove);

            float gap = _target != null ? FlatDist(_target.transform.position) - BodyGap() : 0f;
            int n = set.attacks.Count;
            var w = new float[n];
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                var a = set.attacks[i];
                if (a == null || a.weight <= 0f || (a == _lastMove && n > 1)) continue;
                float reach = WeaponReach() * Mathf.Max(1f, a.reachMul) + a.lungeModelMeters * Scale() * 0.7f + reachSlackFactor * _height;
                w[i] = reach < gap ? a.weight * Tune.outOfReachWeight : a.weight;
                total += w[i];
            }
            if (total <= 0f) return set.Pick(_lastMove);

            float r = Random.value * total;
            for (int i = 0; i < n; i++)
            {
                if (w[i] <= 0f) continue;
                r -= w[i];
                if (r <= 0f) return set.attacks[i];
            }
            return set.Pick(_lastMove);
        }

        // ------------------------------------------------------------------ the Engage state, Smart version
        void TickEngageSmart(float dt, Vector3 to)
        {
            float dist = to.magnitude;
            float stop = StopDist();
            if (dist > stop + 0.35f * _height) { EnterState(State.Approach); return; }

            _cooldown -= dt;
            bool punish = Tune.punishVulnerable && _targetBrain != null && _targetBrain.Vulnerable;
            if (punish) _cooldown = Mathf.Min(_cooldown, 0f);

            // waiting for the next swing: walk an arc around the enemy instead of standing still
            if (_cooldown > Tune.circleUntil && !punish) { Circle(dt, to); return; }

            FaceDirection(to, dt);
            if (_cooldown > 0f) return;
            if (dist > stop + Tune.strikeSlackHeights * _height) { MoveToward(_target.transform.position, stop, runModelMps, dt); return; }
            StartAttack();
        }

        void Circle(float dt, Vector3 to)
        {
            if (Time.time >= _circleFlipAt)
            {
                _circleDir = -_circleDir;
                _circleFlipAt = Time.time + Random.Range(Tune.circleFlipMin, Tune.circleFlipMax);
            }
            float dist = to.magnitude;
            if (dist < 1e-5f) return;
            Vector3 radial = to / dist;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * _circleDir;
            float ring = StopDist() + Tune.ringExtraHeights * _height;
            float pull = Mathf.Clamp((dist - ring) / (0.15f * _height), -1f, 1f);   // too far: drift in. too close: drift out
            Vector3 dir = (tangent + radial * pull).normalized;
            MoveToward(transform.position + dir, 0f, walkModelMps * Tune.circleSpeedMul, dt);
        }

        // ------------------------------------------------------------------ after a swing
        // Returns true if it started a step away.
        bool TryTacticalStep()
        {
            if (!Smart || _target == null || Time.time < _nextTacticalAt) return false;
            float chance = Tune.hitAndRunChance;
            if (HpFrac(this) < Tune.cautiousBelow) chance *= 1.6f;
            if (_targetBrain != null && HpFrac(_targetBrain) < Tune.killBelow) chance *= 0.4f;
            if (_phase >= 2) chance *= 0.5f;
            if (Random.value > chance) return false;
            _nextTacticalAt = Time.time + Tune.hitAndRunGap;
            StartDodge(_target.transform.position, true);
            return true;
        }

        // Smart defence: only worth a dodge or block if the swing can actually reach this fighter.
        bool AttackWillReach(FighterBrain a)
        {
            if (a == null || a._move == null) return true;
            float dist = FlatDist(a.transform.position);
            float reach = a.BodyGap() + a.WeaponReach() * Mathf.Max(1f, a._move.reachMul) + a.reachSlackFactor * a._height + a._move.lungeModelMeters * a.Scale() * 0.7f;
            return dist <= reach * 1.1f;
        }

        void AfterDefence()
        {
            if (Smart && Tune.counterAfterDefence) _cooldown = Mathf.Min(_cooldown, 0.05f);
        }
    }
}
