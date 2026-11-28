using Fighters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The two Codex pages of one archetype in the Book. Left page: the name, the class, the portrait and where you meet it.
    /// Right page: the lore text and the bonuses and penalties. Everything shown comes from the ArchetypeDefinition (data);
    /// nothing about the archetype is written in here. Built in code, like the grave pages, so there is no prefab to set up;
    /// BookStage calls Create() and draws the result into a page picture like any other page.
    /// </summary>
    public static class CodexSpreadView
    {
        static readonly Color Parchment = new Color(0.93f, 0.87f, 0.72f, 1f);
        static readonly Color Frame = new Color(0.55f, 0.43f, 0.27f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.12f, 0.07f, 1f);
        static readonly Color InkSoft = new Color(0.32f, 0.24f, 0.15f, 1f);
        static readonly Color PictureBack = new Color(0.86f, 0.79f, 0.62f, 1f);
        static readonly Color Good = new Color(0.14f, 0.42f, 0.16f, 1f);
        static readonly Color Bad = new Color(0.62f, 0.13f, 0.10f, 1f);

        /// <summary>Width of the portrait frame divided by its height. BookStage makes the portrait picture this shape.</summary>
        public const float PortraitAspect = 0.75f;

        /// <param name="portrait">The picture for the left page (may be null: the frame stays empty).</param>
        /// <param name="seenAs">"Hero", "Opponent" or "Hero  ·  Opponent": taken from the catalog by the caller.</param>
        /// <param name="pageAspect">Width of a page divided by its height, so the picture frame can be made square.</param>
        public static GameObject Create(Transform parent, bool rightPage, ArchetypeDefinition a, Texture2D portrait, string seenAs, float pageAspect)
        {
            var root = new GameObject(rightPage ? "CodexRight" : "CodexLeft", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            Frac(rt, 0f, 0f, 1f, 1f);

            Img(rt, "Background", Parchment, 0f, 0f, 1f, 1f);
            Img(rt, "FrameLine", Frame, 0.045f, 0.035f, 0.955f, 0.965f);
            Img(rt, "FrameInner", Parchment, 0.051f, 0.041f, 0.949f, 0.959f);

            if (rightPage) BuildRight(rt, a); else BuildLeft(rt, a, portrait, seenAs, pageAspect);
            return root;
        }

        // ------------------------------------------------------------------ left: who they are
        static void BuildLeft(RectTransform rt, ArchetypeDefinition a, Texture2D portrait, string seenAs, float pageAspect)
        {
            Txt(rt, "Name", a.displayName, 70f, Ink, TextAlignmentOptions.Center, FontStyles.Bold, 0.08f, 0.870f, 0.92f, 0.945f);
            Txt(rt, "Class", ArchetypeRules.ClassLabel(a.classification), 40f, InkSoft, TextAlignmentOptions.Center, FontStyles.Italic, 0.10f, 0.822f, 0.90f, 0.870f);

            // a tall picture frame for the whole body (the page is taller than wide, so the width in page fractions is
            // the height times the frame's shape, divided by the page's shape)
            float h = 0.545f, w = Mathf.Min(0.80f, h * PortraitAspect / Mathf.Max(0.3f, pageAspect));
            float x0 = 0.5f - w * 0.5f, x1 = 0.5f + w * 0.5f, y0 = 0.262f, y1 = y0 + h, rim = 0.012f;
            Img(rt, "PictureRim", Frame, x0 - rim, y0 - rim * 0.8f, x1 + rim, y1 + rim * 0.8f);
            Img(rt, "PictureBack", PictureBack, x0, y0, x1, y1);
            if (portrait != null)
            {
                var go = new GameObject("Portrait", typeof(RectTransform));
                go.transform.SetParent(rt, false);
                var raw = go.AddComponent<RawImage>();
                raw.texture = portrait;
                raw.raycastTarget = false;
                Frac(raw.rectTransform, x0, y0, x1, y1);
            }

            Img(rt, "Divider", Frame, 0.18f, 0.235f, 0.82f, 0.239f);
            if (!string.IsNullOrEmpty(seenAs))
            {
                Txt(rt, "SeenLabel", "Seen in the arena as", 34f, InkSoft, TextAlignmentOptions.Center, FontStyles.Normal, 0.10f, 0.175f, 0.90f, 0.225f);
                Txt(rt, "Seen", seenAs, 50f, Ink, TextAlignmentOptions.Center, FontStyles.Bold, 0.08f, 0.095f, 0.92f, 0.175f);
            }
        }

        // ------------------------------------------------------------------ right: what they are
        static void BuildRight(RectTransform rt, ArchetypeDefinition a)
        {
            Txt(rt, "Title", "Lore", 66f, Ink, TextAlignmentOptions.Center, FontStyles.Normal, 0.10f, 0.855f, 0.90f, 0.935f);
            Img(rt, "DividerTop", Frame, 0.14f, 0.838f, 0.86f, 0.842f);

            var lore = Txt(rt, "Lore", a.description, 40f, Ink, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0.12f, 0.405f, 0.88f, 0.825f);
            lore.textWrappingMode = TextWrappingModes.Normal;
            lore.fontSizeMin = 20f;

            Img(rt, "DividerMid", Frame, 0.14f, 0.385f, 0.86f, 0.389f);
            Txt(rt, "ModifiersLabel", "Modifiers", 44f, InkSoft, TextAlignmentOptions.Center, FontStyles.Normal, 0.10f, 0.330f, 0.90f, 0.382f);

            int rows = 0;
            if (a.modifiers != null)
            {
                foreach (var m in a.modifiers)
                {
                    if (m.points == 0 || rows >= 4) continue;
                    float top = 0.322f - rows * 0.062f, bottom = top - 0.056f;
                    Color c = m.points > 0 ? Good : Bad;
                    string sign = m.points > 0 ? "+" + m.points : m.points.ToString();   // a negative number prints its own minus
                    Txt(rt, "ModLabel" + rows, ArchetypeRules.Label(m.kind), 44f, Ink, TextAlignmentOptions.Left, FontStyles.Normal, 0.14f, bottom, 0.70f, top);
                    Txt(rt, "ModValue" + rows, sign, 48f, c, TextAlignmentOptions.Right, FontStyles.Bold, 0.66f, bottom, 0.86f, top);
                    rows++;
                }
            }
            if (rows == 0)
                Txt(rt, "NoMods", "No special strengths or weaknesses", 38f, InkSoft, TextAlignmentOptions.Center, FontStyles.Italic, 0.12f, 0.20f, 0.88f, 0.322f);
        }

        // ------------------------------------------------------------------ small UI helpers (same look as the other pages)
        static Image Img(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            Frac(img.rectTransform, x0, y0, x1, y1);
            return img;
        }

        static TMP_Text Txt(Transform parent, string name, string text, float size, Color color,
                            TextAlignmentOptions align, FontStyles style, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.enableAutoSizing = true;          // long names and texts shrink to fit instead of spilling off the page
            t.fontSizeMin = 14f;
            t.fontSizeMax = size;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Frac(t.rectTransform, x0, y0, x1, y1);
            return t;
        }

        static void Frac(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
