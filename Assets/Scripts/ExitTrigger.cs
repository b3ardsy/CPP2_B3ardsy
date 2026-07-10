using UnityEngine;

public class ExitTrigger : MonoBehaviour
{
    [SerializeField] private ExitPromptUI exitPromptUI;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (exitPromptUI != null)
        {
            exitPromptUI.OpenPrompt();
        }
        else
        {
            Debug.LogWarning(
                "ExitTrigger: ExitPromptUI reference is missing."
            );
        }
    }
}