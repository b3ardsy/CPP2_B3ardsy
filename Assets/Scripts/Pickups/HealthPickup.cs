using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    // =========================================================
    // FLOATING
    // =========================================================

    [Header("Floating")]
    [SerializeField] private float floatHeight = 0.25f;
    [SerializeField] private float floatSpeed = 2f;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private Vector3 startPosition;
    private bool collected;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Start()
    {
        startPosition =
            transform.position;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        FloatPickup();
    }

    // =========================================================
    // FLOATING
    // =========================================================

    private void FloatPickup()
    {
        float offset =
            Mathf.Sin(
                Time.time * floatSpeed
            ) *
            floatHeight;

        transform.position =
            startPosition +
            Vector3.up *
            offset;
    }

    // =========================================================
    // COLLECTION
    // =========================================================

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (
            collected ||
            !other.CompareTag("Player")
        )
        {
            return;
        }

        Health health =
            other.GetComponent<Health>();

        if (health == null)
        {
            health =
                other.GetComponentInParent<Health>();
        }

        if (health == null)
        {
            Debug.LogWarning(
                $"{name}: Health was not found on the Player.",
                this
            );

            return;
        }

        if (health.IsDead)
        {
            return;
        }

        collected =
            true;

        health.RestoreFullHealth();

        Debug.Log(
            $"{name}: Health pickup collected.",
            this
        );

        Destroy(
            gameObject
        );
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        floatHeight =
            Mathf.Max(
                0f,
                floatHeight
            );

        floatSpeed =
            Mathf.Max(
                0f,
                floatSpeed
            );
    }
}