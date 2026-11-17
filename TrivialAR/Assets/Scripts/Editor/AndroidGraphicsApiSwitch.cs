#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Build > ...
// A quick A/B test for AR on Android: ARCore + URP is more reliable on OpenGLES3 than on Vulkan on many phones.
namespace KaijuTools
{
    public static class AndroidGraphicsApiSwitch
    {
        [MenuItem("Tools/Kaiju/Build/Android - use OpenGLES3 only (AR test)")]
        public static void UseGles3()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            Debug.Log("[Build] Android graphics API: OpenGLES3 only. Rebuild the APK.");
        }

        [MenuItem("Tools/Kaiju/Build/Android - use Vulkan, then OpenGLES3")]
        public static void UseVulkanThenGles3()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
            });
            Debug.Log("[Build] Android graphics API: Vulkan, then OpenGLES3.");
        }
    }
}
#endif
