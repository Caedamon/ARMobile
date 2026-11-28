using Fighters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The two graveyard pages of one fallen hero in the Book. Left page: the memorial (gravestone, name, title, how they fell).
    /// Right page: the legacy (victories, bosses, streak, mastery, last opponent). Built in code, so there is no prefab to set up;
    /// BookStage calls Create() and draws the result into a page picture like any other page.
    /// </summary>
    public static class GraveSpreadView
    {
        static readonly Color Parchment = new Color(0.93f, 0.87f, 0.72f, 1f);
        static readonly Color Frame = new Color(0.55f, 0.43f, 0.27f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.12f, 0.07f, 1f);
        static readonly Color InkSoft = new Color(0.32f, 0.24f, 0.15f, 1f);
        static readonly Color Stone = new Color(0.66f, 0.64f, 0.60f, 1f);
        static readonly Color StoneEdge = new Color(0.30f, 0.28f, 0.25f, 1f);
        static readonly Color Earth = new Color(0.36f, 0.27f, 0.17f, 1f);

        public static GameObject Create(Transform parent, bool rightPage, FallenFighter f)
        {
            var root = new GameObject(rightPage ? "GraveRight" : "GraveLeft", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            Frac(rt, 0f, 0f, 1f, 1f);

            Img(rt, "Background", Parchment, 0f, 0f, 1f, 1f);
            Img(rt, "FrameLine", Frame, 0.045f, 0.035f, 0.955f, 0.965f);
            Img(rt, "FrameInner", Parchment, 0.051f, 0.041f, 0.949f, 0.959f);

            if (rightPage) BuildRight(rt, f); else BuildLeft(rt, f);
            return root;
        }

        // ------------------------------------------------------------------ left: the memorial
        static void BuildLeft(RectTransform rt, FallenFighter f)
        {
            Txt(rt, "Title", "HALL OF THE FALLEN", 64f, Ink, TextAlignmentOptions.Center, FontStyles.Bold, 0.10f, 0.855f, 0.90f, 0.93f);
            Txt(rt, "InMemory", "In memory of", 38f, InkSoft, TextAlignmentOptions.Center, FontStyles.Normal, 0.10f, 0.805f, 0.90f, 0.85f);

            // the gravestone: an arch (circle) on a block, standing on a mound, with a darker rim made by drawing a slightly bigger one first
            Color stone = Color.Lerp(Stone, Color.Lerp(FighterTiers.Tint(f.tier), Color.black, 0.35f), 0.22f);
            const float x0 = 0.35f, x1 = 0.65f, rim = 0.008f;
            Img(rt, "Mound", Earth, 0.27f, 0.450f, 0.73f, 0.484f);
            Img(rt, "StoneRimBody", StoneEdge, x0 - rim, 0.465f, x1 + rim, 0.670f);
            Circle(rt, "StoneRimArch", StoneEdge, x0 - rim, 0.552f - rim * 0.8f, x1 + rim, 0.792f + rim * 0.8f);
            Img(rt, "StoneBody", stone, x0, 0.470f, x1, 0.670f);
            Circle(rt, "StoneArch", stone, x0, 0.552f, x1, 0.792f);
            Txt(rt, "RIP", "R.I.P.", 56f, StoneEdge, TextAlignmentOptions.Center, FontStyles.Bold, x0 + 0.02f, 0.575f, x1 - 0.02f, 0.700f);
            Txt(rt, "StoneLevel", "Lv " + f.level, 36f, StoneEdge, TextAlignmentOptions.Center, FontStyles.Normal, x0 + 0.03f, 0.500f, x1 - 0.03f, 0.570f);

            Txt(rt, "Name", f.displayName, 62f, Ink, TextAlignmentOptions.Center, FontStyles.Bold, 0.08f, 0.372f, 0.92f, 0.440f);
            Txt(rt, "Epithet", GraveText.Epithet(f), 42f, InkSoft, TextAlignmentOptions.Center, FontStyles.Italic, 0.08f, 0.325f, 0.92f, 0.372f);

            var tierText = Txt(rt, "TierLevel", FighterTiers.Label(f.tier) + "  -  Level " + f.level, 38f,
                Color.Lerp(FighterTiers.Tint(f.tier), Color.black, 0.4f), TextAlignmentOptions.Center, FontStyles.Bold, 0.08f, 0.277f, 0.92f, 0.325f);

            Img(rt, "Divider", Frame, 0.18f, 0.262f, 0.82f, 0.266f);

            var epitaph = Txt(rt, "Epitaph", GraveText.Epitaph(f), 46f, Ink, TextAlignmentOptions.Center, FontStyles.Italic, 0.12f, 0.085f, 0.88f, 0.250f);
            epitaph.textWrappingMode = TextWrappingModes.Normal;
        }

        // ------------------------------------------------------------------ right: the legacy
        static void BuildRight(RectTransform rt, FallenFighter f)
        {
            Txt(rt, "Title", "Legacy", 70f, Ink, TextAlignmentOptions.Center, FontStyles.Normal, 0.10f, 0.855f, 0.90f, 0.93f);
            Img(rt, "DividerTop", Frame, 0.14f, 0.835f, 0.86f, 0.839f);

            string style = string.IsNullOrEmpty(f.bestStyle) || f.bestStyleLevel <= 0 ? "Mastery" : f.bestStyle + " mastery";
            string[] labels = { "Victories", "Defeated bosses", "Longest streak", style };
            string[] values = { f.wins.ToString(), f.detailed ? f.bossesDefeated.ToString() : "-", f.detailed ? f.bestStreak.ToString() : "-",
                                f.detailed && f.bestStyleLevel > 0 ? f.bestStyleLevel.ToString() : "-" };
            for (int i = 0; i < labels.Length; i++)
            {
                float top = 0.805f - i * 0.082f, bottom = top - 0.07f;
                Txt(rt, "Label" + i, labels[i], 44f, Ink, TextAlignmentOptions.Left, FontStyles.Normal, 0.12f, bottom, 0.74f, top);
                Txt(rt, "Value" + i, values[i], 46f, Ink, TextAlignmentOptions.Right, FontStyles.Bold, 0.70f, bottom, 0.88f, top);
            }

            Img(rt, "DividerMid", Frame, 0.14f, 0.455f, 0.86f, 0.459f);

            Txt(rt, "EncounterLabel", "Final encounter", 38f, InkSoft, TextAlignmentOptions.Center, FontStyles.Normal, 0.10f, 0.395f, 0.90f, 0.445f);
            Txt(rt, "Opponent", GraveText.Opponent(f), 56f, Ink, TextAlignmentOptions.Center, FontStyles.Bold, 0.08f, 0.315f, 0.92f, 0.395f);
            Txt(rt, "Occasion", GraveText.Occasion(f), 34f, InkSoft, TextAlignmentOptions.Center, FontStyles.Normal, 0.08f, 0.262f, 0.92f, 0.315f);

            var legend = Txt(rt, "Legend", GraveText.Legend(f), 46f, Ink, TextAlignmentOptions.Center, FontStyles.Italic, 0.12f, 0.085f, 0.88f, 0.215f);
            legend.textWrappingMode = TextWrappingModes.Normal;
        }

        // ------------------------------------------------------------------ small UI helpers (same look as the other pages)
        static Sprite _circle;

        static Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
            tex.Apply(false, true);
            _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _circle;
        }

        static void Circle(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var img = Img(parent, name, color, x0, y0, x1, y1);
            img.sprite = CircleSprite();
        }

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
            t.enableAutoSizing = true;          // long names and sentences shrink to fit instead of spilling off the page
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

    /// <summary>The words on a hero's graves: how they fell, against whom, and what they leave behind. Same hero = same words every time.</summary>
    public static class GraveText
    {
        // ---- small helpers
        static int Seed(FallenFighter f)
        {
            unchecked
            {
                uint h = 2166136261;
                string s = (f.displayName ?? "") + "|" + f.fights + "|" + f.wins;
                for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619; }
                return (int)(h & 0x7fffffff);
            }
        }

        static string Pick(int seed, int salt, System.Collections.Generic.List<string> list)
        {
            return list[((seed >> (salt & 7)) + salt * 7) % list.Count];
        }

        static string Subj(Pronoun p) { return p == Pronoun.She ? "She" : (p == Pronoun.It ? "It" : "He"); }
        static string Poss(Pronoun p) { return p == Pronoun.She ? "her" : (p == Pronoun.It ? "its" : "his"); }
        static string PossCap(Pronoun p) { return p == Pronoun.She ? "Her" : (p == Pronoun.It ? "Its" : "His"); }

        static string Ordinal(int n)
        {
            int m100 = n % 100;
            if (m100 >= 11 && m100 <= 13) return n + "th";
            switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
        }

        static string KillerClean(FallenFighter f)
        {
            string n = f.killerName ?? "";
            if (n.StartsWith("Boss ")) n = n.Substring(5);
            return n.Trim();
        }

        // "the boss Cryptkeeper", "the undead Soul-Eater", "the robot TKR-8 The Obsolete", "Astrid the Unlucky"
        static string Who(FallenFighter f)
        {
            string n = KillerClean(f);
            if (n.Length == 0) return "an unknown foe";
            if (f.killerIsBoss) return "the boss " + n;
            if (f.killerKind == FallenKind.Undead) return "the undead " + n;
            if (f.killerKind == FallenKind.Robot) return "the robot " + n;
            return n;
        }

        static string ModeLabel(FallenFighter f)
        {
            if (!f.detailed) return "";
            switch (f.mode)
            {
                case FightMode.Boss: return "Boss Fight";
                case FightMode.Group: return "Team Battle";
                case FightMode.GroupDeathMatch: return "Group Death Match";
                case FightMode.LastHero: return "Last Hero Standing";
                default: return "Arena Fight to the Death";
            }
        }

        // ---- the pieces shown on the pages
        public static string Epithet(FallenFighter f)
        {
            if (!f.detailed) return f.wins == 0 ? "The Unproven" : "The Fallen";
            if (f.bossesDefeated >= 3) return "The Boss Breaker";
            if (f.bestStreak >= 8) return "The Unbroken";
            if (f.wins >= 50) return "The Legendary";
            if (f.wins >= 20) return "The Undying";
            if (f.wins >= 10) return "The Veteran";
            if (f.bossesDefeated >= 1) return "The Boss Slayer";
            if (f.wins >= 3) return "The Rising";
            if (f.wins >= 1) return "The Hopeful";
            return "The Unproven";
        }

        public static string Opponent(FallenFighter f)
        {
            string n = KillerClean(f);
            if (n.Length == 0) return "Unknown";
            return f.killerIsBoss ? "Boss " + n : n;
        }

        public static string Occasion(FallenFighter f)
        {
            string mode = ModeLabel(f);
            string battle = "Battle " + Mathf.Max(1, f.fights);
            return mode.Length == 0 ? battle : battle + "  -  " + mode;
        }

        public static string Epitaph(FallenFighter f)
        {
            var p = f.Pron();
            string who = Who(f);
            string poss = Poss(p);
            string nth = f.fights > 1 ? Ordinal(f.fights) + " battle" : "very first battle";
            int seed = Seed(f);
            var o = new System.Collections.Generic.List<string>();

            if (!f.detailed)
            {
                o.Add("Fell against " + who + ".");
                o.Add("Lost " + poss + " life against " + who + ".");
                return Pick(seed, 1, o);
            }

            switch (f.mode)
            {
                case FightMode.Boss:
                    o.Add("Challenged " + who + " and paid for it with " + poss + " life.");
                    o.Add("Fell in a Boss Fight against " + who + ", in " + poss + " " + nth + ".");
                    o.Add("Slain by " + who + " in " + poss + " " + nth + ".");
                    break;
                case FightMode.Group:
                    o.Add("Fell in a Team Battle against " + who + ", in " + poss + " " + nth + ".");
                    o.Add("Lost " + poss + " life in a Team Battle, struck down by " + who + ".");
                    break;
                case FightMode.GroupDeathMatch:
                    o.Add("Fell in a Group Death Match, struck down by " + who + ".");
                    o.Add("Met " + poss + " end in a Group Death Match, in " + poss + " " + nth + ".");
                    break;
                case FightMode.LastHero:
                    o.Add("Fell in Last Hero Standing, beaten by " + who + " in " + poss + " " + nth + ".");
                    o.Add("Lost " + poss + " life in Last Hero Standing against " + who + ".");
                    break;
                default:
                    o.Add("Fell against " + who + " in an Arena Fight to the Death!");
                    o.Add("Lost " + poss + " life against " + who + ".");
                    o.Add("Tragic defeat in " + poss + " " + nth + " against " + who + ".");
                    o.Add("Met " + poss + " end at the hands of " + who + " in the arena.");
                    break;
            }

            // a line only a ghoul or a machine would get
            if (f.heroKind == FallenKind.Undead) o.Add("Put back in the grave by " + who + " in " + poss + " " + nth + ".");
            if (f.heroKind == FallenKind.Robot) o.Add("Powered down for good by " + who + " in " + poss + " " + nth + ".");

            return Pick(seed, 2, o);
        }

        // ------------------------------------------------------------------ the closing line (right page): the tone follows the career
        // {killer} = who killed them.  Order: special cases of quality and record > boss fights > undead / robot jokes > how many victories.
        static readonly string[] Unproven =
        {
            "A promising career, tragically short on promises.", "Entered the arena. Never left.", "Glory awaited. Unfortunately, so did {killer}.",
            "History will remember... probably someone else.", "Came. Saw. Died.", "The shortest chapter in the book of legends.",
            "Great ambitions. Questionable execution."
        };
        static readonly string[] Rising =
        {
            "A spark of greatness, extinguished too soon.", "Not every legend is given time to grow.", "The arena demanded more than they could give.",
            "A warrior's journey, unfinished.", "Perhaps greatness awaited in another life."
        };
        static readonly string[] Veteran =
        {
            "Many came before. Few stood as long.", "Death was patient, but never forgotten.", "The arena claimed another of its finest.",
            "Victory became a habit. Defeat became eternal.", "Even the finest blade eventually breaks."
        };
        static readonly string[] Legendary =
        {
            "Death claimed the warrior, but not the legend.", "Generations shall speak of the battles fought here.",
            "The arena has lost a champion. History has gained a legend.", "Let those who follow strive to equal this legacy.",
            "Even Death stood in silent admiration."
        };
        static readonly string[] Undead =
        {
            "Returned to a more traditional state of death.", "Dead. Again.", "Apparently, immortality has its limitations.",
            "Death finally corrected its administrative error.", "The grave welcomes back an old acquaintance.",
            "The second funeral was considerably less expensive.", "Once risen, twice fallen.", "Rest in peace. This time, please."
        };
        static readonly string[] Robot =
        {
            "Fatal error: Fighter.exe has stopped responding.", "Warranty expired during combat.", "Critical system failure. No backup found.",
            "Permanently disconnected from the arena.", "Combat simulation terminated unexpectedly.", "Reboot unsuccessful.",
            "Final diagnostic: excessive structural damage.", "Spare parts may be available in the arena.",
            "No further software updates are planned.", "Have you tried turning it off and on again?"
        };
        static readonly string[] BossFirst =
        {
            "Ambition exceeded ability.", "A courageous decision. A terrible outcome.", "Perhaps a smaller opponent would have been advisable.",
            "Some challenges are best left unchallenged."
        };
        static readonly string[] BossSlayer =
        {
            "Even the hunter eventually becomes the hunted.", "Another giant stood between the warrior and eternity.",
            "The slayer has fallen, but the slain are not forgotten.", "Death finally found a worthy opponent."
        };

        public static string Legend(FallenFighter f)
        {
            int seed = Seed(f);
            string line = null;

            // 1. Quality against destiny (the game's tiers: Elite = "legendary", High quality = "epic")
            var special = new System.Collections.Generic.List<string>();
            if (f.tier == FighterTier.Elite && f.wins == 0) special.Add("Legendary potential. Pathetic results.");
            if (f.tier == FighterTier.Elite && f.fights <= 1) special.Add("A legend in name alone.");
            if (f.tier == FighterTier.HighQuality && f.wins == 0) special.Add("An epic disappointment.");
            if (f.tier == FighterTier.Pathetic && f.wins >= 20) special.Add("Greatness was earned, not granted.");
            if (f.tier == FighterTier.Pathetic && f.detailed && f.bossesDefeated >= 1) special.Add("Against all expectations, a giant fell.");
            if (f.tier == FighterTier.Normal && f.wins >= 50) special.Add("There was nothing ordinary about this warrior.");
            if (special.Count > 0) line = Pick(seed, 3, special);

            // 2. Boss fights: a first challenge or a slayer meeting the end
            if (line == null && f.killerIsBoss)
                line = Pick(seed, 4, new System.Collections.Generic.List<string>(f.bossesDefeated >= 1 ? BossSlayer : BossFirst));

            // 3. Undead and robots get their own jokes most of the time
            if (line == null && (f.heroKind == FallenKind.Undead || f.heroKind == FallenKind.Robot) && (seed % 100) < 70)
                line = Pick(seed, 5, new System.Collections.Generic.List<string>(f.heroKind == FallenKind.Undead ? Undead : Robot));

            // 4. Otherwise by victories
            if (line == null)
            {
                string[] pool = f.wins <= 0 ? Unproven : (f.wins < 10 ? Rising : (f.wins < 50 ? Veteran : Legendary));
                line = Pick(seed, 6, new System.Collections.Generic.List<string>(pool));
            }

            string killer = KillerClean(f).Length > 0 ? Who(f) : "an unknown foe";
            return line.Replace("{killer}", killer);
        }
    }
}
