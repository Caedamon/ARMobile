using UnityEngine;

namespace Game
{
    /// <summary>
    /// Materials for the runtime-built props (roster floor, tombstone). Unity's default primitive material
    /// does not exist in an Android build (it renders magenta), so we copy a real URP material that the scene references.
    /// GameController.stageMaterial is assigned by Tools > Kaiju > Game > Create Stage Material.
    /// </summary>
    public static class StageMaterials
    {
        public static Material Base;
        static Shader _fallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Base = null; _fallback = null; }

        /// <summary>A coloured copy of the stage material (or null if nothing usable exists).</summary>
        public static Material Make(Color color)
        {
            Material m = null;
            if (Base != null)
            {
                m = new Material(Base);
            }
            else
            {
                if (_fallback == null) _fallback = Shader.Find("Universal Render Pipeline/Lit");
                if (_fallback == null) _fallback = Shader.Find("Universal Render Pipeline/Unlit");
                if (_fallback == null) return null;
                m = new Material(_fallback);
            }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }
    }
}
