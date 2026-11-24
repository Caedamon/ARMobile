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
        public GameObject CurrentStage { get; private set; }

        ARRaycastManager _raycast;
        ARPlaneManager _planes;
        ARAnchorManager _anchors;
        Transform _anchorRoot;

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>(8);

        void Awake() { FindManagers(); }

        void FindManagers()
        {
            if (_raycast == null) _raycast = FindFirstObjectByType<ARRaycastManager>();
            if (_planes == null)  _planes  = FindFirstObjectByType<ARPlaneManager>();
            if (_anchors == null) _anchors = FindFirstObjectByType<ARAnchorManager>();
        }

        /// <summary>Call every frame while waiting for placement. True on the frame the player placed the stage anchor.</summary>
        public bool PollTapPlacement()
        {
            if (HasPlacement) return false;
            FindManagers();
            if (_raycast == null) return false;
            if (!TryGetTap(out var screenPos)) return false;
            if (UiGuard.IsOverUI(screenPos)) return false;
            if (!_raycast.Raycast(screenPos, Hits, TrackableType.PlaneWithinPolygon)) return false;

            var hit = Hits[0];
            SetPlacement(hit.pose, hit.trackable as ARPlane);
            return true;
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
                _anchorRoot = anchor.transform;
            }
            else
            {
                var go = new GameObject("StageAnchor");
                go.transform.SetPositionAndRotation(pose.position, pose.rotation);
                _anchorRoot = go.transform;
            }

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
