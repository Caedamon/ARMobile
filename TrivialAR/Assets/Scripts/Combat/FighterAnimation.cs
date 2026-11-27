using UnityEngine;
using Weaponary;

namespace Combat
{
    /// <summary>
    /// Swaps in the right clips for the situation (via the fighter's AnimatorOverrideController):
    /// combat stance by weapon class, directional dodge, hit/death variants, plus pickup and spawn.
    /// Added at runtime by ArenaFighterSpawner next to the FighterBrain.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FighterAnimation : MonoBehaviour
    {
        public enum DodgeDir { Back, Left, Right }

        public AnimationPack pack;
        public bool debugLogs = false;
        public string pickupTrigger = "Pickup";
        public string spawnTrigger = "Spawn";
        public string blockTrigger = "BlockStart";
        public string cheerTrigger = "Cheer";
        [Tooltip("No arrival animation at all (the Boss intro sets this: the boss has already made its entrance).")]
        public bool skipSpawn;

        Animator _animator;
        AnimatorOverrideController _aoc;
        WeaponHolder _holder;
        bool _ready;
        bool _hasPickup, _hasSpawn, _hasBlock, _hasCheer;
        static bool _warnedYield;
        int _stance = -1;
        int _lastHit = -1, _lastDeath = -1;

        /// <summary>True if dodges use left/right/back clips (the fighter should keep facing its attacker).</summary>
        public bool HasDirectionalDodge
        {
            get
            {
                return pack != null && pack.dodgeOriginal != null &&
                       (pack.dodgeBack != null || pack.dodgeLeft != null || pack.dodgeRight != null);
            }
        }

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _holder = GetComponent<WeaponHolder>();
        }

        void Update()
        {
            if (!EnsureReady()) return;
            int s = StanceFor();
            if (s == _stance) return;
            _stance = s;
            AnimationClip clip = s == 1 ? pack.idleUnarmed : (s == 2 ? pack.idleTwoHand : null);
            SetOverride(pack.idleOriginal, clip);   // null = back to the default idle
            Log("stance " + s);
        }

        // 0 = default idle (one-handed / dual wield), 1 = unarmed, 2 = two-handed
        int StanceFor()
        {
            var cur = _holder != null ? _holder.Current : null;
            if (cur == null) return 1;
            return cur.weaponClass == WeaponClass.TwoHand ? 2 : 0;
        }

        /// <summary>Starts the arrival. Returns how long the fighter should wait before it starts to fight.</summary>
        public float PlaySpawn()
        {
            if (skipSpawn) return 0f;
            if (!EnsureReady() || !_hasSpawn || pack.spawnClip == null) return 0f;

            // Portal arrival: the fighter falls out of a portal (FighterArrival moves it) and the "Spawn_Air" clip plays in the controller's Spawn state.
            var arrival = GetComponent<FighterArrival>();
            if (arrival != null && arrival.enabled && pack.spawnAirClip != null)
            {
                SetOverride(pack.spawnClip, pack.spawnAirClip);
                return arrival.Begin(this, pack.spawnAirClip.length);
            }

            _animator.SetTrigger(spawnTrigger);
            return pack.spawnClip.length;
        }

        /// <summary>Plays the arrival animation right now (used by FighterArrival once the portal has opened).</summary>
        public void TriggerSpawn()
        {
            if (_animator != null) _animator.SetTrigger(spawnTrigger);
        }

        public float PlayPickup()
        {
            if (!EnsureReady() || !_hasPickup || pack.pickupClip == null) return 0f;
            _animator.SetTrigger(pickupTrigger);
            return pack.pickupClip.length;
        }

        public float PlayBlock()
        {
            if (!EnsureReady() || !_hasBlock || pack.blockClip == null) return 0f;
            _animator.SetTrigger(blockTrigger);
            return pack.blockClip.length;
        }

