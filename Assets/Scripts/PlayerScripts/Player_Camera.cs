using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Camera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Player_LockOn playerLockOn;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivityX = 0.35f;
    [SerializeField] private float mouseSensitivityY = 0.35f;

    [Header("Pitch Limits")]
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 55f;

    [Header("Starting View")]
    [SerializeField] private float startingPitch = 12f;
    [SerializeField] private float startingYawOffset = 0f;

    [Tooltip(
        "Height of the free-look camera pivot above " +
        "the player's root position."
    )]
    [SerializeField] private float heightOffset = 1.8f;

    [Tooltip(
        "Initial distance of the camera from the player."
    )]
    [SerializeField] private float startingZoomDistance = 6f;

    [Header("Lock-On Camera")]
    [SerializeField] private float lockOnHeightOffset = 2.5f;
    [SerializeField] private float lockOnRotationSmoothTime = 0.12f;

    [Header("Shoulder Camera")]
    [SerializeField] private float shoulderOffset = 0.65f;
    [SerializeField] private float shoulderSwitchSpeed = 8f;
    [SerializeField] private bool startOnRightShoulder = true;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 0.02f;
    [SerializeField] private float zoomSmoothSpeed = 10f;

    [SerializeField] private float minZoomDistance = 3.5f;
    [SerializeField] private float maxZoomDistance = 9f;

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

    private float currentShoulderOffset;
    private float targetShoulderOffset;

    private bool isRightShoulder;

    /*
     * Ignore the first few frames to prevent
     * an initial mouse jump.
     */
    private int framesToIgnore = 5;

    private void Awake()
    {
        ValidateReferences();

        if (!enabled)
        {
            return;
        }

        /*
         * Preserve the Main Camera's existing local Y position,
         * but explicitly control horizontal shoulder offset
         * and Z distance from this script.
         */
        defaultCameraLocalPosition =
            cameraTransform.localPosition;

        startingZoomDistance =
            Mathf.Clamp(
                startingZoomDistance,
                minZoomDistance,
                maxZoomDistance
            );

        currentZoomDistance =
            startingZoomDistance;

        targetZoomDistance =
            startingZoomDistance;

        isRightShoulder =
            startOnRightShoulder;

        targetShoulderOffset =
            isRightShoulder
                ? shoulderOffset
                : -shoulderOffset;

        currentShoulderOffset =
            targetShoulderOffset;

        defaultCameraLocalPosition.x =
            currentShoulderOffset;

        defaultCameraLocalPosition.z =
            -currentZoomDistance;

        pitch =
            Mathf.Clamp(
                startingPitch,
                minPitch,
                maxPitch
            );

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
        HandleShoulderSwitch();

        if (framesToIgnore > 0)
        {
            framesToIgnore--;

            UpdateShoulderPosition();
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
        UpdateShoulderPosition();
        ApplyCameraPosition();
    }

    // =========================================================
    // FREE LOOK
    // =========================================================

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

        pitch =
            Mathf.Clamp(
                pitch,
                minPitch,
                maxPitch
            );

        /*
         * Clear lock-on smoothing so free-look
         * resumes immediately.
         */
        yawSmoothVelocity =
            0f;

        pitchSmoothVelocity =
            0f;
    }

    // =========================================================
    // LOCK-ON
    // =========================================================

    private void UpdateLockOnRotation()
    {
        if (
            playerLockOn == null ||
            !playerLockOn.IsLockedOn
        )
        {
            return;
        }

        Vector3 pivotPosition =
            player.position +
            Vector3.up *
            lockOnHeightOffset;

        Vector3 targetPosition =
            playerLockOn.CurrentTargetPosition;

        Vector3 directionToTarget =
            targetPosition -
            pivotPosition;

        if (
            directionToTarget.sqrMagnitude <=
            0.001f
        )
        {
            return;
        }

        float targetYaw =
            Mathf.Atan2(
                directionToTarget.x,
                directionToTarget.z
            ) *
            Mathf.Rad2Deg;

        float horizontalDistance =
            new Vector2(
                directionToTarget.x,
                directionToTarget.z
            ).magnitude;

        float targetPitch =
            -Mathf.Atan2(
                directionToTarget.y,
                horizontalDistance
            ) *
            Mathf.Rad2Deg;

        targetPitch =
            Mathf.Clamp(
                targetPitch,
                minPitch,
                maxPitch
            );

        yaw =
            Mathf.SmoothDampAngle(
                yaw,
                targetYaw,
                ref yawSmoothVelocity,
                lockOnRotationSmoothTime
            );

        pitch =
            Mathf.SmoothDampAngle(
                pitch,
                targetPitch,
                ref pitchSmoothVelocity,
                lockOnRotationSmoothTime
            );
    }

    // =========================================================
    // SHOULDER
    // =========================================================

    private void HandleShoulderSwitch()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (
            !Keyboard.current.rKey
                .wasPressedThisFrame
        )
        {
            return;
        }

        isRightShoulder =
            !isRightShoulder;

        targetShoulderOffset =
            isRightShoulder
                ? shoulderOffset
                : -shoulderOffset;
    }

    private void UpdateShoulderPosition()
    {
        currentShoulderOffset =
            Mathf.Lerp(
                currentShoulderOffset,
                targetShoulderOffset,
                Time.deltaTime *
                shoulderSwitchSpeed
            );

        defaultCameraLocalPosition.x =
            currentShoulderOffset;
    }

    // =========================================================
    // ZOOM
    // =========================================================

    private void UpdateZoom()
    {
        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (
            Mathf.Abs(scroll) >
            0.01f
        )
        {
            targetZoomDistance -=
                scroll *
                zoomSpeed;

            targetZoomDistance =
                Mathf.Clamp(
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

    // =========================================================
    // CAMERA POSITION
    // =========================================================

    private void ApplyCameraPosition()
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        float activeHeightOffset =
            isLockedOn
                ? lockOnHeightOffset
                : heightOffset;

        transform.position =
            player.position +
            Vector3.up *
            activeHeightOffset;

        transform.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );

        HandleCameraCollision();
    }

    // =========================================================
    // COLLISION
    // =========================================================

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

        if (
            desiredDistance <=
            0.01f
        )
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

            correctedDistance =
                Mathf.Clamp(
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

    // =========================================================
    // CURSOR
    // =========================================================

    private void HandleCursor()
    {
        if (
            Keyboard.current != null &&
            Keyboard.current.escapeKey
                .wasPressedThisFrame
        )
        {
            UnlockCursor();
        }

        if (
            Mouse.current.leftButton
                .wasPressedThisFrame
        )
        {
            LockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible =
            false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void ValidateReferences()
    {
        if (player == null)
        {
            Debug.LogError(
                "ThirdPersonCamera: Player reference is missing.",
                this
            );

            enabled = false;
            return;
        }

        if (cameraTransform == null)
        {
            Debug.LogError(
                "ThirdPersonCamera: Camera Transform reference is missing.",
                this
            );

            enabled = false;
            return;
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                player.GetComponent<Player_LockOn>();
        }

        if (playerLockOn == null)
        {
            Debug.LogWarning(
                "Player_Camera: Player_LockOn was not found. " +
                "The camera will continue to use free-look only.",
                this
            );
        }
    }

    private void OnValidate()
    {
        mouseSensitivityX =
            Mathf.Max(
                0f,
                mouseSensitivityX
            );

        mouseSensitivityY =
            Mathf.Max(
                0f,
                mouseSensitivityY
            );

        maxPitch =
            Mathf.Max(
                minPitch,
                maxPitch
            );

        startingPitch =
            Mathf.Clamp(
                startingPitch,
                minPitch,
                maxPitch
            );

        heightOffset =
            Mathf.Max(
                0f,
                heightOffset
            );

        lockOnHeightOffset =
            Mathf.Max(
                0f,
                lockOnHeightOffset
            );

        lockOnRotationSmoothTime =
            Mathf.Max(
                0.01f,
                lockOnRotationSmoothTime
            );

        shoulderOffset =
            Mathf.Max(
                0f,
                shoulderOffset
            );

        shoulderSwitchSpeed =
            Mathf.Max(
                0f,
                shoulderSwitchSpeed
            );

        zoomSpeed =
            Mathf.Max(
                0f,
                zoomSpeed
            );

        zoomSmoothSpeed =
            Mathf.Max(
                0f,
                zoomSmoothSpeed
            );

        minZoomDistance =
            Mathf.Max(
                0.1f,
                minZoomDistance
            );

        maxZoomDistance =
            Mathf.Max(
                minZoomDistance,
                maxZoomDistance
            );

        startingZoomDistance =
            Mathf.Clamp(
                startingZoomDistance,
                minZoomDistance,
                maxZoomDistance
            );

        collisionRadius =
            Mathf.Max(
                0f,
                collisionRadius
            );

        collisionBuffer =
            Mathf.Max(
                0f,
                collisionBuffer
            );

        minimumCameraDistance =
            Mathf.Max(
                0.1f,
                minimumCameraDistance
            );
    }
}