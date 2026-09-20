using UnityEngine;

public class DemoEndingDoor : MonoBehaviour, IInteract
{
    [Header("Ending")]
    [SerializeField]
    private DemoEndingController demoEndingController;

    private bool endingStarted;

    private void Awake()
    {
        if (demoEndingController == null)
        {
            demoEndingController =
                FindAnyObjectByType<DemoEndingController>();
        }
    }

    public void Interact(
        PlayerInteraction interactor
    )
    {
        if (endingStarted)
        {
            return;
        }

        if (interactor == null)
        {
            return;
        }

        if (demoEndingController == null)
        {
            Debug.LogError(
                $"{name}: DemoEndingController could not be found.",
                this
            );

            return;
        }

        endingStarted = true;

        /*
         * Remove the door from the player's current
         * interaction target before beginning the ending.
         */
        interactor.ClearCurrentInteractable();

        /*
         * Begin the end-of-demo sequence.
         */
        demoEndingController.StartDemoEnding();

        Debug.Log(
            $"{name}: Demo ending started.",
            this
        );
    }
}