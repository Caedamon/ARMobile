using UnityEngine;
using UnityEngine.UI;
using Combat;   // Team, TurnBasedMeleeAI

namespace UI
{
    /// Binds a RawImage to a live RenderTexture portrait of a unit's head.
    public class AutoPortraitBinder : MonoBehaviour
    {
        [Header("Bind")]
        public Team team = Team.Main;
        [Tooltip("RawImage that will display the portrait (must be RawImage, not Image).")]
        public RawImage targetRawImage;

        [Header("Framing")]
        [Tooltip("Orthographic half-height in meters (tune per model scale).")]
        public float orthoSize = 0.06f;
        [Tooltip("Camera is placed this far IN FRONT of face (meters).")]
        public float forwardOffset = 0.14f;
        [Tooltip("Slight upward offset to center face (meters).")]
        public float upOffset = 0.02f;

        [Header("Render")]
        public int textureSize = 256;
        public LayerMask cullingMaskForThisTeam; // e.g. set to Main or Enemy layer
        public Color clearColor = new Color(0,0,0,0); // transparent

        Camera _cam;
        RenderTexture _rt;
        Transform _head;
        TurnBasedMeleeAI _unit;

        void OnEnable()
        {
            TryBind();
        }

        void OnDisable()
        {
            if (_cam) _cam.targetTexture = null;
            if (_rt) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_cam) Destroy(_cam.gameObject);
            _cam = null;
            _head = null;
            _unit = null;
        }

        void LateUpdate()
        {
            if (!_unit || !_unit.isActiveAndEnabled)
            {
                TryBind();
                return;
            }
            if (!_head) _head = FindHead(_unit.transform);

            if (_cam && _head)
            {
                // Camera sits in front of face looking back at it
                var fwd = _head.forward;
                var up  = Vector3.up;
                var pos = _head.position + fwd * forwardOffset + up * upOffset;

                _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-fwd, up));
                _cam.orthographicSize = orthoSize;
            }
        }

        void TryBind()
        {
            // 1) find the unit by team
            _unit = FindUnit(team);
            if (!_unit) return;

            // 2) find head (HeadAnchor > Humanoid Head > fallback to renderer bounds)
            _head = FindHead(_unit.transform);

            // 3) ensure RT + camera exist
            EnsureRenderTargets();

            if (targetRawImage && _rt) targetRawImage.texture = _rt;
        }

        void EnsureRenderTargets()
        {
            if (_rt == null)
            {
                _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
                {
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _rt.Create();
            }
            if (_cam == null)
            {
                var go = new GameObject($"PortraitCam_{team}");
                _cam = go.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = clearColor;
                _cam.nearClipPlane = 0.01f;
                _cam.farClipPlane  = 1.0f;
                _cam.cullingMask = cullingMaskForThisTeam; // set in Inspector to only this team’s layer
                _cam.allowHDR = false;
                _cam.allowMSAA = false;
                _cam.stereoTargetEye = StereoTargetEyeMask.None;
                _cam.depth = -100f;
                _cam.targetTexture = _rt;
            }
        }

        static TurnBasedMeleeAI FindUnit(Team t)
        {
            var units = Object.FindObjectsByType<TurnBasedMeleeAI>(FindObjectsSortMode.None);
            foreach (var u in units) if (u && u.team == t) return u;
            return null;
        }

        static Transform FindHead(Transform root)
        {
            if (!root) return null;

            // 1) Explicit anchor preferred
            var anchor = FindChildByName(root, "HeadAnchor");
            if (anchor) return anchor;

            // 2) Humanoid Animator bone
            var anim = root.GetComponentInChildren<Animator>();
            if (anim && anim.isHuman)
            {
                var bone = anim.GetBoneTransform(HumanBodyBones.Head);
                if (bone) return bone;
            }

            // 3) Fallback: top of renderer bounds
            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = new Bounds(root.position, Vector3.zero);
                foreach (var r in rends) { if (r.enabled) b.Encapsulate(r.bounds); }
                var t = new GameObject("HeadApprox").transform;
                t.position = new Vector3(b.center.x, b.max.y - 0.05f * b.size.y, b.center.z);
                t.forward = root.forward;
                t.SetParent(root, worldPositionStays: true);
                return t;
            }
            return root;
        }

        static Transform FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
