using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class Player_Controller : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 10f;
    [SerializeField] private float runSpeed = 14f;

    [Tooltip(
        "How quickly the character accelerates and decelerates."
    )]
    [SerializeField] private float speedChangeRate = 15f;

    [Tooltip(
        "How quickly the character rotates toward normal movement."
    )]
    [SerializeField] private float turnSpeed = 7f;

    [Header("Lock-On Movement")]
    [SerializeField] private float lockOnMoveSpeed = 8.5f;
    [SerializeField] private float lockOnTurnSpeed = 15f;

    [Header("Jump and Gravity")]
    [Tooltip(
        "Approximate height reached by a normal jump."
    )]
    [SerializeField] private float jumpHeight = 1.5f;

    [Tooltip(
        "Custom downward acceleration."
    )]
    [SerializeField] private float gravity = -25f;

    [Tooltip(
        "Small downward velocity maintained while grounded."
    )]
    [SerializeField] private float groundedVerticalVelocity = -2f;

    [Tooltip(
        "Maximum downward velocity."
    )]
    [SerializeField] private float terminalVelocity = -53f;

    [Header("Ground Check")]
    [Tooltip(
        "Vertical offset from the player's root position."
    )]
    [SerializeField] private float groundedOffset = -0.1f;

    [SerializeField] private float groundedRadius = 0.45f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Idle Waiting")]
    [Tooltip(
        "How long the player must remain completely idle before " +
        "the Waiting animation begins."
    )]
    [SerializeField] private float waitingDelay = 8f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerCombatNew playerCombat;
    [SerializeField] private Player_LockOn playerLockOn;
    [SerializeField] private Player_Dodge playerDodge;

    private CharacterController characterController;
    private Animator animator;

    private Vector2 movementInput;
    private Vector3 moveDirection;

    private float currentMoveSpeed;
    private float verticalVelocity;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isRunning;

    private float waitingTimer;
    private bool isWaiting;

    private readonly HashSet<Object> movementLocks =
        new HashSet<Object>();

    public bool IsMovementLocked =>
        movementLocks.Count > 0;

    public bool IsGrounded =>
        isGrounded;

    public Vector3 CurrentVelocity =>
        characterController != null
            ? characterController.velocity
            : Vector3.zero;

    // Animator parameters.
    private static readonly int SpeedFloat =
        Animator.StringToHash("Speed");

    private static readonly int IsRunningBool =
        Animator.StringToHash("IsRunning");

    private static readonly int IsGroundedBool =
        Animator.StringToHash("IsGrounded");

    private static readonly int VerticalVelocityFloat =
        Animator.StringToHash("VerticalVelocity");

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

    private static readonly int IsWaitingBool =
        Animator.StringToHash("IsWaiting");

    public void AddMovementLock(Object lockSource)
    {
        if (lockSource == null)
        {
            return;
        }

        movementLocks.Add(lockSource);

        ResetWaiting();

        movementInput = Vector2.zero;
        moveDirection = Vector3.zero;
        currentMoveSpeed = 0f;
        isRunning = false;
    }

    public void RemoveMovementLock(Object lockSource)
    {
        if (lockSource == null)
        {
            return;
        }

        movementLocks.Remove(lockSource);
    }

    public void ClearMovementLocks()
    {
        movementLocks.Clear();
    }

    private void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        animator =
            GetComponent<Animator>();

        if (playerCombat == null)
        {
            playerCombat =
                GetComponent<PlayerCombatNew>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<Player_LockOn>();
        }

        if (playerDodge == null)
        {
            playerDodge =
                GetComponent<Player_Dodge>();
        }

        animator.SetBool(
            IsWaitingBool,
            false
        );

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
                $"{name}: Player_Controller requires a Camera Transform.",
                this
            );

            enabled = false;
            return;
        }

        verticalVelocity =
            groundedVerticalVelocity;
    }

    private void Update()
    {
        CheckGrounded();
        ReadMovementInput();

        bool isAttacking =
            playerCombat != null &&
            playerCombat.IsAttacking;

        bool isDodging =
            playerDodge != null &&
            playerDodge.IsDodging;

        bool movementUnavailable =
            IsMovementLocked ||
            isAttacking ||
            isDodging;

        UpdateWaitingState(
            isDodging
        );

        /*
         * Space performs a locked-on dodge or a normal jump.
         */
        HandleJumpOrDodge(
            movementUnavailable
        );

        UpdateGravity();

        if (IsMovementLocked)
        {
            moveDirection =
                Vector3.zero;

            isRunning = false;
        }
        else if (isDodging)
        {
            moveDirection =
                Vector3.zero;

            isRunning = false;
        }
        else
        {
            CalculateMoveDirection();
            UpdateRunningState();
        }

        UpdateMovementSpeed(
            isDodging
        );

        MoveCharacter(
            isDodging
        );

        UpdateRotation(
            isDodging
        );

        UpdateAnimator(
            isDodging
        );

    }

    private void ReadMovementInput()
    {
        movementInput =
            Vector2.zero;

        if (Keyboard.current == null)
        {
            return;
        }

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

    private void UpdateWaitingState(
        bool isDodging
    )
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        bool isCombatBusy =
            playerCombat != null &&
            playerCombat.IsCombatBusy;

        bool hasMovementInput =
            movementInput.sqrMagnitude >
            0.01f;

        bool hasFreshInput =
            HasFreshPlayerInput();

        bool canWait =
            isGrounded &&
            !isLockedOn &&
            !isCombatBusy &&
            !isDodging &&
            !IsMovementLocked &&
            !hasMovementInput;

        if (
            !canWait ||
            hasFreshInput
        )
        {
            ResetWaiting();
            return;
        }

        if (isWaiting)
        {
            return;
        }

        waitingTimer +=
            Time.deltaTime;

        if (
            waitingTimer <
            waitingDelay
        )
        {
            return;
        }

        SetWaiting(
            true
        );
    }

    private bool HasFreshPlayerInput()
    {
        if (
            Keyboard.current != null &&
            Keyboard.current.anyKey.wasPressedThisFrame
        )
        {
            return true;
        }

        if (Mouse.current == null)
        {
            return false;
        }

        return
            Mouse.current.leftButton.wasPressedThisFrame ||
            Mouse.current.rightButton.wasPressedThisFrame ||
            Mouse.current.middleButton.wasPressedThisFrame;
    }

    private void ResetWaiting()
    {
        waitingTimer = 0f;

        if (!isWaiting)
        {
            return;
        }

        SetWaiting(
            false
        );
    }

    private void SetWaiting(
        bool value
    )
    {
        if (
            isWaiting ==
            value
        )
        {
            return;
        }

        isWaiting =
            value;

        if (animator != null)
        {
            animator.SetBool(
                IsWaitingBool,
                isWaiting
            );
        }
    }

    private void HandleJumpOrDodge(
        bool movementUnavailable
    )
    {
        if (Keyboard.current == null)
        {
            return;
        }

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

        if (IsMovementLocked)
        {
            return;
        }

        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        if (isLockedOn)
        {
            if (
                !movementUnavailable &&
                playerDodge != null
            )
            {
                playerDodge.TryDodge(
                    movementInput
                );
            }

            return;
        }

        if (movementUnavailable)
        {
            return;
        }

        ResetWaiting();

        /*
         * v = square root of height × -2 × gravity.
         */
        verticalVelocity =
            Mathf.Sqrt(
                jumpHeight *
                -2f *
                gravity
            );

        isGrounded = false;

        animator.ResetTrigger(
            LandTrigger
        );

        animator.ResetTrigger(
            JumpTrigger
        );

        animator.SetTrigger(
            JumpTrigger
        );
    }

    private void CheckGrounded()
    {
        /*
         * While the player is actively travelling upward,
         * they cannot be grounded.
         *
         * This prevents the ground-check sphere from detecting
         * the terrain for a few frames immediately after jumping.
         */
        if (verticalVelocity > 0f)
        {
            isGrounded = false;
            return;
        }

        Vector3 spherePosition =
            transform.position +
            Vector3.up *
            groundedOffset;

        isGrounded =
            Physics.CheckSphere(
                spherePosition,
                groundedRadius,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

        /*
         * The CharacterController may still report grounded contact
         * during minor terrain transitions, stairs, and slope crests.
         */
        if (
            !isGrounded &&
            characterController.isGrounded &&
            verticalVelocity <= 0f
        )
        {
            isGrounded = true;
        }
    }

    private void UpdateGravity()
    {
        if (isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity =
                    groundedVerticalVelocity;
            }

            return;
        }

        verticalVelocity +=
            gravity *
            Time.deltaTime;

        verticalVelocity =
            Mathf.Max(
                verticalVelocity,
                terminalVelocity
            );
    }

    private void CalculateMoveDirection()
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        if (isLockedOn)
        {
            CalculateLockOnMovement();
        }
        else
        {
            CalculateNormalMovement();
        }
    }

    private void CalculateNormalMovement()
    {
        Vector3 cameraForward =
            cameraTransform.forward;

        Vector3 cameraRight =
            cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        moveDirection =
            (
                cameraForward *
                movementInput.y +

                cameraRight *
                movementInput.x
            ).normalized;
    }

    private void CalculateLockOnMovement()
    {
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
            isRunning = false;
            return;
        }

        isRunning =
            Keyboard.current != null &&
            Keyboard.current
                .leftShiftKey
                .isPressed &&
            movementInput.sqrMagnitude >
            0.01f;
    }

    private void UpdateMovementSpeed(
        bool isDodging
    )
    {
        float targetSpeed = 0f;

        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        bool canUseNormalMovement =
            !IsMovementLocked &&
            !isDodging &&
            movementInput.sqrMagnitude >
            0.01f;

        if (canUseNormalMovement)
        {
            if (isLockedOn)
            {
                targetSpeed =
                    lockOnMoveSpeed;
            }
            else
            {
                targetSpeed =
                    isRunning
                        ? runSpeed
                        : walkSpeed;
            }
        }

        currentMoveSpeed =
            Mathf.MoveTowards(
                currentMoveSpeed,
                targetSpeed,
                speedChangeRate *
                Time.deltaTime
            );
    }

    private void MoveCharacter(
        bool isDodging
    )
    {
        Vector3 horizontalVelocity =
            moveDirection *
            currentMoveSpeed;

        if (
            isDodging &&
            playerDodge != null
        )
        {
            horizontalVelocity =
                playerDodge
                    .CurrentDodgeVelocity;
        }

        Vector3 totalVelocity =
            horizontalVelocity +
            Vector3.up *
            verticalVelocity;

        /*
         * CharacterController.Move expects displacement rather
         * than velocity.
         *
         * All movement is combined into this single call.
         */
        CollisionFlags collisionFlags =
            characterController.Move(
                totalVelocity *
                Time.deltaTime
            );

        /*
         * Prevent vertical velocity from building while colliding
         * with a ceiling.
         */
        if (
            (
                collisionFlags &
                CollisionFlags.Above
            ) != 0 &&
            verticalVelocity > 0f
        )
        {
            verticalVelocity = 0f;
        }
    }

    private void UpdateRotation(
        bool isDodging
    )
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        if (isLockedOn)
        {
            RotateTowardLockOnTarget();
            return;
        }

        if (isDodging)
        {
            return;
        }

        RotateTowardMovementDirection();
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

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed *
                Time.deltaTime
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

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                lockOnTurnSpeed *
                Time.deltaTime
            );
    }

    private void UpdateAnimator(
        bool isDodging
    )
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        bool canAnimateMovement =
            isGrounded &&
            !isDodging &&
            !IsMovementLocked;

        /*
         * Normal locomotion Blend Tree:
         *
         * 0.0 = Idle
         * 0.5 = Walk
         * 1.0 = Run
         */
        float speedValue = 0f;

        if (
            canAnimateMovement &&
            movementInput.sqrMagnitude > 0.01f
        )
        {
            speedValue =
                isRunning
                    ? 1f
                    : 0.5f;
        }

        /*
         * Damp the Speed parameter slightly so transitions
         * between Idle, Walk, and Run blend smoothly.
         */
        animator.SetFloat(
            SpeedFloat,
            speedValue,
            0.1f,
            Time.deltaTime
        );

        /*
         * Retained for now because other Animator transitions
         * may still reference this parameter.
         */
        animator.SetBool(
            IsRunningBool,
            isRunning &&
            canAnimateMovement
        );

        animator.SetBool(
            IsGroundedBool,
            isGrounded
        );

        /*
         * Expose the player's vertical motion to the Animator.
         *
         * Positive = moving upward.
         * Negative = falling.
         */
        animator.SetFloat(
            VerticalVelocityFloat,
            verticalVelocity
        );

        animator.SetBool(
            IsLockedOnBool,
            isLockedOn
        );

        float lockOnHorizontal =
            isLockedOn &&
            canAnimateMovement
                ? movementInput.x
                : 0f;

        float lockOnVertical =
            isLockedOn &&
            canAnimateMovement
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
            animator.ResetTrigger(
                JumpTrigger
            );

            animator.SetTrigger(
                LandTrigger
            );
        }

        wasGrounded =
            isGrounded;
    }

    public void StopMovementImmediately()
    {
        ResetWaiting();

        currentMoveSpeed = 0f;
        moveDirection = Vector3.zero;
        movementInput = Vector2.zero;

        if (
            playerDodge != null &&
            playerDodge.IsDodging
        )
        {
            playerDodge.CancelDodge();
        }
    }

    private void OnDisable()
    {
        ResetWaiting();

        currentMoveSpeed = 0f;
        moveDirection = Vector3.zero;
        movementInput = Vector2.zero;
    }

    private void OnValidate()
    {
        walkSpeed =
            Mathf.Max(
                0f,
                walkSpeed
            );

        runSpeed =
            Mathf.Max(
                walkSpeed,
                runSpeed
            );

        lockOnMoveSpeed =
            Mathf.Max(
                0f,
                lockOnMoveSpeed
            );

        speedChangeRate =
            Mathf.Max(
                0f,
                speedChangeRate
            );

        turnSpeed =
            Mathf.Max(
                0f,
                turnSpeed
            );

        lockOnTurnSpeed =
            Mathf.Max(
                0f,
                lockOnTurnSpeed
            );

        jumpHeight =
            Mathf.Max(
                0f,
                jumpHeight
            );

        gravity =
            Mathf.Min(
                -0.01f,
                gravity
            );

        groundedVerticalVelocity =
            Mathf.Min(
                -0.01f,
                groundedVerticalVelocity
            );

        terminalVelocity =
            Mathf.Min(
                groundedVerticalVelocity,
                terminalVelocity
            );

        groundedRadius =
            Mathf.Max(
                0.01f,
                groundedRadius
            );

        waitingDelay =
            Mathf.Max(
                0.1f,
                waitingDelay
            );
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 spherePosition =
            transform.position +
            Vector3.up *
            groundedOffset;

        Gizmos.DrawWireSphere(
            spherePosition,
            groundedRadius
        );
    }
}