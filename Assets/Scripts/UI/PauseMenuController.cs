using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [Header("Pause UI")]
    [SerializeField] private GameObject pauseCanvas;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject mainMenuConfirmationPanel;
    [SerializeField] private GameObject quitConfirmationPanel;

    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "Game_Start";

    [Header("Gameplay Input")]
    [SerializeField] private MonoBehaviour cameraController;
    [SerializeField] private Player_Combat playerCombat;

    private bool isPaused;
    private Coroutine resumeCoroutine;

    public bool IsPaused => isPaused;

    private void Start()
    {
        ResumeGame();
    }

    private void Update()
    {
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