using UnityEngine;
using UnityEngine.Events;

namespace Game
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
                gameObject.SetActive(false);
            }
        }
    }
}