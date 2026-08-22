using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VictoryTrigger : MonoBehaviour
{
    // =========================================================
    // VICTORY
    // =========================================================

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

    // =========================================================
    // SCENE
    // =========================================================

    [Header("Scene")]
    [Tooltip(
        "Name of the Main Menu scene."
    )]
    [SerializeField]
    private string mainMenuSceneName =
        "Game_Start";

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [Tooltip(
        "Optional HUD notification banner. " +
        "If left empty, it will be found automatically."
    )]
    [SerializeField]
    private HUDNotificationBanner notificationBanner;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private bool victoryTriggered;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (notificationBanner == null)
        {
            notificationBanner =
                FindAnyObjectByType<HUDNotificationBanner>();
        }
    }

    // =========================================================
    // TRIGGER
    // =========================================================

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (victoryTriggered)
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        TriggerVictory();
    }

    private bool IsPlayerCollider(
        Collider other
    )
    {
        if (other == null)
        {
            return false;
        }

        Transform currentTransform =
            other.transform;

        while (currentTransform != null)
        {
            if (
                currentTransform.CompareTag(
                    "Player"
                )
            )
            {
                return true;
            }

            currentTransform =
                currentTransform.parent;
        }

        return false;
    }

    // =========================================================
    // VICTORY
    // =========================================================

    private void TriggerVictory()
    {
        victoryTriggered =
            true;

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

    // =========================================================
    // SCENE TRANSITION
    // =========================================================

    private IEnumerator ReturnToMainMenu()
    {
        yield return new WaitForSeconds(
            returnDelay
        );

        SceneManager.LoadScene(
            mainMenuSceneName
        );
    }

    // =========================================================
    // COLLIDERS
    // =========================================================

    private void DisableTriggerColliders()
    {
        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (
            Collider triggerCollider
            in colliders
        )
        {
            if (triggerCollider != null)
            {
                triggerCollider.enabled =
                    false;
            }
        }
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        returnDelay =
            Mathf.Max(
                0f,
                returnDelay
            );
    }
}