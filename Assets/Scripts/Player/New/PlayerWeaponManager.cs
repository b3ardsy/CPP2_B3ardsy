using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeaponManager : MonoBehaviour
{
    public enum WeaponType
    {
        Wand,
        Staff
    }

    [Header("References")]
    [SerializeField] private PlayerCombatNew playerCombat;

    [Header("Weapon Models")]
    [Tooltip("The Wand object already attached to the player's hand.")]
    [SerializeField] private GameObject wandObject;

    [Tooltip("The Staff object already attached to the player's hand.")]
    [SerializeField] private GameObject staffObject;

    [Header("Starting Weapon")]
    [SerializeField]
    private WeaponType startingWeapon =
        WeaponType.Wand;

    private WeaponType currentWeapon;

    private bool hasWand = true;
    private bool hasStaff;

    public WeaponType CurrentWeapon =>
        currentWeapon;

    public bool HasWand =>
        hasWand;

    public bool HasStaff =>
        hasStaff;

    private void Awake()
    {
        FindReferences();
        ValidateReferences();

        currentWeapon =
            startingWeapon;

        /*
         * The player begins with the Wand.
         * The Staff cannot be equipped until picked up.
         */
        hasWand = true;
        hasStaff = false;

        EquipWeapon(
            currentWeapon
        );
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (
            Keyboard.current.xKey
                .wasPressedThisFrame
        )
        {
            TrySwapWeapon();
        }
    }

    private void FindReferences()
    {
        if (playerCombat == null)
        {
            playerCombat =
                GetComponent<PlayerCombatNew>();
        }
    }

    private void ValidateReferences()
    {
        if (playerCombat == null)
        {
            Debug.LogWarning(
                $"{name}: PlayerWeaponManager could not find " +
                "PlayerCombatNew.",
                this
            );
        }

        if (wandObject == null)
        {
            Debug.LogError(
                $"{name}: PlayerWeaponManager is missing the Wand object.",
                this
            );
        }

        if (staffObject == null)
        {
            Debug.LogError(
                $"{name}: PlayerWeaponManager is missing the Staff object.",
                this
            );
        }
    }

    public void UnlockStaff()
    {
        if (hasStaff)
        {
            return;
        }

        hasStaff = true;

        Debug.Log(
            $"{name}: Staff unlocked.",
            this
        );

        /*
         * Picking up the Staff immediately equips it.
         */
        EquipWeapon(
            WeaponType.Staff
        );
    }

    private void TrySwapWeapon()
    {
        /*
         * Prevent swapping during an active combat animation.
         */
        if (
            playerCombat != null &&
            playerCombat.IsCombatBusy
        )
        {
            return;
        }

        /*
         * There is nothing to swap to until
         * the Staff has been collected.
         */
        if (
            !hasWand ||
            !hasStaff
        )
        {
            return;
        }

        if (
            currentWeapon ==
            WeaponType.Wand
        )
        {
            EquipWeapon(
                WeaponType.Staff
            );

            return;
        }

        EquipWeapon(
            WeaponType.Wand
        );
    }

    private void EquipWeapon(
        WeaponType weapon
    )
    {
        /*
         * Prevent equipping a weapon that
         * has not been unlocked.
         */
        if (
            weapon == WeaponType.Staff &&
            !hasStaff
        )
        {
            weapon =
                WeaponType.Wand;
        }

        currentWeapon =
            weapon;

        if (wandObject != null)
        {
            wandObject.SetActive(
                currentWeapon ==
                WeaponType.Wand
            );
        }

        if (staffObject != null)
        {
            staffObject.SetActive(
                currentWeapon ==
                WeaponType.Staff
            );
        }

        Debug.Log(
            $"{name}: Equipped {currentWeapon}.",
            this
        );
    }
}