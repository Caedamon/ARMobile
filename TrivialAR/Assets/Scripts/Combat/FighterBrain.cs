using Characters;
using Core;
using Fighters;
using UnityEngine;
using Weaponary;

namespace Combat
{
    /// <summary>
    /// Real-time fighter AI. Every fighter runs its own state machine every frame (no shared rounds):
    /// Spawn -> SeekWeapon/Pickup -> Approach -> Engage -> Attack (wind-up, strike, recovery) -> Hit / Dodge -> Dead.
    /// Damage lands at the strike frame of the animation, not when the swing starts.
    /// Switched on per arena with ArenaFighterSpawner.realtimeCombat; the old round-based system is untouched.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeAI))]
    [RequireComponent(typeof(MeleeAnimator))]
    [RequireComponent(typeof(MeleeMotor))]
    [RequireComponent(typeof(Health))]
    public sealed partial class FighterBrain : MonoBehaviour
    {
        enum State { Spawn, SeekWeapon, Pickup, Approach, Engage, Attack, Hit, Dodge, Block, Cheer, Roar, Dead }

        [Header("Debug")]
        public bool debugLogs = false;

        [Header("Moves")]
        [Tooltip("Used when no weapon is equipped.")]
        public Moveset unarmedMoveset;
        [Tooltip("Name of the Animator state the attack clips play in.")]
        public string attackStateName = "Attack_Light";

        [Header("Locomotion (model meters per second; scaled by the fighter's size)")]
        public float walkModelMps = 2.4f;
        public float runModelMps = 3.4f;
        [Tooltip("Speed the walk clip looks right at. Playback is sped up/slowed down to match, so feet don't slide.")]
        public float walkClipModelMps = 1.6f;
        public float turnDegPerSecond = 600f;

        [Header("Reach (fractions of the fighter's own body height, so medium and large scale automatically)")]
        [Tooltip("Half the body width, as a fraction of height. Two fighters' radii add up to their minimum gap.")]
        public float bodyRadiusFactor = 0.20f;
        public float unarmedReachFactor = 0.25f;
        public float oneHandReachFactor = 0.45f;
        public float twoHandReachFactor = 0.60f;
        public float dualReachFactor = 0.40f;
        [Tooltip("How close to a weapon pickup before grabbing it.")]
        public float pickupReachFactor = 0.30f;

        [Header("Hit resolution")]
        public float hitConeDegrees = 80f;
        [Tooltip("Extra forgiveness on the hit check, as a fraction of body height.")]
        public float reachSlackFactor = 0.12f;
        public float hitStopSeconds = 0.05f;

        [Header("Reactions")]
        public float hitStunSeconds = 0.35f;
        public float knockbackSeconds = 0.18f;
        // dodge chance and cooldown now live in the CombatTuning asset
        public float dodgeSeconds = 0.55f;
        public float dodgeInvulnSeconds = 0.45f;
        public float dodgeModelMeters = 1.4f;

        [Header("Balance numbers (shield, dodge, per-style multipliers)")]
        [Tooltip("Set by the ArenaFighterSpawner. Empty = the built-in defaults.")]
        public CombatTuning tuningAsset;

        [Header("Sounds")]
        [Tooltip("Set by the ArenaFighterSpawner (Combat Sounds). Empty = this fighter is silent.")]
        public CombatSounds sounds;

        [Header("Pacing")]
        public float startDelayMax = 0.6f;
        public float retargetInterval = 0.35f;

        /// <summary>Measured at start: the fighter's height in world meters.</summary>
        public float BodyHeight { get { return _height; } }

        /// <summary>True while a dodge's invulnerability window is open.</summary>
        public bool IsBlocking { get { return _state == State.Block; } }

        public bool IsInvulnerable { get { return _state == State.Dodge && _stateTime < _dodgeInvuln; } }

        static readonly AttackMove FallbackMove = new AttackMove();

