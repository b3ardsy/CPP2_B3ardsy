using System;
using TMPro;
using UnityEngine;

public class ShrineSaveUIController : MonoBehaviour
{
    // =========================================================
    // SINGLETON
    // =========================================================

    public static ShrineSaveUIController Instance
    {
        get;
        private set;
    }

    // =========================================================
    // UI
    // =========================================================

    [Header("UI Root")]
    [Tooltip(
        "Full-screen ShrineSaveUI object. Keep this object inactive " +
        "by default. Put this controller on HUDCanvas or another " +
        "always-active scene object."
    )]
    [SerializeField]
    private GameObject shrineSaveUIRoot;

    [Header("Panels")]
    [SerializeField]
    private GameObject attunePanel;

    [SerializeField]
    private GameObject saveSlotPanel;

    [SerializeField]
    private GameObject overwritePanel;

    [Header("Save Slot Text")]
    [SerializeField]
    private TMP_Text slot1Text;

    [SerializeField]
    private TMP_Text slot2Text;

    [SerializeField]
    private TMP_Text slot3Text;

    [Header("Overwrite")]
    [SerializeField]
    private TMP_Text overwriteBodyText;

    // =========================================================
    // SAVE SYSTEM
    // =========================================================

    [Header("Save System")]
    [SerializeField]
    private SaveGameManager saveGameManager;

    [Header("Input / Pause")]
    [SerializeField]
    private PauseMenuController pauseMenuController;

    // =========================================================
    // RUNTIME
    // =========================================================

    private CheckpointShrine currentShrine;
    private PlayerInteraction currentInteractor;

    private int pendingSlot;

    private float previousTimeScale;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    private bool isOpen;

