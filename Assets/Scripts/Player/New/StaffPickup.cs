using UnityEngine;

public class StaffPickup : MonoBehaviour, IInteract
{
    [Header("References")]
    [SerializeField] private InteractPrompt interactPrompt;

    private bool hasBeenCollected;

    private void Awake()
    {
        if (interactPrompt == null)
        {
            interactPrompt =
                GetComponentInChildren<InteractPrompt>();
        }

        if (interactPrompt == null)
        {
            Debug.LogWarning(
                $"{name}: StaffPickup could not find an InteractPrompt.",
                this
            );
            return;
        }

        /*
         * The prompt should always start hidden.
         */
        interactPrompt.Hide();
    }

    public void Interact(
        PlayerInteraction interactor
    )
    {
        if (hasBeenCollected)
        {
            return;
        }

        if (interactor == null)
        {
            return;
        }

        PlayerWeaponManager weaponManager =
            interactor.GetWeaponManager();

        if (weaponManager == null)
        {
            Debug.LogError(
                $"{name}: Player does not have a PlayerWeaponManager.",
                this
            );

            return;
        }

        hasBeenCollected = true;

        weaponManager.UnlockStaff();

        if (interactPrompt != null)
        {
            interactPrompt.Hide();
        }

        interactor.ClearCurrentInteractable();

        Debug.Log(
            $"{name}: Staff picked up.",
            this
        );

        Destroy(gameObject);
    }

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (hasBeenCollected)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (interactPrompt != null)
        {
            interactPrompt.Show();
        }
    }

    private void OnTriggerExit(
        Collider other
    )
    {
        if (hasBeenCollected)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (interactPrompt != null)
        {
            interactPrompt.Hide();
        }
    }
}