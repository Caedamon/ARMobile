using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

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
        int _frames;
        float _windowStart;
        float _worst;
        float _startTime;

        public void Init(Func<string> stateProvider)
        {
            _state = stateProvider;
            _startTime = Time.realtimeSinceStartup;
            _windowStart = Time.unscaledTime;
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
            rt.sizeDelta = new Vector2(1000f, 900f);

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
            _frames++;
            _worst = Mathf.Max(_worst, Time.unscaledDeltaTime);

            Vector2 pos;
            var ts = Touchscreen.current;
            var mouse = Mouse.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { Press(ts.primaryTouch.position.ReadValue()); }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame) { Press(mouse.position.ReadValue()); }
            pos = _lastPos;

            if (Time.unscaledTime < _nextRefresh || _text == null) return;
            float window = Mathf.Max(0.001f, Time.unscaledTime - _windowStart);
            _fps = _frames / window;
            float worstMs = _worst * 1000f;
            _frames = 0;
            _worst = 0f;
            _windowStart = Time.unscaledTime;
            _nextRefresh = Time.unscaledTime + 0.5f;

            _sb.Length = 0;
            _sb.Append("Up ").Append((Time.realtimeSinceStartup - _startTime).ToString("0")).Append("s  frame ").Append(Time.frameCount)
               .Append("  FPS ").Append(_fps.ToString("0.0")).Append("  worst ").Append(worstMs.ToString("0")).Append("ms").Append('\n');
            _sb.Append(SystemInfo.graphicsDeviceType).Append("  ").Append(SystemInfo.deviceModel)
               .Append("  ").Append(Screen.width).Append("x").Append(Screen.height).Append('\n');
            _sb.Append("AR session: ").Append(ARSession.state).Append("   camera permission: ").Append(CameraPermission()).Append('\n');
            _sb.Append("Touchscreen: ").Append(ts != null ? "yes" : "NO").Append("   Mouse: ").Append(mouse != null ? "yes" : "no")
               .Append("   touching now: ").Append(ts != null && ts.primaryTouch.press.isPressed ? "yes" : "no").Append('\n');
            _sb.Append("State: ").Append(_state != null ? _state() : "?").Append("   presses: ").Append(_pressCount)
               .Append("   scene: ").Append(SceneManager.GetActiveScene().name).Append('\n');
            _sb.Append(GameUI.DebugInfo()).Append('\n');

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

            foreach (var e in StartupErrorCapture.Recent) _sb.Append("ERR ").Append(e).Append('\n');
            _text.text = _sb.ToString();
        }

        static string CameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) ? "yes" : "NO";
#else
            return "n/a";
#endif
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
