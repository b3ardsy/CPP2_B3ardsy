using System;
using UnityEngine;

public class Player_ShieldEffect : MonoBehaviour
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

    private Player_DamageController playerDamageController;

    private Action<Player_ShieldEffect>
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
        Player_DamageController damageController,
        float activeDuration,
        Action<Player_ShieldEffect> onShieldEnded
    )
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

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
                $"{name}: Player_ShieldEffect received " +
                "no Player_DamageController.",
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
        if (
            elapsedTime <=
            fullSizeDuration
        )
        {
            transform.localScale =
                startingScale;

            return;
        }

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