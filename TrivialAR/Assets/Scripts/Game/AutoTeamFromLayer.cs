using UnityEngine;
using Combat;

namespace Game
{
    [DisallowMultipleComponent]
    public class AutoTeamFromLayer : MonoBehaviour
    {
        [SerializeField] private bool setOnce = true;

        private SimpleMeleeAI _ai;

        void Awake()
        {
            _ai = GetComponent<SimpleMeleeAI>();
            Apply();
        }

        void Start()
        {
            if (!setOnce) Apply();
        }

        private void Apply()
        {
            if (_ai == null) return;

            var layerName = LayerMask.LayerToName(gameObject.layer);
            if (string.Equals(layerName, "Main", System.StringComparison.OrdinalIgnoreCase))
                _ai.team = Team.Main;
            else if (string.Equals(layerName, "Enemy", System.StringComparison.OrdinalIgnoreCase))
                _ai.team = Team.Enemy;
        }
    }
}