        CombatTuning _tuning;
        CombatTuning T
        {
            get
            {
                if (_tuning == null) _tuning = tuningAsset != null ? tuningAsset : CombatTuning.Default();
                return _tuning;
            }
        }

        // The tuning multipliers for whatever style the fighter is using right now.
        StyleTuning StyleNow()
        {
            return T.For(ProgressionRules.StyleKey(_holder != null ? _holder.Current : null));
        }

        MeleeAI _ai;
        MeleeAnimator _ma;
        MeleeMotor _motor;
        MeleeCombat _meleeCombat;
        Health _hp;
        Animator _animator;
        WeaponHolder _holder;
        WeaponSeeker _seeker;
        PersonalSpace _space;
        FighterAnimation _fa;
        FighterIdentity _identity;
        CharacterStats _stats;

        State _state;
        float _stateTime;
        float _cooldown;
        float _spawnDelay;
        float _retargetTimer;
        float _nextSeekPlan;
        float _nextDodgeAt;
        float _nextBlockAt;
        float _blockSeconds;
        Vector3 _blockFrom;
        float _hitStopUntil;
        float _pickupPause;
        float _stunSeconds;
        float _height = 0.09f;
        float _voicePitch = 1f;
        VoiceStyle _voiceStyle = VoiceStyle.Normal;
        bool _duel;   // Duel: nobody dies, the loser kneels
        bool _hadTarget, _cheered;   // the winner cheers once, when nobody is left to fight

        MeleeAI _target;
        FighterBrain _targetBrain;

        // locomotion
        bool _wantMove;
        float _moveModelMps;
        float _speed01;
        float _animSpeedTarget = 1f;

        // attack
        AttackMove _move;
        AttackMove _lastMove;
        float _attackStart;
        bool _attackSeen;
        bool _hitDone;
        float _lungeApplied;

        // dodge / knockback
        Vector3 _dodgeDir;
        Vector3 _dodgeFromPos;
        float _dodgeDistance;
        float _dodgeMoved;
        Vector3 _kbDir;
        float _kbRemaining;
        float _kbRate;

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            _ai = GetComponent<MeleeAI>();
            _ma = GetComponent<MeleeAnimator>();
            _motor = GetComponent<MeleeMotor>();
            _meleeCombat = GetComponent<MeleeCombat>();
            _hp = GetComponent<Health>();
            _animator = GetComponent<Animator>();
            _holder = GetComponent<WeaponHolder>();
            _seeker = GetComponent<WeaponSeeker>();
            _space = GetComponent<PersonalSpace>();
            _fa = GetComponent<FighterAnimation>();
        }

        void Start()
        {
            _height = MeasureHeight();
            _duel = FighterSession.Mode == FightMode.Duel;
            if (_duel && _hp != null) _hp.keepBodyOnDeath = true;   // the loser stays on the table, kneeling
            if (sounds != null) _voicePitch = sounds.VoicePitchFor(_ai.team == Combat.Team.Enemy);   // each fighter keeps its own voice
            _voiceStyle = PickVoiceStyle();
            Log("body height " + _height.ToString("0.000") + " m, stop distance (unarmed, vs same size) " + (bodyRadiusFactor * 2f * _height + unarmedReachFactor * _height).ToString("0.000") + " m");
            _ma.SetSpeed(0f);
            _spawnDelay = Random.Range(0.15f, Mathf.Max(0.2f, startDelayMax));
            if (_fa != null) _spawnDelay = Mathf.Max(_spawnDelay, _fa.PlaySpawn());   // wait for the arrival animation
            _cooldown = Random.Range(0.1f, 0.4f);
            TacticsStart();
            EnterState(State.Spawn);
        }

        void OnDisable()
        {
            if (_animator != null) _animator.speed = 1f;
        }

