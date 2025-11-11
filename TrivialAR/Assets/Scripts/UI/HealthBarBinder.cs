using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class HealthBarBinder : MonoBehaviour
    {
        [Header("UI")] [Tooltip("The GREEN Image set to Image Type = Filled, Horizontal, Left")]
        public Image fill;

        [Header("Target")] [Tooltip("Root object that has 'Health' (leave empty to use this GameObject)")]
        public Transform targetRoot;

        // -- internal: reflection to read private _hp from your Health
        Health _health;
        FieldInfo _hpField;
        FieldInfo _maxField;

        void Awake()
        {
            if (!targetRoot) targetRoot = transform; // allow dropping on the UI and assign manually
            _health = targetRoot.GetComponent<Health>(); // or drag a specific fighter root in Inspector
            if (_health)
            {
                _hpField = typeof(Health).GetField("_hp", BindingFlags.Instance | BindingFlags.NonPublic);
                _maxField = typeof(Health).GetField("maxHP", BindingFlags.Instance | BindingFlags.Public);
            }

            if (fill) fill.type = Image.Type.Filled; // safety
        }

        void LateUpdate()
        {
            if (!_health || _hpField == null || _maxField == null || !fill) return;

            float max = Mathf.Max(0.0001f, (float)_maxField.GetValue(_health));
            float cur = Mathf.Clamp((float)_hpField.GetValue(_health), 0f, max);
            fill.fillAmount = cur / max;
        }
    }
}