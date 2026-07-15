using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerLockOn playerLockOn;

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

    [Header("Lock-On Camera")]
    [SerializeField] private float lockOnTargetHeight = 1f;
    [SerializeField] private float lockOnRotationSmoothTime = 0.12f;

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

    private float yawSmoothVelocity;
    private float pitchSmoothVelocity;

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

        if (playerLockOn == null)
        {
            playerLockOn =
                player.GetComponent<PlayerLockOn>();
        }

        if (playerLockOn == null)
        {
            Debug.LogWarning(
                "ThirdPersonCamera: PlayerLockOn was not found. " +
                "The camera will continue to use free-look only."
            );
        }

        defaultCameraLocalPosition =
            cameraTransform.localPosition;

        currentZoomDistance =
            Mathf.Abs(defaultCameraLocalPosition.z);

        targetZoomDistance =
            currentZoomDistance;

        pitch = startingPitch;
        yaw =
            player.eulerAngles.y +
            startingYawOffset;

        ApplyCameraPosition();
    }

    private void Start()
    {
        LockCursor();
    }

    private void LateUpdate()
    {
        if (Mouse.current == null)
        {
            return;
        }

        HandleCursor();

        if (framesToIgnore > 0)
        {
            framesToIgnore--;

            ApplyCameraPosition();
            return;
        }

        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        if (isLockedOn)
        {
            UpdateLockOnRotation();
        }
        else
        {
            UpdateFreeLookRotation();
        }

        UpdateZoom();
        ApplyCameraPosition();
    }

    private void UpdateFreeLookRotation()
    {
        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        yaw +=
            mouseDelta.x *
            mouseSensitivityX;

        pitch -=
            mouseDelta.y *
            mouseSensitivityY;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );

        // Clear the lock-on smoothing velocity so the camera
        // responds immediately when free-look resumes.
        yawSmoothVelocity = 0f;
        pitchSmoothVelocity = 0f;
    }

    private void UpdateLockOnRotation()
    {
        Transform target =
            playerLockOn.CurrentTargetTransform;

        if (target == null)
        {
            return;
        }

        Vector3 pivotPosition =
            player.position +
            Vector3.up * heightOffset;

        Vector3 targetPosition =
            target.position +
            Vector3.up * lockOnTargetHeight;

        Vector3 directionToTarget =
            targetPosition - pivotPosition;

        if (directionToTarget.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float targetYaw =
            Mathf.Atan2(
                directionToTarget.x,
                directionToTarget.z
            ) * Mathf.Rad2Deg;

        float horizontalDistance =
            new Vector2(
                directionToTarget.x,
                directionToTarget.z
            ).magnitude;

        float targetPitch =
            -Mathf.Atan2(
                directionToTarget.y,
                horizontalDistance
            ) * Mathf.Rad2Deg;

        targetPitch = Mathf.Clamp(
            targetPitch,
            minPitch,
            maxPitch
        );

        yaw = Mathf.SmoothDampAngle(
            yaw,
            targetYaw,
            ref yawSmoothVelocity,
            lockOnRotationSmoothTime
        );

        pitch = Mathf.SmoothDampAngle(
            pitch,
            targetPitch,
            ref pitchSmoothVelocity,
            lockOnRotationSmoothTime
        );
    }

    private void UpdateZoom()
    {
        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoomDistance -=
                scroll * zoomSpeed;

            targetZoomDistance = Mathf.Clamp(
                targetZoomDistance,
                minZoomDistance,
                maxZoomDistance
            );
        }

        currentZoomDistance =
            Mathf.Lerp(
                currentZoomDistance,
                targetZoomDistance,
                Time.deltaTime *
                zoomSmoothSpeed
            );

        defaultCameraLocalPosition.z =
            -currentZoomDistance;
    }

    private void ApplyCameraPosition()
    {
        transform.position =
            player.position +
            Vector3.up * heightOffset;

        transform.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );

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
            desiredCameraPosition -
            pivotPosition;

        float desiredDistance =
            direction.magnitude;

        if (desiredDistance <= 0.01f)
        {
            return;
        }

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
                hit.distance -
                collisionBuffer;

            correctedDistance = Mathf.Clamp(
                correctedDistance,
                minimumCameraDistance,
                desiredDistance
            );
        }

        cameraTransform.position =
            pivotPosition +
            direction *
            correctedDistance;

        cameraTransform.rotation =
            transform.rotation;
    }

    private void HandleCursor()
    {
        if (
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame
        )
        {
            UnlockCursor();
        }

        if (
            Mouse.current.leftButton.wasPressedThisFrame
        )
        {
            LockCursor();
        }
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