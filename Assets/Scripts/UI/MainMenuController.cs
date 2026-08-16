using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("The scene loaded when the Start button is pressed.")]
    [SerializeField] private string gameSceneName = "Game_01";

    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject quitConfirmationPanel;

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

        ShowMainMenu();
    }

    public void StartGame()
    {
        Debug.Log(
            $"Main Menu: Loading {gameSceneName}."
        );

        SceneManager.LoadScene(
            gameSceneName
        );
    }

    public void OpenSettings()
    {
        Debug.Log(
            "Main Menu: Opening Settings."
        );

        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
        creditsPanel.SetActive(false);
        quitConfirmationPanel.SetActive(false);
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

        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(true);
        quitConfirmationPanel.SetActive(false);
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

        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
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
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        quitConfirmationPanel.SetActive(false);
    }
}