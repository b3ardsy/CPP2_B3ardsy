using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float turnSpeed = 12f;

    [Header("Lock-On Movement")]
    [SerializeField] private float lockOnMoveSpeed = 4f;
    [SerializeField] private float lockOnTurnSpeed = 15f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 6f;

    [Tooltip(
        "Briefly ignores ground detection after jumping so slope movement " +
        "cannot immediately overwrite the jump."
    )]
    [SerializeField] private float jumpGroundIgnoreDuration = 0.15f;

    [Header("Slope Movement")]
    [Tooltip("The steepest slope that the player can move along normally.")]
    [Range(0f, 89f)]
    [SerializeField] private float maxSlopeAngle = 45f;

    [Tooltip(
        "Small downward acceleration used to keep the player attached " +
        "to rolling terrain."
    )]
    [SerializeField] private float groundStickForce = 20f;

    [Tooltip(
        "How far beneath the GroundCheck the slope probe looks."
    )]
    [SerializeField] private float slopeProbeDistance = 0.4f;

    [Tooltip(
        "Raises the slope probe slightly so it does not begin inside " +
        "the terrain."
    )]
    [SerializeField] private float slopeProbeStartOffset = 0.1f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerLockOn playerLockOn;
    [SerializeField] private PlayerDodge playerDodge;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody rb;
    private Animator animator;

    private Vector2 movementInput;
    private Vector3 moveDirection;

    private bool jumpPressed;
    private bool isGrounded;
    private bool wasGrounded;
    private bool isRunning;

    // Slope information from the terrain beneath the player.
    private RaycastHit slopeHit;
    private Vector3 groundNormal = Vector3.up;
    private float slopeAngle;
    private bool hasSlopeHit;
    private bool isOnWalkableSlope;

    // Prevents the ground check from immediately re-grounding the player.
    private float lastJumpTime =
        float.NegativeInfinity;

    // Stores the movement speed used when the player leaves the ground.
    private float airborneSpeed;

    private static readonly int SpeedFloat =
        Animator.StringToHash("Speed");

    private static readonly int IsRunningBool =
        Animator.StringToHash("IsRunning");

    private static readonly int IsGroundedBool =
        Animator.StringToHash("IsGrounded");

    private static readonly int JumpTrigger =
        Animator.StringToHash("Jump");

    private static readonly int LandTrigger =
        Animator.StringToHash("Land");

    private static readonly int IsLockedOnBool =
        Animator.StringToHash("IsLockedOn");

    private static readonly int LockOnHorizontalFloat =
        Animator.StringToHash("LockOnHorizontal");

    private static readonly int LockOnVerticalFloat =
        Animator.StringToHash("LockOnVertical");

    private readonly HashSet<Object> movementLocks =
        new HashSet<Object>();

    public bool IsMovementLocked =>
        movementLocks.Count > 0;

    public void AddMovementLock(Object lockSource)
    {
        if (lockSource == null)
        {
            return;
        }

        movementLocks.Add(lockSource);

        Debug.Log(
            $"Movement Locks: {movementLocks.Count}"
        );

        movementInput = Vector2.zero;
        moveDirection = Vector3.zero;
        isRunning = false;
        jumpPressed = false;
    }

    public void RemoveMovementLock(Object lockSource)
    {
        movementLocks.Remove(lockSource);

        Debug.Log(
            $"Movement Locks: {movementLocks.Count}"
        );
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (playerCombat == null)
        {
            playerCombat =
                GetComponent<PlayerCombat>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<PlayerLockOn>();
        }

        if (playerDodge == null)
        {
            playerDodge =
                GetComponent<PlayerDodge>();
        }

        rb.freezeRotation = true;

        if (
            cameraTransform == null &&
            Camera.main != null
        )
        {
            cameraTransform =
                Camera.main.transform;
        }

        if (cameraTransform == null)
        {
            Debug.LogError(
                "PlayerMovement3D: Camera Transform reference is missing."
            );

            enabled = false;
            return;
        }

        if (groundCheck == null)
        {
            Debug.LogWarning(
                "PlayerMovement3D: GroundCheck reference is missing."
            );
        }

        if (playerDodge == null)
        {
            Debug.LogWarning(
                "PlayerMovement3D: PlayerDodge was not found. " +
                "Locked-on dodging will be unavailable."
            );
        }

        airborneSpeed = walkSpeed;
    }

    private void Update()
    {
        CheckGrounded();

        if (Keyboard.current == null)
        {
            return;
        }

        bool isAttacking =
            playerCombat != null &&
            playerCombat.IsAttacking;

        bool isDodging =
            playerDodge != null &&
            playerDodge.IsDodging;

        /*
         * Keep the current directional input available before
         * starting a dodge. This determines which dodge animation
         * and movement direction PlayerDodge will select.
         */
        ReadMovementInput();

        if (IsMovementLocked)
        {
            movementInput = Vector2.zero;
            moveDirection = Vector3.zero;
            isRunning = false;
            jumpPressed = false;

            UpdateAnimator();
            HandleCursorUnlock();

            return;
        }

        if (isDodging)
        {
            moveDirection = Vector3.zero;
            isRunning = false;
            jumpPressed = false;

            UpdateAnimator();
            HandleCursorUnlock();

            return;
        }

        if (isAttacking)
        {
            movementInput = Vector2.zero;
            moveDirection = Vector3.zero;
            isRunning = false;
            jumpPressed = false;

            UpdateAnimator();
            HandleCursorUnlock();

            return;
        }

        CalculateMoveDirection();
        UpdateRunningState();
        HandleJumpOrDodge();

        UpdateAnimator();
        HandleCursorUnlock();
    }

    private void ReadMovementInput()
    {
        movementInput =
            Vector2.zero;

        if (Keyboard.current.aKey.isPressed)
        {
            movementInput.x -= 1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            movementInput.x += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            movementInput.y -= 1f;
        }

        if (Keyboard.current.wKey.isPressed)
        {
            movementInput.y += 1f;
        }

        movementInput =
            Vector2.ClampMagnitude(
                movementInput,
                1f
            );
    }

    private void CalculateMoveDirection()
    {
        if (
            playerLockOn != null &&
            playerLockOn.IsLockedOn
        )
        {
            CalculateLockOnMovement();
            return;
        }

        CalculateNormalMovement();
    }

    private void CalculateNormalMovement()
    {
        Vector3 camForward =
            cameraTransform.forward;

        Vector3 camRight =
            cameraTransform.right;

        camForward.y = 0f;
        camRight.y = 0f;

        camForward.Normalize();
        camRight.Normalize();

        moveDirection =
            (
                camForward * movementInput.y +
                camRight * movementInput.x
            ).normalized;
    }

    private void CalculateLockOnMovement()
    {
        if (
            playerLockOn == null ||
            !playerLockOn.IsLockedOn
        )
        {
            moveDirection =
                Vector3.zero;

            return;
        }

        Vector3 directionToTarget =
            playerLockOn.CurrentTargetPosition -
            transform.position;

        directionToTarget.y = 0f;

        if (
            directionToTarget.sqrMagnitude <=
            0.001f
        )
        {
            moveDirection =
                Vector3.zero;

            return;
        }

        directionToTarget.Normalize();

        Vector3 targetRight =
            Vector3.Cross(
                Vector3.up,
                directionToTarget
            ).normalized;

        moveDirection =
            (
                directionToTarget *
                movementInput.y +

                targetRight *
                movementInput.x
            ).normalized;
    }

    private void UpdateRunningState()
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        if (isLockedOn)
        {
            // Normal running is disabled during lock-on.
            isRunning = false;
            return;
        }

        if (isGrounded)
        {
            isRunning =
                Keyboard.current
                    .leftShiftKey
                    .isPressed &&

                moveDirection.sqrMagnitude >
                0.01f;
        }
    }

    private void HandleJumpOrDodge()
    {
        if (
            !Keyboard.current
                .spaceKey
                .wasPressedThisFrame
        )
        {
            return;
        }

        if (!isGrounded)
        {
            return;
        }

        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        if (isLockedOn)
        {
            if (playerDodge != null)
            {
                playerDodge.TryDodge(
                    movementInput
                );
            }

            return;
        }

        airborneSpeed =
            isRunning
                ? runSpeed
                : walkSpeed;

        jumpPressed = true;

        animator.SetTrigger(
            JumpTrigger
        );
    }

    private void FixedUpdate()
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        bool isAttacking =
            playerCombat != null &&
            playerCombat.IsAttacking;

        bool isDodging =
            playerDodge != null &&
            playerDodge.IsDodging;

        // Movement locks must override dodging and attacking.
        if (IsMovementLocked)
        {
            StopHorizontalMovement();

            if (isLockedOn)
            {
                RotateTowardLockOnTarget();
            }

            return;
        }

        if (isDodging)
        {
            playerDodge.ApplyDodgeMovement();

            if (isLockedOn)
            {
                RotateTowardLockOnTarget();
            }

            return;
        }

        if (isAttacking)
        {
            StopHorizontalMovement();

            if (isLockedOn)
            {
                RotateTowardLockOnTarget();
            }

            return;
        }

        float currentSpeed;

        if (isGrounded)
        {
            if (isLockedOn)
            {
                currentSpeed =
                    lockOnMoveSpeed;
            }
            else
            {
                currentSpeed =
                    isRunning
                        ? runSpeed
                        : walkSpeed;
            }
        }
        else
        {
            currentSpeed =
                airborneSpeed;
        }

        Vector3 movementDirection =
            moveDirection;

        bool movingOnSlope =
            isGrounded &&
            isOnWalkableSlope &&
            moveDirection.sqrMagnitude > 0.01f;

        if (movingOnSlope)
        {
            Vector3 projectedDirection =
                Vector3.ProjectOnPlane(
                    moveDirection,
                    groundNormal
                ).normalized;

            /*
             * Downhill movement keeps the projected negative Y
             * component so the Rigidbody follows the terrain.
             *
             * Uphill movement uses only the horizontal portion.
             * This prevents the hill from becoming a launch ramp.
             */
            if (projectedDirection.y <= 0f)
            {
                movementDirection =
                    projectedDirection;
            }
            else
            {
                Vector3 horizontalSlopeDirection =
                    new Vector3(
                        projectedDirection.x,
                        0f,
                        projectedDirection.z
                    );

                if (
                    horizontalSlopeDirection.sqrMagnitude >
                    0.001f
                )
                {
                    movementDirection =
                        horizontalSlopeDirection.normalized;
                }
                else
                {
                    movementDirection =
                        Vector3.zero;
                }
            }
        }

        Vector3 velocity =
            movementDirection *
            currentSpeed;

        if (
            movingOnSlope &&
            movementDirection.y < 0f
        )
        {
            /*
             * Use the projected vertical velocity only while moving
             * downhill.
             */
            rb.linearVelocity =
                velocity;
        }
        else
        {
            /*
             * Uphill and flat movement retain the Rigidbody's
             * existing vertical velocity. No positive slope velocity
             * is injected.
             */
            rb.linearVelocity =
                new Vector3(
                    velocity.x,
                    rb.linearVelocity.y,
                    velocity.z
                );
        }

        /*
         * Keep the capsule pressed against the terrain while grounded.
         * Do not apply this during the physics frame containing a jump.
         */
        if (
            isGrounded &&
            !jumpPressed
        )
        {
            rb.AddForce(
                Vector3.down *
                groundStickForce,
                ForceMode.Acceleration
            );
        }

        if (isLockedOn)
        {
            RotateTowardLockOnTarget();
        }
        else
        {
            RotateTowardMovementDirection();
        }

        if (jumpPressed)
        {
            rb.linearVelocity =
                new Vector3(
                    rb.linearVelocity.x,
                    0f,
                    rb.linearVelocity.z
                );

            rb.AddForce(
                Vector3.up *
                jumpForce,
                ForceMode.Impulse
            );

            lastJumpTime =
                Time.time;

            jumpPressed = false;
            isGrounded = false;
            hasSlopeHit = false;
            isOnWalkableSlope = false;
            groundNormal = Vector3.up;
        }
    }

    private void RotateTowardMovementDirection()
    {
        if (
            moveDirection.sqrMagnitude <=
            0.01f
        )
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                moveDirection
            );

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                turnSpeed *
                Time.fixedDeltaTime
            )
        );
    }

    private void RotateTowardLockOnTarget()
    {
        if (
            playerLockOn == null ||
            !playerLockOn.IsLockedOn
        )
        {
            return;
        }

        Vector3 directionToTarget =
            playerLockOn.CurrentTargetPosition -
            transform.position;

        directionToTarget.y = 0f;

        if (
            directionToTarget.sqrMagnitude <=
            0.001f
        )
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                directionToTarget.normalized
            );

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                lockOnTurnSpeed *
                Time.fixedDeltaTime
            )
        );
    }

    private void StopHorizontalMovement()
    {
        rb.linearVelocity =
            new Vector3(
                0f,
                rb.linearVelocity.y,
                0f
            );
    }

    private void CheckGrounded()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            hasSlopeHit = false;
            isOnWalkableSlope = false;
            groundNormal = Vector3.up;

            return;
        }

        bool isIgnoringGround =
            Time.time <
            lastJumpTime +
            jumpGroundIgnoreDuration;

        if (isIgnoringGround)
        {
            isGrounded = false;
            hasSlopeHit = false;
            isOnWalkableSlope = false;
            groundNormal = Vector3.up;
            slopeAngle = 0f;

            return;
        }

        /*
         * Keep the original grounded check so the animator and jump
         * behaviour remain largely unchanged.
         */
        isGrounded =
            Physics.CheckSphere(
                groundCheck.position,
                groundCheckRadius,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

        /*
         * This SphereCast gathers terrain-angle information without
         * replacing the original grounded check.
         */
        Vector3 probeOrigin =
            groundCheck.position +
            Vector3.up *
            slopeProbeStartOffset;

        float probeDistance =
            slopeProbeDistance +
            slopeProbeStartOffset;

        hasSlopeHit =
            Physics.SphereCast(
                probeOrigin,
                groundCheckRadius * 0.9f,
                Vector3.down,
                out slopeHit,
                probeDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

        if (hasSlopeHit)
        {
            groundNormal =
                slopeHit.normal;

            slopeAngle =
                Vector3.Angle(
                    Vector3.up,
                    groundNormal
                );

            isOnWalkableSlope =
                slopeAngle <=
                maxSlopeAngle;
        }
        else
        {
            groundNormal =
                Vector3.up;

            slopeAngle = 0f;
            isOnWalkableSlope = false;
        }
    }

    private void UpdateAnimator()
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        bool isDodging =
            playerDodge != null &&
            playerDodge.IsDodging;

        float speedValue =
            isGrounded &&
            !isDodging
                ? moveDirection.magnitude
                : 0f;

        animator.SetFloat(
            SpeedFloat,
            speedValue
        );

        animator.SetBool(
            IsRunningBool,
            isRunning &&
            isGrounded &&
            !isDodging
        );

        animator.SetBool(
            IsGroundedBool,
            isGrounded
        );

        animator.SetBool(
            IsLockedOnBool,
            isLockedOn
        );

        float lockOnHorizontal =
            isLockedOn &&
            isGrounded &&
            !isDodging
                ? movementInput.x
                : 0f;

        float lockOnVertical =
            isLockedOn &&
            isGrounded &&
            !isDodging
                ? movementInput.y
                : 0f;

        animator.SetFloat(
            LockOnHorizontalFloat,
            lockOnHorizontal,
            0.1f,
            Time.deltaTime
        );

        animator.SetFloat(
            LockOnVerticalFloat,
            lockOnVertical,
            0.1f,
            Time.deltaTime
        );

        if (
            !wasGrounded &&
            isGrounded
        )
        {
            animator.SetTrigger(
                LandTrigger
            );
        }

        wasGrounded =
            isGrounded;
    }

    private void HandleCursorUnlock()
    {
        if (
            Keyboard.current
                .escapeKey
                .wasPressedThisFrame
        )
        {
            Cursor.lockState =
                CursorLockMode.None;

            Cursor.visible = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        // Original grounded-check sphere.
        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );

        // Additional slope-information probe.
        Vector3 probeOrigin =
            groundCheck.position +
            Vector3.up *
            slopeProbeStartOffset;

        float probeDistance =
            slopeProbeDistance +
            slopeProbeStartOffset;

        Vector3 probeEnd =
            probeOrigin +
            Vector3.down *
            probeDistance;

        Gizmos.DrawWireSphere(
            probeOrigin,
            groundCheckRadius * 0.9f
        );

        Gizmos.DrawLine(
            probeOrigin,
            probeEnd
        );

        Gizmos.DrawWireSphere(
            probeEnd,
            groundCheckRadius * 0.9f
        );

        if (hasSlopeHit)
        {
            Gizmos.DrawLine(
                slopeHit.point,
                slopeHit.point +
                slopeHit.normal
            );
        }
    }
}