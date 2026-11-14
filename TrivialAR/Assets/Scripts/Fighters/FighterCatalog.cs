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

        public Model Find(string id)
        {
            foreach (var m in models) if (m != null && m.id == id) return m;
            return null;
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
