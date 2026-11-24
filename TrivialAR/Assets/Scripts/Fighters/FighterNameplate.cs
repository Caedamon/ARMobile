using TMPro;
using UnityEngine;

namespace Fighters
{
    /// <summary>Small billboarded label above a fighter: name, tier and level. Handy for AR testing.</summary>
    public sealed class FighterNameplate : MonoBehaviour
    {
        [Tooltip("Place the label just above the top of the fighter's head (measured per model, so tall models get it higher). OFF = always use 'height'.")]
        public bool autoHeight = true;
        [Tooltip("With autoHeight: empty space between the top of the head and the bottom of the label, in the fighter's local units. Set on the ArenaFighterSpawner (fights) and the RosterStage (hub / roster).")]
        public float gapAboveHead = 0.15f;
        [Tooltip("Height above the fighter's origin, in the fighter's local units (fighters are scaled 0.05). Used when autoHeight is off or no head is found.")]
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
            go.transform.localPosition = new Vector3(0f, PlateHeight(), 0f);

            _text = go.AddComponent<TextMeshPro>();
            // With autoHeight the label is anchored by its BOTTOM edge, so a bigger label (or a camera further away,
            // which scales it up) grows upward instead of sinking into the head.
            _text.alignment = autoHeight ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center;
            if (autoHeight) _text.rectTransform.pivot = new Vector2(0.5f, 0f);
            _text.fontSize = fontSize;
            _text.color = FighterTiers.Tint(d.tier);
            _text.text = d.displayName + "\n<size=70%>" + FighterTiers.Label(d.tier) + " - Lv " + d.level + "</size>";
        }

        // Where the bottom of the label goes: just above the top of the body mesh (or the head bone, if the mesh can't be measured).
        float PlateHeight()
        {
            if (!autoHeight) return height;

            // Head bone (humanoid head, or the PortraitAnchor that FighterIdentity puts on the head).
            Transform head = null;
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>())
                    if (t.name == "PortraitAnchor") { head = t; break; }
            }
            bool haveHead = head != null;
            float headY = haveHead ? transform.InverseTransformPoint(head.position).y : 0f;

            // Top of the body (skinned meshes only, so a raised weapon doesn't count).
            float topY = float.NegativeInfinity;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var wb = r.bounds;
                float y = transform.InverseTransformPoint(new Vector3(wb.center.x, wb.max.y, wb.center.z)).y;
                if (y > topY) topY = y;
            }
            bool haveTop = !float.IsNegativeInfinity(topY);

            float result;
            if (haveTop && haveHead) result = Mathf.Max(Mathf.Min(topY, headY + 1.0f), headY + 0.25f);   // never inside the head, never absurdly high
            else if (haveTop) result = topY;
            else if (haveHead) result = headY + 0.45f;
            else return height;

            return result + gapAboveHead;
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
