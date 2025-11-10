using System.Linq;
using UnityEngine;

namespace Combat
{
    public class DebugTurnBasedHUD : MonoBehaviour
    {
        public bool show = true;
        private GUIStyle _s;
        void OnGUI()
        {
            if (!show) return;
            var units = FindObjectsByType<TurnBasedMeleeAI>(FindObjectsSortMode.None)
                .OrderBy(u => u.name).ToArray();
            if (_s == null) _s = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            int y = 8;
            GUI.Label(new Rect(8, y, 600, 20), $"TB Units: {units.Length}", _s); y += 20;
            foreach (var u in units)
            {
                string tgt = u.CachedTarget ? u.CachedTarget.name : "(none)";
                GUI.Label(new Rect(8, y, 1100, 20),
                    $"{u.name} team={u.team} init={u.CurrentInitiative} intent={u.Intent} tgt={tgt}",
                    _s);
                y += 18;
            }
        }
    }
}