        void Update()
        {
            if (_state == State.Dead) return;
            if (_hp.IsDead) { EnterDead(); return; }

            float dt = Time.deltaTime;
            _stateTime += dt;

            if (Time.time < _hitStopUntil) { _animator.speed = 0f; return; }   // impact freeze

            _wantMove = false;
            _animSpeedTarget = 1f;
            TickTarget(dt);
            if (IsBoss) BossPhaseCheck();

            if (_target != null) _hadTarget = true;
            else if (_hadTarget && !_cheered && (_state == State.Engage || _state == State.Approach)) StartCheer();

            switch (_state)
            {
                case State.Cheer:      break;
                case State.Roar:       TickRoar(dt); break;
                case State.Spawn:      TickSpawn(); break;
                case State.SeekWeapon: TickSeek(dt); break;
                case State.Pickup:     TickPickup(); break;
                case State.Approach:   TickApproach(dt); break;
                case State.Engage:     TickEngage(dt); break;
                case State.Attack:     TickAttack(dt); break;
                case State.Hit:        TickHit(); break;
                case State.Dodge:      TickDodge(dt); break;
                case State.Block:      TickBlock(dt); break;
            }

            ApplyKnockback(dt);
            ApplyLocomotionAnim(dt);
        }

        // Nobody left to fight: celebrate (Cheering clip, if the Animation Pack has one).
        void StartCheer()
        {
            _cheered = true;
            EnterState(State.Cheer);
            if (_fa != null) _fa.PlayCheer();
        }

        void EnterState(State s)
        {
            _state = s;
            _stateTime = 0f;
            Log("state -> " + s);
        }

        // ------------------------------------------------------------------ targeting
        void TickTarget(float dt)
        {
            _retargetTimer -= dt;
            bool bad = _target == null || !_target.isActiveAndEnabled || _target.IsDead;
            if (!bad && _retargetTimer > 0f) return;
            if (_state == State.Attack && !bad) return;   // finish the swing on the same enemy

            _retargetTimer = retargetInterval;
            var best = Smart ? FindSmartTarget() : FindNearestEnemy();
            if (best != _target)
            {
                _target = best;
                _targetBrain = best != null ? best.GetComponent<FighterBrain>() : null;
            }
        }

        MeleeAI FindNearestEnemy()
        {
            MeleeAI best = null;
            float bestSqr = float.MaxValue;
            foreach (var u in FindObjectsByType<MeleeAI>(FindObjectsSortMode.None))
            {
                if (u == null || u == _ai || !u.isActiveAndEnabled || u.IsDead || !MeleeAI.IsHostile(_ai, u)) continue;
                float sq = (u.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSqr) { bestSqr = sq; best = u; }
            }
            return best;
        }

        // ------------------------------------------------------------------ states
        void TickSpawn()
        {
            if (_stateTime >= _spawnDelay) EnterState(State.Approach);
        }

        void TickApproach(float dt)
        {
            if (TryStartSeeking()) return;
            if (_target == null) return;

            float stop = StopDist();
            if (FlatDist(_target.transform.position) <= stop + 0.003f) { EnterState(State.Engage); return; }
            MoveToward(_target.transform.position, stop, walkModelMps, dt);
        }

        void TickEngage(float dt)
        {
            if (_target == null) return;
            if (TryStartSeeking()) return;

            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            if (Smart) { TickEngageSmart(dt, to); return; }
            FaceDirection(to, dt);

            if (to.magnitude > StopDist() + 0.02f) { EnterState(State.Approach); return; }

            _cooldown -= dt;
            if (_cooldown <= 0f) StartAttack();
        }

        bool TryStartSeeking()
        {
            if (_seeker == null || _holder == null || _holder.IsArmed) return false;
            if (Time.time < _nextSeekPlan) return false;
            _nextSeekPlan = Time.time + 0.25f;
            if (!_seeker.PlanSeek()) return false;
            EnterState(State.SeekWeapon);
            return true;
        }

