using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AR
{
    [DisallowMultipleComponent]
    public class ARPlaceArena : MonoBehaviour
    {
        [Header("Refs (auto if on same GO)")]
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARAnchorManager anchorManager;

        [Header("Content")]
        [SerializeField] private GameObject arenaPrefab;

        [Header("Behavior")]
        [SerializeField] private bool allowReposition = false;
        [SerializeField] private bool hidePlanesAfterPlacement = true;
        [SerializeField] private bool attachAnchor = true;
        [SerializeField] private bool faceCameraOnPlace = true;

        private static readonly List<ARRaycastHit> Hits = new(8);
        private GameObject _spawned;
        private ARAnchor _anchor;

        void Awake()
        {
            if (!raycastManager) raycastManager = GetComponent<ARRaycastManager>();
            if (!planeManager)   planeManager   = GetComponent<ARPlaneManager>();
            if (!anchorManager)  anchorManager  = GetComponent<ARAnchorManager>();
        }

        void Update()
        {
            if (raycastManager == null || arenaPrefab == null) return;
            if (!TryGetTap(out var screenPos)) return;
            if (!raycastManager.Raycast(screenPos, Hits, TrackableType.PlaneWithinPolygon)) return;

            var hit  = Hits[0];
            var pose = hit.pose;

            if (faceCameraOnPlace && Camera.main)
            {
                var fwd = Camera.main.transform.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude > 1e-5f)
                    pose.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }

            if (_spawned == null)
            {
                _spawned = Instantiate(arenaPrefab, pose.position, pose.rotation);

                if (attachAnchor && anchorManager != null)
                {
                    _anchor = CreateAnchor(pose, hit.trackable as ARPlane);
                    if (_anchor) _spawned.transform.SetParent(_anchor.transform, true);
                }

                if (hidePlanesAfterPlacement) StopAndHidePlanes();
            }
            else if (allowReposition)
            {
                if (_anchor != null)
                {
                    _spawned.transform.SetParent(null, true);
                    Destroy(_anchor.gameObject);
                    _anchor = CreateAnchor(pose, hit.trackable as ARPlane);
                    if (_anchor)
                    {
                        _spawned.transform.SetPositionAndRotation(pose.position, pose.rotation);
                        _spawned.transform.SetParent(_anchor.transform, true);
                    }
                    else
                    {
                        _spawned.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    }
                }
                else
                {
                    _spawned.transform.SetPositionAndRotation(pose.position, pose.rotation);
                }
            }
        }

        private ARAnchor CreateAnchor(Pose pose, ARPlane plane)
        {
            if (anchorManager == null) return null;

            if (plane != null)
            {
                var attached = anchorManager.AttachAnchor(plane, pose);
                if (attached) return attached;
            }
            var go = new GameObject("WorldAnchor");
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var worldAnchor = go.AddComponent<ARAnchor>();
            return worldAnchor ? worldAnchor : null;
        }

        private void StopAndHidePlanes()
        {
            if (planeManager == null) return;
            planeManager.requestedDetectionMode = PlaneDetectionMode.None;
            foreach (var p in planeManager.trackables) p.gameObject.SetActive(false);
        }

        private static bool TryGetTap(out Vector2 pos)
        {
            pos = default;

            var ts = Touchscreen.current;
            if (ts != null)
            {
                var touch = ts.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                { pos = touch.position.ReadValue(); return true; }
                return false;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            { pos = mouse.position.ReadValue(); return true; }

            return false;
        }
    }
}