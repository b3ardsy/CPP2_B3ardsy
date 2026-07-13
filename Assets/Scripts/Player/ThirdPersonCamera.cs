using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform cameraTransform;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivityX = 0.35f;
    [SerializeField] private float mouseSensitivityY = 0.35f;

    [Header("Pitch Limits")]
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 45f;

    [Header("Starting View")]
    [SerializeField] private float startingPitch = 10f;
    [SerializeField] private float startingYawOffset = 0f;
    [SerializeField] private float heightOffset = 1.5f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 0.02f;
    [SerializeField] private float zoomSmoothSpeed = 10f;
    [SerializeField] private float minZoomDistance = 2f;
    [SerializeField] private float maxZoomDistance = 8f;

    [Header("Camera Collision")]
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionBuffer = 0.1f;
    [SerializeField] private float minimumCameraDistance = 0.5f;

    private float yaw;
    private float pitch;

    private Vector3 defaultCameraLocalPosition;

    private float currentZoomDistance;
    private float targetZoomDistance;

    // Ignore the first few frames to prevent an initial mouse jump.
    private int framesToIgnore = 5;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError(
                "ThirdPersonCamera: Player reference is missing."
            );

            enabled = false;
            return;
        }

        if (cameraTransform == null)
        {
            Debug.LogError(
                "ThirdPersonCamera: Camera Transform reference is missing."
            );

            enabled = false;
            return;
        }

        defaultCameraLocalPosition =
            cameraTransform.localPosition;

        currentZoomDistance =
            Mathf.Abs(defaultCameraLocalPosition.z);

        targetZoomDistance = currentZoomDistance;

        pitch = startingPitch;
        yaw = player.eulerAngles.y + startingYawOffset;

        ApplyCameraPosition();
    }

    private void Start()
    {
        LockCursor();
    }

    private void LateUpdate()
    {
        if (Mouse.current == null)
            return;

        // Press Escape to unlock the cursor while testing.
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        // Left-click to lock it again.
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }

        if (framesToIgnore > 0)
        {
            framesToIgnore--;
            ApplyCameraPosition();
            return;
        }

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivityX;
        pitch -= mouseDelta.y * mouseSensitivityY;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );

        // Mouse wheel zoom.
        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoomDistance -=
                scroll * zoomSpeed;

            targetZoomDistance =
                Mathf.Clamp(
                    targetZoomDistance,
                    minZoomDistance,
                    maxZoomDistance
                );
        }

        // Smoothly move toward the target zoom.
        currentZoomDistance =
            Mathf.Lerp(
                currentZoomDistance,
                targetZoomDistance,
                Time.deltaTime * zoomSmoothSpeed
            );

        defaultCameraLocalPosition.z =
            -currentZoomDistance;

        ApplyCameraPosition();
    }

    private void ApplyCameraPosition()
    {
        transform.position =
            player.position + Vector3.up * heightOffset;

        transform.rotation =
            Quaternion.Euler(pitch, yaw, 0f);

        HandleCameraCollision();
    }

    private void HandleCameraCollision()
    {
        Vector3 pivotPosition =
            transform.position;

        Vector3 desiredCameraPosition =
            transform.TransformPoint(
                defaultCameraLocalPosition
            );

        Vector3 direction =
            desiredCameraPosition - pivotPosition;

        float desiredDistance =
            direction.magnitude;

        if (desiredDistance <= 0.01f)
            return;

        direction.Normalize();

        float correctedDistance =
            desiredDistance;

        bool obstructionFound =
            Physics.SphereCast(
                pivotPosition,
                collisionRadius,
                direction,
                out RaycastHit hit,
                desiredDistance,
                collisionLayers,
                QueryTriggerInteraction.Ignore
            );

        if (obstructionFound)
        {
            correctedDistance =
                hit.distance - collisionBuffer;

            correctedDistance = Mathf.Clamp(
                correctedDistance,
                minimumCameraDistance,
                desiredDistance
            );
        }

        cameraTransform.position =
            pivotPosition +
            direction * correctedDistance;

        cameraTransform.rotation =
            transform.rotation;
    }

    private void LockCursor()
    {
        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;
    }
}