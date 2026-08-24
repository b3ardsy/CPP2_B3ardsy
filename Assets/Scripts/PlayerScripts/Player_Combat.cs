using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Combat : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Player_Controller playerController;
    [SerializeField] private Player_LockOn playerLockOn;

    /*
     * These three systems are still using their legacy classes.
     * They will be replaced individually after Player_Combat
     * has been tested and confirmed working.
     */
    [SerializeField] private PlayerWeaponManager playerWeaponManager;
    [SerializeField] private Player_StaffCombat playerStaffCombat;
    [SerializeField] private PlayerShieldController playerShieldController;

    // =========================================================
    // WAND
    // =========================================================

    [Header("Wand")]
    [Tooltip("Projectile used by the Wand.")]
    [SerializeField] private PlayerSpellProjectile wandProjectilePrefab;

    [Tooltip("Projectile spawn point used by Wand attacks.")]
    [SerializeField] private Transform wandFirePoint;

    [SerializeField] private int wandDamage = 1;
    [SerializeField] private float wandProjectileSpeed = 12f;

    [Header("Wand Aiming")]
    [Tooltip(
        "When not locked on, Wand projectiles travel " +
        "in the direction the player is facing."
    )]
    [SerializeField]
    private bool usePlayerForwardWhenUnlocked = true;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private bool isWandAttackInProgress;

    /*
     * Staff casting prevents normal movement.
     *
     * We preserve the existing behaviour here for now.
     * Once Player_StaffCombat is created, Staff will use the
     * Player_Controller movement-lock system directly.
     */
    public bool IsAttacking =>
        playerStaffCombat != null &&
        playerStaffCombat.IsCasting;

    public bool IsCombatBusy =>
        isWandAttackInProgress ||
        (
            playerStaffCombat != null &&
            playerStaffCombat.IsCasting
        );

    public bool IsWandAttackInProgress =>
        isWandAttackInProgress;

    // =========================================================
    // ANIMATOR PARAMETERS
    // =========================================================

    private static readonly int ShootTrigger =
        Animator.StringToHash("Shoot");

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        FindReferences();
        ValidateReferences();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        HandleCombatCancellation();

        HandleStaffSpellInput();
        HandleMouseInput();
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void HandleMouseInput()
    {
        if (Mouse.current == null)
        {
            return;
        }

        /*
         * RMB activates Shield.
         */
        if (
            Mouse.current.rightButton
                .wasPressedThisFrame
        )
        {
            TryActivateShield();
        }

        /*
         * Each LMB click attempts one Wand attack.
         *
         * Another Wand attack cannot begin until the current
         * attack animation has finished.
         */
        if (
            Mouse.current.leftButton
                .wasPressedThisFrame
        )
        {
            TryFireWand();
        }
    }

    private void HandleStaffSpellInput()
    {
        if (
            Keyboard.current == null ||
            playerWeaponManager == null ||
            playerStaffCombat == null
        )
        {
            return;
        }

        /*
         * Staff abilities do not become available until
         * the Staff has actually been collected.
         */
        if (!playerWeaponManager.HasStaff)
        {
            return;
        }

        /*
         * Do not begin a Staff cast while a Wand attack
         * animation is currently in progress.
         */
        if (isWandAttackInProgress)
        {
            return;
        }

        if (
            Keyboard.current.digit1Key
                .wasPressedThisFrame
        )
        {
            playerStaffCombat.TryCastSpellSlot(1);
            return;
        }

        if (
            Keyboard.current.digit2Key
                .wasPressedThisFrame
        )
        {
            playerStaffCombat.TryCastSpellSlot(2);
            return;
        }

        if (
            Keyboard.current.digit3Key
                .wasPressedThisFrame
        )
        {
            playerStaffCombat.TryCastSpellSlot(3);
        }
    }

    // =========================================================
    // COMBAT CANCELLATION
    // =========================================================

    private void HandleCombatCancellation()
    {
        if (
            isWandAttackInProgress &&
            IsPlayerActionLocked()
        )
        {
            CancelWandAttack();
        }
    }

    /*
     * Common cancellation point for other player systems.
     *
     * Player_DamageController will eventually call this instead
     * of knowing how Wand and Staff attacks work internally.
     */
    public void CancelCombat()
    {
        CancelWandAttack();

        if (playerStaffCombat != null)
        {
            playerStaffCombat.CancelStaffCast();
        }
    }

    // =========================================================
    // SHIELD
    // =========================================================

    private void TryActivateShield()
    {
        if (IsPlayerActionLocked())
        {
            return;
        }

        if (playerShieldController == null)
        {
            return;
        }

        playerShieldController.TryActivateShield();
    }

    // =========================================================
    // WAND
    // =========================================================

    private void TryFireWand()
    {
        if (
            IsPlayerActionLocked() ||
            isWandAttackInProgress
        )
        {
            return;
        }

        /*
         * Wand cannot begin firing during a Staff cast.
         */
        if (
            playerStaffCombat != null &&
            playerStaffCombat.IsCasting
        )
        {
            return;
        }

        if (
            playerWeaponManager != null &&
            !playerWeaponManager.HasWand
        )
        {
            return;
        }

        if (
            wandFirePoint == null ||
            wandProjectilePrefab == null
        )
        {
            Debug.LogWarning(
                $"{name}: Wand attack is missing its " +
                "Fire Point or Projectile Prefab.",
                this
            );

            return;
        }

        isWandAttackInProgress = true;

        PlayWandAttackAnimation();
    }

    /*
     * Animation Event on the Wand attack animation.
     *
     * Keep the existing Animation Event named ShootFireball.
     */
    public void ShootFireball()
    {
        if (
            !isWandAttackInProgress ||
            IsPlayerActionLocked()
        )
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
            CalculateWandFireDirection();

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

    private Vector3 CalculateWandFireDirection()
    {
        /*
         * Locked on:
         * fire directly toward the current target position.
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
         * Unlocked:
         * normally fire in the direction the player faces.
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
            isWandAttackInProgress =
                false;

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
     * Animation Event on the Wand attack animation.
     */
    public void EndWandAttack()
    {
        isWandAttackInProgress =
            false;
    }

    public void CancelWandAttack()
    {
        isWandAttackInProgress =
            false;

        if (animator != null)
        {
            animator.ResetTrigger(
                ShootTrigger
            );
        }
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void FindReferences()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (playerController == null)
        {
            playerController =
                GetComponent<Player_Controller>();
        }

        if (playerController == null)
        {
            playerController =
                GetComponentInParent<Player_Controller>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<Player_LockOn>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponentInParent<Player_LockOn>();
        }

        if (playerWeaponManager == null)
        {
            playerWeaponManager =
                GetComponent<PlayerWeaponManager>();
        }

        if (playerStaffCombat == null)
        {
            playerStaffCombat =
                GetComponent<Player_StaffCombat>();
        }

        if (playerShieldController == null)
        {
            playerShieldController =
                GetComponent<PlayerShieldController>();
        }
    }

    private void ValidateReferences()
    {
        if (animator == null)
        {
            Debug.LogError(
                $"{name}: Player_Combat could not find an Animator.",
                this
            );
        }

        if (playerController == null)
        {
            Debug.LogError(
                $"{name}: Player_Combat could not find " +
                "Player_Controller.",
                this
            );

            enabled = false;
            return;
        }

        if (playerLockOn == null)
        {
            Debug.LogWarning(
                $"{name}: Player_Combat could not find " +
                "Player_LockOn. Locked-on Wand aiming " +
                "will not be available.",
                this
            );
        }

        if (playerWeaponManager == null)
        {
            Debug.LogError(
                $"{name}: Player_Combat could not find " +
                "PlayerWeaponManager.",
                this
            );

            enabled = false;
            return;
        }

        if (playerStaffCombat == null)
        {
            Debug.LogWarning(
                $"{name}: Player_Combat could not find " +
                "PlayerStaffCombat.",
                this
            );
        }

        if (playerShieldController == null)
        {
            Debug.LogWarning(
                $"{name}: Player_Combat could not find " +
                "PlayerShieldController.",
                this
            );
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

    // =========================================================
    // GENERAL
    // =========================================================

    private bool IsPlayerActionLocked()
    {
        return
            playerController != null &&
            playerController.IsMovementLocked;
    }

    private void OnDisable()
    {
        CancelCombat();
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