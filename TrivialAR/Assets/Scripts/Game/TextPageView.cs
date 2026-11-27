using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A page of explanation text in the Book (the Stats pages): a title and up to three headings with a paragraph each.
    /// All layout lives in the TextPage prefab; this script only fills in the words. Drawn to a texture by BookStage.
    /// </summary>
    public sealed class TextPageView : MonoBehaviour
    {
        [Header("Parts (edit them in the prefab)")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text[] headings = new TMP_Text[3];
        [SerializeField] TMP_Text[] bodies = new TMP_Text[3];

        public void Fill(BookTextPage page)
        {
            if (page == null) return;
            if (titleText != null) titleText.text = page.title;
            for (int i = 0; i < headings.Length; i++)
            {
                bool has = page.entries != null && i < page.entries.Length && page.entries[i] != null;
                if (headings[i] != null) { headings[i].gameObject.SetActive(has); if (has) headings[i].text = page.entries[i].heading; }
                if (i < bodies.Length && bodies[i] != null) { bodies[i].gameObject.SetActive(has); if (has) bodies[i].text = page.entries[i].body; }
            }
        }
    }
}
