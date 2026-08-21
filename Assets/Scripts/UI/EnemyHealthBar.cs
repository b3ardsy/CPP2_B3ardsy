using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private Canvas healthBarCanvas;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damageFill;

    // =========================================================
    // VISIBILITY
    // =========================================================

    [Header("Visibility")]
    [Tooltip(
        "Maximum distance from the player where the " +
        "health bar is visible."
    )]
    [SerializeField]
    private float visibleDistance = 10f;

    // =========================================================
    // DAMAGE EFFECT
    // =========================================================

    [Header("Damage Effect")]
    [Tooltip(
        "How long the white damage bar waits before catching up."
    )]
    [SerializeField]
    private float damageDelay = 0.4f;

    [Tooltip(
        "How quickly the white damage bar catches up."
    )]
    [SerializeField]
    private float damageDrainSpeed = 2f;

    // =========================================================
    // RUNTIME REFERENCES
    // =========================================================

    private Transform player;
    private Camera mainCamera;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private Coroutine damageRoutine;
    private bool healthDepleted;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        /*
         * The Health component normally lives on the enemy's
         * root object while this script may live on the
         * world-space health bar child.
         */
        if (health == null)
        {
            health =
                GetComponentInParent<Health>();
        }

        if (healthBarCanvas == null)
        {
            healthBarCanvas =
                GetComponent<Canvas>();
        }

        mainCamera =
            Camera.main;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (playerObject != null)
        {
            player =
                playerObject.transform;
        }

        ValidateReferences();
    }

    private void Start()
    {
        if (health == null)
        {
            return;
        }

        /*
         * The health bar now talks directly to Health.
         *
         * It no longer needs to know that the object is
         * specifically an Enemy.
         */
        health.OnHealthChanged +=
            HandleHealthChanged;

        health.OnDied +=
            HandleHealthDepleted;

        float startingHealth =
            GetNormalizedHealth(
                health.CurrentHealth,
                health.MaxHealth
            );

        if (healthFill != null)
        {
            healthFill.fillAmount =
                startingHealth;
        }

        if (damageFill != null)
        {
            damageFill.fillAmount =
                startingHealth;
        }

        UpdateVisibility();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (healthDepleted)
        {
            return;
        }

        FaceCamera();
        UpdateVisibility();
    }

    // =========================================================
    // HEALTH EVENTS
    // =========================================================

    private void HandleHealthChanged(
        int currentHealth,
        int maxHealth
    )
    {
        float normalizedHealth =
            GetNormalizedHealth(
                currentHealth,
                maxHealth
            );

        /*
         * The red health amount changes immediately.
         */
        if (healthFill != null)
        {
            healthFill.fillAmount =
                normalizedHealth;
        }

        /*
         * Restart the delayed white damage effect whenever
         * another health change occurs.
         */
        if (damageRoutine != null)
        {
            StopCoroutine(
                damageRoutine
            );

            damageRoutine =
                null;
        }

        if (damageFill != null)
        {
            damageRoutine =
                StartCoroutine(
                    DrainDamageBar(
                        normalizedHealth
                    )
                );
        }
    }

    private void HandleHealthDepleted()
    {
        healthDepleted =
            true;

        if (damageRoutine != null)
        {
            StopCoroutine(
                damageRoutine
            );

            damageRoutine =
                null;
        }

        if (healthBarCanvas != null)
        {
            healthBarCanvas.enabled =
                false;
        }
    }

    // =========================================================
    // DAMAGE BAR
    // =========================================================

    private IEnumerator DrainDamageBar(
        float targetAmount
    )
    {
        yield return new WaitForSeconds(
            damageDelay
        );

        while (
            damageFill != null &&
            damageFill.fillAmount > targetAmount
        )
        {
            damageFill.fillAmount =
                Mathf.MoveTowards(
                    damageFill.fillAmount,
                    targetAmount,
                    damageDrainSpeed *
                    Time.deltaTime
                );

            yield return null;
        }

        if (damageFill != null)
        {
            damageFill.fillAmount =
                targetAmount;
        }

        damageRoutine =
            null;
    }

    // =========================================================
    // CAMERA
    // =========================================================

    private void FaceCamera()
    {
        if (mainCamera == null)
        {
            mainCamera =
                Camera.main;

            if (mainCamera == null)
            {
                return;
            }
        }

        /*
         * Keep the world-space health bar facing
         * the player's camera.
         */
        transform.rotation =
            Quaternion.LookRotation(
                transform.position -
                mainCamera.transform.position
            );
    }

    // =========================================================
    // VISIBILITY
    // =========================================================

    private void UpdateVisibility()
    {
        if (healthBarCanvas == null)
        {
            return;
        }

        if (player == null)
        {
            healthBarCanvas.enabled =
                false;

            return;
        }

        float distanceToPlayer =
            Vector3.Distance(
                transform.position,
                player.position
            );

        healthBarCanvas.enabled =
            distanceToPlayer <=
            visibleDistance;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private float GetNormalizedHealth(
        int currentHealth,
        int maxHealth
    )
    {
        if (maxHealth <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(
            (float)currentHealth /
            maxHealth
        );
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void ValidateReferences()
    {
        if (health == null)
        {
            Debug.LogError(
                $"{name}: EnemyHealthBar could not find a Health component.",
                this
            );
        }

        if (healthBarCanvas == null)
        {
            Debug.LogError(
                $"{name}: No Canvas assigned to EnemyHealthBar.",
                this
            );
        }

        if (healthFill == null)
        {
            Debug.LogError(
                $"{name}: No Health Fill image assigned.",
                this
            );
        }

        if (damageFill == null)
        {
            Debug.LogError(
                $"{name}: No Damage Fill image assigned.",
                this
            );
        }

        if (player == null)
        {
            Debug.LogError(
                $"{name}: No GameObject with the Player tag was found.",
                this
            );
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -=
                HandleHealthChanged;

            health.OnDied -=
                HandleHealthDepleted;
        }

        if (damageRoutine != null)
        {
            StopCoroutine(
                damageRoutine
            );

            damageRoutine =
                null;
        }
    }

    // =========================================================
    // EDITOR VALIDATION
    // =========================================================

    private void OnValidate()
    {
        visibleDistance =
            Mathf.Max(
                0f,
                visibleDistance
            );

        damageDelay =
            Mathf.Max(
                0f,
                damageDelay
            );

        damageDrainSpeed =
            Mathf.Max(
                0f,
                damageDrainSpeed
            );
    }
}