        void TickSeek(float dt)
        {
            if (_holder != null && _holder.IsArmed) { EnterState(State.Approach); return; }

            if (Time.time >= _nextSeekPlan)
            {
                _nextSeekPlan = Time.time + 0.25f;
                if (!_seeker.PlanSeek()) { EnterState(State.Approach); return; }
            }

            var item = _seeker.Target;
            if (item == null) { EnterState(State.Approach); return; }

            float reach = Mathf.Max(0.02f, pickupReachFactor * _height);
            Vector3 pos = item.transform.position;

            if (FlatDist(pos) <= reach)
            {
                _pickupPause = _seeker.PickUp();
                if (sounds != null) sounds.PlayPickup();
                if (_fa != null) _pickupPause = Mathf.Max(_pickupPause, _fa.PlayPickup());
                EnterState(State.Pickup);
                return;
            }
            MoveToward(pos, reach * 0.5f, runModelMps, dt);
        }

        void TickPickup()
        {
            if (_stateTime >= _pickupPause) EnterState(State.Approach);
        }

        // ------------------------------------------------------------------ attacking
        Moveset CurrentMoveset()
        {
            if (_holder != null && _holder.Current != null && _holder.Current.moveset != null)
                return _holder.Current.moveset;
            return unarmedMoveset;
        }

        string StyleKeyNow() { return ProgressionRules.StyleKey(_holder != null ? _holder.Current : null); }

        // Proficiency level in 'style', but only while that is the style being fought with (each style's perk works only while using it).
        float ProfLevelFor(string style)
        {
            if (StyleKeyNow() != style) return 0f;
            var d = Data();
            return d != null ? ProgressionRules.ProfLevel(d, style) : 0f;
        }

        float AttackSpeed()
        {
            return Mathf.Max(0.3f, _move.speed * TierSpeedMul * StyleNow().speedMul * (1f + T.attackSpeedPerInsight * (Insight() - 5f))
                                   * (1f + T.profAttackSpeedPerLevel * ProfLevelFor(ProgressionRules.Dual)) * ArchAttackSpeedMul);
        }

        void StartAttack()
        {
            var set = CurrentMoveset();
            _move = PickMove(set);
            if (_move == null) _move = FallbackMove;
            _lastMove = _move;

            if (_move.clip != null && _holder != null) _holder.PrepareAttackClip(_move.clip);

            _animator.SetBool(_ma.heavyBool, false);
            _animator.ResetTrigger(_ma.attackTrigger);
            _animator.SetTrigger(_ma.attackTrigger);
            _speed01 = 0f;
            _ma.SetSpeed(0f);

            _attackStart = Time.time;
            _attackSeen = false;
            _hitDone = false;
            _lastHitLanded = false;
            _lungeApplied = 0f;

            BattleEvents.AnnounceAction(_ai, _move.label);

            // Give the defender a chance to react before the strike lands.
            float len = _move.clip != null ? _move.clip.length : 0.7f;
            float hitDelay = len * _move.hitAt / AttackSpeed();
            if (sounds != null) sounds.PlaySwing(hitDelay, _voicePitch, _voiceStyle);
            if (_targetBrain != null) _targetBrain.OnIncomingAttack(this, hitDelay);

            EnterState(State.Attack);
            Log("attack '" + _move.label + "' hit in " + hitDelay.ToString("0.00") + "s");
        }

        void TickAttack(float dt)
        {
            _animSpeedTarget = AttackSpeed();

            float n;
            bool inAttack = TryGetAttackProgress(out n);
            if (!inAttack)
            {
                if (_attackSeen) { FinishAttack(); return; }                 // animation finished
                if (Time.time - _attackStart > 0.6f)
                {
                    Log("attack state never started; aborting");
                    FinishAttack();
                }
                return;
            }
            _attackSeen = true;

            // Keep facing the enemy until the strike lands.
            if (!_hitDone && _target != null)
                FaceDirection(_target.transform.position - transform.position, dt);

            // Step in during the wind-up.
            if (!_hitDone && _move.lungeModelMeters > 0f && _target != null)
            {
                float p = Mathf.Clamp01(n / Mathf.Max(0.05f, _move.hitAt));
                float eased = 1f - (1f - p) * (1f - p);
                float step = _move.lungeModelMeters * Scale() * eased - _lungeApplied;
                if (step > 0f)
                {
                    float gap = FlatDist(_target.transform.position) - BodyGap();
                    step = Mathf.Min(step, Mathf.Max(0f, gap));
                    transform.position += transform.forward * step;
                    _lungeApplied += step;
                }
            }

            if (!_hitDone && n >= _move.hitAt) { _hitDone = true; ResolveHit(); }
            if (n >= _move.endAt) FinishAttack();
        }

