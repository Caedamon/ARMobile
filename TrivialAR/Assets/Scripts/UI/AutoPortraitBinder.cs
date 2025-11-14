using UnityEngine;
using UnityEngine.UI;
using Combat;   // Team, MeleeAI

namespace UI
{
    /// Binds a RawImage to a live RenderTexture portrait of a unit's head.
    public class AutoPortraitBinder : MonoBehaviour
    {
        [Header("Bind")]
        public Team team = Team.Main;
        public RawImage targetRawImage;

        [Header("Framing")]
        public float orthoSize = 0.06f;
        public float forwardOffset = 0.14f;
        public float upOffset = 0.02f;

        [Header("Render")]
        public int textureSize = 256;
        public LayerMask cullingMaskForThisTeam;
        public Color clearColor = new Color(0, 0, 0, 0);

        Camera _cam;
        RenderTexture _rt;
        Transform _head;
        MeleeAI _unit;
        bool _primed;

        void OnEnable()
        {
            _primed = false;
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
            if (!_cam || !_rt) EnsureRenderTargets();

            if (_cam && _head)
            {
                var fwd = _head.forward;
                var up = Vector3.up;
                var pos = _head.position + fwd * forwardOffset + up * upOffset;

                _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-fwd, up));
                _cam.orthographicSize = orthoSize;

                // One-time force render to avoid blank first frame
                if (!_primed)
                {
                    if (!_rt.IsCreated()) _rt.Create();
                    _cam.Render();
                    _primed = true;
                }
            }
        }

        void TryBind()
        {
            if (!_unit) _unit = FindUnit(team);
            if (!_unit) return;

            if (!_head) _head = FindHead(_unit.transform);

            EnsureRenderTargets();

            if (targetRawImage && _rt && targetRawImage.texture != _rt)
                targetRawImage.texture = _rt;
        }

        void EnsureRenderTargets()
        {
            if (_rt == null || _rt.width != textureSize || _rt.height != textureSize)
            {
                if (_rt != null) { if (_cam && _cam.targetTexture == _rt) _cam.targetTexture = null; _rt.Release(); Destroy(_rt); }
                _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
                {
                    useMipMap = false,
                    autoGenerateMips = false,
                    name = $"PortraitRT_{team}"
                };
                _rt.Create();
                _primed = false;
            }

            if (_cam == null)
            {
                var go = new GameObject($"PortraitCam_{team}");
                _cam = go.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = clearColor;
                _cam.nearClipPlane = 0.01f;
                _cam.farClipPlane = 1.0f;

                // SRP-safe: DO NOT touch stereoTargetEye.
                // _cam.stereoTargetEye = StereoTargetEyeMask.None; // <-- removed

                _cam.allowHDR = false;
                _cam.allowMSAA = false;
                _cam.depth = -100f;

                _cam.cullingMask = cullingMaskForThisTeam.value != 0
                    ? cullingMaskForThisTeam
                    : (1 << (_unit ? _unit.gameObject.layer : 0));

                _cam.targetTexture = _rt;
                _primed = false;
            }
            else
            {
                // Keep mask up-to-date if inspector left it 0
                if (cullingMaskForThisTeam.value == 0 && _unit)
                    _cam.cullingMask = (1 << _unit.gameObject.layer);

                if (_cam.targetTexture != _rt) _cam.targetTexture = _rt;
            }
        }

        static MeleeAI FindUnit(Team t)
        {
            var units = Object.FindObjectsByType<MeleeAI>(FindObjectsSortMode.None);
            foreach (var u in units) if (u && u.team == t) return u;
            return null;
        }

        static Transform FindHead(Transform root)
        {
            if (!root) return null;

            // Preferred explicit anchor
            var anchor = FindChildByName(root, "HeadAnchor");
            if (anchor) return anchor;

            // Humanoid head bone
            var anim = root.GetComponentInChildren<Animator>();
            if (anim && anim.isHuman)
            {
                var bone = anim.GetBoneTransform(HumanBodyBones.Head);
                if (bone) return bone;
            }

            // Bounds-based fallback
            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = new Bounds(root.position, Vector3.zero);
                foreach (var r in rends) if (r.enabled) b.Encapsulate(r.bounds);
                var t = new GameObject("HeadApprox").transform;
                t.position = new Vector3(b.center.x, b.max.y - 0.05f * b.size.y, b.center.z);
                t.forward = root.forward;
                t.SetParent(root, true);
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