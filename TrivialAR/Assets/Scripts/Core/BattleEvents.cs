using System;
using Combat;
using UnityEngine;

namespace Core
{
    // Lightweight event bus for HUD messages (decouples AI from UI).
    public static class BattleEvents
    {
        // Fired for short action strings (e.g., "Dodge!", "Punch!", "Kick!")
        public static event Action<MeleeAI, string> OnActionText;

        // Broadcast a freeform action message for 'who'.
        public static void AnnounceAction(MeleeAI who, string text)
        {
            // Keep UI optional; no hard dependency from combat to UI.
            OnActionText?.Invoke(who, text);
        }

        // Convenience for damage notifications shown on the victim's side.
        public static void AnnounceDamage(MeleeAI target, float amount)
        {
            OnActionText?.Invoke(target, $"Got hit for {Mathf.RoundToInt(amount)}");
        }
    }
}