    public bool IsOpen =>
        isOpen;

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
                this
            );

            return;
        }

        Instance =
            this;

        if (saveGameManager == null)
        {
            saveGameManager =
                FindAnyObjectByType<SaveGameManager>();
        }

        if (pauseMenuController == null)
        {
            pauseMenuController =
                FindAnyObjectByType<PauseMenuController>();
        }

        if (shrineSaveUIRoot != null)
        {
            shrineSaveUIRoot.SetActive(
                false
            );
        }
    }

    // =========================================================
    // OPEN / CLOSE
    // =========================================================

    public void Open(
        CheckpointShrine shrine,
        PlayerInteraction interactor
    )
    {
        if (
            shrine == null ||
            interactor == null ||
            isOpen
        )
        {
            return;
        }

        if (saveGameManager == null)
        {
            saveGameManager =
                FindAnyObjectByType<SaveGameManager>();
        }

        if (saveGameManager == null)
        {
            Debug.LogError(
                "ShrineSaveUIController: No SaveGameManager exists.",
                this
            );

            return;
        }

        currentShrine =
            shrine;

        currentInteractor =
            interactor;

        pendingSlot =
            0;

        previousTimeScale =
            Time.timeScale;

        previousCursorLockMode =
            Cursor.lockState;

        previousCursorVisible =
            Cursor.visible;

        isOpen =
            true;

        currentInteractor.SetInteractionBlocked(
            true
        );

        if (pauseMenuController != null)
        {
            pauseMenuController.SetExternalModalOpen(
                true
            );
        }

        Time.timeScale =
            0f;

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;

        shrineSaveUIRoot.SetActive(
            true
        );

        ShowAttunePanel();
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        if (shrineSaveUIRoot != null)
        {
            shrineSaveUIRoot.SetActive(
                false
            );
        }

        Time.timeScale =
            previousTimeScale;

        Cursor.lockState =
            previousCursorLockMode;

        Cursor.visible =
            previousCursorVisible;

        if (pauseMenuController != null)
        {
            pauseMenuController.SetExternalModalOpen(
                false
            );
        }

        if (currentInteractor != null)
        {
            currentInteractor.SetInteractionBlocked(
                false
            );
        }

        currentShrine =
            null;

        currentInteractor =
            null;

        pendingSlot =
            0;

        isOpen =
            false;
    }

    // =========================================================
    // ATTUNE PANEL BUTTONS
    // =========================================================

    public void ConfirmAttune()
    {
        if (!isOpen)
        {
            return;
        }

        RefreshSlotText();
        ShowSaveSlotPanel();
    }

    public void CancelAttune()
    {
        Close();
    }

    // =========================================================
    // SAVE SLOT PANEL BUTTONS
    // =========================================================

    public void SelectSlot1()
    {
        SelectSlot(
            1
        );
    }

    public void SelectSlot2()
    {
        SelectSlot(
            2
        );
    }

    public void SelectSlot3()
    {
        SelectSlot(
            3
        );
    }

    public void BackToAttune()
    {
        pendingSlot =
            0;

        ShowAttunePanel();
    }

    private void SelectSlot(
        int slot
    )
    {
        if (
            !isOpen ||
            saveGameManager == null
        )
        {
            return;
        }

        pendingSlot =
            slot;

        if (
            saveGameManager.HasSave(
                slot
            )
        )
        {
            if (overwriteBodyText != null)
            {
                overwriteBodyText.text =
                    $"Save Slot {slot} already contains progress.\n" +
                    "Overwrite it?";
            }

            ShowOverwritePanel();

            return;
        }

        SaveToPendingSlot();
    }

    // =========================================================
    // OVERWRITE PANEL BUTTONS
    // =========================================================

    public void ConfirmOverwrite()
    {
        SaveToPendingSlot();
    }

    public void CancelOverwrite()
    {
        pendingSlot =
            0;

        RefreshSlotText();
        ShowSaveSlotPanel();
    }

    // =========================================================
    // SAVE
    // =========================================================

    private void SaveToPendingSlot()
    {
        if (
            pendingSlot < 1 ||
            pendingSlot > 3 ||
            currentShrine == null ||
            currentInteractor == null ||
            saveGameManager == null
        )
        {
            return;
        }

        /*
         * Capture CURRENT player/world state at this shrine first.
         * This also makes this shrine the active runtime checkpoint
         * and turns off the candles on every other shrine.
         */
        bool attuned =
            currentShrine.Attune(
                currentInteractor
            );

        if (!attuned)
        {
            Debug.LogError(
                "ShrineSaveUIController: Shrine attunement failed.",
                this
            );

            return;
        }

        bool saved =
            saveGameManager.SaveGame(
                pendingSlot
            );

        if (!saved)
        {
            Debug.LogError(
                $"ShrineSaveUIController: Failed to save Slot {pendingSlot}.",
                this
            );

            RefreshSlotText();
            ShowSaveSlotPanel();

            return;
        }

        Debug.Log(
            $"ShrineSaveUIController: Shrine saved to Slot {pendingSlot}.",
            currentShrine
        );

        Close();
    }

    // =========================================================
    // SLOT DISPLAY
    // =========================================================

    private void RefreshSlotText()
    {
        UpdateSlotText(
            1,
            slot1Text
        );

        UpdateSlotText(
            2,
            slot2Text
        );

        UpdateSlotText(
            3,
            slot3Text
        );
    }

    private void UpdateSlotText(
        int slot,
        TMP_Text text
    )
    {
        if (
            text == null ||
            saveGameManager == null
        )
        {
            return;
        }

        if (
            !saveGameManager.HasSave(
                slot
            )
        )
        {
            text.text =
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
            text.text =
                $"SLOT {slot}\nUNAVAILABLE";

            return;
        }

        string displayTime =
            "Saved";

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
                        "MMM d • h:mm"
                    );
        }

        text.text =
            $"SLOT {slot}\n{displayTime}";
    }

    // =========================================================
    // PANEL NAVIGATION
    // =========================================================

    private void ShowAttunePanel()
    {
        SetPanels(
            true,
            false,
            false
        );
    }

    private void ShowSaveSlotPanel()
    {
        SetPanels(
            false,
            true,
            false
        );
    }

    private void ShowOverwritePanel()
    {
        SetPanels(
            false,
            false,
            true
        );
    }

    private void SetPanels(
        bool showAttune,
        bool showSlots,
        bool showOverwrite
    )
    {
        if (attunePanel != null)
        {
            attunePanel.SetActive(
                showAttune
            );
        }

        if (saveSlotPanel != null)
        {
            saveSlotPanel.SetActive(
                showSlots
            );
        }

        if (overwritePanel != null)
        {
            overwritePanel.SetActive(
                showOverwrite
            );
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance =
                null;
        }

        if (isOpen)
        {
            Time.timeScale =
                previousTimeScale;

            Cursor.lockState =
                previousCursorLockMode;

            Cursor.visible =
                previousCursorVisible;

            if (pauseMenuController != null)
            {
                pauseMenuController.SetExternalModalOpen(
                    false
                );
            }

            if (currentInteractor != null)
            {
                currentInteractor.SetInteractionBlocked(
                    false
                );
            }
        }
    }
}
