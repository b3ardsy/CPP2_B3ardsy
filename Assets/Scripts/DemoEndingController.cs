using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DemoEndingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup endingCanvasGroup;
    [SerializeField] private TMP_Text thankYouText;
    [SerializeField] private Player_Controller playerController;

    [Header("Ending Settings")]
    [SerializeField] private float fadeDuration = 2f;
    [SerializeField] private float messageDuration = 4f;
    [SerializeField] private string mainMenuSceneName = "Game_Start";

    private bool endingStarted;

    private void Awake()
    {
        if (endingCanvasGroup != null)
        {
            endingCanvasGroup.alpha = 0f;
            endingCanvasGroup.interactable = false;
            endingCanvasGroup.blocksRaycasts = false;
        }

        if (thankYouText != null)
        {
            thankYouText.alpha = 0f;
        }
    }

    public void StartDemoEnding()
    {
        if (endingStarted)
            return;

        endingStarted = true;

        StartCoroutine(EndingSequence());
    }

    private IEnumerator EndingSequence()
    {
        /*
         * Lock player movement once the ending begins.
         */
        if (playerController != null)
        {
            playerController.AddMovementLock(this);
        }

        /*
         * Fade the screen to black.
         */
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(elapsed / fadeDuration);

            endingCanvasGroup.alpha = progress;

            yield return null;
        }

        endingCanvasGroup.alpha = 1f;

        /*
         * Show the thank-you message.
         */
        if (thankYouText != null)
        {
            thankYouText.alpha = 1f;
        }

        /*
         * Leave the message on screen.
         */
        yield return new WaitForSecondsRealtime(
            messageDuration
        );

        /*
         * Return to the Main Menu.
         */
        SceneManager.LoadScene(
            mainMenuSceneName
        );
    }
}