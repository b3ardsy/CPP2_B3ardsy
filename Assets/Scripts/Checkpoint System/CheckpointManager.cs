using System;
using System.Collections.Generic;
using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    // =========================================================
    // SINGLETON
    // =========================================================

    public static CheckpointManager Instance { get; private set; }

    // =========================================================
    // CHECKPOINT DATA
    // =========================================================

    [Serializable]
    public struct PlayerCheckpointState
    {
        public Vector3 position;
        public Quaternion rotation;
        public int currentHealth;
        public int maxHealth;

        public Player_WeaponManager.WeaponProgressionState
            weaponProgression;

        public Player_StaffCombat.SpellProgressionState
            spellProgression;
    }

    [Serializable]
    private struct WorldCheckpointState
    {
        public MonoBehaviour component;
        public bool wasAvailable;

        public WorldCheckpointState(
            MonoBehaviour worldComponent,
            bool available
        )
        {
            component = worldComponent;
            wasAvailable = available;
        }
    }

    private Transform currentCheckpoint;
    private string currentCheckpointId;
    private PlayerCheckpointState playerCheckpointState;

    private readonly List<WorldCheckpointState>
        worldCheckpointStates =
            new List<WorldCheckpointState>();

    public Transform CurrentCheckpoint =>
        currentCheckpoint;

    public string CurrentCheckpointId =>
        currentCheckpointId;

    public bool HasCheckpoint =>
        currentCheckpoint != null;

    public PlayerCheckpointState CurrentPlayerState =>
        playerCheckpointState;

    /*
     * Raised after a complete player/world checkpoint snapshot
     * has been captured successfully.
     */
    public event Action OnCheckpointCaptured;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (
            Instance != null &&
            Instance != this
        )
        {
            Destroy(
                gameObject
            );

            return;
        }

        Instance =
            this;
    }

    // =========================================================
    // CHECKPOINT CAPTURE
    // =========================================================

    public void SetCheckpoint(
        string checkpointId,
        Transform checkpoint,
        Health playerHealth,
        Player_WeaponManager weaponManager,
        Player_StaffCombat staffCombat
    )
    {
        if (checkpoint == null)
        {
            Debug.LogWarning(
                "CheckpointManager: Cannot set a null checkpoint.",
                this
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            Debug.LogError(
                "CheckpointManager: Cannot capture a checkpoint " +
                "without a persistent checkpoint ID.",
                this
            );

            return;
        }

        if (
            playerHealth == null ||
            weaponManager == null ||
            staffCombat == null
        )
        {
            Debug.LogError(
                "CheckpointManager: Cannot capture checkpoint because " +
                "one or more required player systems are missing.",
                this
            );

            return;
        }

        currentCheckpoint =
            checkpoint;

        currentCheckpointId =
            checkpointId;

        playerCheckpointState =
            new PlayerCheckpointState
            {
                position =
                    checkpoint.position,

                rotation =
                    checkpoint.rotation,

                currentHealth =
                    playerHealth.CurrentHealth,

                maxHealth =
                    playerHealth.MaxHealth,

                weaponProgression =
                    weaponManager
                        .CaptureProgressionState(),

                spellProgression =
                    staffCombat
                        .CaptureProgressionState()
            };

        CaptureWorldStates();

        OnCheckpointCaptured?.Invoke();

        Debug.Log(
            $"CheckpointManager: Checkpoint updated to " +
            $"{checkpoint.name} " +
            $"[{currentCheckpointId}]. " +
            $"Health={playerCheckpointState.currentHealth}, " +
            $"WorldObjectsTracked={worldCheckpointStates.Count}.",
            checkpoint
        );
    }

    private void CaptureWorldStates()
    {
        worldCheckpointStates.Clear();

        MonoBehaviour[] behaviours =
            FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include
            );

        foreach (
            MonoBehaviour behaviour
            in behaviours
        )
        {
            if (
                behaviour == null ||
                behaviour is not ICheckpointResettable resettable
            )
            {
                continue;
            }

            worldCheckpointStates.Add(
                new WorldCheckpointState(
                    behaviour,
                    resettable.IsCheckpointAvailable
                )
            );
        }
    }

    // =========================================================
    // PLAYER CHECKPOINT ACCESS
    // =========================================================

    public Vector3 GetRespawnPosition()
    {
        return HasCheckpoint
            ? playerCheckpointState.position
            : Vector3.zero;
    }

    public Quaternion GetRespawnRotation()
    {
        return HasCheckpoint
            ? playerCheckpointState.rotation
            : Quaternion.identity;
    }

    // =========================================================
    // WORLD RESTORE
    // =========================================================

    /*
     * Restores only enemy state here.
     *
     * Player restoration is intentionally handled by the future
     * Player_RespawnController so the checkpoint manager remains
     * the snapshot owner rather than the respawn orchestrator.
     */
    public void RestoreWorldStates()
    {
        if (!HasCheckpoint)
        {
            return;
        }

        foreach (
            WorldCheckpointState state
            in worldCheckpointStates
        )
        {
            if (
                state.component == null ||
                state.component is not
                    ICheckpointResettable resettable
            )
            {
                continue;
            }

            resettable.RestoreCheckpointState(
                state.wasAvailable
            );
        }
    }

}