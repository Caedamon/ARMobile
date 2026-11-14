using Core;
using Items;
using UnityEngine;
using Weaponary;

namespace Combat
{
    /// <summary>
    /// Decides whether this fighter should grab a weapon this round, and walks toward it.
    /// Called by MeleeAI (PlanSeek) and BattleManager (via MeleeAI.ResolveSeek).
    /// Score of a weapon = affinity / (distance + epsilon); preferred class has a higher affinity.
    /// A weapon the enemy is clearly closer to is penalised (it will probably lose the race).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeMotor))]
    [RequireComponent(typeof(WeaponHolder))]
    public sealed class WeaponSeeker : MonoBehaviour
    {
        [Header("Preference (set later from the fighter's data)")]
        public bool hasPreferredClass = false;
        public WeaponClass preferredClass = WeaponClass.OneHand;
        [Min(1f)] public float preferredBonus = 2f;

        [Header("Awareness (world meters)")]
        public float searchRadius = 5f;
        [Tooltip("If the enemy is farther than this, always go for a weapon first. Closer than this, only go if the weapon is nearer than the enemy.")]
        public float engageDistance = 0.25f;
        [Tooltip("Enemy counts as 'winning the race' if its distance to the weapon < this * my distance.")]
        [Range(0.1f, 1f)] public float raceMargin = 0.8f;
        [Range(0.05f, 1f)] public float contestedPenalty = 0.35f;

        [Header("Movement")]
        public float pickupRange = 0.06f;
        [Tooltip("Multiplies MeleeMotor.moveStepMeters while rushing to a weapon.")]
        public float rushMultiplier = 2f;
        public float pickupPauseSeconds = 0.5f;

        public bool IsSeeking { get; private set; }
        public WeaponItem Target { get; private set; }

        MeleeAI _ai;
        MeleeMotor _motor;
        MeleeAnimator _anim;
        WeaponHolder _holder;
        Health _hp;
        PersonalSpace _space;

        void Awake()
        {
            _ai = GetComponent<MeleeAI>();
            _motor = GetComponent<MeleeMotor>();
            _anim = GetComponent<MeleeAnimator>();
            _holder = GetComponent<WeaponHolder>();
            _hp = GetComponent<Health>();
            _space = GetComponent<PersonalSpace>();
        }

        void OnDisable() { ReleaseTarget(); }

        /// <summary>Called once per round before intents are resolved. True = spend this round going for a weapon.</summary>
        public bool PlanSeek()
        {
            IsSeeking = false;
            if (_holder.IsArmed || (_hp != null && _hp.IsDead)) { ReleaseTarget(); return false; }

            var enemy = NearestEnemy();
            float dEnemy = enemy != null ? _motor.FlatDistanceTo(enemy.transform.position) : float.PositiveInfinity;

            WeaponItem best = null;
            float bestScore = 0f, bestDist = 0f;

            foreach (var item in WeaponItem.All)
            {
                if (item == null || !item.IsAvailable) continue;
                if (item.Claimant != null && item.Claimant != this) continue;

                float d = _motor.FlatDistanceTo(item.transform.position);
                if (d > searchRadius) continue;

                float affinity = 1f;
                if (hasPreferredClass && item.definition != null && item.definition.weaponClass == preferredClass)
                    affinity = preferredBonus;

                float score = affinity / (d + 0.02f);

                if (enemy != null)
                {
                    float de = Flat(enemy.transform.position, item.transform.position);
                    if (de < d * raceMargin) score *= contestedPenalty;
                }

                if (score > bestScore) { bestScore = score; best = item; bestDist = d; }
            }

            if (best == null) { ReleaseTarget(); return false; }

            // Weapon first if the enemy is far away, or if the weapon is nearer than the enemy.
            bool go = dEnemy > engageDistance || bestDist < dEnemy;
            if (!go) { ReleaseTarget(); return false; }

            if (Target != best)
            {
                ReleaseTarget();
                if (!best.TryClaim(this)) return false;
                Target = best;
            }

            IsSeeking = true;
            return true;
        }

        /// <summary>One round of movement toward the chosen weapon (or the pickup itself). Returns the action duration.</summary>
        public float ExecuteSeekStep()
        {
            if (Target == null) { IsSeeking = false; return _anim != null ? _anim.PlayIdle() : 0.25f; }

            var pos = Target.transform.position;
            float d = _motor.FlatDistanceTo(pos);
            float selfR = _space != null ? _space.Radius2D : 0f;
            float reach = Mathf.Max(pickupRange, selfR + 0.015f);

            if (d <= reach) return PickUp();

            float budget = _motor.moveStepMeters * Mathf.Max(1f, rushMultiplier);
            _motor.StepTowards(pos, budget, reach * 0.5f, _anim);
            return _anim != null ? _anim.PlayWalk() : 0.5f;
        }

        float PickUp()
        {
            var item = Target;
            Target = null;
            IsSeeking = false;

            var def = item != null ? item.Take() : null;
            if (def != null)
            {
                _holder.Equip(def);
                BattleEvents.AnnounceAction(_ai, "Picked up " + def.displayName + "!");
            }

            float idle = _anim != null ? _anim.PlayIdle() : 0.25f;
            return Mathf.Max(idle, pickupPauseSeconds);
        }

        void ReleaseTarget()
        {
            if (Target != null) Target.Release(this);
            Target = null;
        }

        MeleeAI NearestEnemy()
        {
            MeleeAI best = null;
            float bestSqr = float.MaxValue;
            foreach (var u in FindObjectsByType<MeleeAI>(FindObjectsSortMode.None))
            {
                if (u == null || u == _ai || u.IsDead || u.team == _ai.team) continue;
                float sq = (u.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSqr) { bestSqr = sq; best = u; }
            }
            return best;
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
