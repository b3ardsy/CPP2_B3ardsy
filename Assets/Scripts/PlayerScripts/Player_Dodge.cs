using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Player_Dodge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player_LockOn playerLockOn;

    [Header("Dodge Settings")]
    [SerializeField] private float dodgeSpeed = 15f;
    [SerializeField] private float dodgeDuration = 0.3f;
    [SerializeField] private float dodgeCooldown = 0.2f;

    private Animator animator;

    private Vector3 dodgeDirection;
    private float dodgeEndTime;
    private float nextDodgeTime;

    private bool isDodging;

    public bool IsDodging =>
        isDodging;

    public Vector3 CurrentDodgeVelocity
    {
        get
        {
            if (!isDodging)
            {
                return Vector3.zero;
            }

            return dodgeDirection * dodgeSpeed;
        }
    }

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
        animator =
            GetComponent<Animator>();

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<Player_LockOn>();
        }

        if (animator == null)
        {
            Debug.LogError(
                $"{name}: Player_Dodge could not find an Animator.",
                this
            );

            enabled = false;
        }
    }

    private void Update()
    {
        if (!isDodging)
        {
            return;
        }

        if (Time.time >= dodgeEndTime)
        {
            EndDodge();
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

        BeginDodge();

        return true;
    }

    private void ChooseDodgeDirection(
        Vector2 movementInput,
        Vector3 directionToTarget,
        Vector3 targetRight
    )
    {
        ResetDodgeTriggers();

        /*
         * No directional input defaults
         * to a backward dodge.
         */
        if (
            movementInput.sqrMagnitude <=
            0.01f
        )
        {
            dodgeDirection =
                -directionToTarget;

            animator.SetTrigger(
                DodgeBackwardTrigger
            );

            return;
        }

        /*
         * Use whichever input axis is strongest.
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

    private void BeginDodge()
    {
        isDodging = true;

        dodgeEndTime =
            Time.time +
            dodgeDuration;
    }

    private void EndDodge()
    {
        isDodging = false;
        dodgeDirection = Vector3.zero;

        nextDodgeTime =
            Time.time +
            dodgeCooldown;
    }

    private void ResetDodgeTriggers()
    {
        animator.ResetTrigger(
            DodgeForwardTrigger
        );

        animator.ResetTrigger(
            DodgeBackwardTrigger
        );

        animator.ResetTrigger(
            DodgeLeftTrigger
        );

        animator.ResetTrigger(
            DodgeRightTrigger
        );
    }

    public void CancelDodge()
    {
        isDodging = false;
        dodgeDirection = Vector3.zero;

        ResetDodgeTriggers();
    }

    private void OnDisable()
    {
        CancelDodge();
    }

    private void OnValidate()
    {
        dodgeSpeed =
            Mathf.Max(
                0f,
                dodgeSpeed
            );

        dodgeDuration =
            Mathf.Max(
                0.01f,
                dodgeDuration
            );

        dodgeCooldown =
            Mathf.Max(
                0f,
                dodgeCooldown
            );
    }
}