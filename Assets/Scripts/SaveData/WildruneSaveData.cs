using System;
using System.Collections.Generic;
using System.Xml.Serialization;

[Serializable]
[XmlRoot("WildruneSave")]
public class WildruneSaveData
{
    // =========================================================
    // SAVE METADATA
    // =========================================================

    public int saveVersion = 1;

    public string sceneName =
        string.Empty;

    /*
     * Stored as an ISO-8601 UTC string when the save is written.
     * Example:
     * 2026-08-29T13:45:00.0000000Z
     */
    public string savedUtc =
        string.Empty;

    // =========================================================
    // MANUAL SAVE STATE
    // =========================================================

    /*
     * Exact state used when loading a manual save slot.
     */
    public PlayerSaveData player =
        new PlayerSaveData();

    public ManualWorldSaveData world =
        new ManualWorldSaveData();

    // =========================================================
    // ACTIVE CHECKPOINT SNAPSHOT
    // =========================================================

    /*
     * This is intentionally separate from the manual-save state.
     *
     * Loading a save restores the exact manual-save state first.
     * If the player later dies, this snapshot is what the
     * CheckpointManager must rebuild and roll back to.
     */
    public CheckpointSaveData checkpoint =
        new CheckpointSaveData();
}


// =============================================================
// PLAYER - MANUAL SAVE
// =============================================================

[Serializable]
public class PlayerSaveData
{
    public SerializableVector3 position =
        new SerializableVector3();

    public SerializableQuaternion rotation =
        new SerializableQuaternion();

    public int currentHealth;

    public int maxHealth;

    public PlayerProgressionSaveData progression =
        new PlayerProgressionSaveData();
}


// =============================================================
// PLAYER - PROGRESSION
// =============================================================

[Serializable]
public class PlayerProgressionSaveData
{
    public bool hasStaff;

    public bool lightningUnlocked;

    public bool iceTornadoUnlocked;

    public bool entangleUnlocked;
}


// =============================================================
// MANUAL WORLD STATE
// =============================================================

[Serializable]
public class ManualWorldSaveData
{
    /*
     * Enemies need richer data for manual saves because loading
     * a save should restore their exact meaningful state.
     */
    [XmlArray("Enemies")]
    [XmlArrayItem("Enemy")]
    public List<EnemySaveData> enemies =
        new List<EnemySaveData>();

    /*
     * Authored pickups/runes/etc. currently only need to preserve
     * whether they were available at the moment of the save.
     */
    [XmlArray("WorldObjects")]
    [XmlArrayItem("WorldObject")]
    public List<WorldObjectSaveData> worldObjects =
        new List<WorldObjectSaveData>();
}


[Serializable]
public class EnemySaveData
{
    public string persistentId =
        string.Empty;

    public bool isDead;

    public int currentHealth;

    public int maxHealth;

    public SerializableVector3 position =
        new SerializableVector3();

    public SerializableQuaternion rotation =
        new SerializableQuaternion();

    /*
     * We deliberately do NOT save:
     *
     * - current attack animation frame
     * - cooldown timers
     * - NavMesh path
     * - entangle timer
     * - current target
     *
     * Those are transient runtime details. On load the enemy will
     * resume from a neutral AI state using the meaningful saved
     * position/rotation/health/death state above.
     */
}


[Serializable]
public class WorldObjectSaveData
{
    public string persistentId =
        string.Empty;

    /*
     * Examples:
     *
     * true  = Rune still exists / Heart still exists /
     *         Staff pickup still exists.
     *
     * false = object had already been collected/consumed.
     */
    public bool available;
}


// =============================================================
// CHECKPOINT SNAPSHOT
// =============================================================

[Serializable]
public class CheckpointSaveData
{
    /*
     * Empty means no checkpoint had been activated when the
     * manual save was written.
     */
    public string checkpointId =
        string.Empty;

    public bool hasCheckpoint;

    /*
     * State captured at the moment the active shrine was touched.
     */
    public CheckpointPlayerSaveData player =
        new CheckpointPlayerSaveData();

    /*
     * Checkpoint rollback intentionally stores the simpler
     * "available at checkpoint?" state.
     *
     * For enemies:
     * true  = alive at checkpoint -> reset full/home on death.
     * false = already dead at checkpoint -> remain dead.
     *
     * For authored collectibles:
     * true  = existed at checkpoint.
     * false = already collected at checkpoint.
     */
    [XmlArray("WorldStates")]
    [XmlArrayItem("WorldState")]
    public List<CheckpointWorldStateSaveData> worldStates =
        new List<CheckpointWorldStateSaveData>();
}


[Serializable]
public class CheckpointPlayerSaveData
{
    public int currentHealth;

    public int maxHealth;

    public PlayerProgressionSaveData progression =
        new PlayerProgressionSaveData();
}


[Serializable]
public class CheckpointWorldStateSaveData
{
    public string persistentId =
        string.Empty;

    public bool available;
}


// =============================================================
// XML-FRIENDLY TRANSFORM TYPES
// =============================================================

/*
 * These intentionally avoid serializing UnityEngine.Vector3 and
 * UnityEngine.Quaternion directly. The XML save format therefore
 * contains only simple numeric data and does not depend on Unity's
 * internal serialization behavior.
 */

[Serializable]
public class SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3()
    {
    }

    public SerializableVector3(
        float xValue,
        float yValue,
        float zValue
    )
    {
        x = xValue;
        y = yValue;
        z = zValue;
    }
}


[Serializable]
public class SerializableQuaternion
{
    public float x;
    public float y;
    public float z;
    public float w = 1f;

    public SerializableQuaternion()
    {
    }

    public SerializableQuaternion(
        float xValue,
        float yValue,
        float zValue,
        float wValue
    )
    {
        x = xValue;
        y = yValue;
        z = zValue;
        w = wValue;
    }
}
