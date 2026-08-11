using UnityEngine;

public class InteractPrompt : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas promptCanvas;

    [Header("Hover")]
    [Tooltip("How far the prompt moves up and down.")]
    [SerializeField] private float hoverHeight = 0.1f;

    [Tooltip("How quickly the prompt moves up and down.")]
    [SerializeField] private float hoverSpeed = 2f;

    private Camera mainCamera;

    private Vector3 startingLocalPosition;

    private void Awake()
    {
        if (promptCanvas == null)
        {
            promptCanvas =
                GetComponentInChildren<Canvas>();
        }

        mainCamera =
            Camera.main;

        /*
         * Store the position assigned in the prefab
         * or on the individual interactable.
         */
        startingLocalPosition =
            transform.localPosition;

        ValidateReferences();
    }

    private void LateUpdate()
    {
        FaceCamera();
        UpdateHover();
    }

    public void Show()
    {
        if (promptCanvas == null)
        {
            return;
        }

        promptCanvas.enabled = true;
    }

    public void Hide()
    {
        if (promptCanvas == null)
        {
            return;
        }

        promptCanvas.enabled = false;
    }

    private void FaceCamera()
    {
        if (mainCamera == null)
        {
            mainCamera =
                Camera.main;

            if (mainCamera == null)
            {
                return;
            }
        }

        transform.rotation =
            Quaternion.LookRotation(
                transform.position -
                mainCamera.transform.position
            );
    }

    private void UpdateHover()
    {
        float hoverOffset =
            Mathf.Sin(
                Time.time * hoverSpeed
            ) * hoverHeight;

        transform.localPosition =
            startingLocalPosition +
            Vector3.up * hoverOffset;
    }

    private void ValidateReferences()
    {
        if (promptCanvas == null)
        {
            Debug.LogError(
                $"{name}: InteractPrompt could not find a Canvas.",
                this
            );
        }
    }

    private void OnValidate()
    {
        hoverHeight =
            Mathf.Max(
                0f,
                hoverHeight
            );

        hoverSpeed =
            Mathf.Max(
                0f,
                hoverSpeed
            );
    }
}