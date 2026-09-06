using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Player_DamageController))]
[RequireComponent(typeof(Player_Controller))]
public class Player_RespawnController : MonoBehaviour
{
    // =========================================================
    // TIMING
    // =========================================================

    [Header("Respawn Timing")]
    [Tooltip(
        "How long the death animation is allowed to play before " +
        "checkpoint restoration begins."
    )]
    [SerializeField]
    private float deathAnimationDelay = 2f;

    [Tooltip(
        "How long controls remain locked after the respawn " +
        "animation is triggered."
    )]
    [SerializeField]
    private float respawnControlDelay = 1f;

    // =========================================================
    // RESPAWN PRESENTATION
    // =========================================================

    [Header("Respawn Presentation")]
    [Tooltip(
        "Optional effect spawned at the player when checkpoint " +
        "restoration completes. Assign SpawnNovaGreen here."
    )]
    [SerializeField]
    private GameObject respawnEffectPrefab;

    [SerializeField]
    private GameObject respawnEffectPrefab2;

    [SerializeField]
    private GameObject respawnEffectPrefab3;

    [SerializeField]
    private Vector3 respawnEffectOffset =
        Vector3.zero;

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField]
    private Health health;

    [SerializeField]
    private Player_DamageController damageController;

    [SerializeField]
    private Player_Controller playerController;

    [SerializeField]
    private Player_Combat playerCombat;

    [SerializeField]
    private Player_Dodge playerDodge;

    [SerializeField]
    private Player_LockOn playerLockOn;

    [SerializeField]
    private Player_ShieldController shieldController;

    [SerializeField]
    private Player_StaffCombat staffCombat;

    [SerializeField]
    private Player_WeaponManager weaponManager;

    [SerializeField]
    private Animator animator;

    private CheckpointManager checkpointManager;

    // =========================================================
    // RUNTIME
    // =========================================================

    private Coroutine respawnCoroutine;
    private bool deathEventSubscribed;

    private static readonly int DeathTrigger =
        Animator.StringToHash("Death");

    private static readonly int RespawnTrigger =
        Animator.StringToHash("respawn");

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        FindReferences();
        ValidateReferences();
    }

    private void Start()
    {
        FindCheckpointManager();

        if (checkpointManager == null)
        {
            Debug.LogWarning(
                $"{name}: Player_RespawnController could not find " +
                "CheckpointManager. Legacy Game Over will remain active.",
                this
            );

            return;
        }

        checkpointManager.OnCheckpointCaptured -=
            HandleCheckpointCaptured;

        checkpointManager.OnCheckpointCaptured +=
            HandleCheckpointCaptured;

        /*
         * This also supports a checkpoint that was captured before
         * this component finished Start().
         */
        if (checkpointManager.HasCheckpoint)
        {
            SubscribeToPlayerDeath();
        }
    }

    // =========================================================
    // CHECKPOINT OWNERSHIP
    // =========================================================

    private void HandleCheckpointCaptured()
    {
        SubscribeToPlayerDeath();
    }

    private void SubscribeToPlayerDeath()
    {
        if (
            deathEventSubscribed ||
            damageController == null
        )
        {
            return;
        }

        damageController.OnPlayerDied +=
            HandlePlayerDied;

        deathEventSubscribed =
            true;

        Debug.Log(
            $"{name}: Checkpoint respawning is now active.",
            this
        );
    }

    // =========================================================
    // DEATH / RESPAWN
    // =========================================================

    private void HandlePlayerDied()
    {
        if (
            checkpointManager == null ||
            !checkpointManager.HasCheckpoint ||
            respawnCoroutine != null
        )
        {
            return;
        }

        /*
         * Player_DamageController has already stopped the player's
         * normal actions. Add our own movement lock as well so
         * ownership remains explicit throughout the respawn.
         */
        playerController.AddMovementLock(
            this
        );

        respawnCoroutine =
            StartCoroutine(
                RespawnSequence()
            );
    }

    private IEnumerator RespawnSequence()
    {
        yield return new WaitForSeconds(
            deathAnimationDelay
        );

        CheckpointManager.PlayerCheckpointState state =
            checkpointManager.CurrentPlayerState;

        // -----------------------------------------------------
        // RESET TEMPORARY PLAYER STATE
        // -----------------------------------------------------

        if (playerLockOn != null)
        {
            playerLockOn.ResetForRespawn();
        }

        if (playerCombat != null)
        {
            playerCombat.ResetForRespawn();
        }

        if (playerDodge != null)
        {
            playerDodge.ResetForRespawn();
        }

        if (staffCombat != null)
        {
            staffCombat.ResetForRespawn();
        }

        if (weaponManager != null)
        {
            weaponManager.ResetForRespawn();
        }

        if (playerController != null)
        {
            playerController.ResetForRespawn();
        }

        // -----------------------------------------------------
        // RESTORE WORLD + PLAYER PROGRESSION
        // -----------------------------------------------------

        checkpointManager.RestoreWorldStates();

        if (weaponManager != null)
        {
            weaponManager.RestoreProgressionState(
                state.weaponProgression
            );
        }

        if (staffCombat != null)
        {
            staffCombat.RestoreProgressionState(
                state.spellProgression
            );
        }

        /*
         * Shield availability derives from restored Staff ownership,
         * so reset Shield after weapon progression has been restored.
         */
        if (shieldController != null)
        {
            shieldController.ResetForRespawn();
        }

        // -----------------------------------------------------
        // PLACE + REVIVE PLAYER
        // -----------------------------------------------------

        playerController.Teleport(
            state.position,
            state.rotation
        );

        health.RestoreHealthState(
            state.currentHealth,
            state.maxHealth
        );

        /*
         * DamageController owns the remaining damage/death runtime
         * flags and removes only its own movement lock.
         */
        damageController.ResetForRespawn();

        // -----------------------------------------------------
        // RESPAWN PRESENTATION
        // -----------------------------------------------------

        PlayRespawnPresentation();

        /*
         * Combat/Dodge/LockOn were disabled by the death sequence.
         * Keep them disabled while the respawn animation begins.
         */
        yield return new WaitForSeconds(
            respawnControlDelay
        );

        if (playerCombat != null)
        {
            playerCombat.enabled =
                true;
        }

        if (playerDodge != null)
        {
            playerDodge.enabled =
                true;
        }

        if (playerLockOn != null)
        {
            playerLockOn.enabled =
                true;
        }

        playerController.RemoveMovementLock(
            this
        );

        respawnCoroutine =
            null;

        Debug.Log(
            $"{name}: Player respawn complete. " +
            $"Health={health.CurrentHealth}/{health.MaxHealth}.",
            this
        );
    }

    private void PlayRespawnPresentation()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(
                SoundId.PlayerRespawn,
                transform.position
            );
        }

        if (animator != null)
        {
            animator.ResetTrigger(
                DeathTrigger
            );

            animator.ResetTrigger(
                RespawnTrigger
            );

            animator.SetTrigger(
                RespawnTrigger
            );
        }

        SpawnRespawnEffect(
            respawnEffectPrefab
        );

        SpawnRespawnEffect(
            respawnEffectPrefab2
        );

        SpawnRespawnEffect(
            respawnEffectPrefab3
        );
    }

    private void SpawnRespawnEffect(
        GameObject effectPrefab
    )
    {
        if (effectPrefab == null)
        {
            return;
        }

        Instantiate(
            effectPrefab,
            transform.position +
                respawnEffectOffset,
            effectPrefab.transform.rotation
        );
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void FindReferences()
    {
        if (health == null)
        {
            health =
                GetComponent<Health>();
        }

        if (damageController == null)
        {
            damageController =
                GetComponent<Player_DamageController>();
        }

        if (playerController == null)
        {
            playerController =
                GetComponent<Player_Controller>();
        }

        if (playerCombat == null)
        {
            playerCombat =
                GetComponent<Player_Combat>();
        }

        if (playerDodge == null)
        {
            playerDodge =
                GetComponent<Player_Dodge>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<Player_LockOn>();
        }

        if (shieldController == null)
        {
            shieldController =
                GetComponent<Player_ShieldController>();
        }

        if (staffCombat == null)
        {
            staffCombat =
                GetComponent<Player_StaffCombat>();
        }

        if (weaponManager == null)
        {
            weaponManager =
                GetComponent<Player_WeaponManager>();
        }

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }
    }

    private void FindCheckpointManager()
    {
        checkpointManager =
            CheckpointManager.Instance;

        if (checkpointManager == null)
        {
            checkpointManager =
                FindAnyObjectByType<CheckpointManager>();
        }
    }

    private void ValidateReferences()
    {
        if (health == null)
        {
            Debug.LogError(
                $"{name}: Player_RespawnController requires Health.",
                this
            );
        }

        if (damageController == null)
        {
            Debug.LogError(
                $"{name}: Player_RespawnController requires " +
                "Player_DamageController.",
                this
            );
        }

        if (playerController == null)
        {
            Debug.LogError(
                $"{name}: Player_RespawnController requires " +
                "Player_Controller.",
                this
            );
        }

        if (weaponManager == null)
        {
            Debug.LogWarning(
                $"{name}: Player_WeaponManager was not found. " +
                "Weapon checkpoint progression will not restore.",
                this
            );
        }

        if (staffCombat == null)
        {
            Debug.LogWarning(
                $"{name}: Player_StaffCombat was not found. " +
                "Spell checkpoint progression will not restore.",
                this
            );
        }

        if (animator == null)
        {
            Debug.LogWarning(
                $"{name}: Animator was not found. Respawn animation " +
                "will not be triggered.",
                this
            );
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDisable()
    {
        if (checkpointManager != null)
        {
            checkpointManager.OnCheckpointCaptured -=
                HandleCheckpointCaptured;
        }

        if (
            damageController != null &&
            deathEventSubscribed
        )
        {
            damageController.OnPlayerDied -=
                HandlePlayerDied;

            deathEventSubscribed =
                false;
        }

        if (
            playerController != null &&
            respawnCoroutine != null
        )
        {
            playerController.RemoveMovementLock(
                this
            );
        }
    }

    private void OnValidate()
    {
        deathAnimationDelay =
            Mathf.Max(
                0f,
                deathAnimationDelay
            );

        respawnControlDelay =
            Mathf.Max(
                0f,
                respawnControlDelay
            );
    }
}