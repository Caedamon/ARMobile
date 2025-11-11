// Assets/Scripts/Combat/TurnBasedEvents.cs
using System;
using UnityEngine;

namespace Combat
{
    /// Lightweight event bus for HUD messages (decouples AI from UI).
    public static class TurnBasedEvents
    {
        /// Fired for short action strings (e.g., "Dodge!", "Punch!", "Kick!")
        public static event Action<TurnBasedMeleeAI, string> OnActionText;

        /// Broadcast a freeform action message for 'who'.
        public static void AnnounceAction(TurnBasedMeleeAI who, string text)
        {
            // Keep UI optional; no hard dependency from combat to UI.
            OnActionText?.Invoke(who, text);
        }

        /// Convenience for damage notifications shown on the victim's side.
        public static void AnnounceDamage(TurnBasedMeleeAI target, float amount)
        {
            OnActionText?.Invoke(target, $"Got hit for {Mathf.RoundToInt(amount)}");
        }
    }
}