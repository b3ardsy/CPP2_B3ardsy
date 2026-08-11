using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatNew : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement3DNew playerMovement;
    [SerializeField] private PlayerLockOn playerLockOn;

    [Header("Wand")]
    [Tooltip("Projectile used by the currently equipped wand.")]
    [SerializeField] private PlayerSpellProjectile wandProjectilePrefab;

    [Tooltip("Projectile spawn point used for one-handed wand attacks.")]
    [SerializeField] private Transform wandFirePoint;

    [SerializeField] private int wandDamage = 1;
    [SerializeField] private float wandProjectileSpeed = 12f;

    [Header("Aiming")]
    [Tooltip(
        "When not locked on, wand projectiles travel " +
        "in the direction the player is facing."
    )]
    [SerializeField] private bool usePlayerForwardWhenUnlocked = true;

    /*
     * Tracks the Wand animation separately from IsAttacking.
     *
     * Wand attacks do not block normal movement or dodging,
     * but we still need to prevent the attack animation from
     * being restarted before it reaches its projectile event.
     */
    private bool isWandAttackInProgress;

    /*
     * PlayerMovement3DNew and PlayerDodgeNew currently use
     * IsAttacking to determine whether movement should be blocked.
     *
     * Wand attacks do not block movement, so this remains false.
     * Heavier Staff attacks may use this property later.
     */
    public bool IsAttacking => false;

    private static readonly int ShootTrigger =
        Animator.StringToHash("Shoot");

    private void Awake()
    {
        FindReferences();
        ValidateReferences();
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
    }

    private void ValidateReferences()
    {
        if (animator == null)
        {
            Debug.LogError(
                $"{name}: PlayerCombatNew could not find an Animator.",
                this
            );
        }

        if (playerMovement == null)
        {
            Debug.LogError(
                $"{name}: PlayerCombatNew could not find " +
                "PlayerMovement3DNew.",
                this
            );

            enabled = false;
            return;
        }

        if (wandFirePoint == null)
        {
            Debug.LogWarning(
                $"{name}: Wand Fire Point has not been assigned.",
                this
            );
        }

        if (wandProjectilePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Wand Projectile Prefab has not been assigned.",
                this
            );
        }
    }

    private void Update()
    {
        /*
         * If the player becomes trapped, hit, or otherwise
         * action locked during a Wand attack, cancel the
         * current attack state.
         */
        if (
            isWandAttackInProgress &&
            IsPlayerActionLocked()
        )
        {
            CancelWandAttack();
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        /*
         * One Wand attack begins per click.
         *
         * There is no cooldown or mana cost.
         * The attack animation determines how quickly
         * another projectile can be fired.
         */
        if (
            Mouse.current.leftButton
                .wasPressedThisFrame
        )
        {
            TryFireWand();
        }
    }

    private void TryFireWand()
    {
        if (IsPlayerActionLocked())
        {
            return;
        }

        /*
         * Do not restart the attack animation before
         * the current Wand attack has completed.
         */
        if (isWandAttackInProgress)
        {
            return;
        }

        if (wandFirePoint == null)
        {
            Debug.LogWarning(
                $"{name}: Cannot fire Wand because " +
                "Wand Fire Point is missing.",
                this
            );

            return;
        }

        if (wandProjectilePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Cannot fire Wand because " +
                "no Wand Projectile Prefab is assigned.",
                this
            );

            return;
        }

        isWandAttackInProgress = true;

        PlayWandAttackAnimation();
    }

    /*
     * Called by an Animation Event on the Wand attack clip.
     *
     * Place this event on the exact animation frame where
     * the projectile should leave the Wand.
     */
    public void ShootFireball()
    {
        if (!isWandAttackInProgress)
        {
            return;
        }

        if (IsPlayerActionLocked())
        {
            return;
        }

        if (
            wandFirePoint == null ||
            wandProjectilePrefab == null
        )
        {
            return;
        }

        FireWandProjectile();
    }

    private void FireWandProjectile()
    {
        Vector3 fireDirection =
            CalculateFireDirection();

        if (
            fireDirection.sqrMagnitude <=
            0.001f
        )
        {
            fireDirection =
                transform.forward;
        }

        fireDirection.Normalize();

        Quaternion spawnRotation =
            Quaternion.LookRotation(
                fireDirection,
                Vector3.up
            );

        PlayerSpellProjectile projectile =
            Instantiate(
                wandProjectilePrefab,
                wandFirePoint.position,
                spawnRotation
            );

        projectile.Initialize(
            gameObject,
            fireDirection,
            wandDamage,
            wandProjectileSpeed
        );
    }

    private Vector3 CalculateFireDirection()
    {
        /*
         * When locked on, fire directly toward the
         * currently selected enemy.
         */
        if (
            playerLockOn != null &&
            playerLockOn.IsLockedOn
        )
        {
            Vector3 directionToTarget =
                playerLockOn.CurrentTargetPosition -
                wandFirePoint.position;

            if (
                directionToTarget.sqrMagnitude >
                0.001f
            )
            {
                return
                    directionToTarget.normalized;
            }
        }

        /*
         * When unlocked, fire in the direction
         * the player is facing.
         */
        if (usePlayerForwardWhenUnlocked)
        {
            return
                transform.forward.normalized;
        }

        return
            wandFirePoint.forward.normalized;
    }

    private void PlayWandAttackAnimation()
    {
        if (animator == null)
        {
            isWandAttackInProgress = false;
            return;
        }

        animator.ResetTrigger(
            ShootTrigger
        );

        animator.SetTrigger(
            ShootTrigger
        );
    }

    /*
     * Called by an Animation Event near the end
     * of the Wand attack animation.
     *
     * Once called, another click may immediately
     * begin another Wand attack.
     */
    public void EndWandAttack()
    {
        isWandAttackInProgress = false;
    }

    public void CancelWandAttack()
    {
        isWandAttackInProgress = false;

        if (animator != null)
        {
            animator.ResetTrigger(
                ShootTrigger
            );
        }
    }

    private bool IsPlayerActionLocked()
    {
        return
            playerMovement != null &&
            playerMovement.IsMovementLocked;
    }

    private void OnDisable()
    {
        CancelWandAttack();
    }

    private void OnValidate()
    {
        wandDamage =
            Mathf.Max(
                1,
                wandDamage
            );

        wandProjectileSpeed =
            Mathf.Max(
                0f,
                wandProjectileSpeed
            );
    }
}