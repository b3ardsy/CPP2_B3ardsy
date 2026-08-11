using UnityEngine;

public class PlayerShieldEffect : MonoBehaviour
{
    [Header("Shield")]
    [Tooltip("How long the Shield remains active.")]
    [SerializeField] private float duration = 3f;

    private PlayerStatsNew playerStats;

    private bool initialized;
    private bool protectionApplied;

    public void Initialize(
        PlayerStatsNew stats
    )
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        playerStats = stats;

        if (playerStats == null)
        {
            Debug.LogError(
                $"{name}: PlayerShieldEffect received no PlayerStatsNew.",
                this
            );

            Destroy(gameObject);
            return;
        }

        playerStats.AddShieldProtection();

        protectionApplied = true;

        Destroy(
            gameObject,
            duration
        );
    }

    private void OnDestroy()
    {
        RemoveProtection();
    }

    private void RemoveProtection()
    {
        if (!protectionApplied)
        {
            return;
        }

        protectionApplied = false;

        if (playerStats != null)
        {
            playerStats.RemoveShieldProtection();
        }
    }

    private void OnValidate()
    {
        duration =
            Mathf.Max(
                0.1f,
                duration
            );
    }
}