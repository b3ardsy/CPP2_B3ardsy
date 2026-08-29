using UnityEngine;

[RequireComponent(typeof(PersistentID))]
public class CheckpointShrine :
    MonoBehaviour,
    IInteract
{
    // =========================================================
    // CHECKPOINT
    // =========================================================

    [Header("Checkpoint")]
    [Tooltip("Position where the player will respawn.")]
    [SerializeField]
    private Transform respawnPoint;

    private PersistentID persistentID;

    public string CheckpointId =>
        persistentID != null
            ? persistentID.ID
            : string.Empty;

    public Transform RespawnPoint =>
        respawnPoint != null
            ? respawnPoint
            : transform;

    // =========================================================
    // INTERACTION
    // =========================================================

    [Header("Interaction")]
    [Tooltip(
        "Trigger used to detect the player. Disabled once " +
        "this checkpoint has been activated."
    )]
    [SerializeField]
    private Collider interactionTrigger;

    // =========================================================
    // CANDLES
    // =========================================================

    [Header("Candle Effects")]
    [Tooltip(
        "Particle systems enabled when this shrine is activated."
    )]
    [SerializeField]
    private ParticleSystem[] candleParticles;

    // =========================================================
    // STATE
    // =========================================================

    private bool isActivated;

    public bool IsActivated =>
        isActivated;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        persistentID =
            GetComponent<PersistentID>();

        SetCandleEffects(
            false
        );

        if (interactionTrigger != null)
        {
            interactionTrigger.enabled =
                true;
        }
    }

    // =========================================================
    // INTERACTION
    // =========================================================

    public void Interact(
        PlayerInteraction interactor
    )
    {
        if (isActivated)
        {
            return;
        }

        if (CheckpointManager.Instance == null)
        {
            Debug.LogError(
                $"{name}: No CheckpointManager exists in the scene.",
                this
            );

            return;
        }

        Transform checkpointTransform =
            RespawnPoint;

        if (
            persistentID == null ||
            !persistentID.HasValidID
        )
        {
            Debug.LogError(
                $"{name}: CheckpointShrine requires a valid PersistentID.",
                this
            );

            return;
        }

        if (interactor == null)
        {
            Debug.LogError(
                $"{name}: Checkpoint interaction requires " +
                "a valid PlayerInteraction.",
                this
            );

            return;
        }

        Health playerHealth =
            interactor.GetComponentInParent<Health>();

        Player_WeaponManager weaponManager =
            interactor.GetComponentInParent
                <Player_WeaponManager>();

        Player_StaffCombat staffCombat =
            interactor.GetComponentInParent
                <Player_StaffCombat>();

        if (
            playerHealth == null ||
            weaponManager == null ||
            staffCombat == null
        )
        {
            Debug.LogError(
                $"{name}: Could not find all required player " +
                "checkpoint systems.",
                this
            );

            return;
        }

        CheckpointManager.Instance.SetCheckpoint(
            persistentID.ID,
            checkpointTransform,
            playerHealth,
            weaponManager,
            staffCombat
        );

        isActivated =
            true;

        SetCandleEffects(
            true
        );

        /*
         * Clear the player's current interaction before
         * disabling this shrine's trigger.
         */
        if (interactor != null)
        {
            interactor.ClearCurrentInteractable();
        }

        /*
         * Once activated, this shrine no longer needs to
         * offer interaction.
         */
        if (interactionTrigger != null)
        {
            interactionTrigger.enabled =
                false;
        }

        Debug.Log(
            $"{name}: Checkpoint activated.",
            this
        );
    }

    // =========================================================
    // SAVE / LOAD RESTORE
    // =========================================================

    /*
     * Restores the shrine's activated presentation without
     * capturing a new checkpoint snapshot.
     *
     * Used when a saved active checkpoint is reconstructed.
     */
    public void RestoreActivatedState(
        bool activated
    )
    {
        isActivated =
            activated;

        SetCandleEffects(
            activated
        );

        if (interactionTrigger != null)
        {
            interactionTrigger.enabled =
                !activated;
        }
    }

    // =========================================================
    // CANDLE EFFECTS
    // =========================================================

    private void SetCandleEffects(
        bool active
    )
    {
        if (candleParticles == null)
        {
            return;
        }

        foreach (
            ParticleSystem candleParticle
            in candleParticles
        )
        {
            if (candleParticle == null)
            {
                continue;
            }

            if (active)
            {
                candleParticle.Play(
                    true
                );
            }
            else
            {
                candleParticle.Stop(
                    true,
                    ParticleSystemStopBehavior
                        .StopEmittingAndClear
                );
            }
        }
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        if (persistentID == null)
        {
            persistentID =
                GetComponent<PersistentID>();
        }

        if (interactionTrigger == null)
        {
            interactionTrigger =
                GetComponentInChildren<Collider>();
        }
    }
}