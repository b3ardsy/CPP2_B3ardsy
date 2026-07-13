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

    [Header("Jump")]
    [SerializeField] private float jumpForce = 6f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerCombat playerCombat;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody rb;
    private Animator animator;

    private Vector3 moveDirection;

    private bool jumpPressed;
    private bool isGrounded;
    private bool wasGrounded;
    private bool isRunning;

    // Stores the movement speed used when the player leaves the ground.
    // Pressing or releasing Shift in the air will not change this value.
    private float airborneSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (playerCombat == null)
        {
            playerCombat = GetComponent<PlayerCombat>();
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
            moveDirection = Vector3.zero;
            isRunning = false;
            jumpPressed = false;

            UpdateAnimator();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            return;
        }

        Vector2 input = Vector2.zero;

        if (Keyboard.current.aKey.isPressed)
        {
            input.x -= 1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            input.x += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            input.y -= 1f;
        }

        if (Keyboard.current.wKey.isPressed)
        {
            input.y += 1f;
        }

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;

        camForward.y = 0f;
        camRight.y = 0f;

        camForward.Normalize();
        camRight.Normalize();

        moveDirection =
            (camForward * input.y + camRight * input.x).normalized;

        if (isGrounded)
        {
            isRunning =
                Keyboard.current.leftShiftKey.isPressed &&
                moveDirection.sqrMagnitude > 0.01f;
        }

        if (
            Keyboard.current.spaceKey.wasPressedThisFrame &&
            isGrounded
        )
        {
            airborneSpeed =
                isRunning ? runSpeed : walkSpeed;

            jumpPressed = true;
            animator.SetTrigger("Jump");
        }

        UpdateAnimator();

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void FixedUpdate()
    {
        bool isAttacking =
            playerCombat != null &&
            playerCombat.IsAttacking;

        if (isAttacking)
        {
            StopHorizontalMovement();
            return;
        }

        float currentSpeed;

        if (isGrounded)
        {
            currentSpeed =
                isRunning ? runSpeed : walkSpeed;
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

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    turnSpeed * Time.fixedDeltaTime
                )
            );
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
        float speedValue =
            isGrounded ? moveDirection.magnitude : 0f;

        animator.SetFloat(
            "Speed",
            speedValue
        );

        animator.SetBool(
            "IsRunning",
            isRunning && isGrounded
        );

        animator.SetBool(
            "IsGrounded",
            isGrounded
        );

        if (!wasGrounded && isGrounded)
        {
            animator.SetTrigger("Land");
        }

        wasGrounded = isGrounded;
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