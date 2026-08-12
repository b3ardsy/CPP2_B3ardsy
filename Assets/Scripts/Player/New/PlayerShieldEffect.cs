using System;
using UnityEngine;

public class PlayerShieldEffect : MonoBehaviour
{
    [Header("Reflection")]
    [Tooltip(
        "Trigger collider covering the Shield bubble."
    )]
    [SerializeField] private Collider reflectionCollider;

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

    public bool IsActive =>
        initialized &&
        !hasEnded;

    private void Awake()
    {
        if (reflectionCollider == null)
        {
            reflectionCollider =
                GetComponent<Collider>();
        }

        if (reflectionCollider == null)
        {
            reflectionCollider =
                GetComponentInChildren<Collider>();
        }

        if (reflectionCollider != null)
        {
            reflectionCollider.isTrigger =
                true;
        }
    }

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

        protectionApplied =
            true;
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
         * Keep the Shield at full size.
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

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (!IsActive)
        {
            return;
        }

        /*
         * Look for any projectile that supports
         * the common reflection interface.
         */
        IReflectableProjectile reflectableProjectile =
            other.GetComponentInParent
                <IReflectableProjectile>();

        if (reflectableProjectile == null)
        {
            return;
        }

        /*
         * Ownership is transferred to the player.
         *
         * The projectile itself is responsible for
         * reversing its current travel direction.
         */
        reflectableProjectile.Reflect(
            playerStats.gameObject
        );
    }

    public void EndShield()
    {
        if (hasEnded)
        {
            return;
        }

        hasEnded =
            true;

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
         * Safety cleanup in case the Shield is
         * externally destroyed.
         */
        if (!hasEnded)
        {
            hasEnded =
                true;

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

        if (reflectionCollider != null)
        {
            reflectionCollider.isTrigger =
                true;
        }
    }
}