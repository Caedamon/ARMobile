using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace AR
{
    [RequireComponent(typeof(ARTrackedImageManager))]
    public class TrackedImageKaijuSpawner : MonoBehaviour
    {
        private ARTrackedImageManager manager;

        void Awake()
        {
            manager = GetComponent<ARTrackedImageManager>();
        }

        void OnEnable()
        {
            manager.trackedImagesChanged += OnChanged;
        }

        void OnDisable()
        {
            manager.trackedImagesChanged -= OnChanged;
        }

        void OnChanged(ARTrackedImagesChangedEventArgs args)
        {
            // When a new tracked image is detected
            foreach (var added in args.added)
                UpdatePlacement(added);

            // When an existing one moves/updates
            foreach (var updated in args.updated)
                UpdatePlacement(updated);
        }

        void UpdatePlacement(ARTrackedImage trackedImage)
        {
            if (trackedImage.trackingState != UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
                return;

            Transform t = trackedImage.transform;
            // The spawned KaijuArena prefab is already attached by ARTrackedImageManager
            trackedImage.transform.localScale = Vector3.one; // prevent weird scaling
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
        }
    }
}