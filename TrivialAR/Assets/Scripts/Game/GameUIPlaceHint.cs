using TMPro;
using UnityEngine;

namespace Game
{
    // The hint strip on the "place the arena" screen: the text changes while the phone looks for a surface.
    public sealed partial class GameUI
    {
        TMP_Text _placeHintText;

        public void SetPlaceHint(string text)
        {
            if (_placePanel == null) return;
            if (_placeHintText == null)
            {
                var t = _placePanel.transform.Find("HintStrip/Hint");
                if (t != null) _placeHintText = t.GetComponent<TMP_Text>();
                if (_placeHintText == null) return;
            }
            if (_placeHintText.text != text) _placeHintText.text = text;
        }
    }
}
