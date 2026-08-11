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

    [Header("Wand")]
    [Tooltip("Projectile used by the currently equipped wand.")]
    [SerializeField] private PlayerSpellProjectile wandProjectilePrefab;

    [Tooltip("Projectile spawn point used for one-handed wand attacks.")]
    [SerializeField] private Transform wandFirePoint;

    [SerializeField] private int wandDamage = 1;
    [SerializeField] private float wandProjectileSpeed = 12f;

    [Header("Aiming")]
    [Tooltip(
        "When not locked on, Wand projectiles travel " +
        "in the direction the player is facing."
    )]
    [SerializeField]
    private bool usePlayerForwardWhenUnlocked = true;

    private bool isWandAttackInProgress;

    /*
     * Used by movement and dodge.
     *
     * Wand attacks do not restrict movement.
     * Staff casting currently does.
     */
    public bool IsAttacking =>
        playerStaffCombat != null &&
        playerStaffCombat.IsCasting;

    /*
     * Used for things such as weapon swapping.
     *
     * Both Wand and Staff animation states count
     * as combat being busy.
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

        if (playerWeaponManager == null)
        {
            playerWeaponManager =
                GetComponent<PlayerWeaponManager>();
        }

        if (playerWeaponManager == null)
        {
            playerWeaponManager =
                GetComponentInParent<PlayerWeaponManager>();
        }

        if (playerStaffCombat == null)
        {
            playerStaffCombat =
                GetComponent<PlayerStaffCombat>();
        }

        if (playerStaffCombat == null)
        {
            playerStaffCombat =
                GetComponentInParent<PlayerStaffCombat>();
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
         * Cancel the Wand attack if the player becomes
         * trapped, hit, or otherwise action locked.
         */
        if (
            isWandAttackInProgress &&
            IsPlayerActionLocked()
        )
        {
            CancelWandAttack();
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        HandleStaffSpellSelection();

        if (Mouse.current == null)
        {
            return;
        }

        if (
            Mouse.current.leftButton
                .wasPressedThisFrame
        )
        {
            HandlePrimaryAttack();
        }
    }

    private void HandlePrimaryAttack()
    {
        if (IsPlayerActionLocked())
        {
            return;
        }

        if (
            playerWeaponManager.CurrentWeapon ==
            PlayerWeaponManager.WeaponType.Wand
        )
        {
            TryFireWand();
            return;
        }

        if (
            playerWeaponManager.CurrentWeapon ==
            PlayerWeaponManager.WeaponType.Staff
        )
        {
            if (playerStaffCombat == null)
            {
                return;
            }

            playerStaffCombat.TryCastSelectedSpell();
        }
    }

    private void HandleStaffSpellSelection()
    {
        if (playerStaffCombat == null)
        {
            return;
        }

        if (
            playerWeaponManager.CurrentWeapon !=
            PlayerWeaponManager.WeaponType.Staff
        )
        {
            return;
        }

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

            return;
        }

        if (
            Keyboard.current.digit4Key
                .wasPressedThisFrame
        )
        {
            playerStaffCombat.SelectSpell(
                PlayerStaffCombat.StaffSpell.Shield
            );
        }
    }

    private void TryFireWand()
    {
        if (IsPlayerActionLocked())
        {
            return;
        }

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
     * Wand animation event.
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
     * Wand animation event.
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

        if (playerStaffCombat != null)
        {
            playerStaffCombat.CancelStaffCast();
        }
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