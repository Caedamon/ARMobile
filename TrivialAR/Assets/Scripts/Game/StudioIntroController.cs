using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class StudioIntroController : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private bool debugLogs = false;
    [Tooltip("Paint everything the video does not cover (the edges when its shape differs from the screen) solid black instead of the camera's blue-grey sky.")]
    [SerializeField] private bool blackBackground = true;

    [Tooltip("A tap, click or key press skips the intro.")]
    [SerializeField] private bool tapToSkip = true;
    [Tooltip("Ignore taps for this many seconds at the start (so the tap that launched the game does not skip it).")]
    [SerializeField] private float skipGraceSeconds = 0.4f;

    private VideoPlayer videoPlayer;
    private float _startTime;
    private bool _leaving;

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.loopPointReached += OnVideoFinished;
        _startTime = Time.unscaledTime;

        if (blackBackground)
        {
            // The video is drawn on the camera it plays on; whatever it does not cover shows the camera's own background.
            var cam = videoPlayer.targetCamera != null ? videoPlayer.targetCamera : Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
            else if (debugLogs) Debug.LogWarning("[StudioIntro] No camera found to make black.");
        }
    }

    private void Update()
    {
        if (!tapToSkip || _leaving || Time.unscaledTime - _startTime < skipGraceSeconds) return;

        // read straight from the devices (like the menus do), so it works on the phone without any UI in the scene
        bool tapped =
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame);
        if (tapped) Leave("skipped");
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    private void OnVideoFinished(VideoPlayer player)
    {
        Leave("finished");
    }

    private void Leave(string why)
    {
        if (_leaving) return;
        _leaving = true;

        if (debugLogs)
            Debug.Log("[StudioIntro] Video " + why + ". Loading main menu.");

        if (videoPlayer != null) videoPlayer.Stop();
        SceneManager.LoadScene(mainMenuScene);
    }
}
