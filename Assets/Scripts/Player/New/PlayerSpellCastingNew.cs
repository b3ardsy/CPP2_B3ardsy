using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSpellcastingNew : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement3DNew playerMovement;
    [SerializeField] private PlayerLockOn playerLockOn;

    /*
     * This temporarily references the existing PlayerCombat.
     * When PlayerCombatNew is created, this reference will be
     * changed to PlayerCombatNew.
     */
    [SerializeField] private PlayerCombatNew playerCombat;

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
        FindReferences();
        ValidateRequiredReferences();
    }

    private void FindReferences()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                GetComponent<PlayerMovement3DNew>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                GetComponentInParent<PlayerMovement3DNew>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<PlayerLockOn>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponentInParent<PlayerLockOn>();
        }

        if (playerCombat == null)
        {
            playerCombat =
                GetComponent<PlayerCombatNew>();
        }

        if (playerCombat == null)
        {
            playerCombat =
                GetComponentInParent<PlayerCombatNew>();
        }
    }

    private void ValidateRequiredReferences()
    {
        if (animator == null)
        {
            Debug.LogError(
                $"{name}: PlayerSpellcastingNew could not find an Animator.",
                this
            );

            enabled = false;
            return;
        }

        if (playerMovement == null)
        {
            Debug.LogError(
                $"{name}: PlayerSpellcastingNew could not find " +
                "PlayerMovement3DNew.",
                this
            );

            enabled = false;
            return;
        }

        if (firePoint == null)
        {
            Debug.LogError(
                $"{name}: PlayerSpellcastingNew Fire Point is missing.",
                this
            );
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: No spell projectile prefab has been assigned.",
                this
            );
        }
    }

    private void Update()
    {
        /*
         * If the player becomes trapped, dies, or receives another
         * movement lock during a cast, cancel the pending action.
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

        if (
            Mouse.current.leftButton
                .wasPressedThisFrame
        )
        {
            TryCast();
        }
    }

    private void TryCast()
    {
        /*
         * Bone Prison and hit reactions apply movement locks.
         * Casting is unavailable while one is active.
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
                $"{name}: Cannot cast because Fire Point is missing.",
                this
            );

            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Cannot cast because no projectile " +
                "prefab is assigned.",
                this
            );

            return;
        }

        castCoroutine =
            StartCoroutine(
                PerformCast()
            );
    }

    private IEnumerator PerformCast()
    {
        isCasting = true;
        projectileReleased = false;

        animator.ResetTrigger(
            ShootTrigger
        );

        animator.SetTrigger(
            ShootTrigger
        );

        if (projectileReleaseDelay > 0f)
        {
            yield return new WaitForSeconds(
                projectileReleaseDelay
            );
        }

        /*
         * The player may have been hit or captured after the cast
         * began but before the projectile release moment.
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

        if (IsPlayerActionLocked())
        {
            CancelCast();
            yield break;
        }

        FinishCast();
    }

    public void ReleaseSpellProjectile()
    {
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

        if (
            castDirection.sqrMagnitude <=
            0.001f
        )
        {
            castDirection =
                transform.forward;
        }

        castDirection.Normalize();

        Quaternion spawnRotation =
            Quaternion.LookRotation(
                castDirection,
                Vector3.up
            );

        PlayerSpellProjectile newProjectile =
            Instantiate(
                projectilePrefab,
                firePoint.position,
                spawnRotation
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
                return
                    directionToTarget.normalized;
            }
        }

        if (usePlayerForwardWhenUnlocked)
        {
            return
                transform.forward.normalized;
        }

        return
            firePoint.forward.normalized;
    }

    /*
     * This may be called from an Animation Event.
     *
     * The coroutine also releases and finishes the cast as a
     * fallback, so the projectileReleased flag prevents duplicates.
     */
    public void EndSpellCast()
    {
        if (!isCasting)
        {
            return;
        }

        if (IsPlayerActionLocked())
        {
            CancelCast();
            return;
        }

        ReleaseSpellProjectile();

        if (castCoroutine != null)
        {
            StopCoroutine(
                castCoroutine
            );

            castCoroutine = null;
        }

        FinishCast();
    }

    public void CancelCast()
    {
        if (castCoroutine != null)
        {
            StopCoroutine(
                castCoroutine
            );

            castCoroutine = null;
        }

        if (animator != null)
        {
            animator.ResetTrigger(
                ShootTrigger
            );
        }

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
            Time.time +
            castCooldown;
    }

    private bool IsPlayerActionLocked()
    {
        return
            playerMovement != null &&
            playerMovement.IsMovementLocked;
    }

    private void OnDisable()
    {
        CancelCast();
    }

    private void OnValidate()
    {
        castDuration =
            Mathf.Max(
                0.01f,
                castDuration
            );

        projectileReleaseDelay =
            Mathf.Clamp(
                projectileReleaseDelay,
                0f,
                castDuration
            );

        castCooldown =
            Mathf.Max(
                0f,
                castCooldown
            );

        projectileSpeed =
            Mathf.Max(
                0f,
                projectileSpeed
            );

        spellDamage =
            Mathf.Max(
                1,
                spellDamage
            );
    }
}