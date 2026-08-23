using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    // =========================================================
    // SINGLETON
    // =========================================================

    public static CheckpointManager Instance { get; private set; }

    // =========================================================
    // CHECKPOINT
    // =========================================================

    private Transform currentCheckpoint;

    public Transform CurrentCheckpoint =>
        currentCheckpoint;

    public bool HasCheckpoint =>
        currentCheckpoint != null;

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
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // =========================================================
    // CHECKPOINT MANAGEMENT
    // =========================================================

    public void SetCheckpoint(
        Transform checkpoint
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

        currentCheckpoint =
            checkpoint;

        Debug.Log(
            $"CheckpointManager: Checkpoint updated to " +
            $"{checkpoint.name}.",
            checkpoint
        );
    }

    public Vector3 GetRespawnPosition()
    {
        if (currentCheckpoint == null)
        {
            return Vector3.zero;
        }

        return currentCheckpoint.position;
    }

    public Quaternion GetRespawnRotation()
    {
        if (currentCheckpoint == null)
        {
            return Quaternion.identity;
        }

        return currentCheckpoint.rotation;
    }
}