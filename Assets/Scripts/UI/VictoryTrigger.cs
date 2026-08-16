using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VictoryTrigger : MonoBehaviour
{
    [Header("Victory")]
    [Tooltip(
        "Message displayed when the player reaches the victory trigger."
    )]
    [TextArea]
    [SerializeField]
    private string victoryMessage =
        "VICTORY!";

    [Tooltip(
        "How long to wait before returning to the Main Menu."
    )]
    [SerializeField]
    private float returnDelay = 5f;

    [Header("Scene")]
    [Tooltip(
        "Name of the Main Menu scene."
    )]
    [SerializeField]
    private string mainMenuSceneName =
        "Game_Start";

    [Header("References")]
    [Tooltip(
        "Optional HUD notification banner. " +
        "If left empty, it will be found automatically."
    )]
    [SerializeField]
    private HUDNotificationBanner notificationBanner;

    private bool victoryTriggered;

    private void Awake()
    {
        if (notificationBanner == null)
        {
            notificationBanner =
                FindAnyObjectByType<HUDNotificationBanner>();
        }
    }

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (victoryTriggered)
        {
            return;
        }

        PlayerStatsNew playerStats =
            other.GetComponentInParent<PlayerStatsNew>();

        if (playerStats == null)
        {
            return;
        }

        TriggerVictory();
    }

    private void TriggerVictory()
    {
        victoryTriggered = true;

        DisableTriggerColliders();

        if (notificationBanner != null)
        {
            notificationBanner.ShowMessage(
                victoryMessage
            );
        }
        else
        {
            Debug.LogWarning(
                $"{name}: HUDNotificationBanner could not be found.",
                this
            );
        }

        Debug.Log(
            $"{name}: Victory triggered. " +
            $"Returning to {mainMenuSceneName} " +
            $"in {returnDelay:0.0} seconds.",
            this
        );

        StartCoroutine(
            ReturnToMainMenu()
        );
    }

    private IEnumerator ReturnToMainMenu()
    {
        yield return new WaitForSeconds(
            returnDelay
        );

        SceneManager.LoadScene(
            mainMenuSceneName
        );
    }

    private void DisableTriggerColliders()
    {
        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider triggerCollider in colliders)
        {
            if (triggerCollider != null)
            {
                triggerCollider.enabled =
                    false;
            }
        }
    }

    private void OnValidate()
    {
        returnDelay =
            Mathf.Max(
                0f,
                returnDelay
            );
    }
}