using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // For the device readout: is the menu there, how many buttons does it think are usable, is a popup "open" and blocking the others?
    public sealed partial class GameUI
    {
        public static string DebugInfo()
        {
            if (_instance == null) return "GameUI: NONE";
            return _instance.DebugInfoInternal();
        }

        string DebugInfoInternal()
        {
            int total = 0, usable = 0;
            if (_canvas != null)
            {
                var all = _canvas.GetComponentsInChildren<Button>(true);
                total = all.Length;
                foreach (var b in all) if (b.isActiveAndEnabled && b.interactable) usable++;
            }
            return "GameUI: canvas " + (_canvas != null ? "yes" : "NO") + "  buttons usable " + usable + "/" + total +
                   "  settings " + SettingsOpen + "  stats " + StatsOpen + "  modal " + ModalOpen + "  timeScale " + Time.timeScale.ToString("0.##");
        }
    }
}