        /// <summary>The winner celebrates. Returns the clip length, or 0 if there is no cheer state yet.</summary>
        public float PlayCheer()
        {
            if (!EnsureReady() || !_hasCheer || pack.cheerClip == null) return 0f;
            _animator.SetTrigger(cheerTrigger);
            return pack.cheerClip.length;
        }

        /// <summary>The next hit reaction plays the 'blocked a hit' clip.</summary>
        public void PrepareBlockHit()
        {
            if (!EnsureReady() || pack.hitOriginal == null || pack.blockHitClip == null) return;
            SetOverride(pack.hitOriginal, pack.blockHitClip);
        }

        public void PrepareDodge(DodgeDir dir)
        {
            if (!EnsureReady() || pack.dodgeOriginal == null) return;
            AnimationClip c = dir == DodgeDir.Back ? pack.dodgeBack : (dir == DodgeDir.Left ? pack.dodgeLeft : pack.dodgeRight);
            if (c != null) SetOverride(pack.dodgeOriginal, c);
        }

        public void PrepareHit()
        {
            if (!EnsureReady() || pack.hitOriginal == null) return;
            var c = Pick(pack.hitClips, ref _lastHit);
            if (c != null) SetOverride(pack.hitOriginal, c);
        }

        public void PrepareDeath()
        {
            if (!EnsureReady() || pack.deathOriginal == null) return;
            var c = Pick(pack.deathClips, ref _lastDeath);
            if (c != null) SetOverride(pack.deathOriginal, c);
        }

        public bool HasYield { get { return pack != null && pack.yieldClip != null; } }

        /// <summary>The loser of a Duel gives up: plays the yield clip through the death state. Falls back to the death clip.</summary>
        public void PrepareYield()
        {
            if (!EnsureReady() || pack.deathOriginal == null) return;
            if (pack.yieldClip != null) { SetOverride(pack.deathOriginal, pack.yieldClip); return; }
            if (!_warnedYield)
            {
                _warnedYield = true;
                Debug.LogWarning("[FighterAnim] The Animation Pack has no 'Yield Clip', so the loser of a duel plays the normal death animation. Run Tools > Kaiju > Duel > Set Up Duel (or drag a clip onto 'Yield Clip' in the pack).", this);
            }
            PrepareDeath();
        }

        // ------------------------------------------------------------------ internals
        bool EnsureReady()
        {
            if (_ready) return true;
            if (pack == null || _animator == null) return false;

            var rac = _animator.runtimeAnimatorController;
            if (rac == null) return false;

            _aoc = rac as AnimatorOverrideController;
            if (_aoc == null)
            {
                _aoc = new AnimatorOverrideController(rac);
                _animator.runtimeAnimatorController = _aoc;
            }

            _hasPickup = pack.controllerHasPickup && HasParam(pickupTrigger);
            _hasSpawn = pack.controllerHasSpawn && HasParam(spawnTrigger);
            _hasBlock = pack.controllerHasBlock && HasParam(blockTrigger);
            _hasCheer = pack.controllerHasCheer && HasParam(cheerTrigger);
            _ready = true;
            Log("ready (pickup state: " + _hasPickup + ", spawn state: " + _hasSpawn + ")");
            return true;
        }

        bool HasParam(string paramName)
        {
            foreach (var p in _animator.parameters)
                if (p.name == paramName) return true;
            return false;
        }

        void SetOverride(AnimationClip original, AnimationClip clip)
        {
            if (_aoc != null && original != null) _aoc[original] = clip;
        }

        static AnimationClip Pick(System.Collections.Generic.List<AnimationClip> list, ref int last)
        {
            if (list == null || list.Count == 0) return null;
            int n = list.Count;
            int i = (n == 1 || last < 0) ? Random.Range(0, n) : (last + Random.Range(1, n)) % n;
            last = i;
            return list[i];
        }

        void Log(string message)
        {
            if (debugLogs) Debug.Log("[FighterAnim] " + name + ": " + message, this);
        }
    }
}
