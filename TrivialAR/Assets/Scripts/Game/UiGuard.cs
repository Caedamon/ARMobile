using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    /// <summary>Lets world taps (placing, picking fighters) ignore taps that landed on a UI button.</summary>
    public static class UiGuard
    {
        static readonly List<RaycastResult> Results = new List<RaycastResult>();

        public static bool IsOverUI(Vector2 screenPos)
        {
            if (GameUI.IsPointOverButton(screenPos)) return true;
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            Results.Clear();
            es.RaycastAll(data, Results);
            return Results.Count > 0;
        }
    }
}
