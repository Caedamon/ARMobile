using System.Collections.Generic;
using Combat;
using Fighters;
using UnityEngine;
using Weaponary;

namespace Game
{
    /// <summary>
    /// The table-top display: a floor disc plus "puppet" fighters that only stand and idle.
    /// Used for the 3-fighter selection (ShowCandidates) and for the hub (ShowSingle).
    /// Stage space: +Z points away from the viewer, so puppets are rotated 180 degrees to face the camera.
    /// </summary>
    public sealed class RosterStage : MonoBehaviour
    {
        sealed class Slot
        {
            public FighterData data;
            public GameObject puppet;
            public Vector3 homePos;
            public float baseScale;     // the prefab's own scale (0.05)
            public float homeScaleMul;  // 1 = normal
        }

        const float SlotSpacing = 0.22f;
        const float PuppetY = 0.01f;
        const float SelectedScaleMul = 1.35f;
        static readonly Vector3 DefaultFront = new Vector3(0f, PuppetY, -0.14f);

        [Header("Layout (move these child objects in the prefab)")]
        [SerializeField] Transform[] candidateSlots;   // where the candidates stand
        [SerializeField] Transform frontSlot;          // where the inspected candidate steps to
        [SerializeField] Transform hubSlot;            // where your fighter stands in the hub
        [Tooltip("Leave empty to build a plain floor disc in code.")]
        [SerializeField] GameObject floorObject;

        [Header("Name labels")]
        [Tooltip("Empty space between the top of the head and the bottom of the name label (fighter-local units). Bigger = higher.")]
        [SerializeField] float nameplateGap = 0.15f;

        Vector3 FrontPos { get { return frontSlot != null ? frontSlot.localPosition : DefaultFront; } }

        FighterCatalog _catalog;
        List<WeaponDefinition> _showcase;
        readonly List<Slot> _slots = new List<Slot>();
        int _selected = -1;

        public int SelectedIndex { get { return _selected; } }

        public void Init(FighterCatalog catalog, List<WeaponDefinition> showcaseWeapons)
        {
            _catalog = catalog;
            _showcase = showcaseWeapons;
            if (floorObject == null) BuildFloor();
        }

        void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            var col = floor.GetComponent<Collider>();
            if (col != null) Destroy(col);
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            floor.transform.localScale = new Vector3(0.9f, 0.003f, 0.9f);
            var rend = floor.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.10f, 0.11f, 0.15f);
        }

        // ------------------------------------------------------------------ content
        public void ShowCandidates(List<FighterData> list)
        {
            ClearPuppets();
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) * 0.5f) * SlotSpacing;
                float z = 0.12f - 0.06f * Mathf.Abs(i - (n - 1) * 0.5f);   // gentle arc, outer fighters closer to the viewer
                Vector3 pos = new Vector3(x, PuppetY, z);
                if (candidateSlots != null && i < candidateSlots.Length && candidateSlots[i] != null) pos = candidateSlots[i].localPosition;
                AddSlot(list[i], pos, 1f);
            }
            _selected = -1;
        }

        public void ShowSingle(FighterData d)
        {
            ClearPuppets();
            AddSlot(d, hubSlot != null ? hubSlot.localPosition : new Vector3(0f, PuppetY, -0.04f), SelectedScaleMul);
            _selected = -1;
        }

        void AddSlot(FighterData d, Vector3 pos, float scaleMul)
        {
            var go = SpawnPuppet(d, pos);
            if (go == null) return;
            var s = new Slot
            {
                data = d,
                puppet = go,
                homePos = pos,
                baseScale = go.transform.localScale.x,
                homeScaleMul = scaleMul
            };
            go.transform.localScale = Vector3.one * (s.baseScale * scaleMul);
            _slots.Add(s);
        }

        void ClearPuppets()
        {
            foreach (var s in _slots) if (s.puppet != null) Destroy(s.puppet);
            _slots.Clear();
        }

        GameObject SpawnPuppet(FighterData d, Vector3 localPos)
        {
            var model = _catalog != null ? _catalog.Find(d.modelId) : null;
            var prefab = model != null ? model.playerPrefab : null;
            if (prefab == null)
            {
                Debug.LogError("[Roster] No player prefab for model '" + d.modelId + "'.");
                return null;
            }

            var go = Instantiate(prefab, transform);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var identity = go.GetComponent<FighterIdentity>();
            if (identity == null) identity = go.AddComponent<FighterIdentity>();
            identity.Apply(d);

            Puppetize(go);

            var def = PickShowcaseWeapon(d);
            var holder = go.GetComponent<WeaponHolder>();
            if (def != null && holder != null) holder.Equip(def);

            var plate = go.AddComponent<FighterNameplate>();
            plate.gapAboveHead = nameplateGap;
            plate.Build(d);
            return go;
        }

        // Display only: no AI, no movement, no physics. (BattleManager ignores disabled units.)
        static void Puppetize(GameObject go)
        {
            foreach (var seeker in go.GetComponents<WeaponSeeker>()) seeker.enabled = false;

            var ai = go.GetComponent<MeleeAI>();         if (ai != null) ai.enabled = false;
            var combat = go.GetComponent<MeleeCombat>(); if (combat != null) combat.enabled = false;
            var motor = go.GetComponent<MeleeMotor>();   if (motor != null) motor.enabled = false;

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // A generous tap target (the fighters are only ~9 cm tall).
            var cap = go.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.center = new Vector3(0f, 1f, 0f);
            cap.height = 2f;
            cap.radius = 0.6f;
        }

        WeaponDefinition PickShowcaseWeapon(FighterData d)
        {
            if (_showcase == null || _showcase.Count == 0) return null;
            var matches = new List<WeaponDefinition>();
            foreach (var w in _showcase)
                if (w != null && d.hasPreferredClass && w.weaponClass == d.preferredClass) matches.Add(w);
            if (matches.Count == 0) return null;
            return matches[Random.Range(0, matches.Count)];
        }

        // ------------------------------------------------------------------ selection
        public bool TryPick(Vector2 screenPos, Camera cam, out int index)
        {
            index = -1;
            if (cam == null) return false;

            var ray = cam.ScreenPointToRay(screenPos);
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(ray, 10f))
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    var p = _slots[i].puppet;
                    if (p == null) continue;
                    if (hit.collider.transform.IsChildOf(p.transform) && hit.distance < best)
                    {
                        best = hit.distance;
                        index = i;
                    }
                }
            }
            return index >= 0;
        }

        public void Select(int index) { _selected = index; }
        public void ClearSelection() { _selected = -1; }

        public FighterData DataAt(int index)
        {
            return (index >= 0 && index < _slots.Count) ? _slots[index].data : null;
        }

        void Update()
        {
            float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.puppet == null) continue;

                bool sel = i == _selected;
                Vector3 targetPos = sel ? FrontPos : s.homePos;
                float targetMul = sel ? SelectedScaleMul : s.homeScaleMul;

                var t = s.puppet.transform;
                t.localPosition = Vector3.Lerp(t.localPosition, targetPos, k);
                float curMul = t.localScale.x / Mathf.Max(0.0001f, s.baseScale);
                t.localScale = Vector3.one * (s.baseScale * Mathf.Lerp(curMul, targetMul, k));
            }
        }
    }
}
