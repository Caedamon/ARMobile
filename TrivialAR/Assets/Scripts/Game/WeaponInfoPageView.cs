using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using Weaponary;

namespace Game
{
    /// <summary>
    /// Right page of an Armoury spread: name, kind of weapon, Damage / Reach / Speed bars, the attacks it has, and the
    /// description. All layout lives in the WeaponInfoPage prefab. The description is a field on the WeaponDefinition asset.
    /// </summary>
    public sealed class WeaponInfoPageView : MonoBehaviour
    {
        [Header("Parts (edit them in the prefab)")]
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text typeText;
        [SerializeField] RectTransform[] statFills = new RectTransform[3];    // Damage, Reach, Speed
        [SerializeField] TMP_Text[] statValues = new TMP_Text[3];
        [SerializeField] TMP_Text attacksText;
        [SerializeField] TMP_Text descriptionText;

        [Header("Wording")]
        [SerializeField] string attacksFormat = "Moves:  {0}";
        [SerializeField] string noDescription = "Nobody has written about this weapon yet.";

        /// <summary>Damage and speed of a weapon, the way the Book shows them.</summary>
        public static void Measure(WeaponDefinition def, out float damage, out float reach, out float speedPercent)
        {
            damage = def != null ? def.baseDamage * def.damageScalar : 0f;
            reach = def != null ? def.reach * 100f : 0f;
            speedPercent = 100f;
            if (def == null || def.moveset == null || def.moveset.attacks == null || def.moveset.attacks.Count == 0) return;

            float sum = 0f, weights = 0f;
            foreach (var a in def.moveset.attacks)
            {
                if (a == null) continue;
                sum += a.speed * a.weight;
                weights += a.weight;
            }
            if (weights > 0f) speedPercent = sum / weights * 100f;
        }

        public void Fill(WeaponDefinition def, float maxDamage, float maxReach, float maxSpeed)
        {
            if (def == null) return;
            if (nameText != null) nameText.text = def.displayName;
            if (typeText != null) typeText.text = ClassLabel(def.weaponClass);

            float dmg, reach, speed;
            Measure(def, out dmg, out reach, out speed);
            SetStat(0, dmg, maxDamage, Mathf.RoundToInt(dmg).ToString());
            SetStat(1, reach, maxReach, Mathf.RoundToInt(reach).ToString());
            SetStat(2, speed, maxSpeed, Mathf.RoundToInt(speed) + "%");

            if (attacksText != null) attacksText.text = string.Format(attacksFormat, MoveNames(def));
            if (descriptionText != null) descriptionText.text = string.IsNullOrEmpty(def.description) ? noDescription : def.description;
        }

        void SetStat(int i, float value, float max, string label)
        {
            if (statValues != null && i < statValues.Length && statValues[i] != null) statValues[i].text = label;
            if (statFills != null && i < statFills.Length && statFills[i] != null)
            {
                var r = statFills[i];
                r.anchorMin = new Vector2(0f, r.anchorMin.y);
                r.anchorMax = new Vector2(Mathf.Clamp01(max > 0f ? value / max : 0f), r.anchorMax.y);
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;
            }
        }

        public static string ClassLabel(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.TwoHand:          return "Two-handed";
                case WeaponClass.DualOneHand:      return "Dual wield";
                case WeaponClass.OneHandAndShield: return "One-handed, with shield";
                case WeaponClass.ShieldOnly:       return "Shield";
                default:                           return "One-handed";
            }
        }

        // "1H Chop!" -> "Chop", "Dualwield Stab!" -> "Stab", "Shield bash!" -> "Shield bash"
        static string MoveNames(WeaponDefinition def)
        {
            if (def.moveset == null || def.moveset.attacks == null || def.moveset.attacks.Count == 0)
                return def.attackClips != null && def.attackClips.Count > 0 ? def.attackClips.Count + " attacks" : "-";

            var seen = new List<string>();
            foreach (var a in def.moveset.attacks)
            {
                if (a == null || string.IsNullOrEmpty(a.label)) continue;
                string n = a.label.Trim().TrimEnd('!').Trim();
                foreach (var p in new[] { "1H ", "2H ", "Dualwield ", "Dual ", "Unarmed " })
                    if (n.StartsWith(p)) { n = n.Substring(p.Length); break; }
                if (n.Length > 0 && !seen.Contains(n)) seen.Add(n);
            }
            if (seen.Count == 0) return "-";
            var sb = new StringBuilder();
            for (int i = 0; i < seen.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(seen[i]); }
            return sb.ToString();
        }
    }
}
