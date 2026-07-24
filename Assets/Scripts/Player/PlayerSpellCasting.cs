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
    [SerializeField] private float castDuration = 0.8f;
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
            playerMovement = GetComponent<PlayerMovement3D>();
        }

        if (playerLockOn == null)
        {
            playerLockOn = GetComponent<PlayerLockOn>();
        }

        if (playerCombat == null)
        {
            playerCombat = GetComponent<PlayerCombat>();
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

    private void Update()
    {
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

        playerMovement.AddMovementLock(this);

        RotateTowardCastTarget();

        animator.ResetTrigger(ShootTrigger);
        animator.SetTrigger(ShootTrigger);

        yield return new WaitForSeconds(castDuration);

        FinishCast();
    }

    private void RotateTowardCastTarget()
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

        transform.rotation = Quaternion.LookRotation(
            directionToTarget.normalized
        );
    }

    /*
     * Call this method using an Animation Event on the
     * frame where the projectile leaves the druid's hand.
     */
    public void ReleaseSpellProjectile()
    {
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

        PlayerSpellProjectile newProjectile =
            Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.LookRotation(castDirection)
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
            Vector3 targetPosition =
                playerLockOn.CurrentTargetPosition;

            Vector3 directionToTarget =
                targetPosition -
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

    /*
     * This can optionally be called by an Animation Event
     * at the end of the shoot animation.
     */
    public void EndSpellCast()
    {
        if (!isCasting)
        {
            return;
        }

        if (castCoroutine != null)
        {
            StopCoroutine(castCoroutine);
            castCoroutine = null;
        }

        FinishCast();
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

        if (playerMovement != null)
        {
            playerMovement.RemoveMovementLock(this);
        }
    }

    private void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.RemoveMovementLock(this);
        }

        isCasting = false;
        projectileReleased = false;
        castCoroutine = null;
    }
}