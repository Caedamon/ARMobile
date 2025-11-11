using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class HealthManager : MonoBehaviour
    {
        public Image healthBar;
        public float healthAmount = 100f;

        void Start()
        {

        }

        void Update()
        {

        }

        public void TakeDamage(float damage)
        {
            healthAmount -= damage;
            healthBar.fillAmount = healthAmount / 100f;
        }

        public void Heal(float healingAmount)
        {
            healthAmount += healingAmount;
            healthAmount = Mathf.Clamp(healthAmount, 0, 100);

            healthBar.fillAmount = healthAmount / 100f;
        }
    }
}