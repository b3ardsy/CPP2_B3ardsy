using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerLockOn playerLockOn;

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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (playerCombat == null)
        {
            playerCombat = GetComponent<PlayerCombat>();
        }

        if (playerLockOn == null)
        {
            playerLockOn = GetComponent<PlayerLockOn>();
        }

        rb.freezeRotation = true;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
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

        ReadMovementInput();
        CalculateMoveDirection();
        UpdateRunningState();
        HandleJump();

        UpdateAnimator();
        HandleCursorUnlock();
    }

    private void ReadMovementInput()
    {
        movementInput = Vector2.zero;

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
        Transform target =
            playerLockOn.CurrentTargetTransform;

        if (target == null)
        {
            moveDirection = Vector3.zero;
            return;
        }

        Vector3 directionToTarget =
            target.position - transform.position;

        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude <= 0.001f)
        {
            moveDirection = Vector3.zero;
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
                directionToTarget * movementInput.y +
                targetRight * movementInput.x
            ).normalized;
    }

    private void UpdateRunningState()
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        if (isLockedOn)
        {
            // Do not enter the normal running animation
            // while using lock-on locomotion.
            isRunning = false;
            return;
        }

        if (isGrounded)
        {
            isRunning =
                Keyboard.current.leftShiftKey.isPressed &&
                moveDirection.sqrMagnitude > 0.01f;
        }
    }

    private void HandleJump()
    {
        if (
            Keyboard.current.spaceKey.wasPressedThisFrame &&
            isGrounded
        )
        {
            bool isLockedOn =
                playerLockOn != null &&
                playerLockOn.IsLockedOn;

            if (isLockedOn)
            {
                airborneSpeed = lockOnMoveSpeed;
            }
            else
            {
                airborneSpeed =
                    isRunning ? runSpeed : walkSpeed;
            }

            jumpPressed = true;
            animator.SetTrigger(JumpTrigger);
        }
    }

    private void FixedUpdate()
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        bool isAttacking =
            playerCombat != null &&
            playerCombat.IsAttacking;

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
                currentSpeed = lockOnMoveSpeed;
            }
            else
            {
                currentSpeed =
                    isRunning ? runSpeed : walkSpeed;
            }
        }
        else
        {
            currentSpeed = airborneSpeed;
        }

        Vector3 velocity =
            moveDirection * currentSpeed;

        rb.linearVelocity = new Vector3(
            velocity.x,
            rb.linearVelocity.y,
            velocity.z
        );

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
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                0f,
                rb.linearVelocity.z
            );

            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            jumpPressed = false;
            isGrounded = false;
        }
    }

    private void RotateTowardMovementDirection()
    {
        if (moveDirection.sqrMagnitude <= 0.01f)
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
                turnSpeed * Time.fixedDeltaTime
            )
        );
    }

    private void RotateTowardLockOnTarget()
    {
        Transform target =
            playerLockOn.CurrentTargetTransform;

        if (target == null)
        {
            return;
        }

        Vector3 directionToTarget =
            target.position - transform.position;

        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude <= 0.001f)
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
                lockOnTurnSpeed * Time.fixedDeltaTime
            )
        );
    }

    private void StopHorizontalMovement()
    {
        rb.linearVelocity = new Vector3(
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
            return;
        }

        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    private void UpdateAnimator()
    {
        bool isLockedOn =
            playerLockOn != null &&
            playerLockOn.IsLockedOn;

        float speedValue =
            isGrounded
                ? moveDirection.magnitude
                : 0f;

        animator.SetFloat(
            SpeedFloat,
            speedValue
        );

        animator.SetBool(
            IsRunningBool,
            isRunning && isGrounded
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
            isLockedOn && isGrounded
                ? movementInput.x
                : 0f;

        float lockOnVertical =
            isLockedOn && isGrounded
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

        if (!wasGrounded && isGrounded)
        {
            animator.SetTrigger(LandTrigger);
        }

        wasGrounded = isGrounded;
    }

    private void HandleCursorUnlock()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
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

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}