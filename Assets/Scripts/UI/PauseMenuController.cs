using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("Pause UI")]
    [SerializeField] private GameObject pauseCanvas;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject mainMenuConfirmationPanel;
    [SerializeField] private GameObject quitConfirmationPanel;

    [Header("Load Game UI")]
    [SerializeField] private GameObject loadSavePanel;
    [SerializeField] private GameObject loadConfirmationPanel;

    [SerializeField] private TMP_Text slot1Text;
    [SerializeField] private TMP_Text slot2Text;
    [SerializeField] private TMP_Text slot3Text;

    [SerializeField] private Button slot1Button;
    [SerializeField] private Button slot2Button;
    [SerializeField] private Button slot3Button;

    [SerializeField] private TMP_Text loadConfirmationBodyText;

    [Header("Save System")]
    [SerializeField] private SaveGameManager saveGameManager;

    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "Game_Start";

    [Header("Gameplay Input")]
    [SerializeField] private MonoBehaviour cameraController;
    [SerializeField] private Player_Combat playerCombat;

    private bool isPaused;
    private bool externalModalOpen;
    private Coroutine resumeCoroutine;

    private int pendingLoadSlot;

    public bool IsPaused => isPaused;

    public bool IsExternalModalOpen =>
        externalModalOpen;

    private void Start()
    {
        if (saveGameManager == null)
        {
            saveGameManager =
                FindAnyObjectByType<SaveGameManager>();
        }

        ResumeGame();
    }

    private void Update()
    {
        if (externalModalOpen)
        {
            return;
        }

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (resumeCoroutine != null)
        {
            StopCoroutine(resumeCoroutine);
            resumeCoroutine = null;
        }

        isPaused = true;

        Time.timeScale = 0f;

        pauseCanvas.SetActive(true);

        ShowPauseMenu();

        SetGameplayInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Pause Menu: Game paused.");
    }

    public void ResumeGame()
    {
        isPaused = false;

        Time.timeScale = 1f;

        pauseCanvas.SetActive(false);

        if (resumeCoroutine != null)
        {
            StopCoroutine(resumeCoroutine);
        }

        resumeCoroutine = StartCoroutine(
            RestoreGameplayInputAfterResume()
        );

        Debug.Log("Pause Menu: Game resumed.");
    }

    // =========================================================
    // EXTERNAL MODAL OWNERSHIP
    // =========================================================

    /*
     * Used by gameplay modal UI such as the Shrine Save menu.
     *
     * While active:
     * - Escape cannot open the Pause Menu.
     * - Camera/combat input is disabled using the same ownership
     *   path as normal pausing.
     */
    public void SetExternalModalOpen(
        bool modalOpen
    )
    {
        externalModalOpen =
            modalOpen;

        if (modalOpen)
        {
            if (resumeCoroutine != null)
            {
                StopCoroutine(
                    resumeCoroutine
                );

                resumeCoroutine =
                    null;
            }

            SetGameplayInputEnabled(
                false
            );

            return;
        }

        if (!isPaused)
        {
            SetGameplayInputEnabled(
                true
            );
        }
    }

    // =========================================================
    // LOAD GAME
    // =========================================================

    public void OpenLoadGame()
    {
        if (saveGameManager == null)
        {
            saveGameManager =
                FindAnyObjectByType<SaveGameManager>();
        }

        if (saveGameManager == null)
        {
            Debug.LogError(
                "Pause Menu: SaveGameManager not found.",
                this
            );

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

        Debug.Log(
            "Pause Menu: Opening Load Game."
        );
    }

    public void SelectLoadSlot1()
    {
        SelectLoadSlot(
            1
        );
    }

    public void SelectLoadSlot2()
    {
        SelectLoadSlot(
            2
        );
    }

    public void SelectLoadSlot3()
    {
        SelectLoadSlot(
            3
        );
    }

    private void SelectLoadSlot(
        int slot
    )
    {
        if (
            saveGameManager == null ||
            !saveGameManager.HasSave(
                slot
            )
        )
        {
            return;
        }

        pendingLoadSlot =
            slot;

        if (loadConfirmationBodyText != null)
        {
            loadConfirmationBodyText.text =
                $"Load Save Slot {slot}?\n" +
                "Unsaved progress will be lost.";
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
            pendingLoadSlot > 3 ||
            saveGameManager == null
        )
        {
            return;
        }

        int slotToLoad =
            pendingLoadSlot;

        bool loaded =
            saveGameManager
                .LoadGameInCurrentScene(
                    slotToLoad
                );

        if (!loaded)
        {
            Debug.LogError(
                $"Pause Menu: Failed to load Slot {slotToLoad}.",
                this
            );

            pendingLoadSlot =
                0;

            OpenLoadGame();

            return;
        }

        pendingLoadSlot =
            0;

        Debug.Log(
            $"Pause Menu: Loaded Slot {slotToLoad}."
        );

        /*
         * Resume only AFTER restoration succeeds.
         * ResumeGame already handles delayed gameplay-input recovery
         * so the UI click cannot immediately trigger a gameplay action.
         */
        ResumeGame();
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

        ShowPauseMenu();
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
            saveGameManager.HasSave(
                slot
            );

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

    public void OpenSettings()
    {
        HideAllPanels();

        settingsPanel.SetActive(true);

        Debug.Log("Pause Menu: Opening Settings.");
    }

    public void OpenControls()
    {
        HideAllPanels();

        controlsPanel.SetActive(true);

        Debug.Log("Pause Menu: Opening Controls.");
    }

    public void OpenMainMenuConfirmation()
    {
        HideAllPanels();

        mainMenuConfirmationPanel.SetActive(true);

        Debug.Log("Pause Menu: Opening Main Menu confirmation.");
    }

    public void OpenQuitConfirmation()
    {
        HideAllPanels();

        quitConfirmationPanel.SetActive(true);

        Debug.Log("Pause Menu: Opening Quit confirmation.");
    }

    public void ShowPauseMenu()
    {
        HideAllPanels();

        pauseMenuPanel.SetActive(true);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        Debug.Log(
            $"Pause Menu: Loading {mainMenuSceneName}."
        );

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;

        Debug.Log("Pause Menu: Quitting game.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HideAllPanels()
    {
        pauseMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(false);
        mainMenuConfirmationPanel.SetActive(false);
        quitConfirmationPanel.SetActive(false);

        if (loadSavePanel != null)
        {
            loadSavePanel.SetActive(
                false
            );
        }

        if (loadConfirmationPanel != null)
        {
            loadConfirmationPanel.SetActive(
                false
            );
        }
    }

    private IEnumerator RestoreGameplayInputAfterResume()
    {
        if (Mouse.current != null)
        {
            while (Mouse.current.leftButton.isPressed)
            {
                yield return null;
            }
        }

        yield return null;

        SetGameplayInputEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        resumeCoroutine = null;
    }

    private void SetGameplayInputEnabled(bool enabled)
    {
        if (cameraController != null)
        {
            cameraController.enabled = enabled;
        }

        if (playerCombat != null)
        {
            playerCombat.enabled = enabled;
        }
    }
}