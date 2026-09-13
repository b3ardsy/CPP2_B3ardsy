using UnityEngine;

public class Player_ShieldController : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField]
    private Player_DamageController playerDamageController;

    [SerializeField]
    private Player_Controller playerController;

    [SerializeField]
    private Player_WeaponManager playerWeaponManager;

    // =========================================================
    // SHIELD
    // =========================================================

    [Header("Shield")]
    [Tooltip(
        "Shield effect spawned around the player."
    )]
    [SerializeField]
    private Player_ShieldEffect shieldPrefab;

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

    private Player_ShieldEffect activeShield;

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

    public float RemainingCooldown =>
        Mathf.Max(
            0f,
            nextShieldReadyTime - Time.time
        );

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
                GetComponent<Player_DamageController>();
        }

        if (playerDamageController == null)
        {
            playerDamageController =
                GetComponentInParent<Player_DamageController>();
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

        if (playerWeaponManager == null)
        {
            playerWeaponManager =
                GetComponent<Player_WeaponManager>();
        }

        if (playerWeaponManager == null)
        {
            playerWeaponManager =
                GetComponentInParent<Player_WeaponManager>();
        }
    }

    private void ValidateReferences()
    {
        if (playerDamageController == null)
        {
            Debug.LogError(
                $"{name}: Player_ShieldController could not find " +
                "Player_DamageController.",
                this
            );

            enabled = false;
            return;
        }

        if (playerController == null)
        {
            Debug.LogWarning(
                $"{name}: Player_ShieldController could not find " +
                "Player_Controller.",
                this
            );
        }

        if (playerWeaponManager == null)
        {
            Debug.LogError(
                $"{name}: Player_ShieldController could not find " +
                "Player_WeaponManager.",
                this
            );

            enabled = false;
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
            playerController != null &&
            playerController.IsMovementLocked
        )
        {
            return false;
        }

        if (IsShieldActive)
        {
            return false;
        }

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

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(
                SoundId.Shield,
                activeShield.transform.position
            );
        }

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
        Player_ShieldEffect endedShield
    )
    {
        if (
            activeShield != null &&
            activeShield != endedShield
        )
        {
            return;
        }

        activeShield =
            null;

        Debug.Log(
            $"{name}: Shield ended. " +
            $"Remaining cooldown: {RemainingCooldown:0.0} seconds.",
            this
        );
    }

    // =========================================================
    // RESPAWN RESET
    // =========================================================

    /*
     * Returns Shield to a neutral runtime state.
     *
     * Shield unlock progression is derived from Staff ownership
     * and is intentionally not reset here.
     */
    public void ResetForRespawn()
    {
        if (activeShield != null)
        {
            /*
             * EndShield() removes damage protection, notifies this
             * controller through HandleShieldEnded(), and destroys
             * the spawned Shield effect cleanly.
             */
            activeShield.EndShield();
            activeShield = null;
        }

        nextShieldReadyTime = 0f;

        /*
         * Re-derive unlock state from its actual progression source
         * instead of checkpointing a second copy of the same state.
         */
        isShieldUnlocked =
            startShieldUnlocked ||
            (
                playerWeaponManager != null &&
                playerWeaponManager.HasStaff
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
