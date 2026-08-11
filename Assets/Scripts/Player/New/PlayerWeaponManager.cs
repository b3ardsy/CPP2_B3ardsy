using System;
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
    [Tooltip("The Wand object attached to the player's hand.")]
    [SerializeField] private GameObject wandObject;

    [Tooltip("The Staff object attached to the player's hand.")]
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

    /*
     * UI and other systems can listen for weapon swaps later.
     */
    public event Action<WeaponType> OnWeaponChanged;

    private void Awake()
    {
        FindReferences();
        ValidateReferences();

        hasWand = true;
        hasStaff = false;

        EquipWeapon(
            startingWeapon
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
                $"{name}: PlayerWeaponManager is missing " +
                "the Wand object.",
                this
            );
        }

        if (staffObject == null)
        {
            Debug.LogError(
                $"{name}: PlayerWeaponManager is missing " +
                "the Staff object.",
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

        EquipWeapon(
            WeaponType.Staff
        );
    }

    private void TrySwapWeapon()
    {
        if (
            playerCombat != null &&
            playerCombat.IsCombatBusy
        )
        {
            return;
        }

        if (
            !hasWand ||
            !hasStaff
        )
        {
            return;
        }

        WeaponType nextWeapon =
            currentWeapon ==
            WeaponType.Wand
                ? WeaponType.Staff
                : WeaponType.Wand;

        EquipWeapon(
            nextWeapon
        );
    }

    private void EquipWeapon(
        WeaponType weapon
    )
    {
        if (
            weapon == WeaponType.Staff &&
            !hasStaff
        )
        {
            weapon =
                WeaponType.Wand;
        }

        bool weaponChanged =
            currentWeapon != weapon;

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

        if (weaponChanged)
        {
            OnWeaponChanged?.Invoke(
                currentWeapon
            );
        }

        Debug.Log(
            $"{name}: Equipped {currentWeapon}.",
            this
        );
    }
}