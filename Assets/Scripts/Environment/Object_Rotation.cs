using UnityEngine;

public class Object_Rotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 20f;

    [Tooltip("Reverse the rotation direction.")]
    [SerializeField] private bool reverseRotation = false;

    private void Update()
    {
        float direction =
            reverseRotation
                ? -1f
                : 1f;

        transform.Rotate(
            0f,
            0f,
            rotationSpeed *
            direction *
            Time.deltaTime,
            Space.Self
        );
    }

    private void OnValidate()
    {
        rotationSpeed =
            Mathf.Max(
                0f,
                rotationSpeed
            );
    }
}