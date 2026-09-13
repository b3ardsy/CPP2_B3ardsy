using UnityEngine;

[RequireComponent(typeof(PersistentID))]
public class HeartContainerPickup : MonoBehaviour, ICheckpointResettable
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

    private enum EffectTimingMode
    {
        MatchHealthAnimation,
        FixedDuration
    }

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
        "Determines whether the pickup effect matches the Health HUD " +
        "animation duration or uses a fixed duration."
    )]
    [SerializeField]
    private EffectTimingMode effectTimingMode =
        EffectTimingMode.MatchHealthAnimation;

    [Tooltip(
        "Effect duration when Fixed Duration mode is selected."
    )]
    [SerializeField]
    private float fixedEffectDuration = 3f;

    [Tooltip(
        "Extra time after the main effect duration so particles " +
        "already emitted can naturally finish before the object is destroyed."
    )]
    [SerializeField]
    private float particleTailDuration = 0.75f;

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

    private Coroutine deactivateCoroutine;
    private Collider[] pickupColliders;

    public bool IsCheckpointAvailable =>
        !hasBeenCollected;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        pickupColliders =
            GetComponentsInChildren<Collider>(
                true
            );

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

        AudioManager.Instance?.Play(
            SoundId.HeartPickup,
            transform.position
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

        if (deactivateCoroutine != null)
        {
            StopCoroutine(
                deactivateCoroutine
            );
        }

        deactivateCoroutine =
            StartCoroutine(
                DeactivateAfterDelay()
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
        /*
         * Fixed mode ignores the HUD animation entirely.
         */
        if (
            effectTimingMode ==
            EffectTimingMode.FixedDuration
        )
        {
            return
                fixedEffectDuration;
        }

        /*
         * Match Health Animation mode.
         */
        if (playerHealthHUD == null)
        {
            playerHealthHUD =
                FindAnyObjectByType<PlayerHealthHUD>();
        }

        if (playerHealthHUD == null)
        {
            return
                fallbackEffectLifetime;
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

        /*
         * Give particles emitted near the end of the effect
         * some extra time to naturally finish before destroying
         * the spawned effect object.
         */
        Destroy(
            effect,
            finalDuration +
            particleTailDuration
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

            /*
             * Only alter non-looping systems.
             *
             * Looping systems retain their prefab configuration
             * and are destroyed with the parent effect.
             */
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
    // CHECKPOINT RESTORE
    // =========================================================

    public void RestoreCheckpointState(
        bool wasAvailable
    )
    {
        if (deactivateCoroutine != null)
        {
            StopCoroutine(
                deactivateCoroutine
            );

            deactivateCoroutine =
                null;
        }

        hasBeenCollected =
            !wasAvailable;

        gameObject.SetActive(
            wasAvailable
        );

        if (wasAvailable)
        {
            RestorePickupColliders();
        }
    }

    private System.Collections.IEnumerator DeactivateAfterDelay()
    {
        if (destroyDelay > 0f)
        {
            yield return new WaitForSeconds(
                destroyDelay
            );
        }

        deactivateCoroutine =
            null;

        gameObject.SetActive(
            false
        );
    }

    // =========================================================
    // COLLIDERS
    // =========================================================

    private void DisablePickupColliders()
    {
        if (pickupColliders == null)
        {
            return;
        }

        foreach (
            Collider pickupCollider
            in pickupColliders
        )
        {
            if (pickupCollider != null)
            {
                pickupCollider.enabled =
                    false;
            }
        }
    }

    private void RestorePickupColliders()
    {
        if (pickupColliders == null)
        {
            return;
        }

        foreach (
            Collider pickupCollider
            in pickupColliders
        )
        {
            if (pickupCollider != null)
            {
                pickupCollider.enabled =
                    true;
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

        fixedEffectDuration =
            Mathf.Max(
                0.1f,
                fixedEffectDuration
            );

        particleTailDuration =
            Mathf.Max(
                0f,
                particleTailDuration
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