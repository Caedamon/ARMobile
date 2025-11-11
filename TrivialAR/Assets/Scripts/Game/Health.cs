using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace UI
{
    public class Health : MonoBehaviour
    {
        public float maxHP = 200f;
        public UnityEvent onDeath;

        private float _hp;

        void Awake()
        {
            _hp = maxHP;
        }

        public bool IsDead => _hp <= 0f;

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            _hp -= amount;
            if (_hp <= 0f)
            {
                _hp = 0f;
                onDeath?.Invoke();
                StartCoroutine(DisableAfterSeconds());
            }
        }

        private IEnumerator DisableAfterSeconds()
        {
            const float seconds = 2.0f;
                yield return new WaitForSeconds(seconds);
                gameObject.SetActive(false);
        }
    }
}