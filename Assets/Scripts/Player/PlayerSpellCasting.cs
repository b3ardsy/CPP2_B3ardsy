using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSpellcasting : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement3D playerMovement;
    [SerializeField] private PlayerLockOn playerLockOn;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private Transform firePoint;

    [Header("Current Spell")]
    [SerializeField] private PlayerSpellProjectile projectilePrefab;
    [SerializeField] private int spellDamage = 1;
    [SerializeField] private float projectileSpeed = 12f;

    [Header("Casting")]
    [Tooltip("Total length of one casting action.")]
    [SerializeField] private float castDuration = 0.8f;

    [Tooltip(
        "How long after pressing the attack button the projectile appears. " +
        "Set this to match the release moment in the animation."
    )]
    [SerializeField] private float projectileReleaseDelay = 0.3f;

    [Tooltip("Additional delay before another spell can be cast.")]
    [SerializeField] private float castCooldown = 0.25f;

    [Header("Aiming")]
    [Tooltip(
        "When not locked on, the projectile travels in the " +
        "direction the player is facing."
    )]
    [SerializeField] private bool usePlayerForwardWhenUnlocked = true;

    private bool isCasting;
    private bool projectileReleased;

    private float nextCastTime;

    private Coroutine castCoroutine;

    public bool IsCasting => isCasting;

    private static readonly int ShootTrigger =
        Animator.StringToHash("Shoot");

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                GetComponent<PlayerMovement3D>();
        }

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

        if (animator == null)
        {
            Debug.LogError(
                $"{name}: PlayerSpellcasting could not find an Animator."
            );

            enabled = false;
            return;
        }

        if (playerMovement == null)
        {
            Debug.LogError(
                $"{name}: PlayerSpellcasting could not find " +
                "PlayerMovement3D."
            );

            enabled = false;
            return;
        }

        if (firePoint == null)
        {
            Debug.LogError(
                $"{name}: PlayerSpellcasting Fire Point is missing."
            );
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: No spell projectile prefab has been assigned."
            );
        }
    }

    private void OnValidate()
    {
        castDuration = Mathf.Max(
            0.01f,
            castDuration
        );

        projectileReleaseDelay = Mathf.Clamp(
            projectileReleaseDelay,
            0f,
            castDuration
        );

        castCooldown = Mathf.Max(
            0f,
            castCooldown
        );

        projectileSpeed = Mathf.Max(
            0f,
            projectileSpeed
        );

        spellDamage = Mathf.Max(
            1,
            spellDamage
        );
    }

    private void Update()
    {
        /*
         * If the player becomes trapped during a cast,
         * cancel the pending spell immediately.
         */
        if (
            isCasting &&
            IsPlayerActionLocked()
        )
        {
            CancelCast();
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryCast();
        }
    }

    private void TryCast()
    {
        /*
         * Bone Prison applies a movement lock.
         * While movement is locked, spellcasting is disabled.
         */
        if (IsPlayerActionLocked())
        {
            return;
        }

        if (isCasting)
        {
            return;
        }

        if (
            playerCombat != null &&
            playerCombat.IsAttacking
        )
        {
            return;
        }

        if (Time.time < nextCastTime)
        {
            return;
        }

        if (firePoint == null)
        {
            Debug.LogWarning(
                $"{name}: Cannot cast because Fire Point is missing."
            );

            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Cannot cast because no projectile " +
                "prefab is assigned."
            );

            return;
        }

        castCoroutine = StartCoroutine(
            PerformCast()
        );
    }

    private IEnumerator PerformCast()
    {
        isCasting = true;
        projectileReleased = false;

        animator.ResetTrigger(ShootTrigger);
        animator.SetTrigger(ShootTrigger);

        /*
         * Wait until the projectile release moment.
         */
        if (projectileReleaseDelay > 0f)
        {
            yield return new WaitForSeconds(
                projectileReleaseDelay
            );
        }

        /*
         * The player may have been captured after beginning
         * the casting animation but before the projectile
         * release time.
         */
        if (IsPlayerActionLocked())
        {
            CancelCast();
            yield break;
        }

        ReleaseSpellProjectile();

        float remainingCastTime =
            castDuration -
            projectileReleaseDelay;

        if (remainingCastTime > 0f)
        {
            yield return new WaitForSeconds(
                remainingCastTime
            );
        }

        /*
         * Do not complete or release anything further if the
         * player became trapped during the remaining cast time.
         */
        if (IsPlayerActionLocked())
        {
            CancelCast();
            yield break;
        }

        FinishCast();
    }

    public void ReleaseSpellProjectile()
    {
        /*
         * Prevent any projectile release while the player
         * is trapped or otherwise movement-locked.
         */
        if (IsPlayerActionLocked())
        {
            return;
        }

        if (!isCasting)
        {
            return;
        }

        if (projectileReleased)
        {
            return;
        }

        if (
            firePoint == null ||
            projectilePrefab == null
        )
        {
            return;
        }

        projectileReleased = true;

        Vector3 castDirection =
            CalculateCastDirection();

        if (castDirection.sqrMagnitude <= 0.001f)
        {
            castDirection =
                transform.forward;
        }

        castDirection.Normalize();

        PlayerSpellProjectile newProjectile =
            Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.LookRotation(
                    castDirection
                )
            );

        newProjectile.Initialize(
            gameObject,
            castDirection,
            spellDamage,
            projectileSpeed
        );
    }

    private Vector3 CalculateCastDirection()
    {
        if (
            playerLockOn != null &&
            playerLockOn.IsLockedOn
        )
        {
            Vector3 directionToTarget =
                playerLockOn.CurrentTargetPosition -
                firePoint.position;

            if (
                directionToTarget.sqrMagnitude >
                0.001f
            )
            {
                return directionToTarget.normalized;
            }
        }

        if (usePlayerForwardWhenUnlocked)
        {
            return transform.forward.normalized;
        }

        return firePoint.forward.normalized;
    }

    public void EndSpellCast()
    {
        if (!isCasting)
        {
            return;
        }

        /*
         * Do not release a projectile if an end event occurs
         * while the player is trapped.
         */
        if (IsPlayerActionLocked())
        {
            CancelCast();
            return;
        }

        ReleaseSpellProjectile();

        if (castCoroutine != null)
        {
            StopCoroutine(castCoroutine);
            castCoroutine = null;
        }

        FinishCast();
    }

    private bool IsPlayerActionLocked()
    {
        return
            playerMovement != null &&
            playerMovement.IsMovementLocked;
    }

    private void CancelCast()
    {
        if (castCoroutine != null)
        {
            StopCoroutine(castCoroutine);
            castCoroutine = null;
        }

        animator.ResetTrigger(ShootTrigger);

        isCasting = false;
        projectileReleased = false;
    }

    private void FinishCast()
    {
        if (!isCasting)
        {
            return;
        }

        isCasting = false;
        projectileReleased = false;
        castCoroutine = null;

        nextCastTime =
            Time.time + castCooldown;
    }

    private void OnDisable()
    {
        if (castCoroutine != null)
        {
            StopCoroutine(castCoroutine);
            castCoroutine = null;
        }

        if (animator != null)
        {
            animator.ResetTrigger(ShootTrigger);
        }

        isCasting = false;
        projectileReleased = false;
    }
}