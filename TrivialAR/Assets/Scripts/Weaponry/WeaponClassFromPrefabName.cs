using System.Text.RegularExpressions;
using UnityEngine;

namespace Weaponary
{
    public sealed class WeaponClassFromPrefabName : MonoBehaviour, IWeaponClassProvider
    {
        public bool manualOverride = false;
        public WeaponClass manual = WeaponClass.OneHand;

        public WeaponClass GetWeaponClass()
        {
            if (manualOverride) return manual;

            var n = gameObject.name;
            var is1H = Regex.IsMatch(n, @"\b(1H|One[_\s-]?Hand(ed)?)\b", RegexOptions.IgnoreCase);
            var is2H = Regex.IsMatch(n, @"\b(2H|Two[_\s-]?Hand(ed)?)\b", RegexOptions.IgnoreCase);
            var isShield = Regex.IsMatch(n, @"\b(Shield|Buckler|Kite)\b", RegexOptions.IgnoreCase);

            if (isShield && !is1H && !is2H) return WeaponClass.ShieldOnly;
            if (is2H) return WeaponClass.TwoHand;
            if (is1H) return WeaponClass.OneHand;
            return WeaponClass.OneHand;
        }
    }
}