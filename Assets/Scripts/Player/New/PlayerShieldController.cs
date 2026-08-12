using UnityEngine;

public class PlayerShieldController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStatsNew playerStats;
    [SerializeField] private PlayerMovement3DNew playerMovement;

    [Header("Shield")]
    [Tooltip("Shield effect spawned around the player.")]
    [SerializeField] private PlayerShieldEffect shieldPrefab;

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
    [SerializeField] private float shieldDuration = 4f;

    [Tooltip(
        "Cooldown that begins once the Shield ends."
    )]
    [SerializeField] private float shieldCooldown = 5f;

    private PlayerShieldEffect activeShield;

    private float nextShieldReadyTime;

    public bool IsShieldActive =>
        activeShield != null;

    public bool IsShieldReady =>
        !IsShieldActive &&
        Time.time >= nextShieldReadyTime;

    public float RemainingCooldown =>
        Mathf.Max(
            0f,
            nextShieldReadyTime - Time.time
        );

    private void Awake()
    {
        FindReferences();
        ValidateReferences();
    }

    private void FindReferences()
    {
        if (playerStats == null)
        {
            playerStats =
                GetComponent<PlayerStatsNew>();
        }

        if (playerStats == null)
        {
            playerStats =
                GetComponentInParent<PlayerStatsNew>();
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
    }

    private void ValidateReferences()
    {
        if (playerStats == null)
        {
            Debug.LogError(
                $"{name}: PlayerShieldController could not find " +
                "PlayerStatsNew.",
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

    public bool TryActivateShield()
    {
        if (playerStats == null)
        {
            return false;
        }

        if (playerStats.IsDead)
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
         * Shield is still cooling down.
         */
        if (!IsShieldReady)
        {
            Debug.Log(
                $"{name}: Shield is on cooldown for " +
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

    private void SpawnShield()
    {
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
            playerStats,
            shieldDuration,
            HandleShieldEnded
        );

        Debug.Log(
            $"{name}: Shield activated.",
            this
        );
    }

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

        activeShield = null;

        nextShieldReadyTime =
            Time.time +
            shieldCooldown;

        Debug.Log(
            $"{name}: Shield ended. " +
            $"Cooldown started for {shieldCooldown:0.0} seconds.",
            this
        );
    }

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