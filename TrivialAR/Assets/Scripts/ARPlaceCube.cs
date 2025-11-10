using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

public class ARPlaceCube : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    private bool isPlacing = false;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        if (!raycastManager) return;
        if (isPlacing) return;

        bool pressed = false;
        Vector2 screenPosition = default;
            
            if(Touchscreen.current != null)
            {
                var primary = Touchscreen.current.primaryTouch;
                if (primary.press.wasPressedThisFrame)
                {
                    pressed = true;
                    screenPosition = primary.position.ReadValue();
                }
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                pressed = true;
                screenPosition = Mouse.current.position.ReadValue();
            }

            if (pressed)
            {
                isPlacing = true;
                PlaceObject(screenPosition);
            }
    }

    void PlaceObject(Vector2 touchPosition)
    {
        var rayHits = new List<ARRaycastHit>();
        raycastManager.Raycast(touchPosition, rayHits, TrackableType.AllTypes);

        if (rayHits.Count > 0)
        {
            Vector3 hitPosePosition = rayHits[0].pose.position;
            Quaternion hitPoseRotation = rayHits[0].pose.rotation;
            Instantiate(raycastManager.raycastPrefab, hitPosePosition, hitPoseRotation);
        }

        StartCoroutine(SetIsPlacingToFalseWithDelay());
    }

    IEnumerator SetIsPlacingToFalseWithDelay()
    {
        yield return new WaitForSeconds(0.25f);
        isPlacing = false;
    }
}
