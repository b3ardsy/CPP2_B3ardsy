using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatNew : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement3DNew playerMovement;
    [SerializeField] private PlayerLockOn playerLockOn;
    [SerializeField] private PlayerWeaponManager playerWeaponManager;
    [SerializeField] private PlayerStaffCombat playerStaffCombat;
    [SerializeField] private PlayerShieldController playerShieldController;

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

    private bool isWandAttackInProgress;

    /*
     * Staff casts currently block movement and dodge.
     * Wand attacks and Shield activation do not.
     */
    public bool IsAttacking =>
        playerStaffCombat != null &&
        playerStaffCombat.IsCasting;

    /*
     * Shield does not count as combat busy.
     *
     * This allows the player to attack and eventually
     * swap weapons while the Shield is active.
     */
    public bool IsCombatBusy =>
        isWandAttackInProgress ||
        (
            playerStaffCombat != null &&
            playerStaffCombat.IsCasting
        );

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

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<PlayerLockOn>();
        }

        if (playerWeaponManager == null)
        {
            playerWeaponManager =
                GetComponent<PlayerWeaponManager>();
        }

        if (playerStaffCombat == null)
        {
            playerStaffCombat =
                GetComponent<PlayerStaffCombat>();
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

        if (playerWeaponManager == null)
        {
            Debug.LogError(
                $"{name}: PlayerCombatNew could not find " +
                "PlayerWeaponManager.",
                this
            );

            enabled = false;
            return;
        }

        if (playerStaffCombat == null)
        {
            Debug.LogWarning(
                $"{name}: PlayerCombatNew could not find " +
                "PlayerStaffCombat.",
                this
            );
        }

        if (playerShieldController == null)
        {
            Debug.LogWarning(
                $"{name}: PlayerCombatNew could not find " +
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

    private void Update()
    {
        HandleCombatCancellation();

        if (Keyboard.current != null)
        {
            HandleStaffSpellSelection();
        }

        if (Mouse.current == null)
        {
            return;
        }

        /*
         * RMB activates Shield regardless of whether
         * Wand or Staff is currently equipped.
         */
        if (
            Mouse.current.rightButton
                .wasPressedThisFrame
        )
        {
            TryActivateShield();
        }

        if (
            Mouse.current.leftButton
                .wasPressedThisFrame
        )
        {
            HandlePrimaryAttack();
        }
    }

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

    private void HandlePrimaryAttack()
    {
        if (IsPlayerActionLocked())
        {
            return;
        }

        switch (
            playerWeaponManager.CurrentWeapon
        )
        {
            case PlayerWeaponManager.WeaponType.Wand:
                TryFireWand();
                break;

            case PlayerWeaponManager.WeaponType.Staff:
                playerStaffCombat?.TryCastSelectedSpell();
                break;
        }
    }

    private void HandleStaffSpellSelection()
    {
        if (
            playerStaffCombat == null ||
            playerWeaponManager.CurrentWeapon !=
            PlayerWeaponManager.WeaponType.Staff
        )
        {
            return;
        }

        /*
         * Slot 1 is still the temporary placeholder
         * until Entangle is implemented next.
         */
        if (
            Keyboard.current.digit1Key
                .wasPressedThisFrame
        )
        {
            playerStaffCombat.SelectSpell(
                PlayerStaffCombat.StaffSpell.Flamethrower
            );

            return;
        }

        if (
            Keyboard.current.digit2Key
                .wasPressedThisFrame
        )
        {
            playerStaffCombat.SelectSpell(
                PlayerStaffCombat.StaffSpell.IceTornado
            );

            return;
        }

        if (
            Keyboard.current.digit3Key
                .wasPressedThisFrame
        )
        {
            playerStaffCombat.SelectSpell(
                PlayerStaffCombat.StaffSpell.LightningStrike
            );
        }
    }

    private void TryFireWand()
    {
        if (
            IsPlayerActionLocked() ||
            isWandAttackInProgress
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

        isWandAttackInProgress =
            true;

        PlayWandAttackAnimation();
    }

    /*
     * Animation Event on the Wand attack animation.
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

    private bool IsPlayerActionLocked()
    {
        return
            playerMovement != null &&
            playerMovement.IsMovementLocked;
    }

    private void OnDisable()
    {
        CancelWandAttack();

        if (playerStaffCombat != null)
        {
            playerStaffCombat.CancelStaffCast();
        }

        /*
         * Do NOT destroy the Shield here.
         *
         * PlayerCombatNew can temporarily be disabled by
         * hit reactions. The Shield is an independent system.
         */
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