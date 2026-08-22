using UnityEngine;

public class PlayerShieldController : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField]
    private PlayerDamageController playerDamageController;

    [SerializeField]
    private PlayerMovement3DNew playerMovement;

    [SerializeField]
    private PlayerWeaponManager playerWeaponManager;

    // =========================================================
    // SHIELD
    // =========================================================

    [Header("Shield")]
    [Tooltip(
        "Shield effect spawned around the player."
    )]
    [SerializeField]
    private PlayerShieldEffect shieldPrefab;

    [Tooltip(
        "Local position offset used to center the Shield " +
        "around the player's body."
    )]
    [SerializeField]
    private Vector3 shieldLocalOffset =
        new Vector3(0f, 0.8f, 0f);

    [Tooltip(
        "Maximum time the Shield remains active."
    )]
    [SerializeField]
    private float shieldDuration = 4f;

    [Tooltip(
        "Additional cooldown time after the Shield ends."
    )]
    [SerializeField]
    private float shieldCooldown = 5f;

    // =========================================================
    // PROGRESSION
    // =========================================================

    [Header("Progression")]
    [Tooltip(
        "If true, the Shield starts unlocked immediately. " +
        "Normally this should remain false."
    )]
    [SerializeField]
    private bool startShieldUnlocked;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private PlayerShieldEffect activeShield;

    /*
     * Absolute time when the Shield can be used again.
     *
     * This includes:
     * Shield active duration + post-Shield cooldown.
     */
    private float nextShieldReadyTime;

    private bool isShieldUnlocked;

    // =========================================================
    // PUBLIC PROPERTIES
    // =========================================================

    public bool IsShieldActive =>
        activeShield != null;

    public bool IsShieldReady =>
        isShieldUnlocked &&
        !IsShieldActive &&
        Time.time >= nextShieldReadyTime;

    public bool IsShieldUnlocked =>
        isShieldUnlocked;

    /*
     * Total remaining time until the Shield can be used again.
     *
     * This includes the active Shield duration.
     */
    public float RemainingCooldown =>
        Mathf.Max(
            0f,
            nextShieldReadyTime - Time.time
        );

    /*
     * Total duration represented by the HUD cooldown.
     *
     * Example:
     * 4 second Shield
     * + 5 second cooldown
     * = 9 second total HUD sweep.
     */
    public float CooldownDuration =>
        shieldDuration +
        shieldCooldown;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        FindReferences();
        ValidateReferences();

        /*
         * Shield can begin unlocked for testing, or if the
         * player already has the Staff when this object starts.
         */
        isShieldUnlocked =
            startShieldUnlocked ||
            (
                playerWeaponManager != null &&
                playerWeaponManager.HasStaff
            );
    }

    private void OnEnable()
    {
        SubscribeToWeaponEvents();
    }

    private void Start()
    {
        /*
         * OnEnable can occur before all references have been
         * established depending on object lifecycle/order.
         *
         * Calling this again is safe because we unsubscribe first.
         */
        SubscribeToWeaponEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromWeaponEvents();
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void FindReferences()
    {
        if (playerDamageController == null)
        {
            playerDamageController =
                GetComponent<PlayerDamageController>();
        }

        if (playerDamageController == null)
        {
            playerDamageController =
                GetComponentInParent<PlayerDamageController>();
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
    }

    private void ValidateReferences()
    {
        if (playerDamageController == null)
        {
            Debug.LogError(
                $"{name}: PlayerShieldController could not find " +
                "PlayerDamageController.",
                this
            );

            enabled =
                false;

            return;
        }

        if (playerWeaponManager == null)
        {
            Debug.LogError(
                $"{name}: PlayerShieldController could not find " +
                "PlayerWeaponManager.",
                this
            );

            enabled =
                false;

            return;
        }

        if (shieldPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Shield Prefab has not been assigned.",
                this
            );
        }
    }

    // =========================================================
    // WEAPON EVENTS
    // =========================================================

    private void SubscribeToWeaponEvents()
    {
        if (playerWeaponManager == null)
        {
            return;
        }

        /*
         * Prevent duplicate subscriptions.
         */
        playerWeaponManager.OnStaffUnlocked -=
            UnlockShield;

        playerWeaponManager.OnStaffUnlocked +=
            UnlockShield;
    }

    private void UnsubscribeFromWeaponEvents()
    {
        if (playerWeaponManager == null)
        {
            return;
        }

        playerWeaponManager.OnStaffUnlocked -=
            UnlockShield;
    }

    private void UnlockShield()
    {
        if (isShieldUnlocked)
        {
            return;
        }

        isShieldUnlocked =
            true;

        Debug.Log(
            $"{name}: Shield unlocked.",
            this
        );
    }

    // =========================================================
    // ACTIVATION
    // =========================================================

    public bool TryActivateShield()
    {
        if (!isShieldUnlocked)
        {
            Debug.Log(
                $"{name}: Shield is locked. Collect the Staff first.",
                this
            );

            return false;
        }

        if (playerDamageController == null)
        {
            return false;
        }

        if (playerDamageController.IsDead)
        {
            return false;
        }

        if (
            playerMovement != null &&
            playerMovement.IsMovementLocked
        )
        {
            return false;
        }

        /*
         * Cannot activate another Shield while one
         * is already active.
         */
        if (IsShieldActive)
        {
            return false;
        }

        /*
         * Shield is still within its active/cooldown cycle.
         */
        if (!IsShieldReady)
        {
            Debug.Log(
                $"{name}: Shield is unavailable for " +
                $"{RemainingCooldown:0.0} more seconds.",
                this
            );

            return false;
        }

        if (shieldPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Cannot activate Shield because " +
                "no Shield Prefab is assigned.",
                this
            );

            return false;
        }

        SpawnShield();

        return true;
    }

    // =========================================================
    // SPAWNING
    // =========================================================

    private void SpawnShield()
    {
        /*
         * Start the entire Shield availability timer
         * immediately when the Shield is activated.
         *
         * The HUD can now begin its cooldown sweep right away.
         */
        nextShieldReadyTime =
            Time.time +
            shieldDuration +
            shieldCooldown;

        Quaternion shieldRotation =
            transform.rotation *
            shieldPrefab.transform.rotation;

        activeShield =
            Instantiate(
                shieldPrefab,
                transform.position,
                shieldRotation,
                transform
            );

        activeShield.transform.localPosition =
            shieldLocalOffset;

        activeShield.Initialize(
            playerDamageController,
            shieldDuration,
            HandleShieldEnded
        );

        Debug.Log(
            $"{name}: Shield activated. " +
            $"Ready again in {CooldownDuration:0.0} seconds.",
            this
        );
    }

    // =========================================================
    // SHIELD END
    // =========================================================

    private void HandleShieldEnded(
        PlayerShieldEffect endedShield
    )
    {
        /*
         * Ignore callbacks from an old Shield if one
         * somehow ends after another has been created.
         */
        if (
            activeShield != null &&
            activeShield != endedShield
        )
        {
            return;
        }

        activeShield =
            null;

        /*
         * Do not start a new timer here.
         *
         * The full active-duration + cooldown timer was
         * already started when the Shield was activated.
         */
        Debug.Log(
            $"{name}: Shield ended. " +
            $"Remaining cooldown: {RemainingCooldown:0.0} seconds.",
            this
        );
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        shieldDuration =
            Mathf.Max(
                0.1f,
                shieldDuration
            );

        shieldCooldown =
            Mathf.Max(
                0f,
                shieldCooldown
            );
    }
}