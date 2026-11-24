using Fighters;
using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The "current fighter" page of the Book. All layout lives in the FighterPage prefab (open it and edit);
    /// this script only fills in the words and bars. Drawn to a texture by BookStage.
    /// </summary>
    public sealed class FighterPageView : MonoBehaviour
    {
        [Header("Parts (edit them in the prefab)")]
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text tierText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] RectTransform xpFill;            // stretches 0..1 inside its bar
        [SerializeField] TMP_Text[] statValues = new TMP_Text[4];     // MIGHT, EGO, EDUCATION, INSIGHT
        [SerializeField] RectTransform[] statFills = new RectTransform[4];
        [SerializeField] TMP_Text styleText;
        [SerializeField] TMP_Text proficiencyText;
        [SerializeField] TMP_Text recordText;

        [Header("Wording. Level: {0} level, {1} xp, {2} xp needed.  Record: {0} wins, {1} fights")]
        [SerializeField] string levelFormat = "Level {0}    XP {1} / {2}";
        [SerializeField] string styleFormat = "Prefers: {0}";
        [SerializeField] string recordFormat = "Record:  {0} win{2}  in  {1} fight{3}";

        [Tooltip("Tier colours are made for dark menus; this darkens them so they read on parchment (0 = unchanged, 1 = black).")]
        [SerializeField, Range(0f, 1f)] float tierDarken = 0.4f;

        public void Fill(FighterData d)
        {
            if (d == null) return;
            Set(nameText, d.displayName);

            if (tierText != null)
            {
                tierText.text = FighterTiers.Label(d.tier);
                tierText.color = Color.Lerp(FighterTiers.Tint(d.tier), Color.black, tierDarken);
            }

            int need = ProgressionRules.XpToNext(d.level);
            Set(levelText, Fmt(levelFormat, d.level, d.xp, need));
            SetFill(xpFill, need > 0 ? d.xp / (float)need : 0f);

            int[] stats = { d.might, d.ego, d.education, d.insight };
            for (int i = 0; i < 4; i++)
            {
                if (i < statValues.Length) Set(statValues[i], stats[i].ToString());
                if (i < statFills.Length) SetFill(statFills[i], stats[i] / (float)FighterGenerator.StatMax);
            }

            Set(styleText, Fmt(styleFormat, d.PreferredStyleLabel));
            Set(proficiencyText, ProgressionRules.ProfLine(d).Replace("   ", "\n").Replace("Proficiency:  ", "Proficiency\n"));
            Set(recordText, Fmt(recordFormat, d.wins, d.fights, d.wins == 1 ? "" : "s", d.fights == 1 ? "" : "s"));
        }

        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }

        static void SetFill(RectTransform r, float amount)
        {
            if (r == null) return;
            r.anchorMin = new Vector2(0f, r.anchorMin.y);
            r.anchorMax = new Vector2(Mathf.Clamp01(amount), r.anchorMax.y);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static string Fmt(string format, object a, object b = null, object c = null, object d = null)
        {
            try { return string.Format(format, a, b, c, d); }
            catch (System.FormatException) { return format; }   // a typo in the wording must never break the game
        }
    }
}
