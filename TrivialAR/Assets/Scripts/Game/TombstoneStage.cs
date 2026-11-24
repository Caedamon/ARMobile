using Fighters;
using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tombstone shown when your fighter dies. Everything visual lives in the TombstoneStage prefab
    /// (shapes, materials, text positions and sizes). This script only fills in the words and plays the rise.
    /// </summary>
    public sealed class TombstoneStage : MonoBehaviour
    {
        [Header("Parts (edit them in the prefab)")]
        [SerializeField] Transform stone;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text detailsText;
        [SerializeField] TMP_Text killedByText;

        [Header("Rise animation")]
        [SerializeField] float riseSeconds = 1.2f;
        [SerializeField] float riseDepth = 0.3f;

        [Header("Wording. Details: {0} tier, {1} level, {2} wins.  Killed by: {0} enemy name, {1} match number")]
        [SerializeField] string titleFormat = "R.I.P.";
        [SerializeField] string detailsFormat = "{0}  Lv {1}  -  {2}";
        [SerializeField] string killedByFormat = "Died against {0}\nin match {1}";
        [SerializeField] string killedByUnknownFormat = "Died in match {0}";

        Vector3 _restPos;
        float _t = 999f;

        public void Build(FighterData d, FighterData killer)
        {
            if (titleText != null) titleText.text = titleFormat;
            if (nameText != null) nameText.text = d.displayName;
            if (detailsText != null)
                detailsText.text = Fmt(detailsFormat, FighterTiers.Label(d.tier), d.level, d.wins + (d.wins == 1 ? " win" : " wins"));
            if (killedByText != null)
                killedByText.text = killer != null
                    ? Fmt(killedByFormat, killer.displayName, d.fights)
                    : Fmt(killedByUnknownFormat, d.fights);

            if (stone != null && riseSeconds > 0f)
            {
                _restPos = stone.localPosition;
                stone.localPosition = _restPos + Vector3.down * riseDepth;
                _t = 0f;
            }
        }

        void Update()
        {
            if (stone == null || _t >= riseSeconds) return;
            _t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / riseSeconds));
            stone.localPosition = _restPos + Vector3.down * (riseDepth * (1f - k));
        }

        static string Fmt(string format, object a, object b = null, object c = null)
        {
            try { return string.Format(format, a, b, c); }
            catch (System.FormatException) { return format; }   // a typo in the wording must never break the game
        }
    }
}
