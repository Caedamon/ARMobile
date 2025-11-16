using UnityEngine;

namespace Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterStats : MonoBehaviour
    {
        [Header("Core Stats")]
        [Min(0)] public float EGO = 0f;
        [Min(0)] public float EDUCATION = 0f;
        [Min(0)] public float MIGHT = 5f; // main melee scaler
        [Min(0)] public float INSIGHT = 5f; // crit chance/multiplier
        [Header("Hidden")]
        [Min(0)] public float LUCK = 5f; // hidden crit smoothing

        [Header("Meta")]
        [Min(1)] public int Level = 1;

        // 5% per MIGHT by default
        public float GetMeleeScale() => 1f + 0.05f * Mathf.Max(0f, MIGHT);

        // Base 5% + 0.5% per INSIGHT + 0.5% per LUCK, clamped
        public float GetCritChance01()
        {
            float c = 0.05f + 0.005f * (Mathf.Max(0f, INSIGHT) + Mathf.Max(0f, LUCK));
            return Mathf.Clamp01(c);
        }

        // 1.5x base + 1% per INSIGHT
        public float GetCritMultiplier() => 1.5f + 0.01f * Mathf.Max(0f, INSIGHT);
    }
}