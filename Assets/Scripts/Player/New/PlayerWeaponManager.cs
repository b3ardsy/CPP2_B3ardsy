using System;
using UnityEngine;

public class PlayerWeaponManager : MonoBehaviour
{
    [Header("Weapon Models")]
    [Tooltip("The Wand object attached to the player's hand.")]
    [SerializeField] private GameObject wandObject;

    [Tooltip("The Staff object attached to the player's other hand.")]
    [SerializeField] private GameObject staffObject;

    private bool hasWand = true;
    private bool hasStaff;

    public bool HasWand => hasWand;
    public bool HasStaff => hasStaff;

    /*
     * Useful later for HUD updates when the Staff is collected.
     */
    public event Action OnStaffUnlocked;

    private void Awake()
    {
        hasWand = true;
        hasStaff = false;

        ApplyWeaponVisibility();
        ValidateReferences();
    }

    public void UnlockStaff()
    {
        if (hasStaff)
        {
            return;
        }

        hasStaff = true;

        ApplyWeaponVisibility();

        OnStaffUnlocked?.Invoke();

        Debug.Log(
            $"{name}: Staff unlocked. Wand and Staff are now available.",
            this
        );
    }

    private void ApplyWeaponVisibility()
    {
        if (wandObject != null)
        {
            wandObject.SetActive(true);
        }

        if (staffObject != null)
        {
            staffObject.SetActive(hasStaff);
        }
    }

    private void ValidateReferences()
    {
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
}