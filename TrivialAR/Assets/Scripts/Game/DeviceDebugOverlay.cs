using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Small on-screen readout for device testing: input devices, last tap, what the EventSystem and our own
    /// hit-test find under it, game state and the last few errors. Turned on/off by GameController.deviceDebugOverlay.
    /// </summary>
    public sealed class DeviceDebugOverlay : MonoBehaviour
    {
        TextMeshProUGUI _text;
        Func<string> _state;
        readonly Queue<string> _errors = new Queue<string>();
        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        readonly StringBuilder _sb = new StringBuilder();

        bool _hasPress;
        Vector2 _lastPos;
        float _lastPressTime;
        int _pressCount;
        float _nextRefresh;
        float _fps;

        public void Init(Func<string> stateProvider)
        {
            _state = stateProvider;
            Build();
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        void Build()
        {
            var go = new GameObject("DeviceDebugCanvas");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)tgo.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(12f, -90f);
            rt.sizeDelta = new Vector2(1000f, 700f);

            _text = tgo.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 26f;
            _text.color = new Color(1f, 0.95f, 0.3f, 0.95f);
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.raycastTarget = false;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string line = message.Length > 110 ? message.Substring(0, 110) : message;
            _errors.Enqueue(line.Replace("\n", " "));
            while (_errors.Count > 4) _errors.Dequeue();
        }

        void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.1f);

            Vector2 pos;
            var ts = Touchscreen.current;
            var mouse = Mouse.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { Press(ts.primaryTouch.position.ReadValue()); }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame) { Press(mouse.position.ReadValue()); }
            pos = _lastPos;

            if (Time.unscaledTime < _nextRefresh || _text == null) return;
            _nextRefresh = Time.unscaledTime + 0.2f;

            _sb.Length = 0;
            _sb.Append("FPS ").Append(_fps.ToString("0")).Append("   screen ").Append(Screen.width).Append("x").Append(Screen.height)
               .Append("   safe ").Append(Mathf.RoundToInt(Screen.safeArea.width)).Append("x").Append(Mathf.RoundToInt(Screen.safeArea.height)).Append('\n');
            _sb.Append("Touchscreen: ").Append(ts != null ? "yes" : "NO").Append("   Mouse: ").Append(mouse != null ? "yes" : "no")
               .Append("   touching now: ").Append(ts != null && ts.primaryTouch.press.isPressed ? "yes" : "no").Append('\n');
            _sb.Append("State: ").Append(_state != null ? _state() : "?").Append("   presses: ").Append(_pressCount).Append('\n');

            var es = EventSystem.current;
            _sb.Append("EventSystem: ").Append(es != null ? es.name : "NONE");
            if (es != null)
            {
                var module = es.currentInputModule;
                _sb.Append(" / ").Append(module != null ? module.GetType().Name : "no module");
            }
            _sb.Append('\n');

            if (_hasPress)
            {
                _sb.Append("Last tap ").Append(Mathf.RoundToInt(_lastPos.x)).Append(",").Append(Mathf.RoundToInt(_lastPos.y))
                   .Append("  (").Append((Time.unscaledTime - _lastPressTime).ToString("0.0")).Append("s ago)\n");

                _sb.Append("  EventSystem sees: ");
                _hits.Clear();
                if (es != null) es.RaycastAll(new PointerEventData(es) { position = _lastPos }, _hits);
                if (_hits.Count == 0) _sb.Append("nothing");
                for (int i = 0; i < _hits.Count && i < 3; i++) _sb.Append(_hits[i].gameObject.name).Append("  ");
                _sb.Append('\n');

                _sb.Append("  our hit-test: ").Append(GameUI.IsPointOverButton(_lastPos) ? "a button" : "no button").Append('\n');
            }
            else
            {
                _sb.Append("Tap the screen once to see what is under your finger.\n");
            }

            foreach (var e in _errors) _sb.Append("ERR ").Append(e).Append('\n');
            _text.text = _sb.ToString();
        }

        void Press(Vector2 p)
        {
            _hasPress = true;
            _lastPos = p;
            _lastPressTime = Time.unscaledTime;
            _pressCount++;
        }
    }
}
