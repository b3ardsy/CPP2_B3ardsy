using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveLoadSceneBridge : MonoBehaviour
{
    private static SaveLoadSceneBridge instance;
    private static WildruneSaveData pendingSaveData;

    private bool restoreQueued;

    public static void QueueLoad(
        WildruneSaveData saveData
    )
    {
        pendingSaveData =
            saveData;

        EnsureInstance();
    }

    public static void ClearPendingLoad()
    {
        pendingSaveData =
            null;
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject bridgeObject =
            new GameObject(
                "SaveLoadSceneBridge"
            );

        instance =
            bridgeObject.AddComponent
                <SaveLoadSceneBridge>();

        DontDestroyOnLoad(
            bridgeObject
        );
    }

    private void Awake()
    {
        if (
            instance != null &&
            instance != this
        )
        {
            Destroy(
                gameObject
            );

            return;
        }

        instance =
            this;

        DontDestroyOnLoad(
            gameObject
        );

        SceneManager.sceneLoaded +=
            HandleSceneLoaded;
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode
    )
    {
        if (
            pendingSaveData == null ||
            restoreQueued
        )
        {
            return;
        }

        restoreQueued =
            true;

        StartCoroutine(
            RestoreAfterSceneLoad()
        );
    }

    private IEnumerator RestoreAfterSceneLoad()
    {
        yield return null;
        yield return null;

        SaveGameManager saveGameManager =
            FindAnyObjectByType<SaveGameManager>();

        if (saveGameManager == null)
        {
            Debug.LogError(
                "SaveLoadSceneBridge: SaveGameManager not found " +
                "after scene load."
            );

            restoreQueued =
                false;

            yield break;
        }

        WildruneSaveData dataToRestore =
            pendingSaveData;

        pendingSaveData =
            null;

        bool restored =
            saveGameManager.RestoreSaveData(
                dataToRestore
            );

        Debug.Log(
            "SaveLoadSceneBridge: Main-menu restore " +
            $"{(restored ? "complete." : "failed.")}"
        );

        restoreQueued =
            false;

        Destroy(
            gameObject
        );
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -=
            HandleSceneLoaded;

        if (instance == this)
        {
            instance =
                null;
        }
    }
}
