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


    public CheckpointSaveData CapturePersistentCheckpointData()
    {
        CheckpointSaveData data =
            new CheckpointSaveData();

        data.hasCheckpoint =
            HasCheckpoint;

        if (!HasCheckpoint)
        {
            return data;
        }

        data.checkpointId =
            currentCheckpointId;

        data.player.currentHealth =
            playerCheckpointState.currentHealth;

        data.player.maxHealth =
            playerCheckpointState.maxHealth;

        data.player.progression.hasStaff =
            playerCheckpointState
                .weaponProgression
                .hasStaff;

        data.player.progression.lightningUnlocked =
            playerCheckpointState
                .spellProgression
                .lightningUnlocked;

        data.player.progression.iceTornadoUnlocked =
            playerCheckpointState
                .spellProgression
                .iceTornadoUnlocked;

        data.player.progression.entangleUnlocked =
            playerCheckpointState
                .spellProgression
                .entangleUnlocked;

        foreach (
            WorldCheckpointState state
            in worldCheckpointStates
        )
        {
            if (state.component == null)
            {
                continue;
            }

            PersistentID persistentID =
                state.component.GetComponent<PersistentID>();

            if (
                persistentID == null ||
                !persistentID.HasValidID
            )
            {
                continue;
            }

            data.worldStates.Add(
                new CheckpointWorldStateSaveData
                {
                    persistentId =
                        persistentID.ID,

                    available =
                        state.wasAvailable
                }
            );
        }

        return data;
    }

    // =========================================================
    // PERSISTENT CHECKPOINT RECONSTRUCTION
    // =========================================================

    /*
     * Rebuilds the runtime checkpoint snapshot from XML-friendly
     * save data.
     *
     * No disk/XML work happens here. This class only reconstructs
     * the runtime state it already owns.
     */
    public bool RestorePersistentCheckpointData(
        CheckpointSaveData data
    )
    {
        if (
            data == null ||
            !data.hasCheckpoint
        )
        {
            ClearCheckpoint();

            return true;
        }

        if (
            string.IsNullOrWhiteSpace(
                data.checkpointId
            )
        )
        {
            Debug.LogError(
                "CheckpointManager: Saved checkpoint has no ID.",
                this
            );

            return false;
        }

        PersistentID[] persistentObjects =
            FindObjectsByType<PersistentID>(
                FindObjectsInactive.Include
            );

        System.Collections.Generic.Dictionary<string, PersistentID>
            objectsById =
                new System.Collections.Generic.Dictionary<string, PersistentID>();

        foreach (
            PersistentID persistentObject
            in persistentObjects
        )
        {
            if (
                persistentObject == null ||
                !persistentObject.HasValidID ||
                objectsById.ContainsKey(
                    persistentObject.ID
                )
            )
            {
                continue;
            }

            objectsById.Add(
                persistentObject.ID,
                persistentObject
            );
        }

        if (
            !objectsById.TryGetValue(
                data.checkpointId,
                out PersistentID shrinePersistentId
            )
        )
        {
            Debug.LogError(
                "CheckpointManager: Saved checkpoint shrine could not " +
                $"be found: {data.checkpointId}",
                this
            );

            return false;
        }

        CheckpointShrine shrine =
            shrinePersistentId
                .GetComponent<CheckpointShrine>();

        if (shrine == null)
        {
            Debug.LogError(
                "CheckpointManager: Persistent checkpoint ID does not " +
                $"belong to a CheckpointShrine: {data.checkpointId}",
                shrinePersistentId
            );

            return false;
        }

        currentCheckpoint =
            shrine.RespawnPoint;

        currentCheckpointId =
            data.checkpointId;

        playerCheckpointState =
            new PlayerCheckpointState
            {
                position =
                    shrine.RespawnPoint.position,

                rotation =
                    shrine.RespawnPoint.rotation,

                currentHealth =
                    data.player.currentHealth,

                maxHealth =
                    data.player.maxHealth,

                weaponProgression =
                    new Player_WeaponManager
                        .WeaponProgressionState(
                            data.player
                                .progression
                                .hasStaff
                        ),

                spellProgression =
                    new Player_StaffCombat
                        .SpellProgressionState(
                            data.player
                                .progression
                                .lightningUnlocked,

                            data.player
                                .progression
                                .iceTornadoUnlocked,

                            data.player
                                .progression
                                .entangleUnlocked
                        )
            };

        worldCheckpointStates.Clear();

        int missingWorldStates =
            0;

        foreach (
            CheckpointWorldStateSaveData savedState
            in data.worldStates
        )
        {
            if (
                savedState == null ||
                string.IsNullOrWhiteSpace(
                    savedState.persistentId
                ) ||
                !objectsById.TryGetValue(
                    savedState.persistentId,
                    out PersistentID persistentObject
                )
            )
            {
                missingWorldStates++;

                continue;
            }

            MonoBehaviour resettableComponent =
                FindCheckpointResettableComponent(
                    persistentObject
                );

            if (resettableComponent == null)
            {
                missingWorldStates++;

                continue;
            }

            worldCheckpointStates.Add(
                new WorldCheckpointState(
                    resettableComponent,
                    savedState.available
                )
            );
        }

        /*
         * A freshly loaded scene starts with all shrines unlit.
         * Mark the saved active shrine as activated without
         * capturing a new snapshot.
         */
        UpdateShrineVisuals(
            data.checkpointId
        );

        /*
         * Player_RespawnController listens for this event.
         * Raising it here gives a loaded checkpoint the same runtime
         * death ownership as one activated normally during gameplay.
         */
        OnCheckpointCaptured?.Invoke();

        Debug.Log(
            "CheckpointManager: Persistent checkpoint restored.\n" +
            $"Checkpoint={currentCheckpointId}\n" +
            $"Health={playerCheckpointState.currentHealth}/" +
            $"{playerCheckpointState.maxHealth}\n" +
            $"WorldStates={worldCheckpointStates.Count}\n" +
            $"MissingWorldStates={missingWorldStates}",
            shrine
        );

        return missingWorldStates == 0;
    }

    private MonoBehaviour FindCheckpointResettableComponent(
        PersistentID persistentObject
    )
    {
        if (persistentObject == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours =
            persistentObject
                .GetComponents<MonoBehaviour>();

        foreach (
            MonoBehaviour behaviour
            in behaviours
        )
        {
            if (
                behaviour != null &&
                behaviour is ICheckpointResettable
            )
            {
                return behaviour;
            }
        }

        return null;
    }

    public void ClearCheckpoint()
    {
        currentCheckpoint =
            null;

        currentCheckpointId =
            string.Empty;

        playerCheckpointState =
            default;

        worldCheckpointStates.Clear();

        UpdateShrineVisuals(
            string.Empty
        );
    }

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

        UpdateShrineVisuals(
            checkpointId
        );

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

    private void UpdateShrineVisuals(
        string activeCheckpointId
    )
    {
        CheckpointShrine[] shrines =
            FindObjectsByType<CheckpointShrine>(
                FindObjectsInactive.Include
            );

        foreach (
            CheckpointShrine shrine
            in shrines
        )
        {
            if (shrine == null)
            {
                continue;
            }

            bool isActiveShrine =
                !string.IsNullOrWhiteSpace(
                    activeCheckpointId
                ) &&
                shrine.CheckpointId ==
                    activeCheckpointId;

            shrine.RestoreActivatedState(
                isActiveShrine
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