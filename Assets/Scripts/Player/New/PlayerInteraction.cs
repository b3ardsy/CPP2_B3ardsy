using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("UI")]
    [Tooltip(
        "World-space E prompt shown when the player " +
        "is close enough to interact."
    )]
    [SerializeField] private GameObject interactionPrompt;

    private IInteract currentInteractable;

    private void Awake()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(
                false
            );
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (
            Keyboard.current.eKey
                .wasPressedThisFrame
        )
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        if (currentInteractable == null)
        {
            return;
        }

        currentInteractable.Interact(
            this
        );
    }

    private void OnTriggerEnter(
        Collider other
    )
    {
        IInteract interactable =
            other.GetComponent<IInteract>();

        if (interactable == null)
        {
            interactable =
                other.GetComponentInParent<IInteract>();
        }

        if (interactable == null)
        {
            return;
        }

        currentInteractable =
            interactable;

        SetPromptVisible(
            true
        );
    }

    private void OnTriggerExit(
        Collider other
    )
    {
        if (currentInteractable == null)
        {
            return;
        }

        IInteract interactable =
            other.GetComponent<IInteract>();

        if (interactable == null)
        {
            interactable =
                other.GetComponentInParent<IInteract>();
        }

        if (
            interactable == null ||
            !ReferenceEquals(
                currentInteractable,
                interactable
            )
        )
        {
            return;
        }

        ClearCurrentInteractable();
    }

    public PlayerWeaponManager GetWeaponManager()
    {
        return
            GetComponent<PlayerWeaponManager>();
    }

    public void ClearCurrentInteractable()
    {
        currentInteractable = null;

        SetPromptVisible(
            false
        );
    }

    private void SetPromptVisible(
        bool visible
    )
    {
        if (interactionPrompt == null)
        {
            return;
        }

        interactionPrompt.SetActive(
            visible
        );
    }

    private void OnDisable()
    {
        ClearCurrentInteractable();
    }
}