using UnityEngine;

public class WaterWheelRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 15f;

    [Tooltip("Reverse the water wheel's rotation direction.")]
    [SerializeField] private bool reverseRotation = false;

    private void Update()
    {
        float direction = reverseRotation ? -1f : 1f;

        transform.Rotate(
            0f,
            0f,
            rotationSpeed * direction * Time.deltaTime,
            Space.Self
        );
    }
}