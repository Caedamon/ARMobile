using System.Collections.Generic;
using UnityEngine;
using Weaponary;

namespace Items
{
    /// <summary>A weapon lying in the arena. Fighters find it through WeaponItem.All.</summary>
    [DisallowMultipleComponent]
    public sealed class WeaponItem : MonoBehaviour
    {
        public static readonly List<WeaponItem> All = new List<WeaponItem>();

        public WeaponDefinition definition;

        [Header("Presentation (meters / seconds)")]
        public float bobHeight = 0.004f;
        public float bobSpeed = 2.0f;
        public float spinDegPerSecond = 60f;

        public Component Claimant { get; private set; }
        public bool IsAvailable => !_taken;

        Transform _visual;
        bool _taken;
        float _phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        void OnEnable()  { if (!All.Contains(this)) All.Add(this); _phase = Random.value * 6.28f; }
        void OnDisable() { All.Remove(this); }

        public void Init(WeaponDefinition def, float worldScale)
        {
            definition = def;
            if (_visual != null) Destroy(_visual.gameObject);
            if (def == null || def.visualPrefab == null) return;

            var go = Instantiate(def.visualPrefab, transform);
            go.name = "Visual";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = def.visualPrefab.transform.localScale * worldScale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            _visual = go.transform;
        }

        void Update()
        {
            if (_taken || _visual == null) return;
            _phase += Time.deltaTime * bobSpeed;
            var p = _visual.localPosition;
            p.y = Mathf.Sin(_phase) * bobHeight;
            _visual.localPosition = p;
            _visual.Rotate(0f, spinDegPerSecond * Time.deltaTime, 0f, Space.Self);
        }

        /// <summary>First come, first served. A destroyed claimant counts as free.</summary>
        public bool TryClaim(Component who)
        {
            if (_taken) return false;
            if (Claimant != null && Claimant != who) return false;
            Claimant = who;
            return true;
        }

        public void Release(Component who)
        {
            if (Claimant == who) Claimant = null;
        }

        /// <summary>Removes the item from the arena and returns its definition (null if already taken).</summary>
        public WeaponDefinition Take()
        {
            if (_taken) return null;
            _taken = true;
            All.Remove(this);
            var d = definition;
            Destroy(gameObject);
            return d;
        }
    }
}
