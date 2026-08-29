using System;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveGameManager : MonoBehaviour
{
    // =========================================================
    // TEMPORARY REAL-GAME XML CAPTURE TEST
    // =========================================================

    private const string TestFileName =
        "wildrune_xml_test.xml";

    public string TestFilePath =>
        Path.Combine(
            Application.persistentDataPath,
            TestFileName
        );

    // =========================================================
    // WRITE CURRENT GAME
    // =========================================================

    [ContextMenu("Write Current Game XML")]
    public void WriteCurrentGameXml()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SaveGameManager: Enter Play Mode before capturing " +
                "the current game state.",
                this
            );

            return;
        }

        if (!TryBuildCurrentSaveData(
                out WildruneSaveData saveData
            ))
        {
            return;
        }

        try
        {
            XmlSerializer serializer =
                new XmlSerializer(
                    typeof(WildruneSaveData)
                );

            using (
                StreamWriter writer =
                    new StreamWriter(
                        TestFilePath,
                        false
                    )
            )
            {
                serializer.Serialize(
                    writer,
                    saveData
                );
            }

            Debug.Log(
                "SaveGameManager: Current game XML written successfully.\n" +
                $"Path: {TestFilePath}\n" +
                $"PlayerHealth={saveData.player.currentHealth}/" +
                $"{saveData.player.maxHealth}\n" +
                $"Enemies={saveData.world.enemies.Count}\n" +
                $"WorldObjects={saveData.world.worldObjects.Count}\n" +
                $"Checkpoint=" +
                $"{(saveData.checkpoint.hasCheckpoint ? saveData.checkpoint.checkpointId : "None")}",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "SaveGameManager: Failed to write current game XML.\n" +
                exception,
                this
            );
        }
    }

    // =========================================================
    // READ TEST FILE
    // =========================================================

    [ContextMenu("Read Current Game XML")]
    public void ReadCurrentGameXml()
    {
        if (!File.Exists(TestFilePath))
        {
            Debug.LogWarning(
                "SaveGameManager: No XML test save exists yet.\n" +
                $"Expected path: {TestFilePath}",
                this
            );

            return;
        }

        try
        {
            XmlSerializer serializer =
                new XmlSerializer(
                    typeof(WildruneSaveData)
                );

            WildruneSaveData loadedData;

            using (
                StreamReader reader =
                    new StreamReader(
                        TestFilePath
                    )
            )
            {
                loadedData =
                    serializer.Deserialize(
                        reader
                    ) as WildruneSaveData;
            }

            if (loadedData == null)
            {
                Debug.LogError(
                    "SaveGameManager: XML was read, but no " +
                    "WildruneSaveData object was created.",
                    this
                );

                return;
            }

            Debug.Log(
                "SaveGameManager: Current game XML loaded successfully.\n" +
                $"Scene={loadedData.sceneName}\n" +
                $"PlayerHealth={loadedData.player.currentHealth}/" +
                $"{loadedData.player.maxHealth}\n" +
                $"Enemies={loadedData.world.enemies.Count}\n" +
                $"WorldObjects={loadedData.world.worldObjects.Count}\n" +
                $"Checkpoint=" +
                $"{(loadedData.checkpoint.hasCheckpoint ? loadedData.checkpoint.checkpointId : "None")}",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "SaveGameManager: Failed to read current game XML.\n" +
                exception,
                this
            );
        }
    }

    // =========================================================
    // PLAYER RESTORE TEST
    // =========================================================

    [ContextMenu("Restore Player From Current Game XML")]
    public void RestorePlayerFromCurrentGameXml()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SaveGameManager: Enter Play Mode before restoring " +
                "the player from XML.",
                this
            );

            return;
        }

        if (!TryReadSaveData(
                out WildruneSaveData saveData
            ))
        {
            return;
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
                "SaveGameManager: Save belongs to scene " +
                $"'{saveData.sceneName}', but the active scene is " +
                $"'{activeSceneName}'. Player restore aborted.",
                this
            );

            return;
        }

        Player_Controller playerController =
            FindAnyObjectByType<Player_Controller>();

        if (playerController == null)
        {
            Debug.LogError(
                "SaveGameManager: Could not find Player_Controller.",
                this
            );

            return;
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
            Debug.LogError(
                "SaveGameManager: Player is missing Health, " +
                "Player_WeaponManager, or Player_StaffCombat.",
                playerController
            );

            return;
        }

        weaponManager.ResetForRespawn();
        staffCombat.ResetForRespawn();
        playerController.ResetForRespawn();

        Player_WeaponManager.WeaponProgressionState
            weaponState =
                new Player_WeaponManager
                    .WeaponProgressionState(
                        saveData.player
                            .progression
                            .hasStaff
                    );

        Player_StaffCombat.SpellProgressionState
            spellState =
                new Player_StaffCombat
                    .SpellProgressionState(
                        saveData.player
                            .progression
                            .lightningUnlocked,

                        saveData.player
                            .progression
                            .iceTornadoUnlocked,

                        saveData.player
                            .progression
                            .entangleUnlocked
                    );

        weaponManager.RestoreProgressionState(
            weaponState
        );

        staffCombat.RestoreProgressionState(
            spellState
        );

        Vector3 savedPosition =
            FromSerializableVector3(
                saveData.player.position
            );

        Quaternion savedRotation =
            FromSerializableQuaternion(
                saveData.player.rotation
            );

        playerController.Teleport(
            savedPosition,
            savedRotation
        );

        health.RestoreHealthState(
            saveData.player.currentHealth,
            saveData.player.maxHealth
        );

        PlayerAbilityHUD abilityHUD =
            FindAnyObjectByType<PlayerAbilityHUD>();

        if (abilityHUD != null)
        {
            abilityHUD.RefreshFromProgression();
        }

        Debug.Log(
            "SaveGameManager: Player restored from XML.\n" +
            $"Position={savedPosition}\n" +
            $"Health={health.CurrentHealth}/{health.MaxHealth}\n" +
            $"Staff={weaponManager.HasStaff}\n" +
            $"Lightning=" +
            $"{staffCombat.IsSpellUnlocked(Player_StaffCombat.StaffSpell.LightningStrike)}\n" +
            $"IceTornado=" +
            $"{staffCombat.IsSpellUnlocked(Player_StaffCombat.StaffSpell.IceTornado)}\n" +
            $"Entangle=" +
            $"{staffCombat.IsSpellUnlocked(Player_StaffCombat.StaffSpell.Entangle)}",
            this
        );
    }

    // =========================================================
    // CHECKPOINT RECONSTRUCTION TEST
    // =========================================================

    [ContextMenu("Restore Checkpoint From Current Game XML")]
    public void RestoreCheckpointFromCurrentGameXml()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SaveGameManager: Enter Play Mode before restoring " +
                "the checkpoint from XML.",
                this
            );

            return;
        }

        if (!TryReadSaveData(
                out WildruneSaveData saveData
            ))
        {
            return;
        }

        CheckpointManager checkpointManager =
            CheckpointManager.Instance;

        if (checkpointManager == null)
        {
            checkpointManager =
                FindAnyObjectByType<CheckpointManager>();
        }

        if (checkpointManager == null)
        {
            Debug.LogError(
                "SaveGameManager: No CheckpointManager exists in scene.",
                this
            );

            return;
        }

        bool restored =
            checkpointManager
                .RestorePersistentCheckpointData(
                    saveData.checkpoint
                );

        if (!restored)
        {
            Debug.LogWarning(
                "SaveGameManager: Checkpoint reconstruction completed " +
                "with one or more unresolved saved world states.",
                this
            );

            return;
        }

        Debug.Log(
            "SaveGameManager: Checkpoint reconstructed from XML.\n" +
            $"Checkpoint=" +
            $"{(saveData.checkpoint.hasCheckpoint ? saveData.checkpoint.checkpointId : "None")}",
            this
        );
    }

    // =========================================================
    // ENEMY RESTORE TEST
    // =========================================================

    [ContextMenu("Restore Enemies From Current Game XML")]
    public void RestoreEnemiesFromCurrentGameXml()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SaveGameManager: Enter Play Mode before restoring " +
                "enemies from XML.",
                this
            );

            return;
        }

        if (!TryReadSaveData(
                out WildruneSaveData saveData
            ))
        {
            return;
        }

        PersistentID[] persistentObjects =
            FindObjectsByType<PersistentID>(
                FindObjectsInactive.Include
            );

        System.Collections.Generic.Dictionary<string, PersistentID>
            sceneObjectsById =
                new System.Collections.Generic.Dictionary<string, PersistentID>();

        foreach (
            PersistentID persistentObject
            in persistentObjects
        )
        {
            if (
                persistentObject == null ||
                !persistentObject.HasValidID ||
                sceneObjectsById.ContainsKey(
                    persistentObject.ID
                )
            )
            {
                continue;
            }

            sceneObjectsById.Add(
                persistentObject.ID,
                persistentObject
            );
        }

        int restored =
            0;

        int missing =
            0;

        foreach (
            EnemySaveData enemyData
            in saveData.world.enemies
        )
        {
            if (
                string.IsNullOrWhiteSpace(
                    enemyData.persistentId
                ) ||
                !sceneObjectsById.TryGetValue(
                    enemyData.persistentId,
                    out PersistentID persistentObject
                )
            )
            {
                missing++;
                continue;
            }

            EnemyController enemy =
                persistentObject
                    .GetComponent<EnemyController>();

            if (enemy == null)
            {
                missing++;
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

            restored++;
        }

        Debug.Log(
            "SaveGameManager: Enemy restore complete.\n" +
            $"Restored={restored}\n" +
            $"Missing={missing}",
            this
        );
    }

    // =========================================================
    // AUTHORED WORLD RESTORE TEST
    // =========================================================

    [ContextMenu("Restore Authored World From Current Game XML")]
    public void RestoreAuthoredWorldFromCurrentGameXml()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SaveGameManager: Enter Play Mode before restoring " +
                "authored world objects from XML.",
                this
            );

            return;
        }

        if (!TryReadSaveData(
                out WildruneSaveData saveData
            ))
        {
            return;
        }

        PersistentID[] persistentObjects =
            FindObjectsByType<PersistentID>(
                FindObjectsInactive.Include
            );

        System.Collections.Generic.Dictionary<string, PersistentID>
            sceneObjectsById =
                new System.Collections.Generic.Dictionary<string, PersistentID>();

        foreach (
            PersistentID persistentObject
            in persistentObjects
        )
        {
            if (
                persistentObject == null ||
                !persistentObject.HasValidID ||
                sceneObjectsById.ContainsKey(
                    persistentObject.ID
                )
            )
            {
                continue;
            }

            sceneObjectsById.Add(
                persistentObject.ID,
                persistentObject
            );
        }

        int restored =
            0;

        int missing =
            0;

        foreach (
            WorldObjectSaveData worldData
            in saveData.world.worldObjects
        )
        {
            if (
                string.IsNullOrWhiteSpace(
                    worldData.persistentId
                ) ||
                !sceneObjectsById.TryGetValue(
                    worldData.persistentId,
                    out PersistentID persistentObject
                )
            )
            {
                missing++;
                continue;
            }

            /*
             * Enemy state is restored separately because manual
             * enemy saves contain health and transform data.
             */
            if (
                persistentObject.GetComponent<EnemyController>() != null
            )
            {
                continue;
            }

            ICheckpointResettable resettable =
                persistentObject
                    .GetComponent<ICheckpointResettable>();

            if (resettable == null)
            {
                missing++;
                continue;
            }

            /*
             * The existing checkpoint restore contract already
             * expresses exactly what authored collectibles need:
             * available=true restores them; false keeps them consumed.
             */
            resettable.RestoreCheckpointState(
                worldData.available
            );

            restored++;
        }

        Debug.Log(
            "SaveGameManager: Authored world restore complete.\n" +
            $"Restored={restored}\n" +
            $"Missing={missing}",
            this
        );
    }

    // =========================================================
    // LOAD VALIDATION
    // =========================================================

    [ContextMenu("Validate Current Game XML")]
    public void ValidateCurrentGameXml()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SaveGameManager: Enter Play Mode before validating " +
                "the save against the current scene.",
                this
            );

            return;
        }

        if (!TryReadSaveData(
                out WildruneSaveData saveData
            ))
        {
            return;
        }

        PersistentID[] persistentObjects =
            FindObjectsByType<PersistentID>(
                FindObjectsInactive.Include
            );

        System.Collections.Generic.Dictionary<string, PersistentID>
            sceneObjectsById =
                new System.Collections.Generic.Dictionary<string, PersistentID>();

        int duplicateSceneIds =
            0;

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
                sceneObjectsById.ContainsKey(
                    persistentObject.ID
                )
            )
            {
                duplicateSceneIds++;

                Debug.LogError(
                    "SaveGameManager: Duplicate PersistentID found in scene: " +
                    $"{persistentObject.ID}",
                    persistentObject
                );

                continue;
            }

            sceneObjectsById.Add(
                persistentObject.ID,
                persistentObject
            );
        }

        int missingEnemies =
            ValidateEnemyIds(
                saveData,
                sceneObjectsById
            );

        int missingWorldObjects =
            ValidateWorldObjectIds(
                saveData,
                sceneObjectsById
            );

        int missingCheckpointObjects =
            ValidateCheckpointWorldIds(
                saveData,
                sceneObjectsById
            );

        int missingCheckpointShrine =
            ValidateCheckpointShrine(
                saveData,
                sceneObjectsById
            );

        bool playerFound =
            FindAnyObjectByType<Player_Controller>() != null;

        int totalMissing =
            duplicateSceneIds +
            missingEnemies +
            missingWorldObjects +
            missingCheckpointObjects +
            missingCheckpointShrine +
            (playerFound ? 0 : 1);

        if (!playerFound)
        {
            Debug.LogError(
                "SaveGameManager: Player_Controller was not found " +
                "in the current scene.",
                this
            );
        }

        if (totalMissing == 0)
        {
            Debug.Log(
                "SaveGameManager: Save validation PASSED.\n" +
                $"Scene={saveData.sceneName}\n" +
                $"Enemies matched={saveData.world.enemies.Count}\n" +
                $"World objects matched={saveData.world.worldObjects.Count}\n" +
                $"Checkpoint world states matched=" +
                $"{saveData.checkpoint.worldStates.Count}\n" +
                $"Checkpoint=" +
                $"{(saveData.checkpoint.hasCheckpoint ? saveData.checkpoint.checkpointId : "None")}",
                this
            );

            return;
        }

        Debug.LogWarning(
            "SaveGameManager: Save validation FAILED.\n" +
            $"Duplicate scene IDs={duplicateSceneIds}\n" +
            $"Missing player={(playerFound ? 0 : 1)}\n" +
            $"Missing enemies={missingEnemies}\n" +
            $"Missing world objects={missingWorldObjects}\n" +
            $"Missing checkpoint world objects={missingCheckpointObjects}\n" +
            $"Missing checkpoint shrine={missingCheckpointShrine}",
            this
        );
    }

    private bool TryReadSaveData(
        out WildruneSaveData saveData
    )
    {
        saveData =
            null;

        if (!File.Exists(TestFilePath))
        {
            Debug.LogWarning(
                "SaveGameManager: No XML test save exists yet.\n" +
                $"Expected path: {TestFilePath}",
                this
            );

            return false;
        }

        try
        {
            XmlSerializer serializer =
                new XmlSerializer(
                    typeof(WildruneSaveData)
                );

            using (
                StreamReader reader =
                    new StreamReader(
                        TestFilePath
                    )
            )
            {
                saveData =
                    serializer.Deserialize(
                        reader
                    ) as WildruneSaveData;
            }

            if (saveData == null)
            {
                Debug.LogError(
                    "SaveGameManager: XML was read, but no " +
                    "WildruneSaveData object was created.",
                    this
                );

                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "SaveGameManager: Failed to read XML for validation.\n" +
                exception,
                this
            );

            return false;
        }
    }

    private int ValidateEnemyIds(
        WildruneSaveData saveData,
        System.Collections.Generic.Dictionary<string, PersistentID>
            sceneObjectsById
    )
    {
        int missing =
            0;

        foreach (
            EnemySaveData enemyData
            in saveData.world.enemies
        )
        {
            if (
                !TryResolvePersistentObject(
                    enemyData.persistentId,
                    sceneObjectsById,
                    out PersistentID persistentObject
                ) ||
                persistentObject.GetComponent<EnemyController>() == null
            )
            {
                missing++;

                Debug.LogError(
                    "SaveGameManager: Saved enemy could not be matched: " +
                    $"{enemyData.persistentId}",
                    this
                );
            }
        }

        return missing;
    }

    private int ValidateWorldObjectIds(
        WildruneSaveData saveData,
        System.Collections.Generic.Dictionary<string, PersistentID>
            sceneObjectsById
    )
    {
        int missing =
            0;

        foreach (
            WorldObjectSaveData worldData
            in saveData.world.worldObjects
        )
        {
            if (
                !TryResolvePersistentObject(
                    worldData.persistentId,
                    sceneObjectsById,
                    out PersistentID persistentObject
                ) ||
                persistentObject.GetComponent<ICheckpointResettable>() == null
            )
            {
                missing++;

                Debug.LogError(
                    "SaveGameManager: Saved world object could not be matched: " +
                    $"{worldData.persistentId}",
                    this
                );
            }
        }

        return missing;
    }

    private int ValidateCheckpointWorldIds(
        WildruneSaveData saveData,
        System.Collections.Generic.Dictionary<string, PersistentID>
            sceneObjectsById
    )
    {
        int missing =
            0;

        foreach (
            CheckpointWorldStateSaveData checkpointState
            in saveData.checkpoint.worldStates
        )
        {
            if (
                !TryResolvePersistentObject(
                    checkpointState.persistentId,
                    sceneObjectsById,
                    out PersistentID persistentObject
                ) ||
                persistentObject.GetComponent<ICheckpointResettable>() == null
            )
            {
                missing++;

                Debug.LogError(
                    "SaveGameManager: Checkpoint world object could not be matched: " +
                    $"{checkpointState.persistentId}",
                    this
                );
            }
        }

        return missing;
    }

    private int ValidateCheckpointShrine(
        WildruneSaveData saveData,
        System.Collections.Generic.Dictionary<string, PersistentID>
            sceneObjectsById
    )
    {
        if (!saveData.checkpoint.hasCheckpoint)
        {
            return 0;
        }

        if (
            !TryResolvePersistentObject(
                saveData.checkpoint.checkpointId,
                sceneObjectsById,
                out PersistentID persistentObject
            ) ||
            persistentObject.GetComponent<CheckpointShrine>() == null
        )
        {
            Debug.LogError(
                "SaveGameManager: Active checkpoint shrine could not be matched: " +
                $"{saveData.checkpoint.checkpointId}",
                this
            );

            return 1;
        }

        return 0;
    }

    private bool TryResolvePersistentObject(
        string persistentId,
        System.Collections.Generic.Dictionary<string, PersistentID>
            sceneObjectsById,
        out PersistentID persistentObject
    )
    {
        persistentObject =
            null;

        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return false;
        }

        return sceneObjectsById.TryGetValue(
            persistentId,
            out persistentObject
        );
    }

    [ContextMenu("Delete XML Test Save")]
    public void DeleteXmlTestSave()
    {
        if (!File.Exists(TestFilePath))
        {
            Debug.Log(
                "SaveGameManager: No XML test save exists to delete.",
                this
            );

            return;
        }

        File.Delete(
            TestFilePath
        );

        Debug.Log(
            "SaveGameManager: XML test save deleted.",
            this
        );
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
                "SaveGameManager: Player is missing Health, " +
                "Player_WeaponManager, or Player_StaffCombat.",
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

        Player_WeaponManager.WeaponProgressionState
            weaponState =
                weaponManager
                    .CaptureProgressionState();

        Player_StaffCombat.SpellProgressionState
            spellState =
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
                Debug.LogWarning(
                    $"{enemy.name}: Enemy skipped during save capture " +
                    "because it has no valid PersistentID.",
                    enemy
                );

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
    // CONVERSION
    // =========================================================

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
}
