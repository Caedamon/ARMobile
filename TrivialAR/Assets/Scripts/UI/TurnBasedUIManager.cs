// Assets/Scripts/UI/TurnBasedUIManager.cs
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Combat;   // Team, TurnBasedMeleeAI, TurnBasedEvents
using Game;     // Health (change/remove this 'using' if your Health is in global namespace)

namespace UI
{
    /// Drives corner health bars and action text. Put on your Screen-Space Canvas.
    public class TurnBasedUIManager : MonoBehaviour
    {
        [Header("Main (Left)")]
        public Image mainPortrait;        // optional
        public Image mainHealthFill;      // GREEN Image (Filled Horizontal, Left)
        public TMP_Text mainActionText;   // TMP under Main

        [Header("Enemy (Right)")]
        public Image enemyPortrait;       // optional
        public Image enemyHealthFill;     // GREEN Image (Filled Horizontal, Left)
        public TMP_Text enemyActionText;  // TMP under Enemy

        private Health _mainH, _enemyH;
        private FieldInfo _hpF, _maxF;

        void OnEnable()  => TurnBasedEvents.OnActionText += HandleActionText;
        void OnDisable() => TurnBasedEvents.OnActionText -= HandleActionText;

        void Awake()
        {
            // Reads private float _hp and public float maxHP from your Health.
            _hpF  = typeof(Health).GetField("_hp",  BindingFlags.Instance | BindingFlags.NonPublic);
            _maxF = typeof(Health).GetField("maxHP", BindingFlags.Instance | BindingFlags.Public);

            if (mainActionText)  mainActionText.alpha  = 0f;
            if (enemyActionText) enemyActionText.alpha = 0f;
        }

        void Start() => Rebind(); // AR: call Rebind() again after Instantiate

        public void Rebind()
        {
            _mainH = _enemyH = null;

            var units = FindObjectsByType<TurnBasedMeleeAI>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                var h = u.GetComponent<Health>();
                if (!h) continue;
                if (u.team == Team.Main  && _mainH  == null) _mainH  = h;
                if (u.team == Team.Enemy && _enemyH == null) _enemyH = h;
            }

            // Push initial values immediately
            LateUpdate();
        }

        void LateUpdate()
        {
            if (_hpF == null || _maxF == null) return;

            if (mainHealthFill && _mainH)  mainHealthFill.fillAmount  = Ratio(_mainH);
            if (enemyHealthFill && _enemyH) enemyHealthFill.fillAmount = Ratio(_enemyH);
        }

        private float Ratio(Health h)
        {
            float max = Mathf.Max(0.0001f, (float)_maxF.GetValue(h));
            float cur = Mathf.Clamp((float)_hpF.GetValue(h), 0f, max);
            return cur / max;
        }

        private void HandleActionText(TurnBasedMeleeAI who, string text)
        {
            var label = (who.team == Team.Main) ? mainActionText : enemyActionText;
            if (!label) return;

            StopAllCoroutines(); // keeps the latest call visible
            StartCoroutine(FadeMessage(label, text, 0.15f, 0.9f, 0.6f));
        }

        private IEnumerator FadeMessage(TMP_Text label, string text, float fadeIn, float hold, float fadeOut)
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