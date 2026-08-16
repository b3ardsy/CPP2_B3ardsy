using UnityEngine;

public class PlayerAbilityHUD : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerWeaponManager weaponManager;
    [SerializeField] private PlayerStaffCombat staffCombat;
    [SerializeField] private PlayerShieldController shieldController;

    [Header("Ability Slot Objects")]
    [SerializeField] private GameObject shieldSlotObject;
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

        SubscribeToEvents();

        UpdateAllVisibility();
        UpdateAllCooldowns();
    }

    private void Update()
    {
        UpdateShieldCooldown();

        if (staffCombat == null)
        {
            return;
        }

        UpdateUnlockedStaffCooldowns();
    }

    // =========================================================
    // EVENTS
    // =========================================================

    private void SubscribeToEvents()
    {
        if (weaponManager != null)
        {
            weaponManager.OnStaffUnlocked +=
                HandleStaffUnlocked;
        }

        if (staffCombat != null)
        {
            staffCombat.OnSpellUnlocked +=
                HandleSpellUnlocked;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (weaponManager != null)
        {
            weaponManager.OnStaffUnlocked -=
                HandleStaffUnlocked;
        }

        if (staffCombat != null)
        {
            staffCombat.OnSpellUnlocked -=
                HandleSpellUnlocked;
        }
    }

    private void HandleStaffUnlocked()
    {
        /*
         * Staff collection unlocks Shield.
         *
         * Refresh the full HUD because the Shield controller
         * updates its progression state from the same event.
         */
        UpdateAllVisibility();
        UpdateShieldCooldown();
    }

    private void HandleSpellUnlocked(
        PlayerStaffCombat.StaffSpell spell
    )
    {
        UpdateSpellVisibility(spell);
        UpdateStaffCooldown(spell);
    }

    // =========================================================
    // VISIBILITY
    // =========================================================

    private void UpdateAllVisibility()
    {
        UpdateShieldVisibility();

        UpdateSpellVisibility(
            PlayerStaffCombat.StaffSpell.LightningStrike
        );

        UpdateSpellVisibility(
            PlayerStaffCombat.StaffSpell.IceTornado
        );

        UpdateSpellVisibility(
            PlayerStaffCombat.StaffSpell.Entangle
        );
    }

    private void UpdateShieldVisibility()
    {
        if (shieldSlotObject == null)
        {
            return;
        }

        bool showShield =
            shieldController != null &&
            shieldController.IsShieldUnlocked;

        shieldSlotObject.SetActive(
            showShield
        );
    }

    private void UpdateSpellVisibility(
        PlayerStaffCombat.StaffSpell spell
    )
    {
        if (staffCombat == null)
        {
            return;
        }

        bool unlocked =
            staffCombat.IsSpellUnlocked(spell);

        GameObject slotObject =
            GetSlotObject(spell);

        if (slotObject == null)
        {
            return;
        }

        slotObject.SetActive(
            unlocked
        );
    }

    private GameObject GetSlotObject(
        PlayerStaffCombat.StaffSpell spell
    )
    {
        switch (spell)
        {
            case PlayerStaffCombat.StaffSpell.LightningStrike:
                return lightningSlotObject;

            case PlayerStaffCombat.StaffSpell.IceTornado:
                return tornadoSlotObject;

            case PlayerStaffCombat.StaffSpell.Entangle:
                return entangleSlotObject;

            default:
                return null;
        }
    }

    // =========================================================
    // COOLDOWNS
    // =========================================================

    private void UpdateAllCooldowns()
    {
        UpdateShieldCooldown();
        UpdateUnlockedStaffCooldowns();
    }

    private void UpdateUnlockedStaffCooldowns()
    {
        if (staffCombat == null)
        {
            return;
        }

        if (
            staffCombat.IsSpellUnlocked(
                PlayerStaffCombat.StaffSpell.LightningStrike
            )
        )
        {
            UpdateStaffCooldown(
                PlayerStaffCombat.StaffSpell.LightningStrike
            );
        }

        if (
            staffCombat.IsSpellUnlocked(
                PlayerStaffCombat.StaffSpell.IceTornado
            )
        )
        {
            UpdateStaffCooldown(
                PlayerStaffCombat.StaffSpell.IceTornado
            );
        }

        if (
            staffCombat.IsSpellUnlocked(
                PlayerStaffCombat.StaffSpell.Entangle
            )
        )
        {
            UpdateStaffCooldown(
                PlayerStaffCombat.StaffSpell.Entangle
            );
        }
    }

    private void UpdateStaffCooldown(
        PlayerStaffCombat.StaffSpell spell
    )
    {
        if (staffCombat == null)
        {
            return;
        }

        AbilityCooldownUI cooldownUI =
            GetCooldownUI(spell);

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

    private AbilityCooldownUI GetCooldownUI(
        PlayerStaffCombat.StaffSpell spell
    )
    {
        switch (spell)
        {
            case PlayerStaffCombat.StaffSpell.LightningStrike:
                return lightningCooldownUI;

            case PlayerStaffCombat.StaffSpell.IceTornado:
                return tornadoCooldownUI;

            case PlayerStaffCombat.StaffSpell.Entangle:
                return entangleCooldownUI;

            default:
                return null;
        }
    }

    private void UpdateShieldCooldown()
    {
        if (
            shieldController == null ||
            shieldCooldownUI == null ||
            !shieldController.IsShieldUnlocked
        )
        {
            return;
        }

        shieldCooldownUI.SetCooldown(
            shieldController.RemainingCooldown,
            shieldController.CooldownDuration
        );
    }

    // =========================================================
    // REFERENCES
    // =========================================================

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

        if (shieldSlotObject == null)
        {
            Debug.LogWarning(
                $"{name}: Shield Slot Object has not been assigned.",
                this
            );
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
}