using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Combat;

namespace UI
{
    public class AutoPortraitBinder : MonoBehaviour
    {
        public Team team = Team.Main;
        public RawImage targetRawImage;

        public float orthoSize = 0.06f;
        public float forwardOffset = 0.14f;
        public float upOffset = 0.02f;

        public int textureSize = 256;
        public LayerMask cullingMaskForThisTeam;
        public Color clearColor = new Color(0, 0, 0, 0);

        [Header("Group fights: one fighter per portrait")]
        [Tooltip("When the HUD tells this portrait which fighter to show, the camera draws ONLY that fighter: for a moment each frame the fighter is moved onto this layer, drawn, and moved back. Pick a layer nothing else uses (31 is almost always free).")]
        [Range(8, 31)] public int portraitLayer = 31;
        [Tooltip("Redraw every N frames (2 = 30 times a second at 60 fps). Higher = lighter on the phone with many portraits.")]
        [Range(1, 6)] public int redrawEvery = 2;

        Camera _cam;
        RenderTexture _rt;
        Transform _head;
        MeleeAI _unit;
        bool _primed;
        float _nextRepick;
        bool _fixed;   // told by the HUD which fighter to show (group fights): never picks one itself

        /// <summary>Show this fighter (and only this one). Used by the fight HUD, which has one portrait per fighter.</summary>
        public void Bind(MeleeAI u)
        {
            _fixed = true;
            _unit = u;
            _head = null;
            _primed = false;
            if (_cam)
            {
                _cam.enabled = false;   // drawn by hand (RenderSolo), so only this fighter is in the picture
                _cam.cullingMask = 1 << portraitLayer;
            }
            if (_unit) TryBind();
        }

        static readonly List<Renderer> s_rends = new List<Renderer>();
        static readonly List<GameObject> s_objs = new List<GameObject>();
        static readonly List<int> s_layers = new List<int>();

        // Draw the picture with this fighter alone: its meshes (and what it holds) go onto the portrait layer for the duration of the
        // draw, then back to where they were. Nothing else can be in the picture, however close it stands. Floating name labels are skipped.
        void RenderSolo()
        {
            if (!_cam || !_unit) return;
            _unit.GetComponentsInChildren(true, s_rends);
            s_objs.Clear();
            s_layers.Clear();
            try
            {
                for (int i = 0; i < s_rends.Count; i++)
                {
                    var r = s_rends[i];
                    if (r == null || r.GetComponent<TMPro.TMP_Text>() != null) continue;   // the name label is not part of the portrait
                    var go = r.gameObject;
                    if (go.layer == portraitLayer) continue;   // already moved (two renderers on one object)
                    s_objs.Add(go);
                    s_layers.Add(go.layer);
                    go.layer = portraitLayer;
                }
                _cam.cullingMask = 1 << portraitLayer;
                _cam.Render();
            }
            finally
            {
                for (int i = 0; i < s_objs.Count; i++)
                    if (s_objs[i] != null) s_objs[i].layer = s_layers[i];
                s_objs.Clear();
                s_layers.Clear();
                s_rends.Clear();
            }
        }

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
            if (_fixed)
            {
                if (!_unit) return;   // that fighter is gone: the picture just stays as it was
            }
            // The right-hand portrait follows the nearest living opponent: when it has fallen, move on to the next one.
            else if (_unit && team == Team.Enemy && (_unit.IsDead || !_unit.isActiveAndEnabled) && Time.time >= _nextRepick)
            {
                _nextRepick = Time.time + 0.25f;
                var next = FindUnit(team);
                if (next != null && next != _unit) { _unit = next; _head = null; _primed = false; TryBind(); }
            }

            if (!_fixed && (!_unit || !_unit.isActiveAndEnabled))
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

                if (_fixed)
                {
                    if (!_rt.IsCreated()) _rt.Create();
                    int phase = (GetInstanceID() & 0x7fffffff) % Mathf.Max(1, redrawEvery);   // the portraits take turns
                    if (!_primed || (Time.frameCount % Mathf.Max(1, redrawEvery)) == phase)
                    {
                        RenderSolo();
                        _primed = true;
                    }
                }
                else if (!_primed)
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

                // SRP-safe: do not touch stereoTargetEye
                _cam.allowHDR = false;
                _cam.allowMSAA = false;
                _cam.depth = -100f;

                _cam.cullingMask = _fixed ? (1 << portraitLayer)
                    : (cullingMaskForThisTeam.value != 0
                        ? cullingMaskForThisTeam
                        : (1 << (_unit ? _unit.gameObject.layer : 0)));
                if (_fixed) _cam.enabled = false;

                _cam.targetTexture = _rt;
                _primed = false;
            }
            else
            {
                if (_fixed) { _cam.cullingMask = 1 << portraitLayer; _cam.enabled = false; }
                else if (cullingMaskForThisTeam.value == 0 && _unit)
                    _cam.cullingMask = (1 << _unit.gameObject.layer);

                if (_cam.targetTexture != _rt) _cam.targetTexture = _rt;
            }
        }

        // Main: the player's own fighter (not a teammate). Enemy: the living opponent nearest to the player's fighter.
        static MeleeAI FindUnit(Team t)
        {
            var units = Object.FindObjectsByType<MeleeAI>(FindObjectsSortMode.None);

            MeleeAI hero = null, firstOfTeam = null;
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i];
                if (!u || u.team != Team.Main) continue;
                if (firstOfTeam == null) firstOfTeam = u;
                var id = u.GetComponent<Fighters.FighterIdentity>();
                if (id != null && id.Data != null && id.Data == Fighters.FighterSession.PlayerFighter) { hero = u; break; }
            }
            if (hero == null) hero = firstOfTeam;

            if (t == Team.Main) return hero;

            MeleeAI best = null, anyEnemy = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i];
                if (!u || u.team != t) continue;
                if (anyEnemy == null) anyEnemy = u;
                if (u.IsDead || !u.isActiveAndEnabled) continue;
                float sq = hero != null ? (u.transform.position - hero.transform.position).sqrMagnitude : 0f;
                if (sq < bestSqr) { bestSqr = sq; best = u; }
            }
            return best != null ? best : anyEnemy;
        }

        static Transform FindHead(Transform root)
        {
            if (!root) return null;

            var anchor = FindChildByName(root, "HeadAnchor");
            if (anchor) return anchor;

            var anim = root.GetComponentInChildren<Animator>();
            if (anim && anim.isHuman)
            {
                var bone = anim.GetBoneTransform(HumanBodyBones.Head);
                if (bone) return bone;
            }

            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = new Bounds(root.position, Vector3.zero);
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i];
                    if (r.enabled) b.Encapsulate(r.bounds);
                }
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
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }
    }
}
