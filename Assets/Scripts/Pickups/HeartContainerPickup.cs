using UnityEngine;

public class HeartContainerPickup : MonoBehaviour
{
    [Header("Heart Upgrade")]
    [Tooltip(
        "Amount of maximum health added when collected. " +
        "4 health equals one full heart."
    )]
    [SerializeField]
    private int healthIncrease =
        PlayerStatsNew.HealthPerHeart;

    [Header("HUD")]
    [Tooltip(
        "Optional reference to the player's Health HUD. " +
        "If left empty, it will be found automatically."
    )]
    [SerializeField]
    private PlayerHealthHUD playerHealthHUD;

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

    [Header("Pickup")]
    [Tooltip(
        "Optional delay before the pickup object is destroyed."
    )]
    [SerializeField]
    private float destroyDelay = 0f;

    private bool hasBeenCollected;

    private void Awake()
    {
        /*
         * A scene reference usually cannot be stored directly
         * on a prefab asset, so automatically find the HUD
         * if one was not manually assigned.
         */
        if (playerHealthHUD == null)
        {
            playerHealthHUD =
                FindAnyObjectByType<PlayerHealthHUD>();
        }
    }

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (hasBeenCollected)
        {
            return;
        }

        PlayerStatsNew playerStats =
            other.GetComponentInParent<PlayerStatsNew>();

        if (playerStats == null)
        {
            return;
        }

        Collect(
            playerStats
        );
    }

    private void Collect(
        PlayerStatsNew playerStats
    )
    {
        hasBeenCollected = true;

        /*
         * Capture the old state before increasing health.
         * The HUD uses this to determine how many hearts
         * need to visibly refill.
         */
        int startingHealth =
            playerStats.CurrentHealth;

        int startingMaxHealth =
            playerStats.MaxHealth;

        /*
         * Increase maximum health and fully restore the player.
         *
         * This also broadcasts OnHealthChanged, which begins
         * the HUD's Heart Container refill animation.
         */
        playerStats.IncreaseMaxHealth(
            healthIncrease
        );

        int targetHealth =
            playerStats.CurrentHealth;

        int targetMaxHealth =
            playerStats.MaxHealth;

        float effectDuration =
            CalculateEffectDuration(
                startingHealth,
                startingMaxHealth,
                targetHealth,
                targetMaxHealth
            );

        PlayPickupEffect(
            playerStats.transform,
            effectDuration
        );

        Debug.Log(
            $"{name}: Heart Container collected. " +
            $"Player maximum health is now " +
            $"{playerStats.MaxHealth}.",
            this
        );

        DisablePickupColliders();

        Destroy(
            gameObject,
            destroyDelay
        );
    }

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

        /*
         * Spawn the effect around the middle of the player
         * and keep it parented so it follows movement.
         */
        effect.transform.localPosition =
            Vector3.up *
            effectHeightOffset;

        /*
         * Preserve the particle prefab's authored rotation.
         */
        effect.transform.localRotation =
            pickupEffectPrefab.transform.localRotation;

        /*
         * Preserve the prefab's authored scale.
         */
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

            /*
             * Match non-looping particle emission duration
             * to the HUD reward animation.
             */
            ParticleSystem.MainModule main =
                particleSystem.main;

            if (!main.loop)
            {
                main.duration =
                    duration;
            }

            /*
             * Restart the particle after adjusting duration.
             */
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

    private void OnValidate()
    {
        healthIncrease =
            Mathf.Max(
                PlayerStatsNew.HealthPerHeart,
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