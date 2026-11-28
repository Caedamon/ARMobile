using System.Collections.Generic;
using UnityEngine;

namespace Fighters
{
    /// <summary>
    /// The list of all archetypes, in the order they appear in the Codex. The FighterCatalog points at it, so both the fights
    /// (modifiers) and the Book (pages) find the same data.
    /// </summary>
    [CreateAssetMenu(fileName = "ArchetypeLibrary", menuName = "Kaiju/Archetype Library")]
    public sealed class ArchetypeLibrary : ScriptableObject
    {
        public List<ArchetypeDefinition> entries = new List<ArchetypeDefinition>();

        public ArchetypeDefinition Find(string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return null;
            foreach (var a in entries) if (a != null && a.AppliesTo(modelId)) return a;
            return null;
        }
    }
}
