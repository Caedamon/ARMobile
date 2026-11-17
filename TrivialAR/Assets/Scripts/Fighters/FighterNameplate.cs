using TMPro;
using UnityEngine;

namespace Fighters
{
    /// <summary>Small billboarded label above a fighter: name, tier and level. Handy for AR testing.</summary>
    public sealed class FighterNameplate : MonoBehaviour
    {
        [Tooltip("Height above the fighter's origin, in the fighter's local units (fighters are scaled 0.05).")]
        public float height = 2.3f;
        [Tooltip("TextMeshPro font size. About 3 is readable at table scale; raise/lower to taste.")]
        public float fontSize = 3f;

        [Tooltip("Distance (meters) at which the label has its normal size. Closer = smaller, farther = bigger, so it stays readable.")]
        public float refDistance = 0.6f;
        public float minScale = 0.25f;
        public float maxScale = 2f;

        TextMeshPro _text;
        Camera _cam;

        public void Build(FighterData d)
        {
            var go = new GameObject("Nameplate");
            go.layer = 0;   // Default layer, so the portrait cameras (which cull by team layer) don't draw it
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);

            _text = go.AddComponent<TextMeshPro>();
            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = fontSize;
            _text.color = FighterTiers.Tint(d.tier);
            _text.text = d.displayName + "\n<size=70%>" + FighterTiers.Label(d.tier) + " - Lv " + d.level + "</size>";
        }

        void LateUpdate()
        {
            if (_text == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            _text.transform.rotation = _cam.transform.rotation;

            // Keep the label roughly the same size on screen, wherever the camera is.
            float d = Vector3.Distance(_cam.transform.position, _text.transform.position);
            float s = Mathf.Clamp(d / Mathf.Max(0.05f, refDistance), minScale, maxScale);
            _text.transform.localScale = Vector3.one * s;
        }
    }
}
