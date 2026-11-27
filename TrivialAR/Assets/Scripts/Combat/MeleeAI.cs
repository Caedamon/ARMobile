using System.Collections.Generic;
using System.Linq;
using Core;
using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeAnimator))]
    [RequireComponent(typeof(MeleeMotor))]
    [RequireComponent(typeof(Health))]
    public sealed class MeleeAI : MonoBehaviour
    {
        [Header("Team")]
        public Team team = Team.Main;
        [Tooltip("Last Hero Standing: ON = this fighter is an enemy of EVERYONE else, whatever team it is on. Set by the arena spawner.")]
        public bool freeForAll;

        /// <summary>Would these two fighters fight each other? Different teams, or a free-for-all.</summary>
        public static bool IsHostile(MeleeAI a, MeleeAI b)
        {
            if (a == null || b == null || a == b) return false;
            if (a.freeForAll || b.freeForAll) return true;
            return a.team != b.team;
        }

        [Header("Runtime")]
        public int CurrentInitiative { get; set; }
        public Intent Intent { get; private set; } = Intent.Idle;
        public MeleeAI CachedTarget { get; private set; }
        public float LastActionDuration { get; private set; } = 0f;

        public bool IsDead => _hp != null && _hp.IsDead;
        
        // Real-time combat: true when a FighterBrain drives this fighter (BattleManager then skips it).
        public bool UsesRealtimeBrain
        {
            get
            {
                if (_brain == null) _brain = GetComponent<FighterBrain>();
                return _brain != null && _brain.isActiveAndEnabled;
            }
        }

        // Weapon scramble: true while this unit is spending its rounds going for a weapon.
        public bool IsSeekingWeapon => _seeker != null && _seeker.IsSeeking;
        public void ResolveSeek() { LastActionDuration = _seeker != null ? _seeker.ExecuteSeekStep() : _anim.PlayIdle(); }

        public float MoveStepMeters => _motor != null ? _motor.moveStepMeters : 0.025f;
        public float AttackRange    => _motor != null ? _motor.attackRange    : 0.095f;

        MeleeAnimator _anim;
        MeleeMotor _motor;
        MeleeCombat _combat;
        Health _hp;
        WeaponSeeker _seeker;
        FighterBrain _brain;

        void Awake()
        {
            _anim  = GetComponent<MeleeAnimator>();
            _motor = GetComponent<MeleeMotor>();
            _combat= GetComponent<MeleeCombat>();
            _hp    = GetComponent<Health>();
            _seeker = GetComponent<WeaponSeeker>();
        }

        void Start()
        {
            _anim.PlayIdle();
        }

        public int RollInitiative() => Random.Range(1, 21) + Random.Range(0, 3);
        public void BeginThinking(float _) { Intent = Intent.Idle; LastActionDuration = 0f; }

        public void FinalizeIntent()
        {
            if (IsDead) { Intent = Intent.Idle; return; }

            // Unarmed fighters head for a weapon first (see WeaponSeeker for the rules).
            if (_seeker != null && _seeker.PlanSeek()) { Intent = Intent.Move; return; }

            var opp = FindClosestEnemy();
            CachedTarget = opp;
            if (!opp || opp.IsDead) { Intent = Intent.Idle; return; }

            float dist   = _motor.FlatDistanceTo(opp.transform.position);
            float stopAt = _motor.StopDistanceTo(opp);
            Intent = (dist > stopAt) ? Intent.Move
                                     : (Random.value < _combat.dodgeProbabilityInRange ? Intent.Dodge : Intent.Attack);
        }

        public void CacheTarget(List<MeleeAI> all)
        {
            if (IsDead) { CachedTarget = null; return; }
            bool need = CachedTarget == null || CachedTarget.IsDead ||
                        (CachedTarget.transform.position - transform.position).sqrMagnitude >
                        _motor.sightRadius * _motor.sightRadius;

            if (need)
            {
                CachedTarget = all
                    .Where(u => u && u != this && !u.IsDead && IsHostile(this, u))
                    .OrderBy(u => (u.transform.position - transform.position).sqrMagnitude)
                    .FirstOrDefault();
            }
        }

        public void ResolveSoloIntent(float moveStepOverride)
        {
            if (IsDead) return;
            float step = (moveStepOverride > 0f) ? moveStepOverride : _motor.moveStepMeters;

            switch (Intent)
            {
                case Intent.Move:
                    if (CachedTarget) { _motor.StartMoveTo(CachedTarget, step, _motor.attackRange, _anim); LastActionDuration = _anim.PlayWalk(); }
                    else { LastActionDuration = _anim.PlayIdle(); }
                    break;

                case Intent.Attack:
                    if (CachedTarget && _motor.InMeleeRange(CachedTarget))
                        LastActionDuration = _combat.ExecuteAttack(this, CachedTarget, _anim);
                    else
                        LastActionDuration = _anim.PlayIdle();
                    break;

                case Intent.Dodge:
                    LastActionDuration = _combat.ExecuteDodge(_anim);
                    break;

                default:
                    LastActionDuration = _anim.PlayIdle();
                    break;
            }
        }

        public void ResolveVsOpponent(MeleeAI opp, float moveStepOverride)
        {
            if (IsDead || !opp) { LastActionDuration = 0f; return; }

            if (Intent == Intent.Move)
            {
                if (!_motor.InMeleeRange(opp))
                {
                    _motor.StartMoveTo(opp, moveStepOverride, _motor.attackRange, _anim);
                    LastActionDuration = _anim.PlayWalk();
                    return;
                }

                Intent = (Random.value < _combat.dodgeProbabilityInRange) ? Intent.Dodge : Intent.Attack;
                if (Intent == Intent.Dodge) { LastActionDuration = _combat.ExecuteDodge(_anim); return; }
                if (Intent == Intent.Attack) { LastActionDuration = _combat.ExecuteAttack(this, opp, _anim); return; }
                LastActionDuration = _anim.PlayIdle();
                return;
            }

            if (Intent == Intent.Dodge) { LastActionDuration = _combat.ExecuteDodge(_anim); return; }

            if (Intent == Intent.Attack)
            {
                if (_motor.InMeleeRange(opp)) LastActionDuration = _combat.ExecuteAttack(this, opp, _anim);
                else LastActionDuration = _anim.PlayIdle();
                return;
            }

            LastActionDuration = _anim.PlayIdle();
        }

        // Old API shims
        public void ExecuteMoveStepTowards(Vector3 targetPos, float step, float stopAtDistance)
            => _motor.StepTowards(targetPos, step, stopAtDistance, _anim);
        public void ExecuteDodge() => _combat.ExecuteDodge(_anim);
        public void ExecuteAttack(MeleeAI target) => _combat.ExecuteAttack(this, target, _anim);

        MeleeAI FindClosestEnemy()
        {
            var all = FindObjectsByType<MeleeAI>(FindObjectsSortMode.None);
            float best = float.MaxValue; MeleeAI bestU = null;
            foreach (var u in all)
            {
                if (!u || u == this || u.IsDead) continue;
                if (!IsHostile(this, u)) continue;
                float sq = (u.transform.position - transform.position).sqrMagnitude;
                if (sq < best && sq <= _motor.sightRadius * _motor.sightRadius) { best = sq; bestU = u; }
            }
            return bestU;
        }

        void LateUpdate()
        {
            if (IsDead) return;
            if (UsesRealtimeBrain) return;   // <-- ADD (otherwise this keeps forcing the idle animation)
            if (Intent == Intent.Idle) _anim.EnsureIdlePlaying();
        }
    }
}
