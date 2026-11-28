using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fighters
{
    public enum FighterSide { Player, Enemy }

    /// <summary>
    /// Which character models exist and which prefab to use for each side.
    /// (Player and Enemy prefabs differ in team/layer, so each model can have one of each.)
    /// Filled automatically by Tools > Kaiju > Fighters > Setup Fighter Generation.
    /// </summary>
    [CreateAssetMenu(fileName = "FighterCatalog", menuName = "Kaiju/Fighter Catalog")]
    public sealed class FighterCatalog : ScriptableObject
    {
        [Serializable]
        public class Model
        {
            public string id;
            public GameObject playerPrefab;
            public GameObject enemyPrefab;
            public bool large;
        }

        public List<Model> models = new List<Model>();

        [Tooltip("The archetypes (classes): lore for the Codex pages and small stat modifiers. Optional: without it every fighter fights as before.")]
        public ArchetypeLibrary archetypes;

        /// <summary>The archetype of a model id, or null.</summary>
        public ArchetypeDefinition ArchetypeOf(string modelId)
        {
            return archetypes != null ? archetypes.Find(modelId) : null;
        }

        public Model Find(string id)
        {
            foreach (var m in models) if (m != null && m.id == id) return m;
            return null;
        }

        /// <summary>The gender set on the model's prefab (FighterGenderTag). Looks at the prefab of the given side first, then the other one.</summary>
        public FighterGender GenderOf(string modelId, FighterSide side)
        {
            var m = Find(modelId);
            if (m == null) return FighterGender.NotSet;
            var g = ReadTag(side == FighterSide.Player ? m.playerPrefab : m.enemyPrefab);
            if (g == FighterGender.NotSet) g = ReadTag(side == FighterSide.Player ? m.enemyPrefab : m.playerPrefab);
            return g;
        }

        static FighterGender ReadTag(GameObject prefab)
        {
            if (prefab == null) return FighterGender.NotSet;
            var tag = prefab.GetComponent<FighterGenderTag>();
            return tag != null ? tag.Gender : FighterGender.NotSet;
        }

        public List<Model> ForSide(FighterSide side)
        {
            var list = new List<Model>();
            foreach (var m in models)
            {
                if (m == null) continue;
                var prefab = side == FighterSide.Player ? m.playerPrefab : m.enemyPrefab;
                if (prefab != null) list.Add(m);
            }
            return list;
        }
    }
}
