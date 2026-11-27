using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Weaponary;

namespace Game
{
    /// <summary>
    /// Left page of an Armoury spread: the weapon seen from the side, flat on the page, with its name.
    /// All layout lives in the WeaponPicturePage prefab. The picture is drawn by WeaponSnapshot (a side view of the model).
    /// </summary>
    public sealed class WeaponPicturePageView : MonoBehaviour
    {
        [Header("Parts (edit them in the prefab)")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] RawImage picture;

        public void Fill(WeaponDefinition def, Texture picturetexture)
        {
            if (titleText != null) titleText.text = def != null ? def.displayName : "";
            if (picture != null)
            {
                picture.texture = picturetexture;
                picture.enabled = picturetexture != null;
            }
        }
    }
}
