using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Game
{
    /// <summary>
    /// Makes sure the phone's camera permission is asked for as soon as the game starts, BEFORE the AR scene needs it, and if the player
    /// has refused it for good, shows a clear message with a button that opens this app's settings page.
    /// Creates itself (no scene setup). Does nothing in the Editor and on non-Android.
    /// </summary>
    public sealed class CameraPermissionGate : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static CameraPermissionGate _instance;

        bool _asked, _showPanel, _dontAskAgain;
        GUIStyle _box, _label, _button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (_instance != null) return;
            var go = new GameObject("CameraPermissionGate");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CameraPermissionGate>();
        }

        void Start() { Check(); }

        // coming back from the settings page: look again
        void OnApplicationFocus(bool focus) { if (focus && _asked) Check(); }

        void Check()
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                _showPanel = false;
                return;
            }
            if (_asked && _dontAskAgain) { _showPanel = true; return; }   // Android will not show its dialog again

            _asked = true;
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += _ => { _showPanel = false; };
            cb.PermissionDenied += _ =>
            {
                // no rationale to show after a refusal = Android will not ask again (refused twice, or "don't ask again")
                _dontAskAgain = !Permission.ShouldShowRequestPermissionRationale(Permission.Camera);
                _showPanel = true;
            };
            Permission.RequestUserPermission(Permission.Camera, cb);
        }

        void OnGUI()
        {
            if (!_showPanel) return;
            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box) { fontSize = 40 };
                _label = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
                _button = new GUIStyle(GUI.skin.button) { fontSize = 44 };
            }
            float w = Screen.width * 0.86f, h = Screen.height * 0.42f;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(r, GUIContent.none, _box);
            GUI.Label(new Rect(r.x + 30, r.y + 20, r.width - 60, r.height * 0.5f),
                "ARena needs the camera to show the arena on your table.\nCamera permission is switched off.", _label);

            if (GUI.Button(new Rect(r.x + 40, r.y + r.height * 0.58f, r.width - 80, r.height * 0.18f),
                    _dontAskAgain ? "Open settings" : "Allow camera", _button))
            {
                if (_dontAskAgain) OpenSettings(); else Check();
            }
            if (GUI.Button(new Rect(r.x + 40, r.y + r.height * 0.79f, r.width - 80, r.height * 0.16f), "Not now", _button))
                _showPanel = false;
        }

        static void OpenSettings()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS"))
                using (var uri = new AndroidJavaClass("android.net.Uri").CallStatic<AndroidJavaObject>("fromParts", "package", activity.Call<string>("getPackageName"), null))
                {
                    intent.Call<AndroidJavaObject>("setData", uri);
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000);   // FLAG_ACTIVITY_NEW_TASK
                    activity.Call("startActivity", intent);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Camera] Could not open the settings page: " + e.Message); }
        }
#endif
    }
}