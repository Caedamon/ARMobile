using System;
using System.Collections.Generic;
using UnityEngine;
using Weaponary;

namespace Items
{
    /// <summary>
    /// Put on the arena root. Spawns a fully random set of weapons: random type (weighted by the table)
    /// at random positions. Positions are local to this object, in meters.
    /// Fighters start at z = +/-0.5 in KaijuArenaAR, so the default area keeps weapons between them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponSpawner : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public WeaponDefinition weapon;
            [Min(0f)] public float weight;
        }

        [Header("What can spawn")]
        public List<Entry> table = new List<Entry>();

        [Header("Layout (local meters)")]
        [Min(0)] public int weaponCount = 5;
        public Vector2 areaHalfExtents = new Vector2(0.35f, 0.30f);
        public float minSpacing = 0.10f;
        public float hoverHeight = 0.03f;
        [Tooltip("Characters are scaled 0.05 in your prefabs, so weapons use the same scale.")]
        public float weaponWorldScale = 0.05f;

        [Header("Behaviour")]
        public bool spawnOnStart = true;
        [Tooltip("0 = different layout every time. Any other number = repeatable layout (handy for testing).")]
        public int seed = 0;

        readonly List<GameObject> _spawned = new List<GameObject>();

        void Start() { if (spawnOnStart) SpawnAll(); }

        public void ClearAll()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
        }

        public void SpawnAll()
        {
            ClearAll();
            var rng = seed != 0 ? new System.Random(seed) : new System.Random();
            var used = new List<Vector2>();

            for (int i = 0; i < weaponCount; i++)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    var p = RandomPoint(rng);
                    if (!FarFromAll(p, used)) continue;

                    var def = Pick(rng);
                    if (def == null) return;       // empty table
                    Place(def, p);
                    used.Add(p);
                    break;
                }
            }
        }

        Vector2 RandomPoint(System.Random rng)
        {
            float x = ((float)rng.NextDouble() * 2f - 1f) * areaHalfExtents.x;
            float z = ((float)rng.NextDouble() * 2f - 1f) * areaHalfExtents.y;
            return new Vector2(x, z);
        }

        bool FarFromAll(Vector2 p, List<Vector2> others)
        {
            foreach (var o in others) if ((o - p).magnitude < minSpacing) return false;
            return true;
        }

        WeaponDefinition Pick(System.Random rng)
        {
            float total = 0f;
            foreach (var e in table) if (e.weapon != null && e.weight > 0f) total += e.weight;
            if (total <= 0f) return null;

            double r = rng.NextDouble() * total;
            WeaponDefinition last = null;
            foreach (var e in table)
            {
                if (e.weapon == null || e.weight <= 0f) continue;
                last = e.weapon;
                r -= e.weight;
                if (r <= 0.0) return e.weapon;
            }
            return last;
        }

        void Place(WeaponDefinition def, Vector2 p)
        {
            var go = new GameObject("Pickup_" + def.displayName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(p.x, hoverHeight, p.y);
            var item = go.AddComponent<WeaponItem>();
            item.Init(def, weaponWorldScale);
            _spawned.Add(go);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, hoverHeight, 0f), new Vector3(areaHalfExtents.x * 2f, 0.01f, areaHalfExtents.y * 2f));
        }
#endif
    }
}