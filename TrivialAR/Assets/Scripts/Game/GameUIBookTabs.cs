using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The four tabs under the open Book (Index, Memoria Dies, Stats, Armoury). Part of GameUI (a partial class).
    /// Built by AddBookTabs (Tools > Kaiju > Book > Set Up Index, Stats and Armoury puts them into the GameUI prefab,
    /// where you can restyle them) and as a run-time fallback.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>A tab under the Book was tapped: 0 Index, 1 Stats, 2 Armoury, 3 Memento Mori (the same order as BookStage.Section).</summary>
        public event Action<int> BookTabClicked;

        [Header("Book tabs (made by Set Up Index, Stats and Armoury)")]
        [SerializeField] Button[] _bookTabs = new Button[6];

        [Tooltip("Names on the tabs, in book order: Index, Stats, Armoury, then the graveyard.")]
        [SerializeField] string[] _bookTabNames = { "Index", "Arena Rules", "Stats", "Armoury", "Codex", "Memento Mori" };

        public bool HasBookTabs()
        {
            if (_bookPanel == null) return true;   // no Book screen, so nothing to add tabs to
            if (_bookTabs == null || _bookTabs.Length != 6) return false;
            for (int i = 0; i < 6; i++) if (_bookTabs[i] == null) return false;
            return true;
        }

        public void AddBookTabs()
        {
            if (_bookPanel == null) return;
            if (_bookTabs == null || _bookTabs.Length != 6) _bookTabs = new Button[6];

            float[] x = { 0.09f, 0.25f, 0.41f, 0.57f, 0.73f, 0.89f };
            for (int i = 0; i < 6; i++)
            {
                if (_bookTabs[i] != null) continue;
                string name = "BookTab" + i;

                var found = _bookPanel.transform.Find(name);
                if (found != null) _bookTabs[i] = found.GetComponent<Button>();
                if (_bookTabs[i] != null) continue;

                TMP_Text l;
                _bookTabs[i] = NewButton(_bookPanel.transform, name, TabName(i), x[i], 0.225f, new Vector2(164f, 100f), Grey, out l);
                l.fontSize = 32f;
            }
        }

        string TabName(int i)
        {
            string[] fallback = { "Index", "Arena Rules", "Stats", "Armoury", "Codex", "Memento Mori" };
            bool saved = _bookTabNames != null && _bookTabNames.Length == fallback.Length;   // names saved before the Arena Rules tab existed are the wrong length: ignore them
            return saved && !string.IsNullOrEmpty(_bookTabNames[i]) ? _bookTabNames[i] : fallback[i];
        }

        void WireBookTabs()
        {
            if (_bookTabs == null) return;
            for (int i = 0; i < _bookTabs.Length; i++)
            {
                int idx = i;
                if (_bookTabs[i] != null)
                {
                    var lab = _bookTabs[i].GetComponentInChildren<TMP_Text>();   // tabs made earlier get the current names too
                    if (lab != null) lab.text = TabName(i);
                }
                Wire(_bookTabs[i], () => Raise(() => { if (BookTabClicked != null) BookTabClicked(idx); }));
            }
        }

        /// <summary>Shows or hides the row of tab buttons under the book (hidden when the book has its own side tabs).</summary>
        public void SetBookTabButtonsVisible(bool visible)
        {
            if (_bookTabs == null) return;
            foreach (var b in _bookTabs) if (b != null) b.gameObject.SetActive(visible);
        }

        // The tabs work when the Book is open and not in the middle of turning.
        void SetBookTabsEnabled(bool on)
        {
            if (_bookTabs == null) return;
            foreach (var b in _bookTabs) if (b != null) b.interactable = on;
        }
    }
}
