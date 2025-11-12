// File: Assets/Scripts/UI/TurnBasedUIManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Combat;
using Core;
using Game;

namespace UI
{
    public class BattleUIManager : MonoBehaviour
    {
        [Header("Main (Left)")]
        public RawImage mainPortrait;     // <-- RawImage for live RenderTexture
        public Image mainHealthFill;      // Filled Image (GREEN)
        public TMP_Text mainActionText;

        [Header("Enemy (Right)")]
        public RawImage enemyPortrait;    // <-- RawImage for live RenderTexture
        public Image enemyHealthFill;     // Filled Image (GREEN)
        public TMP_Text enemyActionText;

        Health _mainH, _enemyH;

        void OnEnable()  => BattleEvents.OnActionText += HandleActionText;
        void OnDisable() => BattleEvents.OnActionText -= HandleActionText;
        void Start()     => Rebind();

        public void Rebind()
        {
            _mainH = _enemyH = null;

            var units = FindObjectsByType<MeleeAI>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                var h = u.GetComponent<Health>();
                if (!h) continue;
                if (u.team == Team.Main  && _mainH  == null) _mainH  = h;
                if (u.team == Team.Enemy && _enemyH == null) _enemyH = h;
            }

            UpdateFill(mainHealthFill, _mainH);
            UpdateFill(enemyHealthFill, _enemyH);

            if (_mainH)  _mainH.OnHealthChanged  += (cur, max) => UpdateFill(mainHealthFill, _mainH);
            if (_enemyH) _enemyH.OnHealthChanged += (cur, max) => UpdateFill(enemyHealthFill, _enemyH);
        }

        void UpdateFill(Image img, Health h)
        {
            if (!img || !h) return;
            var max = Mathf.Max(0.0001f, h.MaxHP);
            img.fillAmount = Mathf.Clamp(h.CurrentHP, 0f, max) / max;
        }

        void HandleActionText(MeleeAI who, string text)
        {
            var label = (who.team == Team.Main) ? mainActionText : enemyActionText;
            if (!label) return;

            StopAllCoroutines();
            StartCoroutine(FadeMessage(label, text, 0.15f, 0.9f, 0.6f));
        }

        IEnumerator FadeMessage(TMP_Text label, string text, float fadeIn, float hold, float fadeOut)
        {
            label.text = text;

            for (float t = 0f; t < fadeIn; t += Time.deltaTime) { label.alpha = Mathf.Lerp(0f, 1f, t / fadeIn); yield return null; }
            label.alpha = 1f;

            yield return new WaitForSeconds(hold);

            for (float t = 0f; t < fadeOut; t += Time.deltaTime) { label.alpha = Mathf.Lerp(1f, 0f, t / fadeOut); yield return null; }
            label.alpha = 0f;
        }
    }
}
