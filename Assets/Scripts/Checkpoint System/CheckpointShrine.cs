using UnityEngine;

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
            respawnPoint != null
                ? respawnPoint
                : transform;

        CheckpointManager.Instance.SetCheckpoint(
            checkpointTransform
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
        if (interactionTrigger == null)
        {
            interactionTrigger =
                GetComponentInChildren<Collider>();
        }
    }
}