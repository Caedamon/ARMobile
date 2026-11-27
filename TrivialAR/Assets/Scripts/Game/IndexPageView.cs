using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The Index page of the Book (right page of the first spread): a title and a list of sections with their page numbers.
    /// All layout lives in the IndexPage prefab; this script only fills in the words. Drawn to a texture by BookStage.
    /// </summary>
    public sealed class IndexPageView : MonoBehaviour
    {
        [Header("Parts (edit them in the prefab)")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text[] entryNames = new TMP_Text[6];
        [SerializeField] TMP_Text[] entryPages = new TMP_Text[6];
        [SerializeField] TMP_Text hintText;

        [Header("Wording")]
        [SerializeField] string title = "Index";
        [SerializeField] string hint = "Tap a tab under the book to jump there.";
        [Tooltip("Shown after the page number: {0} is the page number.")]
        [SerializeField] string pageFormat = "p. {0}";

        public void Fill(IList<string> names, IList<int> pages)
        {
            if (titleText != null) titleText.text = title;
            if (hintText != null) hintText.text = hint;
            for (int i = 0; i < entryNames.Length; i++)
            {
                bool has = names != null && i < names.Count;
                if (entryNames[i] != null) { entryNames[i].gameObject.SetActive(has); if (has) entryNames[i].text = names[i]; }
                if (i < entryPages.Length && entryPages[i] != null)
                {
                    entryPages[i].gameObject.SetActive(has);
                    if (has) entryPages[i].text = string.Format(pageFormat, pages != null && i < pages.Count ? pages[i] : 0);
                }
            }
        }
    }
}
