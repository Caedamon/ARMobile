using Combat;
using UnityEngine;

namespace UI
{
    public class DebugBattleHUD : MonoBehaviour
    {
        [SerializeField] private MeleeAI a;
        [SerializeField] private MeleeAI b;

        void OnGUI()
        {
            if (!a || !b) return;
            GUI.Label(new Rect(10, 10, 400, 20), $"A: {a.name}  Intent={a.Intent}  HPDead={a.IsDead}");
            GUI.Label(new Rect(10, 30, 400, 20), $"B: {b.name}  Intent={b.Intent}  HPDead={b.IsDead}");
        }
    }
}