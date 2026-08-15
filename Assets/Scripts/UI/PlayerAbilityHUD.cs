using UnityEngine;

public class PlayerAbilityHUD : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerWeaponManager weaponManager;
    [SerializeField] private PlayerStaffCombat staffCombat;
    [SerializeField] private PlayerShieldController shieldController;

    [Header("Staff Slot Objects")]
    [SerializeField] private GameObject lightningSlotObject;
    [SerializeField] private GameObject tornadoSlotObject;
    [SerializeField] private GameObject entangleSlotObject;

    [Header("Cooldown UI")]
    [SerializeField] private AbilityCooldownUI lightningCooldownUI;
    [SerializeField] private AbilityCooldownUI tornadoCooldownUI;
    [SerializeField] private AbilityCooldownUI entangleCooldownUI;
    [SerializeField] private AbilityCooldownUI shieldCooldownUI;

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        ValidateReferences();

        if (weaponManager != null)
        {
            weaponManager.OnStaffUnlocked +=
                HandleStaffUnlocked;
        }

        UpdateStaffVisibility();
        UpdateAllCooldowns();
    }

    private void Update()
    {
        UpdateShieldCooldown();

        if (
            weaponManager != null &&
            weaponManager.HasStaff
        )
        {
            UpdateStaffCooldowns();
        }
    }

    private void HandleStaffUnlocked()
    {
        UpdateStaffVisibility();
        UpdateStaffCooldowns();
    }

    private void UpdateStaffVisibility()
    {
        bool showStaffSlots =
            weaponManager != null &&
            weaponManager.HasStaff;

        if (lightningSlotObject != null)
        {
            lightningSlotObject.SetActive(
                showStaffSlots
            );
        }

        if (tornadoSlotObject != null)
        {
            tornadoSlotObject.SetActive(
                showStaffSlots
            );
        }

        if (entangleSlotObject != null)
        {
            entangleSlotObject.SetActive(
                showStaffSlots
            );
        }
    }

    private void UpdateAllCooldowns()
    {
        UpdateShieldCooldown();

        if (
            weaponManager != null &&
            weaponManager.HasStaff
        )
        {
            UpdateStaffCooldowns();
        }
    }

    private void UpdateStaffCooldowns()
    {
        if (staffCombat == null)
        {
            return;
        }

        UpdateStaffCooldown(
            lightningCooldownUI,
            PlayerStaffCombat.StaffSpell.LightningStrike
        );

        UpdateStaffCooldown(
            tornadoCooldownUI,
            PlayerStaffCombat.StaffSpell.IceTornado
        );

        UpdateStaffCooldown(
            entangleCooldownUI,
            PlayerStaffCombat.StaffSpell.Entangle
        );
    }

    private void UpdateStaffCooldown(
        AbilityCooldownUI cooldownUI,
        PlayerStaffCombat.StaffSpell spell
    )
    {
        if (cooldownUI == null)
        {
            return;
        }

        float remainingCooldown =
            staffCombat.GetRemainingCooldown(
                spell
            );

        float totalCooldown =
            staffCombat.GetCooldownDuration(
                spell
            );

        cooldownUI.SetCooldown(
            remainingCooldown,
            totalCooldown
        );
    }

    private void UpdateShieldCooldown()
    {
        if (
            shieldController == null ||
            shieldCooldownUI == null
        )
        {
            return;
        }

        shieldCooldownUI.SetCooldown(
            shieldController.RemainingCooldown,
            shieldController.CooldownDuration
        );
    }

    private void FindReferences()
    {
        if (weaponManager == null)
        {
            weaponManager =
                FindAnyObjectByType<PlayerWeaponManager>();
        }

        if (staffCombat == null)
        {
            staffCombat =
                FindAnyObjectByType<PlayerStaffCombat>();
        }

        if (shieldController == null)
        {
            shieldController =
                FindAnyObjectByType<PlayerShieldController>();
        }
    }

    private void ValidateReferences()
    {
        if (weaponManager == null)
        {
            Debug.LogError(
                $"{name}: PlayerAbilityHUD could not find " +
                "PlayerWeaponManager.",
                this
            );
        }

        if (staffCombat == null)
        {
            Debug.LogWarning(
                $"{name}: PlayerAbilityHUD could not find " +
                "PlayerStaffCombat.",
                this
            );
        }

        if (shieldController == null)
        {
            Debug.LogWarning(
                $"{name}: PlayerAbilityHUD could not find " +
                "PlayerShieldController.",
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (weaponManager != null)
        {
            weaponManager.OnStaffUnlocked -=
                HandleStaffUnlocked;
        }
    }
}