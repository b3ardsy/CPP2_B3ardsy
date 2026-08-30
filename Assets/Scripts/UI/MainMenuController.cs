using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("The scene loaded when the Start button is pressed.")]
    [SerializeField] private string gameSceneName = "Game_01";

    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject loadSavePanel;
    [SerializeField] private GameObject loadConfirmationPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject quitConfirmationPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button loadButton;

    [Header("Load Slot UI")]
    [SerializeField] private TMP_Text slot1Text;
    [SerializeField] private TMP_Text slot2Text;
    [SerializeField] private TMP_Text slot3Text;

    [SerializeField] private Button slot1Button;
    [SerializeField] private Button slot2Button;
    [SerializeField] private Button slot3Button;

    [SerializeField] private TMP_Text loadConfirmationBodyText;

    [Header("Save System")]
    [SerializeField] private SaveGameManager saveGameManager;

    private int pendingLoadSlot;

    private void Awake()
    {
        /*
         * Always restore normal game time when entering
         * the Main Menu.
         */
        Time.timeScale = 1f;

        /*
         * Gameplay locks and hides the cursor.
         * The Main Menu should always restore normal mouse control.
         */
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;

        if (saveGameManager == null)
        {
            saveGameManager =
                FindAnyObjectByType<SaveGameManager>();
        }

        ShowMainMenu();
    }

    public void NewGame()
    {
        Debug.Log(
            $"Main Menu: Starting new game in {gameSceneName}."
        );

        SaveLoadSceneBridge.ClearPendingLoad();

        SceneManager.LoadScene(
            gameSceneName
        );
    }

    public void ContinueGame()
    {
        if (!TryGetMostRecentSaveSlot(
                out int newestSlot
            ))
        {
            Debug.LogWarning(
                "Main Menu: No save is available to continue.",
                this
            );

            RefreshMainMenuState();

            return;
        }

        BeginLoadSlot(
            newestSlot
        );
    }

    public void OpenLoadGame()
    {
        if (!EnsureSaveGameManager())
        {
            return;
        }

        pendingLoadSlot =
            0;

        RefreshLoadSlots();

        HideAllPanels();

        if (loadSavePanel != null)
        {
            loadSavePanel.SetActive(
                true
            );
        }
    }

    public void SelectLoadSlot1()
    {
        SelectLoadSlot(1);
    }

    public void SelectLoadSlot2()
    {
        SelectLoadSlot(2);
    }

    public void SelectLoadSlot3()
    {
        SelectLoadSlot(3);
    }

    private void SelectLoadSlot(
        int slot
    )
    {
        if (
            !EnsureSaveGameManager() ||
            !saveGameManager.HasSave(slot)
        )
        {
            return;
        }

        pendingLoadSlot =
            slot;

        if (loadConfirmationBodyText != null)
        {
            loadConfirmationBodyText.text =
                $"Load Save Slot {slot}?\n";
        }

        HideAllPanels();

        if (loadConfirmationPanel != null)
        {
            loadConfirmationPanel.SetActive(
                true
            );
        }
    }

    public void ConfirmLoadGame()
    {
        if (
            pendingLoadSlot < 1 ||
            pendingLoadSlot > 3
        )
        {
            return;
        }

        BeginLoadSlot(
            pendingLoadSlot
        );
    }

    public void CancelLoadConfirmation()
    {
        pendingLoadSlot =
            0;

        OpenLoadGame();
    }

    public void BackFromLoadGame()
    {
        pendingLoadSlot =
            0;

        ShowMainMenu();
    }

    private void BeginLoadSlot(
        int slot
    )
    {
        if (
            !EnsureSaveGameManager() ||
            !saveGameManager.TryReadSaveSlot(
                slot,
                out WildruneSaveData saveData
            )
        )
        {
            Debug.LogError(
                $"Main Menu: Could not read Save Slot {slot}.",
                this
            );

            return;
        }

        string sceneToLoad =
            string.IsNullOrWhiteSpace(
                saveData.sceneName
            )
                ? gameSceneName
                : saveData.sceneName;

        SaveLoadSceneBridge.QueueLoad(
            saveData
        );

        SceneManager.LoadScene(
            sceneToLoad
        );
    }

    private void RefreshMainMenuState()
    {
        bool hasAnySave =
            HasAnySave();

        if (continueButton != null)
        {
            continueButton.interactable =
                hasAnySave;
        }

        if (loadButton != null)
        {
            loadButton.interactable =
                hasAnySave;
        }
    }

    private void RefreshLoadSlots()
    {
        UpdateLoadSlot(
            1,
            slot1Text,
            slot1Button
        );

        UpdateLoadSlot(
            2,
            slot2Text,
            slot2Button
        );

        UpdateLoadSlot(
            3,
            slot3Text,
            slot3Button
        );
    }

    private void UpdateLoadSlot(
        int slot,
        TMP_Text slotText,
        Button slotButton
    )
    {
        bool hasSave =
            saveGameManager != null &&
            saveGameManager.HasSave(slot);

        if (slotButton != null)
        {
            slotButton.interactable =
                hasSave;
        }

        if (slotText == null)
        {
            return;
        }

        if (!hasSave)
        {
            slotText.text =
                $"SLOT {slot}\nEMPTY";

            return;
        }

        if (
            !saveGameManager.TryGetSlotSummary(
                slot,
                out string sceneName,
                out string savedUtc,
                out int currentHealth,
                out int maxHealth
            )
        )
        {
            slotText.text =
                $"SLOT {slot}\nUNAVAILABLE";

            if (slotButton != null)
            {
                slotButton.interactable =
                    false;
            }

            return;
        }

        string displayTime =
            "SAVED";

        if (
            DateTime.TryParse(
                savedUtc,
                null,
                System.Globalization
                    .DateTimeStyles
                    .RoundtripKind,
                out DateTime parsedTime
            )
        )
        {
            displayTime =
                parsedTime
                    .ToLocalTime()
                    .ToString(
                        "MMM d • HH:mm"
                    )
                    .ToUpperInvariant();
        }

        slotText.text =
            $"SLOT {slot}\n{displayTime}";
    }

    private bool HasAnySave()
    {
        if (!EnsureSaveGameManager())
        {
            return false;
        }

        return
            saveGameManager.HasSave(1) ||
            saveGameManager.HasSave(2) ||
            saveGameManager.HasSave(3);
    }

    private bool TryGetMostRecentSaveSlot(
        out int mostRecentSlot
    )
    {
        mostRecentSlot =
            0;

        if (!EnsureSaveGameManager())
        {
            return false;
        }

        DateTime newestTime =
            DateTime.MinValue;

        for (int slot = 1; slot <= 3; slot++)
        {
            if (
                !saveGameManager.TryGetSlotSummary(
                    slot,
                    out string sceneName,
                    out string savedUtc,
                    out int currentHealth,
                    out int maxHealth
                )
            )
            {
                continue;
            }

            DateTime.TryParse(
                savedUtc,
                null,
                System.Globalization
                    .DateTimeStyles
                    .RoundtripKind,
                out DateTime parsedTime
            );

            if (
                mostRecentSlot == 0 ||
                parsedTime > newestTime
            )
            {
                mostRecentSlot =
                    slot;

                newestTime =
                    parsedTime;
            }
        }

        return mostRecentSlot != 0;
    }

    private bool EnsureSaveGameManager()
    {
        if (saveGameManager == null)
        {
            saveGameManager =
                FindAnyObjectByType<SaveGameManager>();
        }

        if (saveGameManager != null)
        {
            return true;
        }

        Debug.LogError(
            "Main Menu: SaveGameManager not found. " +
            "Add one to Game_Start.",
            this
        );

        return false;
    }

    public void OpenSettings()
    {
        Debug.Log(
            "Main Menu: Opening Settings."
        );

        HideAllPanels();
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        Debug.Log(
            "Main Menu: Closing Settings."
        );

        ShowMainMenu();
    }

    public void OpenCredits()
    {
        Debug.Log(
            "Main Menu: Opening Credits."
        );

        HideAllPanels();
        creditsPanel.SetActive(true);
    }

    public void CloseCredits()
    {
        Debug.Log(
            "Main Menu: Closing Credits."
        );

        ShowMainMenu();
    }

    public void OpenQuitConfirmation()
    {
        Debug.Log(
            "Main Menu: Opening Quit confirmation."
        );

        HideAllPanels();
        quitConfirmationPanel.SetActive(true);
    }

    public void CancelQuit()
    {
        Debug.Log(
            "Main Menu: Quit cancelled."
        );

        ShowMainMenu();
    }

    public void QuitGame()
    {
        Debug.Log(
            "Main Menu: Quitting game."
        );

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowMainMenu()
    {
        HideAllPanels();

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(
                true
            );
        }

        RefreshMainMenuState();
    }

    private void HideAllPanels()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(loadSavePanel, false);
        SetPanelActive(loadConfirmationPanel, false);
        SetPanelActive(settingsPanel, false);
        SetPanelActive(creditsPanel, false);
        SetPanelActive(quitConfirmationPanel, false);
    }

    private void SetPanelActive(
        GameObject panel,
        bool active
    )
    {
        if (panel != null)
        {
            panel.SetActive(
                active
            );
        }
    }
}