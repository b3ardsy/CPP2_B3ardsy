using UnityEngine;
using UnityEngine.UI;

public class ExitPromptUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject exitPrompt;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Player References")]
    [SerializeField] private PlayerMovement3D playerMovement;
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;

    private bool isPromptOpen;

    private void Awake()
    {
        if (exitPrompt != null)
        {
            exitPrompt.SetActive(false);
        }

        if (yesButton != null)
        {
            yesButton.onClick.AddListener(QuitGame);
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(ClosePrompt);
        }
    }

    public void OpenPrompt()
    {
        if (isPromptOpen)
        {
            return;
        }

        isPromptOpen = true;

        if (exitPrompt != null)
        {
            exitPrompt.SetActive(true);
        }

        // Freeze gameplay.
        Time.timeScale = 0f;

        // Stop the movement and camera scripts from reading input.
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.enabled = false;
        }

        // Release the mouse so the player can click the buttons.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Select the No button by default.
        if (noButton != null)
        {
            noButton.Select();
        }
    }

    public void ClosePrompt()
    {
        isPromptOpen = false;

        if (exitPrompt != null)
        {
            exitPrompt.SetActive(false);
        }

        // Resume gameplay.
        Time.timeScale = 1f;

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void QuitGame()
    {
        // Always restore time scale before leaving Play mode.
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        // Prevent the project from remaining paused if this object
        // is destroyed while the prompt is open.
        Time.timeScale = 1f;
    }
}