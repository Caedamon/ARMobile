using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Core
{
    public class Health : MonoBehaviour
    {
        [Header("Config")]
        public float maxHP = 200f;
        public UnityEvent onDeath;

        [Header("Runtime (read-only)")]
        [SerializeField] private float _hp;

        // <summary>Current health points.</summary>
        public float CurrentHP => _hp;
        // <summary>Maximum health points.</summary>
        public float MaxHP => maxHP;

        // <summary>(current, max) fired whenever HP changes.</summary>
        public event Action<float, float> OnHealthChanged;

        void Awake()
        {
            _hp = Mathf.Max(0f, maxHP);
            OnHealthChanged?.Invoke(_hp, maxHP);
        }

        public bool IsDead => _hp <= 0f;

        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            if (amount <= 0f) return;

            _hp = Mathf.Max(0f, _hp - amount);
            OnHealthChanged?.Invoke(_hp, maxHP);

            if (_hp <= 0f)
            {
                onDeath?.Invoke();
                StartCoroutine(DisableAfterSeconds(2.0f));
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            if (amount <= 0f) return;

            _hp = Mathf.Min(maxHP, _hp + amount);
            OnHealthChanged?.Invoke(_hp, maxHP);
        }

        public void ResetHP(float? to = null)
        {
            _hp = Mathf.Clamp(to ?? maxHP, 0f, maxHP);
            OnHealthChanged?.Invoke(_hp, maxHP);
        }

        private IEnumerator DisableAfterSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            gameObject.SetActive(false);
        }
    }
}