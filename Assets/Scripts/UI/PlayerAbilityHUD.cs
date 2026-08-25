using UnityEngine;

public class PlayerAbilityHUD : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private Player_WeaponManager weaponManager;
    [SerializeField] private Player_StaffCombat staffCombat;

    /*
     * Shield remains legacy until Player_ShieldController
     * is migrated.
     */
    [SerializeField] private Player_ShieldController shieldController;

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

    [Header("Unlock Notifications")]
    [SerializeField] private HUDNotificationBanner notificationBanner;

    [TextArea]
    [SerializeField]
    private string lightningUnlockMessage =
        "The winds call to you: Lightning Strike Unlocked";

    [TextArea]
    [SerializeField]
    private string tornadoUnlockMessage =
        "The storms call to you: Ice Tornado Unlocked";

    [TextArea]
    [SerializeField]
    private string entangleUnlockMessage =
        "The earth calls to you: Entangle Unlocked";

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
        Player_StaffCombat.StaffSpell spell
    )
    {
        UpdateSpellVisibility(spell);
        UpdateStaffCooldown(spell);

        ShowSpellUnlockNotification(spell);
    }

    // =========================================================
    // NOTIFICATIONS
    // =========================================================

    private void ShowSpellUnlockNotification(
        Player_StaffCombat.StaffSpell spell
    )
    {
        if (notificationBanner == null)
        {
            return;
        }

        string message;

        switch (spell)
        {
            case Player_StaffCombat.StaffSpell.LightningStrike:
                message =
                    lightningUnlockMessage;
                break;

            case Player_StaffCombat.StaffSpell.IceTornado:
                message =
                    tornadoUnlockMessage;
                break;

            case Player_StaffCombat.StaffSpell.Entangle:
                message =
                    entangleUnlockMessage;
                break;

            default:
                return;
        }

        notificationBanner.ShowMessage(
            message
        );
    }

    // =========================================================
    // VISIBILITY
    // =========================================================

    private void UpdateAllVisibility()
    {
        UpdateShieldVisibility();

        UpdateSpellVisibility(
            Player_StaffCombat.StaffSpell.LightningStrike
        );

        UpdateSpellVisibility(
            Player_StaffCombat.StaffSpell.IceTornado
        );

        UpdateSpellVisibility(
            Player_StaffCombat.StaffSpell.Entangle
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
        Player_StaffCombat.StaffSpell spell
    )
    {
        GameObject slotObject =
            GetSlotObject(spell);

        if (slotObject == null)
        {
            return;
        }

        /*
         * Fail safely.
         *
         * If Staff Combat is unavailable, the HUD should
         * never imply that a spell has been unlocked.
         */
        if (staffCombat == null)
        {
            slotObject.SetActive(false);
            return;
        }

        bool unlocked =
            staffCombat.IsSpellUnlocked(spell);

        slotObject.SetActive(
            unlocked
        );
    }

    private GameObject GetSlotObject(
        Player_StaffCombat.StaffSpell spell
    )
    {
        switch (spell)
        {
            case Player_StaffCombat.StaffSpell.LightningStrike:
                return lightningSlotObject;

            case Player_StaffCombat.StaffSpell.IceTornado:
                return tornadoSlotObject;

            case Player_StaffCombat.StaffSpell.Entangle:
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
                Player_StaffCombat.StaffSpell.LightningStrike
            )
        )
        {
            UpdateStaffCooldown(
                Player_StaffCombat.StaffSpell.LightningStrike
            );
        }

        if (
            staffCombat.IsSpellUnlocked(
                Player_StaffCombat.StaffSpell.IceTornado
            )
        )
        {
            UpdateStaffCooldown(
                Player_StaffCombat.StaffSpell.IceTornado
            );
        }

        if (
            staffCombat.IsSpellUnlocked(
                Player_StaffCombat.StaffSpell.Entangle
            )
        )
        {
            UpdateStaffCooldown(
                Player_StaffCombat.StaffSpell.Entangle
            );
        }
    }

    private void UpdateStaffCooldown(
        Player_StaffCombat.StaffSpell spell
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
        Player_StaffCombat.StaffSpell spell
    )
    {
        switch (spell)
        {
            case Player_StaffCombat.StaffSpell.LightningStrike:
                return lightningCooldownUI;

            case Player_StaffCombat.StaffSpell.IceTornado:
                return tornadoCooldownUI;

            case Player_StaffCombat.StaffSpell.Entangle:
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
                FindAnyObjectByType<Player_WeaponManager>();
        }

        if (staffCombat == null)
        {
            staffCombat =
                FindAnyObjectByType<Player_StaffCombat>();
        }

        if (shieldController == null)
        {
            shieldController =
                FindAnyObjectByType<Player_ShieldController>();
        }

        if (notificationBanner == null)
        {
            notificationBanner =
                FindAnyObjectByType<HUDNotificationBanner>();
        }
    }

    private void ValidateReferences()
    {
        if (weaponManager == null)
        {
            Debug.LogError(
                $"{name}: PlayerAbilityHUD could not find " +
                "Player_WeaponManager.",
                this
            );
        }

        if (staffCombat == null)
        {
            Debug.LogWarning(
                $"{name}: PlayerAbilityHUD could not find " +
                "Player_StaffCombat.",
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

        if (notificationBanner == null)
        {
            Debug.LogWarning(
                $"{name}: HUD Notification Banner has not been assigned.",
                this
            );
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
}