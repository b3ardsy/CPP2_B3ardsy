using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Prompt")]
    [Tooltip("Shared world-space interaction prompt used for all interactables.")]
    [SerializeField] private GameObject interactionPrompt;

    [Tooltip("Extra space between the top of an interactable and the prompt.")]
    [SerializeField] private float promptHeightOffset = 0.35f;

    private IInteract currentInteractable;
    private Transform currentInteractableTransform;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                $"{name}: No Interaction Prompt has been assigned.",
                this
            );
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    private void LateUpdate()
    {
        if (
            currentInteractable == null ||
            currentInteractableTransform == null ||
            interactionPrompt == null
        )
        {
            return;
        }

        UpdatePromptPosition();
        FacePromptTowardCamera();
    }

    private void TryInteract()
    {
        if (currentInteractable == null)
        {
            return;
        }

        currentInteractable.Interact(this);
    }

    private void OnTriggerEnter(Collider other)
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

        currentInteractable = interactable;

        MonoBehaviour interactableBehaviour =
            interactable as MonoBehaviour;

        if (interactableBehaviour != null)
        {
            currentInteractableTransform =
                interactableBehaviour.transform;
        }
        else
        {
            currentInteractableTransform =
                other.transform;
        }

        UpdatePromptPosition();

        SetPromptVisible(true);

        Debug.Log(
            $"{name}: Interaction available with " +
            $"{currentInteractableTransform.name}.",
            this
        );
    }

    private void OnTriggerExit(Collider other)
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

    private void UpdatePromptPosition()
    {
        if (
            interactionPrompt == null ||
            currentInteractableTransform == null
        )
        {
            return;
        }

        Vector3 promptPosition =
            currentInteractableTransform.position;

        Renderer[] renderers =
            currentInteractableTransform
                .GetComponentsInChildren<Renderer>();

        bool foundValidRenderer = false;
        Bounds combinedBounds = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            if (
                renderer == null ||
                renderer is ParticleSystemRenderer ||
                renderer is TrailRenderer ||
                renderer is LineRenderer
            )
            {
                continue;
            }

            if (!foundValidRenderer)
            {
                combinedBounds =
                    renderer.bounds;

                foundValidRenderer =
                    true;
            }
            else
            {
                combinedBounds.Encapsulate(
                    renderer.bounds
                );
            }
        }

        if (foundValidRenderer)
        {
            promptPosition =
                new Vector3(
                    combinedBounds.center.x,
                    combinedBounds.max.y +
                        promptHeightOffset,
                    combinedBounds.center.z
                );
        }
        else
        {
            promptPosition +=
                Vector3.up *
                promptHeightOffset;
        }

        interactionPrompt.transform.position =
            promptPosition;
    }

    private void FacePromptTowardCamera()
    {
        if (
            interactionPrompt == null ||
            mainCamera == null
        )
        {
            return;
        }

        interactionPrompt.transform.rotation =
            Quaternion.LookRotation(
                mainCamera.transform.forward,
                mainCamera.transform.up
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

    public Player_WeaponManager GetWeaponManager()
    {
        return
            GetComponent<Player_WeaponManager>();
    }

    public void ClearCurrentInteractable()
    {
        currentInteractable = null;
        currentInteractableTransform = null;

        SetPromptVisible(false);
    }

    private void OnDisable()
    {
        ClearCurrentInteractable();
    }
}