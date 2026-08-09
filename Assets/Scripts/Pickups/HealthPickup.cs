using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    [Header("Floating")]
    [SerializeField] private float floatHeight = 0.25f;
    [SerializeField] private float floatSpeed = 2f;

    private Vector3 startPosition;
    private bool collected;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        FloatPickup();
    }

    private void FloatPickup()
    {
        float offset =
            Mathf.Sin(Time.time * floatSpeed) *
            floatHeight;

        transform.position =
            startPosition +
            Vector3.up * offset;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (
            collected ||
            !other.CompareTag("Player")
        )
        {
            return;
        }

        PlayerStatsNew playerStats =
            other.GetComponent<PlayerStatsNew>();

        if (playerStats == null)
        {
            playerStats =
                other.GetComponentInParent<PlayerStatsNew>();
        }

        if (playerStats == null)
        {
            Debug.LogWarning(
                $"{name}: PlayerStatsNew was not found.",
                this
            );

            return;
        }

        if (playerStats.IsDead)
        {
            return;
        }

        collected = true;

        playerStats.RestoreFullHealth();

        Debug.Log(
            $"{name}: Health pickup collected.",
            this
        );

        Destroy(gameObject);
    }
}