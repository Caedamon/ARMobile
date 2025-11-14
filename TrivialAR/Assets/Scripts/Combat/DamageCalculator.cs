using Characters;
using UnityEngine;
using Weaponary;

namespace Combat
{
    public static class DamageCalculator
    {
        // Safe fetchers
        static CharacterStats StatsOf(GameObject go)
        {
            if (!go) return null;
            return go.GetComponent<CharacterStats>() ?? go.GetComponentInParent<CharacterStats>();
        }

        static WeaponDamage WeaponOf(GameObject go)
        {
            if (!go) return null;
            // prefer equipped weapon in children, else self
            var wd = go.GetComponentInChildren<WeaponDamage>();
            return wd ? wd : go.GetComponent<WeaponDamage>();
        }

        public static float ComputeMelee(GameObject attacker, GameObject target, float fallbackDamage)
        {
            var atkStats = StatsOf(attacker);
            var wpn = WeaponOf(attacker);

            // Base damage
            float baseDmg = wpn ? wpn.GetBaseDamage() : Mathf.Max(1f, fallbackDamage);

            // Scale by MIGHT
            float scaled = baseDmg * (atkStats ? atkStats.GetMeleeScale() : 1f);

            // ±10% variance
            float variance = Random.Range(0.9f, 1.1f);
            float outDmg = scaled * variance;

            // Crit
            if (atkStats)
            {
                float critP = atkStats.GetCritChance01();
                if (Random.value < critP)
                    outDmg *= atkStats.GetCritMultiplier();
            }

            return Mathf.Max(1f, outDmg);
        }
    }
}