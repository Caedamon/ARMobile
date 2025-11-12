using Characters;
using Core;
using Game;
using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    public sealed class MeleeCombat : MonoBehaviour
    {
        [Header("AI Tuning")]
        [Range(0f, 1f)] public float dodgeProbabilityInRange = 0.0f;

        [Header("Damage")]
        public float damage = 25f;

        [Header("FX")]
        public float knockbackStrength = 0.02f; // 0 = off

        public float ExecuteDodge(MeleeAnimator anim)
        {
            var clip = anim.PickDodge();
            var dur = anim.PlayDodge(clip);
            BattleEvents.AnnounceAction(GetComponent<MeleeAI>(), "Dodge!");
            return dur;
        }

        public float ExecuteAttack(MeleeAI attacker, MeleeAI target, MeleeAnimator anim)
        {
            if (!target || target.IsDead) return anim.PlayIdle();

            // face target
            var to = target.transform.position - attacker.transform.position; to.y = 0f;
            if (to.sqrMagnitude > 1e-6f) attacker.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

            // play attack
            var ac = anim.PickAttack();
            var aDur = anim.PlayAttack(ac);

            // UI text
            string action = (ac != null && ac.Contains("Kick")) ? "Kick!" :
                            (ac != null && ac.Contains("Punch")) ? "Punch!" : "Attack!";
            BattleEvents.AnnounceAction(attacker, action);

            // apply damage
            var hp = target.GetComponent<Health>();
            hp?.TakeDamage(damage);
            BattleEvents.AnnounceDamage(target, damage);

            // victim hit anim (if alive)
            var tAnim = target.GetComponent<MeleeAnimator>();
            float vDur = 0f;
            if (tAnim != null && !target.IsDead)
            {
                var hc = tAnim.PickHit();
                vDur = tAnim.PlayHit(hc);
            }

            // knockback (optional)
            if (knockbackStrength > 0f && to.sqrMagnitude > 1e-6f)
            {
                var kb = target.GetComponent<KaijuBody>() ?? target.GetComponentInChildren<KaijuBody>();
                if (kb != null) kb.ApplyKnockback(to.normalized, knockbackStrength);
            }

            if (target.IsDead)
            {
                var dDur = target.GetComponent<MeleeAnimator>()?.PlayDeath() ?? 0f;
                return Mathf.Max(aDur, vDur, dDur);
            }

            return Mathf.Max(aDur, vDur);
        }
    }
}
