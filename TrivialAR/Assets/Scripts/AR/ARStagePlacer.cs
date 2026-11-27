using System.Collections.Generic;
using Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AR
{
    /// <summary>
    /// Replaces ARPlaceArena. The player places ONE anchor on the table; after that, "stages"
    /// (roster, hub, arena...) are swapped in and out at that same spot. No re-aiming between screens.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ARStagePlacer : MonoBehaviour
    {
        public bool hidePlanesAfterPlacement = true;
        [Tooltip("'Place in front of me' fallback: distance ahead of the camera (meters).")]
        public float inFrontDistance = 0.6f;
        [Tooltip("'Place in front of me' fallback: how far below the camera (meters).")]
        public float inFrontDrop = 0.35f;

        public bool HasPlacement { get; private set; }
        /// <summary>True when the stage was put on a real surface the phone found (a table). False for "Place in front of me": it hangs in the air.</summary>
        public bool PlacedOnSurface { get; private set; }
        public GameObject CurrentStage { get; private set; }

        ARRaycastManager _raycast;
        ARPlaneManager _planes;
        ARAnchorManager _anchors;
        Transform _anchorRoot;
        ARAnchor _anchor;

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>(8);

        void Awake() { FindManagers(); }

        // When the game stops, Unity tears the scene down in no fixed order. If the AR Anchor Manager is destroyed first, the anchor then
        // asks the dead manager to remove it and logs a MissingReferenceException. Switching the anchor off BEFORE the teardown (the manager
        // is still alive at that point) lets it unregister properly. Harmless for the game: everything is being closed anyway.
        void OnApplicationQuit() { ReleaseAnchor(); }
        void OnDestroy() { ReleaseAnchor(); HideReticle(); }

        void ReleaseAnchor()
        {
            var a = _anchor;
            _anchor = null;
            if (a == null) return;
            try { a.enabled = false; }
            catch (System.Exception) { /* the manager was already gone: nothing left to unregister from */ }
        }

        void FindManagers()
        {
            if (_raycast == null) _raycast = FindFirstObjectByType<ARRaycastManager>();
            if (_planes == null)  _planes  = FindFirstObjectByType<ARPlaneManager>();
            if (_anchors == null) _anchors = FindFirstObjectByType<ARAnchorManager>();
            _sharedRaycast = _raycast;
        }

        // ------------------------------------------------------------------ finding the table
        public enum Quality { None, Rough, Surface }

        /// <summary>What the player should be told right now while looking for a surface (shown in the hint strip).</summary>
        public string HintText { get; private set; } = "Slowly sweep the phone over the table";
        /// <summary>None = nothing under the ring, Rough = only loose feature points (ring is yellow), Surface = a real flat plane (ring is green).</summary>
        public Quality FoundQuality { get; private set; }

        Pose _aim;
        ARPlane _aimPlane;
        bool _hasAim, _hadAim;
        Vector3 _smoothPos;
        float _noHitSeconds;
        int _lastPollFrame = -10;
        Transform _reticle;
        Renderer[] _reticleRends;
        Material _reticleGood, _reticleRough;

        /// <summary>
        /// Call every frame while waiting for placement. True on the frame the player placed the stage anchor.
        /// A ring in the middle of the screen shows where the arena will go; tap anywhere to put it there.
        /// Green ring = a real horizontal surface; yellow ring = only rough depth points (less exact, but works on plain or dark tables).
        /// </summary>
        public bool PollTapPlacement()
        {
            if (HasPlacement) { HideReticle(); return false; }
            FindManagers();
            if (_raycast == null) return false;

            _lastPollFrame = Time.frameCount;
            ScanForFloorsOnly();
            UpdateAim();
            ShowReticle();

            if (!TryGetTap(out var screenPos)) return false;
            if (UiGuard.IsOverUI(screenPos)) return false;
            if (!_hasAim) return false;   // the hint strip already says what to do

            SetPlacement(_aim, _aimPlane);
            HideReticle();
            return true;
        }

        // Only look for horizontal surfaces: walls and other things the phone finds just confuse the placement.
        void ScanForFloorsOnly()
        {
            if (_planes == null) return;
            if (!_planes.enabled) _planes.enabled = true;
            if (_planes.requestedDetectionMode != PlaneDetectionMode.Horizontal)
                _planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
        }

        void UpdateAim()
        {
            _hasAim = false;
            _aimPlane = null;
            FoundQuality = Quality.None;

            var cam = Camera.main;
            if (cam != null)
            {
                var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Pose pose;
                ARPlane plane;
                if (TryPlaneHit(centre, TrackableType.PlaneWithinPolygon, out pose, out plane) ||
                    TryPlaneHit(centre, TrackableType.PlaneWithinBounds | TrackableType.PlaneEstimated, out pose, out plane))
                {
                    _hasAim = true; _aimPlane = plane; FoundQuality = Quality.Surface; _aim = pose;
                }
                else if (TryPointHit(centre, cam, out pose))
                {
                    _hasAim = true; FoundQuality = Quality.Rough; _aim = pose;
                }
            }

            if (_hasAim)
            {
                // a little smoothing so the ring does not jitter
                _smoothPos = _hadAim ? Vector3.Lerp(_smoothPos, _aim.position, 1f - Mathf.Exp(-14f * Time.deltaTime)) : _aim.position;
                _aim = new Pose(_smoothPos, _aim.rotation);
                _noHitSeconds = 0f;
            }
            else _noHitSeconds += Time.deltaTime;
            _hadAim = _hasAim;

            if (FoundQuality == Quality.Surface) HintText = "Surface found - tap to place the arena";
            else if (FoundQuality == Quality.Rough) HintText = "Rough surface - keep scanning, or tap to place here";
            else if (_noHitSeconds > 7f) HintText = "Try more light, a patterned surface, 1 m away - or place it in front of you";
            else HintText = "Slowly sweep the phone over the table";
        }

        // A real flat plane facing up (never a wall).
        static bool TryPlaneHit(Vector2 screen, TrackableType types, out Pose pose, out ARPlane plane)
        {
            pose = default; plane = null;
            var raycast = _sharedRaycast;
            if (raycast == null || !raycast.Raycast(screen, Hits, types)) return false;
            for (int i = 0; i < Hits.Count; i++)
            {
                var p = Hits[i].trackable as ARPlane;
                if (p == null || p.alignment != PlaneAlignment.HorizontalUp) continue;
                pose = Hits[i].pose; plane = p;
                return true;
            }
            return false;
        }

        // A loose depth point that lies below the phone, a sensible distance away and faces up.
        bool TryPointHit(Vector2 screen, Camera cam, out Pose pose)
        {
            pose = default;
            if (!_raycast.Raycast(screen, Hits, TrackableType.FeaturePoint)) return false;
            Vector3 eye = cam.transform.position;
            for (int i = 0; i < Hits.Count; i++)
            {
                var h = Hits[i].pose;
                if (h.position.y > eye.y - 0.15f) continue;                       // not above or beside the phone
                float d = Vector3.Distance(eye, h.position);
                if (d < 0.3f || d > 4f) continue;
                if (Vector3.Dot(h.up, Vector3.up) < 0.7f) continue;               // facing roughly up
                pose = h;
                return true;
            }
            return false;
        }

        static ARRaycastManager _sharedRaycast;

        // ------------------------------------------------------------------ the ring that shows where the arena will go
        void ShowReticle()
        {
            if (_reticle == null) BuildReticle();
            if (_reticle == null) return;
            _reticle.gameObject.SetActive(_hasAim);
            if (!_hasAim) return;

            _reticle.position = _aim.position + Vector3.up * 0.004f;
            _reticle.rotation = Quaternion.Euler(0f, Time.time * 40f, 0f);
            var mat = FoundQuality == Quality.Surface ? _reticleGood : _reticleRough;
            foreach (var r in _reticleRends) if (r != null && r.sharedMaterial != mat) r.sharedMaterial = mat;
        }

        void BuildReticle()
        {
            _reticleGood = ReticleMaterial(new Color(0.25f, 1f, 0.45f, 1f));
            _reticleRough = ReticleMaterial(new Color(1f, 0.82f, 0.2f, 1f));
            if (_reticleGood == null || _reticleRough == null) return;   // no usable material: no ring, the hint text still works

            var root = new GameObject("PlacementReticle");
            _reticle = root.transform;
            var list = new List<Renderer>();
            const int beads = 20;
            const float R = 0.28f;
            for (int i = 0; i < beads; i++)
            {
                float a = i * Mathf.PI * 2f / beads;
                list.Add(Bead(_reticle, new Vector3(Mathf.Cos(a) * R, 0f, Mathf.Sin(a) * R), i % 2 == 0 ? 0.024f : 0.014f));
            }
            list.Add(Bead(_reticle, Vector3.zero, 0.03f));
            _reticleRends = list.ToArray();
            root.SetActive(false);
        }

        Renderer Bead(Transform parent, Vector3 localPos, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size, size * 0.5f, size);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _reticleGood;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static Material ReticleMaterial(Color c)
        {
            var m = StageMaterials.Make(c);
            if (m == null) return null;
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.6f);
            return m;
        }

        void HideReticle()
        {
            if (_reticle != null) Destroy(_reticle.gameObject);
            _reticle = null;
            _reticleRends = null;
            if (_reticleGood != null) Destroy(_reticleGood);
            if (_reticleRough != null) Destroy(_reticleRough);
            _reticleGood = _reticleRough = null;
            _hadAim = false;
        }

        // The game left the "placing" step without placing (back to the title, say): do not leave the ring hanging in the air.
        void LateUpdate()
        {
            if (_reticle != null && Time.frameCount - _lastPollFrame > 1) HideReticle();
        }

        /// <summary>Fallback when no plane is detected: place the stage ahead of and below the camera.</summary>
        public void PlaceInFrontOfCamera()
        {
            if (HasPlacement) return;
            var cam = Camera.main;
            if (cam == null) return;

            var fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();

            var pos = cam.transform.position + fwd * inFrontDistance + Vector3.down * inFrontDrop;
            SetPlacement(new Pose(pos, Quaternion.LookRotation(fwd, Vector3.up)), null);
        }

        void SetPlacement(Pose pose, ARPlane plane)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var fwd = cam.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 1e-5f) pose.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }

            ARAnchor anchor = null;
            if (_anchors != null)
            {
                if (plane != null) anchor = _anchors.AttachAnchor(plane, pose);
                if (anchor == null)
                {
                    var go = new GameObject("StageAnchor");
                    go.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    anchor = go.AddComponent<ARAnchor>();
                }
                _anchor = anchor;
                _anchorRoot = anchor.transform;
            }
            else
            {
                var go = new GameObject("StageAnchor");
                go.transform.SetPositionAndRotation(pose.position, pose.rotation);
                _anchorRoot = go.transform;
            }

            PlacedOnSurface = plane != null;
            HasPlacement = true;
            if (hidePlanesAfterPlacement) StopAndHidePlanes();
        }

        /// <summary>The spot on the table where the stage anchor was placed (world space).</summary>
        public Vector3 StageOrigin { get { return _anchorRoot != null ? _anchorRoot.position : Vector3.zero; } }

        /// <summary>
        /// Where a screen point lands on the table (the horizontal plane through the stage anchor).
        /// Works on a phone and in the editor. Points farther than maxRadius from the anchor are pulled in.
        /// </summary>
        public bool TryGroundPoint(Vector2 screenPos, out Vector3 point, float maxRadius = 1.2f)
        {
            point = default;
            if (_anchorRoot == null) return false;
            var cam = Camera.main;
            if (cam == null) return false;

            var plane = new Plane(_anchorRoot.up, _anchorRoot.position);
            var ray = cam.ScreenPointToRay(screenPos);
            float enter;
            if (!plane.Raycast(ray, out enter)) return false;

            point = ray.GetPoint(enter);
            Vector3 off = point - _anchorRoot.position;
            if (off.magnitude > maxRadius) point = _anchorRoot.position + off.normalized * maxRadius;
            return true;
        }

        /// <summary>A prefab placed on the table next to the current stage (the stage is NOT replaced). Anchored like the stage.</summary>
        public GameObject InstantiateAt(GameObject prefab, Vector3 worldPos, Quaternion worldRot)
        {
            if (_anchorRoot == null || prefab == null) return null;
            var go = Instantiate(prefab, _anchorRoot);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            return go;
        }

        /// <summary>An empty stage you build content into.</summary>
        public GameObject CreateStage(string stageName)
        {
            ClearStage();
            if (_anchorRoot == null) return null;
            var go = new GameObject(stageName);
            go.transform.SetParent(_anchorRoot, false);
            CurrentStage = go;
            return go;
        }

        /// <summary>A prefab as the stage (used for the arena).</summary>
        public GameObject InstantiateStage(GameObject prefab)
        {
            ClearStage();
            if (_anchorRoot == null || prefab == null) return null;
            var go = Instantiate(prefab, _anchorRoot);   // parented BEFORE its Awake runs, so spawn points are in the right place
            CurrentStage = go;
            return go;
        }

        public void ClearStage()
        {
            if (CurrentStage == null) return;
            CurrentStage.SetActive(false);   // gone for FindObjects* calls immediately; Destroy only happens at end of frame
            Destroy(CurrentStage);
            CurrentStage = null;
        }

        void StopAndHidePlanes()
        {
            if (_planes == null) return;
            _planes.requestedDetectionMode = PlaneDetectionMode.None;
            foreach (var p in _planes.trackables) p.gameObject.SetActive(false);
        }

        /// <summary>A touch/click that started this frame.</summary>
        public static bool TryGetTap(out Vector2 pos)
        {
            pos = default;

            var ts = Touchscreen.current;
            if (ts != null)
            {
                var touch = ts.primaryTouch;
                if (touch.press.wasPressedThisFrame) { pos = touch.position.ReadValue(); return true; }
                return false;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                pos = mouse.position.ReadValue();
                return true;
            }
            return false;
        }
    }
}
