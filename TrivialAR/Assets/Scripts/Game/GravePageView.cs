using Fighters;
using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A graveyard page of the Book: one fallen fighter (or the "nobody has fallen yet" message).
    /// All layout lives in the GravePage prefab; this script only fills in the words.
    /// </summary>
    public sealed class GravePageView : MonoBehaviour
    {
        [Header("Parts (edit them in the prefab)")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text detailsText;
        [SerializeField] TMP_Text deathText;

        [Header("Wording. Details: {0} tier, {1} level, {2} wins.  Death: {0} killer, {1} killer level, {2} match number")]
        [SerializeField] string titleFormat = "R.I.P.";
        [SerializeField] string detailsFormat = "{0}   Level {1}\n{2}";
        [SerializeField] string deathFormat = "Died against {0}\nin match {2}";
        [SerializeField] string deathUnknownFormat = "Died in match {2}";
        [SerializeField] string emptyTitle = "Graveyard";
        [SerializeField] string emptyText = "Nobody has fallen yet.";

        public void Fill(FallenFighter f)
        {
            Set(titleText, titleFormat);
            Set(nameText, f.displayName);
            Set(detailsText, Fmt(detailsFormat, FighterTiers.Label(f.tier), f.level, f.wins + (f.wins == 1 ? " win" : " wins")));
            Set(deathText, string.IsNullOrEmpty(f.killerName)
                ? Fmt(deathUnknownFormat, "", 0, f.fights)
                : Fmt(deathFormat, f.killerName, f.killerLevel, f.fights));
        }

        public void FillEmpty()
        {
            Set(titleText, emptyTitle);
            Set(nameText, emptyText);
            Set(detailsText, "");
            Set(deathText, "");
        }

        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }

        static string Fmt(string format, object a, object b, object c)
        {
            try { return string.Format(format, a, b, c); }
            catch (System.FormatException) { return format; }
        }
    }
}
