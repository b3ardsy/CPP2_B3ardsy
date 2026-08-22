using UnityEngine;

public class HeartContainerPickup : MonoBehaviour
{
    // =========================================================
    // HEART UPGRADE
    // =========================================================

    private const int HealthPerHeart = 4;

    [Header("Heart Upgrade")]
    [Tooltip(
        "Amount of maximum health added when collected. " +
        "4 health equals one full heart."
    )]
    [SerializeField]
    private int healthIncrease =
        HealthPerHeart;

    // =========================================================
    // HUD
    // =========================================================

    [Header("HUD")]
    [Tooltip(
        "Optional reference to the player's Health HUD. " +
        "If left empty, it will be found automatically."
    )]
    [SerializeField]
    private PlayerHealthHUD playerHealthHUD;

    // =========================================================
    // NOTIFICATION
    // =========================================================

    [Header("Notification")]
    [Tooltip(
        "Optional HUD banner used to display the health upgrade message. " +
        "If left empty, it will be found automatically."
    )]
    [SerializeField]
    private HUDNotificationBanner notificationBanner;

    [TextArea]
    [Tooltip(
        "Message displayed when the Heart Container is collected."
    )]
    [SerializeField]
    private string pickupMessage =
        "Your vitality grows: Maximum Health Increased";

    // =========================================================
    // PICKUP EFFECT
    // =========================================================

    [Header("Pickup Effect")]
    [Tooltip(
        "Particle effect spawned around the player " +
        "when the Heart Container is collected."
    )]
    [SerializeField]
    private GameObject pickupEffectPrefab;

    [Tooltip(
        "Vertical offset applied to the pickup effect."
    )]
    [SerializeField]
    private float effectHeightOffset = 1f;

    [Tooltip(
        "Fallback effect lifetime if the Health HUD " +
        "cannot be found."
    )]
    [SerializeField]
    private float fallbackEffectLifetime = 1.5f;

    // =========================================================
    // PICKUP
    // =========================================================

    [Header("Pickup")]
    [Tooltip(
        "Optional delay before the pickup object is destroyed."
    )]
    [SerializeField]
    private float destroyDelay = 0f;

    private bool hasBeenCollected;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (playerHealthHUD == null)
        {
            playerHealthHUD =
                FindAnyObjectByType<PlayerHealthHUD>();
        }

        if (notificationBanner == null)
        {
            notificationBanner =
                FindAnyObjectByType<HUDNotificationBanner>();
        }
    }

    // =========================================================
    // COLLECTION
    // =========================================================

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (
            hasBeenCollected ||
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

        Collect(
            health
        );
    }

    private void Collect(
        Health health
    )
    {
        hasBeenCollected =
            true;

        int startingHealth =
            health.CurrentHealth;

        int startingMaxHealth =
            health.MaxHealth;

        health.IncreaseMaxHealth(
            healthIncrease
        );

        int targetHealth =
            health.CurrentHealth;

        int targetMaxHealth =
            health.MaxHealth;

        float effectDuration =
            CalculateEffectDuration(
                startingHealth,
                startingMaxHealth,
                targetHealth,
                targetMaxHealth
            );

        PlayPickupEffect(
            health.transform,
            effectDuration
        );

        ShowPickupNotification();

        Debug.Log(
            $"{name}: Heart Container collected. " +
            $"Player maximum health is now {health.MaxHealth}.",
            this
        );

        DisablePickupColliders();

        Destroy(
            gameObject,
            destroyDelay
        );
    }

    // =========================================================
    // NOTIFICATION
    // =========================================================

    private void ShowPickupNotification()
    {
        if (notificationBanner == null)
        {
            Debug.LogWarning(
                $"{name}: HUDNotificationBanner could not be found.",
                this
            );

            return;
        }

        notificationBanner.ShowMessage(
            pickupMessage
        );
    }

    // =========================================================
    // EFFECT TIMING
    // =========================================================

    private float CalculateEffectDuration(
        int startingHealth,
        int startingMaxHealth,
        int targetHealth,
        int targetMaxHealth
    )
    {
        if (playerHealthHUD == null)
        {
            playerHealthHUD =
                FindAnyObjectByType<PlayerHealthHUD>();
        }

        if (playerHealthHUD == null)
        {
            return fallbackEffectLifetime;
        }

        return
            playerHealthHUD
                .GetHealthUpgradeAnimationDuration(
                    startingHealth,
                    startingMaxHealth,
                    targetHealth,
                    targetMaxHealth
                );
    }

    // =========================================================
    // PICKUP EFFECT
    // =========================================================

    private void PlayPickupEffect(
        Transform playerTransform,
        float duration
    )
    {
        if (
            pickupEffectPrefab == null ||
            playerTransform == null
        )
        {
            return;
        }

        GameObject effect =
            Instantiate(
                pickupEffectPrefab,
                playerTransform
            );

        effect.transform.localPosition =
            Vector3.up *
            effectHeightOffset;

        effect.transform.localRotation =
            pickupEffectPrefab.transform.localRotation;

        effect.transform.localScale =
            pickupEffectPrefab.transform.localScale;

        float finalDuration =
            Mathf.Max(
                0.1f,
                duration
            );

        MatchParticleDuration(
            effect,
            finalDuration
        );

        Destroy(
            effect,
            finalDuration
        );
    }

    private void MatchParticleDuration(
        GameObject effect,
        float duration
    )
    {
        ParticleSystem[] particleSystems =
            effect.GetComponentsInChildren<ParticleSystem>();

        foreach (
            ParticleSystem particleSystem
            in particleSystems
        )
        {
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main =
                particleSystem.main;

            if (!main.loop)
            {
                main.duration =
                    duration;
            }

            particleSystem.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmittingAndClear
            );

            particleSystem.Play(
                true
            );
        }
    }

    // =========================================================
    // COLLIDERS
    // =========================================================

    private void DisablePickupColliders()
    {
        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (
            Collider pickupCollider
            in colliders
        )
        {
            if (pickupCollider != null)
            {
                pickupCollider.enabled =
                    false;
            }
        }
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        healthIncrease =
            Mathf.Max(
                HealthPerHeart,
                healthIncrease
            );

        effectHeightOffset =
            Mathf.Max(
                0f,
                effectHeightOffset
            );

        fallbackEffectLifetime =
            Mathf.Max(
                0.1f,
                fallbackEffectLifetime
            );

        destroyDelay =
            Mathf.Max(
                0f,
                destroyDelay
            );
    }
}