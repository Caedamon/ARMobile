using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Collects every error and exception from the very first moment the app runs (before any of our scripts have started),
    /// so the yellow device readout can show errors from the start-up too. If the readout itself never appears (because the game
    /// controller failed to start) a small red box with the errors is drawn instead.
    /// </summary>
    public static class StartupErrorCapture
    {
        static readonly List<string> Lines = new List<string>();

        public static IList<string> Recent { get { return Lines; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            Lines.Clear();
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void MakeWatcher()
        {
            if (Object.FindFirstObjectByType<StartupWatch>() != null) return;
            var go = new GameObject("StartupWatch");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<StartupWatch>();
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string first = message.Replace("\n", " ");
            if (first.Length > 110) first = first.Substring(0, 110);
            string where = "";
            if (!string.IsNullOrEmpty(stack))
            {
                int nl = stack.IndexOf('\n');
                where = " @ " + (nl > 0 ? stack.Substring(0, nl) : stack);
                if (where.Length > 90) where = where.Substring(0, 90);
            }
            Lines.Add("[" + SceneManager.GetActiveScene().name + " " + Time.frameCount + "] " + first + where);
            while (Lines.Count > 6) Lines.RemoveAt(0);
        }
    }

    /// <summary>Draws the captured errors in red if the normal yellow readout is not there.</summary>
    public sealed class StartupWatch : MonoBehaviour
    {
        float _nextCheck;
        bool _show;
        GUIStyle _style;

        void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 1f;
            _show = StartupErrorCapture.Recent.Count > 0 && FindFirstObjectByType<DeviceDebugOverlay>() == null;
        }

        void OnGUI()
        {
            if (!_show) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, Screen.height / 55), wordWrap = true };
                _style.normal.textColor = new Color(1f, 0.35f, 0.3f);
            }
            var sb = new System.Text.StringBuilder("ERRORS (the readout never started):\n");
            foreach (var l in StartupErrorCapture.Recent) sb.Append(l).Append('\n');
            GUI.Label(new Rect(12f, 90f, Screen.width - 24f, Screen.height * 0.6f), sb.ToString(), _style);
        }
    }
}
