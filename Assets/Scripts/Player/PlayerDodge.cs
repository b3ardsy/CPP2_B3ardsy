using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerDodge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerLockOn playerLockOn;
    [SerializeField] private PlayerCombat playerCombat;

    [Header("Dodge Settings")]
    [SerializeField] private float dodgeSpeed = 8f;
    [SerializeField] private float dodgeDuration = 0.45f;
    [SerializeField] private float dodgeCooldown = 0.2f;

    private Rigidbody rb;
    private Animator animator;

    private Vector3 dodgeDirection;

    private bool isDodging;
    private float nextDodgeTime;

    public bool IsDodging => isDodging;

    private static readonly int DodgeForwardTrigger =
        Animator.StringToHash("DodgeForward");

    private static readonly int DodgeBackwardTrigger =
        Animator.StringToHash("DodgeBackward");

    private static readonly int DodgeLeftTrigger =
        Animator.StringToHash("DodgeLeft");

    private static readonly int DodgeRightTrigger =
        Animator.StringToHash("DodgeRight");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<PlayerLockOn>();
        }

        if (playerCombat == null)
        {
            playerCombat =
                GetComponent<PlayerCombat>();
        }
    }

    public bool TryDodge(Vector2 movementInput)
    {
        if (isDodging)
        {
            return false;
        }

        if (Time.time < nextDodgeTime)
        {
            return false;
        }

        if (
            playerLockOn == null ||
            !playerLockOn.IsLockedOn
        )
        {
            return false;
        }

        if (
            playerCombat != null &&
            playerCombat.IsAttacking
        )
        {
            return false;
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
            return false;
        }

        directionToTarget.Normalize();

        Vector3 targetRight =
            Vector3.Cross(
                Vector3.up,
                directionToTarget
            ).normalized;

        ChooseDodgeDirection(
            movementInput,
            directionToTarget,
            targetRight
        );

        StartCoroutine(
            DodgeRoutine()
        );

        return true;
    }

    private void ChooseDodgeDirection(
        Vector2 movementInput,
        Vector3 directionToTarget,
        Vector3 targetRight
    )
    {
        /*
         * No input defaults to a backward dodge.
         */
        if (movementInput.sqrMagnitude <= 0.01f)
        {
            dodgeDirection =
                -directionToTarget;

            animator.SetTrigger(
                DodgeBackwardTrigger
            );

            return;
        }

        /*
         * Choose the strongest input axis.
         * Vertical wins when both axes are equal.
         */
        if (
            Mathf.Abs(movementInput.x) >
            Mathf.Abs(movementInput.y)
        )
        {
            if (movementInput.x > 0f)
            {
                dodgeDirection =
                    targetRight;

                animator.SetTrigger(
                    DodgeRightTrigger
                );
            }
            else
            {
                dodgeDirection =
                    -targetRight;

                animator.SetTrigger(
                    DodgeLeftTrigger
                );
            }

            return;
        }

        if (movementInput.y > 0f)
        {
            dodgeDirection =
                directionToTarget;

            animator.SetTrigger(
                DodgeForwardTrigger
            );
        }
        else
        {
            dodgeDirection =
                -directionToTarget;

            animator.SetTrigger(
                DodgeBackwardTrigger
            );
        }
    }

    private IEnumerator DodgeRoutine()
    {
        isDodging = true;

        float elapsedTime = 0f;

        while (elapsedTime < dodgeDuration)
        {
            elapsedTime +=
                Time.fixedDeltaTime;

            yield return new WaitForFixedUpdate();
        }

        StopHorizontalMovement();

        isDodging = false;

        nextDodgeTime =
            Time.time + dodgeCooldown;
    }

    public void ApplyDodgeMovement()
    {
        if (!isDodging)
        {
            return;
        }

        rb.linearVelocity =
            new Vector3(
                dodgeDirection.x *
                dodgeSpeed,

                rb.linearVelocity.y,

                dodgeDirection.z *
                dodgeSpeed
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
}