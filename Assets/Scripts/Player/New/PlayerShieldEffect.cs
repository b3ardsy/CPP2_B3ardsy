using System;
using UnityEngine;

public class PlayerShieldEffect : MonoBehaviour
{
    // =========================================================
    // REFLECTION
    // =========================================================

    [Header("Reflection")]
    [Tooltip(
        "Trigger collider covering the Shield bubble."
    )]
    [SerializeField]
    private Collider reflectionCollider;

    // =========================================================
    // SHRINKING
    // =========================================================

    [Header("Shrinking")]
    [Tooltip(
        "How long the Shield remains at full size " +
        "before it begins shrinking."
    )]
    [SerializeField]
    private float fullSizeDuration = 3f;

    [Tooltip(
        "How small the Shield becomes by the end " +
        "of its active duration."
    )]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float minimumScaleMultiplier = 0.1f;

    // =========================================================
    // REFERENCES
    // =========================================================

    private PlayerDamageController playerDamageController;

    private Action<PlayerShieldEffect>
        shieldEndedCallback;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private Vector3 startingScale;

    private float duration;
    private float elapsedTime;

    private bool initialized;
    private bool protectionApplied;
    private bool hasEnded;

    // =========================================================
    // PUBLIC PROPERTIES
    // =========================================================

    public bool IsActive =>
        initialized &&
        !hasEnded;

    // =========================================================
    // INITIALIZATION
    // =========================================================

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
        PlayerDamageController damageController,
        float activeDuration,
        Action<PlayerShieldEffect> onShieldEnded
    )
    {
        if (initialized)
        {
            return;
        }

        initialized =
            true;

        playerDamageController =
            damageController;

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

        if (playerDamageController == null)
        {
            Debug.LogError(
                $"{name}: PlayerShieldEffect received " +
                "no PlayerDamageController.",
                this
            );

            EndShield();
            return;
        }

        playerDamageController.AddShieldProtection();

        protectionApplied =
            true;
    }

    // =========================================================
    // UPDATE
    // =========================================================

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

    // =========================================================
    // SCALE
    // =========================================================

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

    // =========================================================
    // REFLECTION
    // =========================================================

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

        if (playerDamageController == null)
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
            playerDamageController.gameObject
        );
    }

    // =========================================================
    // END SHIELD
    // =========================================================

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

        if (playerDamageController != null)
        {
            playerDamageController.RemoveShieldProtection();
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

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

    // =========================================================
    // VALIDATION
    // =========================================================

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