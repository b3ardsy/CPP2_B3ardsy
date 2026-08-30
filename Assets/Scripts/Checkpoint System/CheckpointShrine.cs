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
        "Trigger used to detect the player. Shrines remain " +
        "interactable after they have been attuned."
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
        if (interactor == null)
        {
            Debug.LogError(
                $"{name}: Checkpoint interaction requires " +
                "a valid PlayerInteraction.",
                this
            );

            return;
        }

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

        if (CheckpointManager.Instance == null)
        {
            Debug.LogError(
                $"{name}: No CheckpointManager exists in the scene.",
                this
            );

            return;
        }

        ShrineSaveUIController shrineSaveUI =
            ShrineSaveUIController.Instance;

        if (shrineSaveUI == null)
        {
            shrineSaveUI =
                FindAnyObjectByType<ShrineSaveUIController>();
        }

        if (shrineSaveUI == null)
        {
            Debug.LogError(
                $"{name}: No ShrineSaveUIController exists in the scene.",
                this
            );

            return;
        }

        shrineSaveUI.Open(
            this,
            interactor
        );
    }

    /*
     * Called only after the player has confirmed a save slot.
     *
     * The shrine captures the CURRENT player/world state and becomes
     * the active runtime respawn checkpoint. Saving to disk is owned
     * by ShrineSaveUIController + SaveGameManager.
     */
    public bool Attune(
        PlayerInteraction interactor
    )
    {
        if (
            interactor == null ||
            CheckpointManager.Instance == null ||
            persistentID == null ||
            !persistentID.HasValidID
        )
        {
            return false;
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

            return false;
        }

        CheckpointManager.Instance.SetCheckpoint(
            persistentID.ID,
            RespawnPoint,
            playerHealth,
            weaponManager,
            staffCombat
        );

        Debug.Log(
            $"{name}: Shrine attuned.",
            this
        );

        return true;
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
                true;
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