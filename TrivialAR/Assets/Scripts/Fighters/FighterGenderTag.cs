using UnityEngine;

namespace Fighters
{
    /// <summary>
    /// Which kind of name the name generator gives this model. Male / Female = a matching human first name. Either = any human name.
    /// Undead = human names and also grim ones ("Soul-Eater", "the Devourer"). Robot = machine names ("Clank the Rusty").
    /// New values are added at the END so prefabs that were already set keep their choice.
    /// </summary>
    public enum FighterGender { NotSet = 0, Male = 1, Female = 2, Either = 3, Undead = 4, Robot = 5 }

    /// <summary>
    /// A normal component with a dropdown, on every fighter prefab (Tools > Kaiju > Fighters > Set Up Gender Tags adds it). It is NOT
    /// Unity's own "Tag" (that is the Untagged / Player / Enemy dropdown at the top of the Inspector, which this never touches).
    /// It does nothing in the game and is shown nowhere: the ONLY thing that uses it is the name generator, for the name on the nameplate.
    /// "Not Set" means someone forgot to choose (names then fall back to "either" and the Console warns).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaiju/Fighter Gender Tag")]
    public sealed class FighterGenderTag : MonoBehaviour
    {
        [Tooltip("Male / Female = matching first name. Either = any human name. Undead = human or grim names. Robot = machine names. Used ONLY by the name generator.")]
        [SerializeField] FighterGender gender = FighterGender.NotSet;

        public FighterGender Gender { get { return gender; } }
    }
}
