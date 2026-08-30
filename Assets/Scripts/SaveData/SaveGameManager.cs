using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveGameManager : MonoBehaviour
{
    // =========================================================
    // SAVE SLOTS
    // =========================================================

    private const int MinimumSlot = 1;
    private const int MaximumSlot = 3;

    private const string SaveFilePrefix =
        "wildrune_slot_";

    private const string SaveFileExtension =
        ".sav";

    // =========================================================
    // ENCRYPTION
    // =========================================================

    /*
     * XML is serialized in memory first, then encrypted before
     * anything is written to disk.
     *
     * AES handles reversible encryption.
     * SHA-256 derives separate encryption/integrity keys.
     * HMAC-SHA256 detects corruption/tampering before decryption.
     *
     * This is appropriate for the assignment/save-obfuscation use
     * case. A secret embedded in a shipped client is not equivalent
     * to server-side key security.
     */
    private const string SaveSecret =
        "Wildrune_CPP2_SaveData_2026";

    private static readonly byte[] FileMagic =
        Encoding.ASCII.GetBytes(
            "WRN1"
        );

    private const int IvLength = 16;
    private const int HmacLength = 32;

    // =========================================================
    // PUBLIC SLOT API
    // =========================================================

    public bool SaveGame(
        int slot
    )
    {
        if (!ValidateSlot(slot))
        {
            return false;
        }

        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SaveGameManager: SaveGame must be called in Play Mode.",
                this
            );

            return false;
        }

        if (!TryBuildCurrentSaveData(
                out WildruneSaveData saveData
            ))
        {
            return false;
        }

        try
        {
            byte[] xmlBytes =
                SerializeToXmlBytes(
                    saveData
                );

            byte[] encryptedBytes =
                EncryptSaveBytes(
                    xmlBytes
                );

            string path =
                GetSlotPath(
                    slot
                );

            File.WriteAllBytes(
                path,
                encryptedBytes
            );

            Debug.Log(
                $"SaveGameManager: Saved Slot {slot}.\n" +
                $"Path: {path}\n" +
                $"Scene={saveData.sceneName}\n" +
                $"Health={saveData.player.currentHealth}/" +
                $"{saveData.player.maxHealth}",
                this
            );

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"SaveGameManager: Failed to save Slot {slot}.\n" +
                exception,
                this
            );

            return false;
        }
    }

    public bool HasSave(
        int slot
    )
    {
        if (!IsSlotNumberValid(slot))
        {
            return false;
        }

        return File.Exists(
            GetSlotPath(slot)
        );
    }

    public bool DeleteSave(
        int slot
    )
    {
        if (!ValidateSlot(slot))
        {
            return false;
        }

        string path =
            GetSlotPath(
                slot
            );

        if (!File.Exists(path))
        {
            return true;
        }

        try
        {
            File.Delete(
                path
            );

            Debug.Log(
                $"SaveGameManager: Deleted Slot {slot}.",
                this
            );

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"SaveGameManager: Failed to delete Slot {slot}.\n" +
                exception,
                this
            );

            return false;
        }
    }

    /*
     * Reads/decrypts/deserializes a slot without changing gameplay.
     *
     * GameSessionManager will use this later for Main Menu loading
     * before it transitions to the saved scene.
     */
    public bool TryReadSaveSlot(
        int slot,
        out WildruneSaveData saveData
    )
    {
        saveData =
            null;

        if (!ValidateSlot(slot))
        {
            return false;
        }

        string path =
            GetSlotPath(
                slot
            );

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            byte[] encryptedBytes =
                File.ReadAllBytes(
                    path
                );

            byte[] xmlBytes =
                DecryptSaveBytes(
                    encryptedBytes
                );

            saveData =
                DeserializeXmlBytes(
                    xmlBytes
                );

            return saveData != null;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"SaveGameManager: Failed to read Slot {slot}.\n" +
                exception.Message,
                this
            );

            saveData =
                null;

            return false;
        }
    }

    /*
     * Restores a previously read save into the CURRENTLY LOADED
     * gameplay scene.
     *
     * Scene transitions remain a GameSessionManager responsibility.
     */
    public bool RestoreSaveData(
        WildruneSaveData saveData
    )
    {
        if (
            saveData == null ||
            !Application.isPlaying
        )
        {
            return false;
        }

        string activeSceneName =
            SceneManager
                .GetActiveScene()
                .name;

        if (
            !string.IsNullOrWhiteSpace(
                saveData.sceneName
            ) &&
            saveData.sceneName !=
                activeSceneName
        )
        {
            Debug.LogError(
                "SaveGameManager: Cannot restore save for scene " +
                $"'{saveData.sceneName}' while '{activeSceneName}' " +
                "is loaded.",
                this
            );

            return false;
        }

        Dictionary<string, PersistentID> objectsById =
            BuildPersistentIdLookup();

        bool playerRestored =
            RestorePlayer(
                saveData.player
            );

        bool worldRestored =
            RestoreAuthoredWorld(
                saveData.world,
                objectsById
            );

        bool enemiesRestored =
            RestoreEnemies(
                saveData.world,
                objectsById
            );

        bool checkpointRestored =
            RestoreCheckpoint(
                saveData.checkpoint
            );

        bool success =
            playerRestored &&
            worldRestored &&
            enemiesRestored &&
            checkpointRestored;

        Debug.Log(
            "SaveGameManager: Full save restore " +
            $"{(success ? "PASSED" : "completed with errors")}.",
            this
        );

        return success;
    }

    /*
     * Convenience method for testing while already inside the saved
     * gameplay scene. Main Menu will use GameSessionManager instead.
     */
    public bool LoadGameInCurrentScene(
        int slot
    )
    {
        if (
            !TryReadSaveSlot(
                slot,
                out WildruneSaveData saveData
            )
        )
        {
            return false;
        }

        return RestoreSaveData(
            saveData
        );
    }

    // =========================================================
    // SLOT METADATA
    // =========================================================

    public bool TryGetSlotSummary(
        int slot,
        out string sceneName,
        out string savedUtc,
        out int currentHealth,
        out int maxHealth
    )
    {
        sceneName =
            string.Empty;

        savedUtc =
            string.Empty;

        currentHealth =
            0;

        maxHealth =
            0;

        if (
            !TryReadSaveSlot(
                slot,
                out WildruneSaveData saveData
            )
        )
        {
            return false;
        }

        sceneName =
            saveData.sceneName;

        savedUtc =
            saveData.savedUtc;

        currentHealth =
            saveData.player.currentHealth;

        maxHealth =
            saveData.player.maxHealth;

        return true;
    }

    public string GetSlotPath(
        int slot
    )
    {
        return Path.Combine(
            Application.persistentDataPath,
            SaveFilePrefix +
            slot +
            SaveFileExtension
        );
    }

    // =========================================================
    // TEMPORARY INSPECTOR TEST COMMANDS
    // =========================================================

    [ContextMenu("TEST - Save Slot 1")]
    private void TestSaveSlot1()
    {
        SaveGame(1);
    }

    [ContextMenu("TEST - Load Slot 1 In Current Scene")]
    private void TestLoadSlot1()
    {
        LoadGameInCurrentScene(1);
    }

    [ContextMenu("TEST - Delete Slot 1")]
    private void TestDeleteSlot1()
    {
        DeleteSave(1);
    }

    // =========================================================
    // CAPTURE
    // =========================================================

    private bool TryBuildCurrentSaveData(
        out WildruneSaveData saveData
    )
    {
        saveData =
            new WildruneSaveData();

        Player_Controller player =
            FindAnyObjectByType<Player_Controller>();

        if (player == null)
        {
            Debug.LogError(
                "SaveGameManager: Could not find Player_Controller.",
                this
            );

            return false;
        }

        Health playerHealth =
            player.GetComponent<Health>();

        Player_WeaponManager weaponManager =
            player.GetComponent<Player_WeaponManager>();

        Player_StaffCombat staffCombat =
            player.GetComponent<Player_StaffCombat>();

        if (
            playerHealth == null ||
            weaponManager == null ||
            staffCombat == null
        )
        {
            Debug.LogError(
                "SaveGameManager: Player is missing required systems.",
                player
            );

            return false;
        }

        saveData.saveVersion =
            1;

        saveData.sceneName =
            SceneManager
                .GetActiveScene()
                .name;

        saveData.savedUtc =
            DateTime.UtcNow.ToString("O");

        CapturePlayer(
            saveData.player,
            player.transform,
            playerHealth,
            weaponManager,
            staffCombat
        );

        CaptureEnemies(
            saveData.world
        );

        CaptureWorldObjects(
            saveData.world
        );

        if (CheckpointManager.Instance != null)
        {
            saveData.checkpoint =
                CheckpointManager.Instance
                    .CapturePersistentCheckpointData();

            /*
             * Persistent saves are shrine-anchored.
             *
             * The player may be standing slightly beside the shrine
             * interaction trigger when saving, but loading the slot
             * should begin at the shrine's RespawnPoint.
             */
            if (saveData.checkpoint.hasCheckpoint)
            {
                CheckpointManager.PlayerCheckpointState
                    checkpointPlayerState =
                        CheckpointManager.Instance
                            .CurrentPlayerState;

                saveData.player.position =
                    ToSerializableVector3(
                        checkpointPlayerState.position
                    );

                saveData.player.rotation =
                    ToSerializableQuaternion(
                        checkpointPlayerState.rotation
                    );

                saveData.player.currentHealth =
                    checkpointPlayerState.currentHealth;

                saveData.player.maxHealth =
                    checkpointPlayerState.maxHealth;

                saveData.player.progression.hasStaff =
                    checkpointPlayerState
                        .weaponProgression
                        .hasStaff;

                saveData.player.progression.lightningUnlocked =
                    checkpointPlayerState
                        .spellProgression
                        .lightningUnlocked;

                saveData.player.progression.iceTornadoUnlocked =
                    checkpointPlayerState
                        .spellProgression
                        .iceTornadoUnlocked;

                saveData.player.progression.entangleUnlocked =
                    checkpointPlayerState
                        .spellProgression
                        .entangleUnlocked;
            }
        }

        return true;
    }

    private void CapturePlayer(
        PlayerSaveData data,
        Transform playerTransform,
        Health health,
        Player_WeaponManager weaponManager,
        Player_StaffCombat staffCombat
    )
    {
        data.position =
            ToSerializableVector3(
                playerTransform.position
            );

        data.rotation =
            ToSerializableQuaternion(
                playerTransform.rotation
            );

        data.currentHealth =
            health.CurrentHealth;

        data.maxHealth =
            health.MaxHealth;

        Player_WeaponManager.WeaponProgressionState weaponState =
            weaponManager
                .CaptureProgressionState();

        Player_StaffCombat.SpellProgressionState spellState =
            staffCombat
                .CaptureProgressionState();

        data.progression.hasStaff =
            weaponState.hasStaff;

        data.progression.lightningUnlocked =
            spellState.lightningUnlocked;

        data.progression.iceTornadoUnlocked =
            spellState.iceTornadoUnlocked;

        data.progression.entangleUnlocked =
            spellState.entangleUnlocked;
    }

    private void CaptureEnemies(
        ManualWorldSaveData world
    )
    {
        EnemyController[] enemies =
            FindObjectsByType<EnemyController>(
                FindObjectsInactive.Include
            );

        foreach (
            EnemyController enemy
            in enemies
        )
        {
            if (enemy == null)
            {
                continue;
            }

            PersistentID persistentID =
                enemy.GetComponent<PersistentID>();

            if (
                persistentID == null ||
                !persistentID.HasValidID
            )
            {
                continue;
            }

            world.enemies.Add(
                new EnemySaveData
                {
                    persistentId =
                        persistentID.ID,

                    isDead =
                        enemy.IsDead,

                    currentHealth =
                        enemy.CurrentHealth,

                    maxHealth =
                        enemy.MaxHealth,

                    position =
                        ToSerializableVector3(
                            enemy.transform.position
                        ),

                    rotation =
                        ToSerializableQuaternion(
                            enemy.transform.rotation
                        )
                }
            );
        }
    }

    private void CaptureWorldObjects(
        ManualWorldSaveData world
    )
    {
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
                behaviour is EnemyController ||
                behaviour is HealthPickup ||
                behaviour is not ICheckpointResettable resettable
            )
            {
                continue;
            }

            PersistentID persistentID =
                behaviour.GetComponent<PersistentID>();

            if (
                persistentID == null ||
                !persistentID.HasValidID
            )
            {
                continue;
            }

            world.worldObjects.Add(
                new WorldObjectSaveData
                {
                    persistentId =
                        persistentID.ID,

                    available =
                        resettable.IsCheckpointAvailable
                }
            );
        }
    }

    // =========================================================
    // RESTORE - PLAYER
    // =========================================================

    private bool RestorePlayer(
        PlayerSaveData data
    )
    {
        if (data == null)
        {
            return false;
        }

        Player_Controller playerController =
            FindAnyObjectByType<Player_Controller>();

        if (playerController == null)
        {
            return false;
        }

        Health health =
            playerController.GetComponent<Health>();

        Player_WeaponManager weaponManager =
            playerController
                .GetComponent<Player_WeaponManager>();

        Player_StaffCombat staffCombat =
            playerController
                .GetComponent<Player_StaffCombat>();

        if (
            health == null ||
            weaponManager == null ||
            staffCombat == null
        )
        {
            return false;
        }

        weaponManager.ResetForRespawn();
        staffCombat.ResetForRespawn();
        playerController.ResetForRespawn();

        weaponManager.RestoreProgressionState(
            new Player_WeaponManager
                .WeaponProgressionState(
                    data.progression.hasStaff
                )
        );

        staffCombat.RestoreProgressionState(
            new Player_StaffCombat
                .SpellProgressionState(
                    data.progression.lightningUnlocked,
                    data.progression.iceTornadoUnlocked,
                    data.progression.entangleUnlocked
                )
        );

        playerController.Teleport(
            FromSerializableVector3(
                data.position
            ),
            FromSerializableQuaternion(
                data.rotation
            )
        );

        health.RestoreHealthState(
            data.currentHealth,
            data.maxHealth
        );

        PlayerAbilityHUD abilityHUD =
            FindAnyObjectByType<PlayerAbilityHUD>();

        if (abilityHUD != null)
        {
            abilityHUD.RefreshFromProgression();
        }

        return true;
    }

    // =========================================================
    // RESTORE - AUTHORED WORLD
    // =========================================================

    private bool RestoreAuthoredWorld(
        ManualWorldSaveData world,
        Dictionary<string, PersistentID> objectsById
    )
    {
        if (world == null)
        {
            return false;
        }

        bool success =
            true;

        foreach (
            WorldObjectSaveData worldData
            in world.worldObjects
        )
        {
            if (
                worldData == null ||
                !objectsById.TryGetValue(
                    worldData.persistentId,
                    out PersistentID persistentObject
                )
            )
            {
                success =
                    false;

                continue;
            }

            if (
                persistentObject
                    .GetComponent<EnemyController>() != null
            )
            {
                continue;
            }

            ICheckpointResettable resettable =
                persistentObject
                    .GetComponent<ICheckpointResettable>();

            if (resettable == null)
            {
                success =
                    false;

                continue;
            }

            resettable.RestoreCheckpointState(
                worldData.available
            );
        }

        return success;
    }

    // =========================================================
    // RESTORE - ENEMIES
    // =========================================================

    private bool RestoreEnemies(
        ManualWorldSaveData world,
        Dictionary<string, PersistentID> objectsById
    )
    {
        if (world == null)
        {
            return false;
        }

        bool success =
            true;

        foreach (
            EnemySaveData enemyData
            in world.enemies
        )
        {
            if (
                enemyData == null ||
                !objectsById.TryGetValue(
                    enemyData.persistentId,
                    out PersistentID persistentObject
                )
            )
            {
                success =
                    false;

                continue;
            }

            EnemyController enemy =
                persistentObject
                    .GetComponent<EnemyController>();

            if (enemy == null)
            {
                success =
                    false;

                continue;
            }

            enemy.RestoreManualSaveState(
                FromSerializableVector3(
                    enemyData.position
                ),
                FromSerializableQuaternion(
                    enemyData.rotation
                ),
                enemyData.currentHealth,
                enemyData.maxHealth,
                enemyData.isDead
            );
        }

        return success;
    }

    // =========================================================
    // RESTORE - CHECKPOINT
    // =========================================================

    private bool RestoreCheckpoint(
        CheckpointSaveData checkpoint
    )
    {
        CheckpointManager checkpointManager =
            CheckpointManager.Instance;

        if (checkpointManager == null)
        {
            checkpointManager =
                FindAnyObjectByType<CheckpointManager>();
        }

        if (checkpointManager == null)
        {
            return checkpoint == null ||
                !checkpoint.hasCheckpoint;
        }

        return checkpointManager
            .RestorePersistentCheckpointData(
                checkpoint
            );
    }

    // =========================================================
    // PERSISTENT ID LOOKUP
    // =========================================================

    private Dictionary<string, PersistentID>
        BuildPersistentIdLookup()
    {
        Dictionary<string, PersistentID> lookup =
            new Dictionary<string, PersistentID>();

        PersistentID[] persistentObjects =
            FindObjectsByType<PersistentID>(
                FindObjectsInactive.Include
            );

        foreach (
            PersistentID persistentObject
            in persistentObjects
        )
        {
            if (
                persistentObject == null ||
                !persistentObject.HasValidID
            )
            {
                continue;
            }

            if (
                lookup.ContainsKey(
                    persistentObject.ID
                )
            )
            {
                Debug.LogError(
                    "SaveGameManager: Duplicate PersistentID in scene: " +
                    persistentObject.ID,
                    persistentObject
                );

                continue;
            }

            lookup.Add(
                persistentObject.ID,
                persistentObject
            );
        }

        return lookup;
    }

    // =========================================================
    // XML SERIALIZATION
    // =========================================================

    private byte[] SerializeToXmlBytes(
        WildruneSaveData saveData
    )
    {
        XmlSerializer serializer =
            new XmlSerializer(
                typeof(WildruneSaveData)
            );

        using (
            MemoryStream stream =
                new MemoryStream()
        )
        {
            serializer.Serialize(
                stream,
                saveData
            );

            return stream.ToArray();
        }
    }

    private WildruneSaveData DeserializeXmlBytes(
        byte[] xmlBytes
    )
    {
        XmlSerializer serializer =
            new XmlSerializer(
                typeof(WildruneSaveData)
            );

        using (
            MemoryStream stream =
                new MemoryStream(
                    xmlBytes
                )
        )
        {
            return serializer.Deserialize(
                stream
            ) as WildruneSaveData;
        }
    }

    // =========================================================
    // ENCRYPTION / INTEGRITY
    // =========================================================

    private byte[] EncryptSaveBytes(
        byte[] plainBytes
    )
    {
        byte[] encryptionKey =
            DeriveKey(
                "encryption"
            );

        byte[] hmacKey =
            DeriveKey(
                "integrity"
            );

        byte[] iv;
        byte[] cipherBytes;

        using (
            Aes aes =
                Aes.Create()
        )
        {
            aes.KeySize =
                256;

            aes.BlockSize =
                128;

            aes.Mode =
                CipherMode.CBC;

            aes.Padding =
                PaddingMode.PKCS7;

            aes.Key =
                encryptionKey;

            aes.GenerateIV();

            iv =
                aes.IV;

            using (
                ICryptoTransform encryptor =
                    aes.CreateEncryptor()
            )
            {
                cipherBytes =
                    encryptor.TransformFinalBlock(
                        plainBytes,
                        0,
                        plainBytes.Length
                    );
            }
        }

        byte[] authenticatedBytes =
            CombineBytes(
                iv,
                cipherBytes
            );

        byte[] hmac;

        using (
            HMACSHA256 hmacSha256 =
                new HMACSHA256(
                    hmacKey
                )
        )
        {
            hmac =
                hmacSha256.ComputeHash(
                    authenticatedBytes
                );
        }

        return CombineBytes(
            FileMagic,
            iv,
            hmac,
            cipherBytes
        );
    }

    private byte[] DecryptSaveBytes(
        byte[] fileBytes
    )
    {
        int minimumLength =
            FileMagic.Length +
            IvLength +
            HmacLength +
            1;

        if (
            fileBytes == null ||
            fileBytes.Length <
                minimumLength
        )
        {
            throw new InvalidDataException(
                "Save file is incomplete."
            );
        }

        for (
            int index = 0;
            index < FileMagic.Length;
            index++
        )
        {
            if (
                fileBytes[index] !=
                    FileMagic[index]
            )
            {
                throw new InvalidDataException(
                    "Save file header is invalid."
                );
            }
        }

        int offset =
            FileMagic.Length;

        byte[] iv =
            CopyBytes(
                fileBytes,
                offset,
                IvLength
            );

        offset +=
            IvLength;

        byte[] storedHmac =
            CopyBytes(
                fileBytes,
                offset,
                HmacLength
            );

        offset +=
            HmacLength;

        byte[] cipherBytes =
            CopyBytes(
                fileBytes,
                offset,
                fileBytes.Length -
                    offset
            );

        byte[] hmacKey =
            DeriveKey(
                "integrity"
            );

        byte[] authenticatedBytes =
            CombineBytes(
                iv,
                cipherBytes
            );

        byte[] calculatedHmac;

        using (
            HMACSHA256 hmacSha256 =
                new HMACSHA256(
                    hmacKey
                )
        )
        {
            calculatedHmac =
                hmacSha256.ComputeHash(
                    authenticatedBytes
                );
        }

        if (
            !ByteArraysEqual(
                storedHmac,
                calculatedHmac
            )
        )
        {
            throw new InvalidDataException(
                "Save integrity check failed."
            );
        }

        byte[] encryptionKey =
            DeriveKey(
                "encryption"
            );

        using (
            Aes aes =
                Aes.Create()
        )
        {
            aes.KeySize =
                256;

            aes.BlockSize =
                128;

            aes.Mode =
                CipherMode.CBC;

            aes.Padding =
                PaddingMode.PKCS7;

            aes.Key =
                encryptionKey;

            aes.IV =
                iv;

            using (
                ICryptoTransform decryptor =
                    aes.CreateDecryptor()
            )
            {
                return decryptor
                    .TransformFinalBlock(
                        cipherBytes,
                        0,
                        cipherBytes.Length
                    );
            }
        }
    }

    private byte[] DeriveKey(
        string purpose
    )
    {
        string material =
            SaveSecret +
            "|" +
            purpose;

        using (
            SHA256 sha256 =
                SHA256.Create()
        )
        {
            return sha256.ComputeHash(
                Encoding.UTF8.GetBytes(
                    material
                )
            );
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private bool ValidateSlot(
        int slot
    )
    {
        if (IsSlotNumberValid(slot))
        {
            return true;
        }

        Debug.LogError(
            $"SaveGameManager: Invalid save slot {slot}. " +
            $"Valid slots are {MinimumSlot}-{MaximumSlot}.",
            this
        );

        return false;
    }

    private bool IsSlotNumberValid(
        int slot
    )
    {
        return
            slot >= MinimumSlot &&
            slot <= MaximumSlot;
    }

    private Vector3 FromSerializableVector3(
        SerializableVector3 value
    )
    {
        if (value == null)
        {
            return Vector3.zero;
        }

        return new Vector3(
            value.x,
            value.y,
            value.z
        );
    }

    private Quaternion FromSerializableQuaternion(
        SerializableQuaternion value
    )
    {
        if (value == null)
        {
            return Quaternion.identity;
        }

        Quaternion rotation =
            new Quaternion(
                value.x,
                value.y,
                value.z,
                value.w
            );

        if (
            rotation.x == 0f &&
            rotation.y == 0f &&
            rotation.z == 0f &&
            rotation.w == 0f
        )
        {
            return Quaternion.identity;
        }

        return rotation.normalized;
    }

    private SerializableVector3 ToSerializableVector3(
        Vector3 value
    )
    {
        return new SerializableVector3(
            value.x,
            value.y,
            value.z
        );
    }

    private SerializableQuaternion ToSerializableQuaternion(
        Quaternion value
    )
    {
        return new SerializableQuaternion(
            value.x,
            value.y,
            value.z,
            value.w
        );
    }

    private byte[] CombineBytes(
        params byte[][] arrays
    )
    {
        int totalLength =
            0;

        foreach (
            byte[] array
            in arrays
        )
        {
            totalLength +=
                array.Length;
        }

        byte[] combined =
            new byte[totalLength];

        int offset =
            0;

        foreach (
            byte[] array
            in arrays
        )
        {
            Buffer.BlockCopy(
                array,
                0,
                combined,
                offset,
                array.Length
            );

            offset +=
                array.Length;
        }

        return combined;
    }

    private byte[] CopyBytes(
        byte[] source,
        int offset,
        int length
    )
    {
        byte[] result =
            new byte[length];

        Buffer.BlockCopy(
            source,
            offset,
            result,
            0,
            length
        );

        return result;
    }

    private bool ByteArraysEqual(
        byte[] first,
        byte[] second
    )
    {
        if (
            first == null ||
            second == null ||
            first.Length != second.Length
        )
        {
            return false;
        }

        int difference =
            0;

        for (
            int index = 0;
            index < first.Length;
            index++
        )
        {
            difference |=
                first[index] ^
                second[index];
        }

        return difference == 0;
    }
}
