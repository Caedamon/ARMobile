using Fighters;
using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>A small tombstone built from primitives (no art needed), rising out of the table.</summary>
    public sealed class TombstoneStage : MonoBehaviour
    {
        const float RiseSeconds = 1.2f;
        Transform _stone;
        float _t;

        public void Build(FighterData d)
        {
            var stoneColor = new Color(0.55f, 0.57f, 0.60f);
            var darkColor = new Color(0.30f, 0.31f, 0.34f);

            Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.003f, 0f), new Vector3(0.9f, 0.003f, 0.9f), new Color(0.10f, 0.11f, 0.15f));

            _stone = new GameObject("Stone").transform;
            _stone.SetParent(transform, false);

            Prim(PrimitiveType.Cube, _stone, new Vector3(0f, 0.012f, 0f), new Vector3(0.10f, 0.012f, 0.045f), darkColor);          // base slab
            var body = Prim(PrimitiveType.Cube, _stone, new Vector3(0f, 0.063f, 0f), new Vector3(0.07f, 0.09f, 0.016f), stoneColor);  // slab
            var cap = Prim(PrimitiveType.Cylinder, _stone, new Vector3(0f, 0.108f, 0f), new Vector3(0.07f, 0.008f, 0.07f), stoneColor); // rounded top
            cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var go = new GameObject("Epitaph");
            go.transform.SetParent(_stone, false);
            go.transform.localPosition = new Vector3(0f, 0.075f, -0.0092f);   // front face, toward the viewer (-Z)
            var text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 0.17f;
            text.color = new Color(0.12f, 0.12f, 0.14f);
            string wins = d.wins + " win" + (d.wins == 1 ? "" : "s");
            text.text = "R.I.P.\n<size=80%>" + d.displayName + "</size>\n<size=60%>" + FighterTiers.Label(d.tier) + "  Lv " + d.level + "  -  " + wins + "</size>";

            _stone.localPosition = new Vector3(0f, -0.16f, 0f);   // starts underground
        }

        void Update()
        {
            if (_stone == null || _t >= RiseSeconds) return;
            _t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / RiseSeconds));
            _stone.localPosition = new Vector3(0f, Mathf.Lerp(-0.16f, 0f, k), 0f);
        }

        static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = c;
            return go;
        }
    }
}
