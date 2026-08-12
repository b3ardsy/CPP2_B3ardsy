using System;
using UnityEngine;

public class PlayerShieldEffect : MonoBehaviour
{
    [Header("Shrinking")]
    [Tooltip(
        "How long the Shield remains at full size " +
        "before it begins shrinking."
    )]
    [SerializeField] private float fullSizeDuration = 3f;

    [Tooltip(
        "How small the Shield becomes by the end " +
        "of its active duration."
    )]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float minimumScaleMultiplier = 0.1f;

    private PlayerStatsNew playerStats;

    private Action<PlayerShieldEffect>
        shieldEndedCallback;

    private Vector3 startingScale;

    private float duration;
    private float elapsedTime;

    private bool initialized;
    private bool protectionApplied;
    private bool hasEnded;

    public void Initialize(
        PlayerStatsNew stats,
        float activeDuration,
        Action<PlayerShieldEffect> onShieldEnded
    )
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        playerStats =
            stats;

        duration =
            Mathf.Max(
                0.1f,
                activeDuration
            );

        shieldEndedCallback =
            onShieldEnded;

        startingScale =
            transform.localScale;

        /*
         * Prevent the full-size phase from lasting
         * longer than the complete Shield duration.
         */
        fullSizeDuration =
            Mathf.Clamp(
                fullSizeDuration,
                0f,
                duration
            );

        if (playerStats == null)
        {
            Debug.LogError(
                $"{name}: PlayerShieldEffect received " +
                "no PlayerStatsNew.",
                this
            );

            EndShield();
            return;
        }

        playerStats.AddShieldProtection();

        protectionApplied = true;
    }

    private void Update()
    {
        if (
            !initialized ||
            hasEnded
        )
        {
            return;
        }

        elapsedTime +=
            Time.deltaTime;

        UpdateShieldScale();

        if (
            elapsedTime >=
            duration
        )
        {
            EndShield();
        }
    }

    private void UpdateShieldScale()
    {
        /*
         * Phase 1:
         * Keep the Shield completely full-sized.
         */
        if (
            elapsedTime <=
            fullSizeDuration
        )
        {
            transform.localScale =
                startingScale;

            return;
        }

        /*
         * Phase 2:
         * Shrink during the remaining lifetime.
         */
        float shrinkDuration =
            duration -
            fullSizeDuration;

        if (
            shrinkDuration <=
            0.001f
        )
        {
            transform.localScale =
                startingScale *
                minimumScaleMultiplier;

            return;
        }

        float shrinkElapsedTime =
            elapsedTime -
            fullSizeDuration;

        float normalizedShrinkTime =
            Mathf.Clamp01(
                shrinkElapsedTime /
                shrinkDuration
            );

        float scaleMultiplier =
            Mathf.Lerp(
                1f,
                minimumScaleMultiplier,
                normalizedShrinkTime
            );

        transform.localScale =
            startingScale *
            scaleMultiplier;
    }

    public void EndShield()
    {
        if (hasEnded)
        {
            return;
        }

        hasEnded = true;

        RemoveProtection();

        shieldEndedCallback?.Invoke(
            this
        );

        shieldEndedCallback =
            null;

        Destroy(
            gameObject
        );
    }

    private void RemoveProtection()
    {
        if (!protectionApplied)
        {
            return;
        }

        protectionApplied =
            false;

        if (playerStats != null)
        {
            playerStats.RemoveShieldProtection();
        }
    }

    private void OnDestroy()
    {
        /*
         * Safety cleanup in case the Shield is destroyed
         * externally before its timer finishes.
         */
        if (!hasEnded)
        {
            hasEnded = true;

            RemoveProtection();

            shieldEndedCallback?.Invoke(
                this
            );

            shieldEndedCallback =
                null;
        }
    }

    private void OnValidate()
    {
        fullSizeDuration =
            Mathf.Max(
                0f,
                fullSizeDuration
            );

        minimumScaleMultiplier =
            Mathf.Clamp(
                minimumScaleMultiplier,
                0.01f,
                1f
            );
    }
}