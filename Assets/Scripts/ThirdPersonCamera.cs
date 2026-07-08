using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivityX = 0.35f;
    [SerializeField] private float mouseSensitivityY = 0.35f;

    [Header("Pitch Limits")]
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 45f;

    [Header("Starting View")]
    [SerializeField] private float startingPitch = 10f;
    [SerializeField] private float startingYawOffset = 0f;
    [SerializeField] private float heightOffset = 1.5f;

    private float yaw;
    private float pitch;

    // Ignore the first few frames to prevent an initial mouse jump.
    private int framesToIgnore = 5;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError("ThirdPersonCamera: Player reference is missing.");
            enabled = false;
            return;
        }

        pitch = startingPitch;
        yaw = player.eulerAngles.y + startingYawOffset;

        ApplyCameraPosition();
    }

    private void Start()
    {
        LockCursor();
    }

    private void LateUpdate()
    {
        if (Mouse.current == null)
            return;

        // Press Escape to unlock the cursor while testing.
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        // Left-click to lock it again.
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }

        if (framesToIgnore > 0)
        {
            framesToIgnore--;
            ApplyCameraPosition();
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivityX;
        pitch -= mouseDelta.y * mouseSensitivityY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        ApplyCameraPosition();
    }

    private void ApplyCameraPosition()
    {
        transform.position = player.position + Vector3.up * heightOffset;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}