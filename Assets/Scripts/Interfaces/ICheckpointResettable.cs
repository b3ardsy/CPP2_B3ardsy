public interface ICheckpointResettable
{
    /*
     * True when this world object should exist/be available
     * in the currently captured world state.
     */
    bool IsCheckpointAvailable { get; }

    /*
     * Restores this object to the availability state it had
     * when the checkpoint was captured.
     */
    void RestoreCheckpointState(
        bool wasAvailable
    );
}