        bool TryGetAttackProgress(out float n)
        {
            var cur = _animator.GetCurrentAnimatorStateInfo(0);
            if (cur.IsName(attackStateName)) { n = cur.normalizedTime; return true; }
            if (_animator.IsInTransition(0))
            {
                var next = _animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(attackStateName)) { n = next.normalizedTime; return true; }
            }
            n = 0f;
            return false;
        }

        void FinishAttack()
        {
            var set = CurrentMoveset();
            Vector2 range = set != null ? set.cooldownRange : new Vector2(0.3f, 0.6f);
            float insightFactor = Mathf.Clamp(1f + 0.04f * (Insight() - 5f), 0.6f, 1.6f);
            _cooldown = Random.Range(range.x, range.y) * StyleNow().cooldownMul / insightFactor;
            if (Smart) _cooldown *= CooldownFactor();
            bool combo = IsBoss && _lastHitLanded && Random.value < ComboChance;
            if (combo) _cooldown = Mathf.Min(_cooldown, Tune.comboCooldown);
            if (!combo && TryTacticalStep()) return;
            EnterState(State.Engage);
        }

        void ResolveHit()
        {
            if (_target == null || _target.IsDead) return;
            var hp = _target.GetComponent<Health>();
            if (hp == null) return;

            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            float reach = BodyGap() + WeaponReach() * Mathf.Max(1f, _move.reachMul) + reachSlackFactor * _height + _lungeApplied * 0.5f;
            bool inCone = dist < 1e-4f || Vector3.Angle(transform.forward, to) <= hitConeDegrees;

            if (dist > reach || !inCone) { Log("whiff (range/angle)"); return; }
            if (_targetBrain != null && _targetBrain.IsInvulnerable) { Log("target dodged"); return; }

            var data = Data();
            string style = ProgressionRules.StyleKey(_holder != null ? _holder.Current : null);
            float profLevel = data != null ? ProgressionRules.ProfLevel(data, style) : 0f;
            float profBonus = T.profDamagePerLevel * profLevel;

            float fallback = _meleeCombat != null ? _meleeCombat.damage : 25f;
            float dmg = DamageCalculator.ComputeMelee(gameObject, _target.gameObject, fallback) * _move.damageMul * StyleNow().damageMul * TierDamageMul * ArchDamageMul * (1f + profBonus) * (_targetBrain != null ? _targetBrain.IncomingDamageFactor() : 1f);
            // One-handed proficiency: an extra, separate chance to crit.
            if (style == ProgressionRules.OneHand && profLevel > 0f && Random.value < T.profCritPerLevel * profLevel)
                dmg *= Stats != null ? Stats.GetCritMultiplier() : 1.5f;
            hp.TakeDamage(dmg);
            _lastHitLanded = true;
            BattleEvents.AnnounceDamage(_target, dmg);
            if (data != null)
            {
                float profMul = _duel ? T.duelProfXpMultiplier : 1f;   // duels are training
                ProgressionRules.AddProfXp(data, style, Mathf.Max(1, Mathf.RoundToInt(ProgressionRules.ProfGain(data, hp.IsDead) * profMul)));
            }
            Log("hit for " + dmg.ToString("0.0"));

            bool wasBlocked = _targetBrain != null && _targetBrain.IsBlocking;
            if (sounds != null) sounds.PlayImpact(style, wasBlocked, wasBlocked ? _targetBrain.HeldWeapon : null);
            if (_targetBrain != null && !wasBlocked) _targetBrain.PlayHurtVoice();

            if (_targetBrain != null)
            {
                if (_targetBrain.IsBlocking) _targetBrain.ReceiveBlockedHit(transform.position, _move.knockbackModelMeters);
                else _targetBrain.ReceiveHit(transform.position, _move.knockbackModelMeters, _move.stagger);
                _targetBrain.StartHitStop(hitStopSeconds);
            }
            StartHitStop(hitStopSeconds);
        }

        // ------------------------------------------------------------------ reactions
        public void StartHitStop(float seconds)
        {
            _hitStopUntil = Mathf.Max(_hitStopUntil, Time.time + seconds);
        }

        /// <summary>The weapon (loadout) this fighter is holding, or null.</summary>
        public WeaponDefinition HeldWeapon { get { return _holder != null ? _holder.Current : null; } }

        /// <summary>The attacker's hit just landed on this fighter: a grunt (unless it was blocked or it is dying).</summary>
        public void PlayHurtVoice()
        {
            if (sounds != null && _state != State.Dead && _hp != null && !_hp.IsDead) sounds.PlayHurt(_voicePitch, _voiceStyle);
        }

        /// <summary>Called by an attacker at the start of its swing. The defender may decide to dodge.</summary>
        public void OnIncomingAttack(FighterBrain attacker, float hitDelay)
        {
            if (_state == State.Dead || _state == State.Attack || _state == State.Hit || _state == State.Dodge || _state == State.Block || _state == State.Roar) return;
            if (Smart && Tune.defendOnlyIfReachable && !AttackWillReach(attacker)) return;   // that swing will miss anyway
            float defMul = Smart ? Tune.defenceMul : 1f;

            // Shield users prefer to block.
            if (_holder != null && _holder.HasShield && Time.time >= _nextBlockAt)
            {
                float blockChance = Mathf.Clamp01((T.BlockChance(Insight()) + T.profBlockPerLevel * ProfLevelFor(ProgressionRules.Shield)) * defMul);
                if (Random.value <= blockChance) { StartBlock(attacker.transform.position, hitDelay); return; }
            }

            if (Time.time < _nextDodgeAt) return;

            float chance = Mathf.Clamp01((T.DodgeChance(Insight()) + T.profDodgePerLevel * ProfLevelFor(ProgressionRules.Unarmed) + ArchDodgeAdd) * defMul);
            if (Random.value > chance) return;
            StartDodge(attacker.transform.position);
        }

        void StartBlock(Vector3 fromPos, float hitDelay)
        {
            _blockFrom = fromPos;
            _blockSeconds = Mathf.Max(0.5f, hitDelay + 0.4f);
            _nextBlockAt = Time.time + _blockSeconds + T.blockCooldown;
            if (_fa != null) _fa.PlayBlock();
            BattleEvents.AnnounceAction(_ai, "Block!");
            EnterState(State.Block);
        }

        void TickBlock(float dt)
        {
            FaceDirection(_blockFrom - transform.position, dt);
            if (_stateTime >= _blockSeconds) { AfterDefence(); EnterState(State.Engage); }
        }

        /// <summary>A hit landed on a raised shield: small shove and the 'blocked' reaction, but no stagger.</summary>
        public void ReceiveBlockedHit(Vector3 fromPos, float knockbackModelMeters)
        {
            if (_state != State.Block) return;

            Vector3 dir = transform.position - fromPos;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = -transform.forward;
            dir.Normalize();

            float kb = knockbackModelMeters * Scale() * 0.3f;
            _kbDir = dir;
            _kbRemaining = kb;
            _kbRate = kb / Mathf.Max(0.05f, knockbackSeconds);

            if (_fa != null) _fa.PrepareBlockHit();
            _ma.PlayHit(null);
            Log("blocked a hit");
        }

        /// <summary>Multiplier on damage this fighter takes (blocking, or just carrying a shield).</summary>
        public float IncomingDamageFactor()
        {
            return T.IncomingFactor(_state == State.Block, _holder != null && _holder.HasShield);
        }

        void StartDodge(Vector3 fromPos, bool tactical = false)
        {
            Vector3 away = transform.position - fromPos;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) away = -transform.forward;
            away.Normalize();

            Vector3 side = Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? 1f : -1f);
            _dodgeDir = (away * 0.6f + side).normalized;
            _dodgeFromPos = fromPos;
            if (_fa != null)
            {
                Vector3 local = transform.InverseTransformDirection(_dodgeDir);
                _fa.PrepareDodge(local.z < -0.45f ? FighterAnimation.DodgeDir.Back
                                                  : (local.x >= 0f ? FighterAnimation.DodgeDir.Right : FighterAnimation.DodgeDir.Left));
            }
            _dodgeDistance = dodgeModelMeters * Scale();
            _dodgeMoved = 0f;
            if (!tactical) _nextDodgeAt = Time.time + T.dodgeCooldown;   // a tactical step does not use up the real dodge
            _dodgeInvuln = tactical ? 0f : dodgeInvulnSeconds;
            _dodgeTactical = tactical;

            if (sounds != null) sounds.PlayDodge();
            _ma.PlayDodge(null);
            if (!tactical) BattleEvents.AnnounceAction(_ai, "Dodge!");
            EnterState(State.Dodge);
        }

        void TickDodge(float dt)
        {
            float p = Mathf.Clamp01(_stateTime / Mathf.Max(0.1f, dodgeSeconds));
            float eased = 1f - (1f - p) * (1f - p);
            float step = _dodgeDistance * eased - _dodgeMoved;
            if (step > 0f) { transform.position += _dodgeDir * step; _dodgeMoved += step; }

            // Directional dodge clips keep the fighter facing its attacker; the old roll faced the way it moved.
            Vector3 face = (_fa != null && _fa.HasDirectionalDodge) ? (_dodgeFromPos - transform.position) : _dodgeDir;
            FaceDirection(face, dt);
            if (_stateTime >= dodgeSeconds)
            {
                if (!_dodgeTactical) AfterDefence();
                EnterState(State.Engage);
            }
        }

        /// <summary>Called by the attacker when its strike connects (after damage is applied).</summary>
        public void ReceiveHit(Vector3 fromPos, float knockbackModelMeters, float stagger)
        {
            if (_state == State.Dead || _hp.IsDead) return;

            Vector3 dir = transform.position - fromPos;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = -transform.forward;
            dir.Normalize();

            bool resist = _state == State.Roar || (_state == State.Attack && Random.value < Ego() * T.egoPoisePerPoint + T.profPoisePerLevel * ProfLevelFor(ProgressionRules.TwoHand) + TierPoise + ArchPoiseAdd);
            float kb = knockbackModelMeters * Scale() * (resist ? 0.4f : 1f);
            _kbDir = dir;
            _kbRemaining = kb;
            _kbRate = kb / Mathf.Max(0.05f, knockbackSeconds);

            if (resist) { Log("shrugged off the flinch"); return; }

            _stunSeconds = hitStunSeconds * Mathf.Max(0.2f, stagger);
            if (_fa != null) _fa.PrepareHit();
            _ma.PlayHit(null);
            EnterState(State.Hit);
        }

        void TickHit()
        {
            if (_stateTime < _stunSeconds) return;
            _cooldown = Mathf.Max(_cooldown, 0.15f);
            EnterState(State.Engage);
        }

        void ApplyKnockback(float dt)
        {
            if (_kbRemaining <= 0f) return;
            float step = Mathf.Min(_kbRemaining, _kbRate * dt);
            transform.position += _kbDir * step;
            _kbRemaining -= step;
        }

        void EnterDead()
        {
            _state = State.Dead;
            _animator.speed = 1f;
            if (_fa != null) { if (_duel) _fa.PrepareYield(); else _fa.PrepareDeath(); }
            if (sounds != null) { if (_duel) sounds.PlayHurt(_voicePitch, _voiceStyle); else sounds.PlayDeath(_voicePitch, _voiceStyle); }
            _ma.PlayDeath();
            Log("dead");
        }

        // ------------------------------------------------------------------ movement helpers
        void MoveToward(Vector3 worldPos, float stopDistance, float modelMps, float dt)
        {
            Vector3 to = worldPos - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 1e-5f) return;

            Vector3 dir = to / dist;
            FaceDirection(dir, dt);

            modelMps *= ArchMoveMul;   // archetype movement modifier (1 when there is none)
            float step = Mathf.Min(modelMps * Scale() * dt, Mathf.Max(0f, dist - stopDistance));
            if (step <= 0f) return;

            transform.position += dir * step;
            _wantMove = true;
            _moveModelMps = modelMps;
        }

        void FaceDirection(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-8f) return;
            var want = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnDegPerSecond * dt);
        }

        void ApplyLocomotionAnim(float dt)
        {
            _speed01 = Mathf.MoveTowards(_speed01, _wantMove ? 1f : 0f, 8f * dt);
            _ma.SetSpeed(_speed01);

            if (_wantMove)
                _animSpeedTarget = Mathf.Clamp(_moveModelMps / Mathf.Max(0.1f, walkClipModelMps), 0.75f, 2f);
            _animator.speed = _animSpeedTarget;
        }

        // ------------------------------------------------------------------ small helpers
        float Scale() { return Mathf.Max(0.0001f, transform.lossyScale.y); }

        float FlatDist(Vector3 worldPos)
        {
            Vector3 a = transform.position;
            a.y = 0f;
            worldPos.y = 0f;
            return Vector3.Distance(a, worldPos);
        }

        // Distance between the two bodies' surfaces when both stand still (so it scales with size).
        float BodyGap()
        {
            float otherH = _targetBrain != null ? _targetBrain.BodyHeight : _height;
            return bodyRadiusFactor * (_height + otherH);
        }

        float WeaponReach()
        {
            float f = unarmedReachFactor;
            if (_holder != null && _holder.Current != null)
            {
                switch (_holder.Current.weaponClass)
                {
                    case WeaponClass.TwoHand:     f = twoHandReachFactor; break;
                    case WeaponClass.DualOneHand: f = dualReachFactor;    break;
                    default:                      f = oneHandReachFactor; break;
                }
            }
            return f * _height;
        }

        // How far from the target's centre this fighter stands to attack.
        float StopDist() { return BodyGap() + WeaponReach(); }

        float MeasureHeight()
        {
            float fallback = 1.8f * Scale();
            var rends = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (rends.Length == 0) return fallback;

            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return Mathf.Clamp(b.size.y, 0.8f * Scale(), 4.5f * Scale());
        }

        CharacterStats Stats
        {
            get
            {
                if (_stats == null) _stats = GetComponent<CharacterStats>();
                return _stats;
            }
        }
        // Which kind of voice: from the prefab's Fighter Gender Tag. Either (or nothing set) goes by the generated first name.
        VoiceStyle PickVoiceStyle()
        {
            var tag = GetComponent<FighterGenderTag>();
            var g = tag != null ? tag.Gender : FighterGender.NotSet;
            switch (g)
            {
                case FighterGender.Robot:  return VoiceStyle.Robot;
                case FighterGender.Undead: return VoiceStyle.Undead;
                case FighterGender.Female: return VoiceStyle.Female;
                case FighterGender.Male:   return VoiceStyle.Normal;
            }
            var d = Data();
            return d != null && FighterGenerator.IsFemaleName(d.displayName) ? VoiceStyle.Female : VoiceStyle.Normal;
        }

        FighterData Data()
        {
            if (_identity == null) _identity = GetComponent<FighterIdentity>();
            return _identity != null ? _identity.Data : null;
        }

        float Insight() { var s = Stats; return s != null ? s.INSIGHT : 5f; }
        float Ego()     { var s = Stats; return s != null ? s.EGO : 5f; }

        void Log(string message)
        {
            if (debugLogs) Debug.Log("[Brain] " + name + ": " + message, this);
        }
    }
}
