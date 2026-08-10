using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy enemy;
    [SerializeField] private Canvas healthBarCanvas;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damageFill;

    [Header("Visibility")]
    [Tooltip("Maximum distance from the player where the health bar is visible.")]
    [SerializeField] private float visibleDistance = 10f;

    [Header("Damage Effect")]
    [Tooltip("How long the white damage bar waits before catching up.")]
    [SerializeField] private float damageDelay = 0.4f;

    [Tooltip("How quickly the white damage bar catches up.")]
    [SerializeField] private float damageDrainSpeed = 2f;

    private Transform player;
    private Camera mainCamera;
    private Coroutine damageRoutine;
    private bool enemyDead;

    private void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponentInParent<Enemy>();
        }

        if (healthBarCanvas == null)
        {
            healthBarCanvas = GetComponent<Canvas>();
        }

        mainCamera = Camera.main;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        ValidateReferences();
    }

    private void Start()
    {
        if (enemy == null)
        {
            return;
        }

        enemy.OnHealthChanged += HandleHealthChanged;
        enemy.OnDied += HandleEnemyDied;

        float startingHealth =
            GetNormalizedHealth(
                enemy.CurrentHealth,
                enemy.MaxHealth
            );

        healthFill.fillAmount = startingHealth;
        damageFill.fillAmount = startingHealth;

        UpdateVisibility();
    }

    private void LateUpdate()
    {
        if (enemyDead)
        {
            return;
        }

        FaceCamera();
        UpdateVisibility();
    }

    private void OnDestroy()
    {
        if (enemy == null)
        {
            return;
        }

        enemy.OnHealthChanged -= HandleHealthChanged;
        enemy.OnDied -= HandleEnemyDied;
    }

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

        // Red health changes immediately.
        healthFill.fillAmount = normalizedHealth;

        // Restart the delayed white damage effect.
        if (damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
        }

        damageRoutine = StartCoroutine(
            DrainDamageBar(normalizedHealth)
        );
    }

    private IEnumerator DrainDamageBar(
        float targetAmount
    )
    {
        yield return new WaitForSeconds(
            damageDelay
        );

        while (damageFill.fillAmount > targetAmount)
        {
            damageFill.fillAmount =
                Mathf.MoveTowards(
                    damageFill.fillAmount,
                    targetAmount,
                    damageDrainSpeed * Time.deltaTime
                );

            yield return null;
        }

        damageFill.fillAmount = targetAmount;
        damageRoutine = null;
    }

    private void FaceCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                return;
            }
        }

        // Keep the health bar facing the player's camera.
        transform.rotation =
            Quaternion.LookRotation(
                transform.position -
                mainCamera.transform.position
            );
    }

    private void UpdateVisibility()
    {
        if (healthBarCanvas == null)
        {
            return;
        }

        if (player == null)
        {
            healthBarCanvas.enabled = false;
            return;
        }

        float distanceToPlayer =
            Vector3.Distance(
                transform.position,
                player.position
            );

        healthBarCanvas.enabled =
            distanceToPlayer <= visibleDistance;
    }

    private void HandleEnemyDied()
    {
        enemyDead = true;

        if (damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
            damageRoutine = null;
        }

        if (healthBarCanvas != null)
        {
            healthBarCanvas.enabled = false;
        }
    }

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
            (float)currentHealth / maxHealth
        );
    }

    private void ValidateReferences()
    {
        if (enemy == null)
        {
            Debug.LogError(
                $"{name}: EnemyHealthBar could not find an Enemy."
            );
        }

        if (healthBarCanvas == null)
        {
            Debug.LogError(
                $"{name}: No Canvas assigned to EnemyHealthBar."
            );
        }

        if (healthFill == null)
        {
            Debug.LogError(
                $"{name}: No Health Fill image assigned."
            );
        }

        if (damageFill == null)
        {
            Debug.LogError(
                $"{name}: No Damage Fill image assigned."
            );
        }

        if (player == null)
        {
            Debug.LogError(
                $"{name}: No GameObject with the Player tag was found."
            );
        }
    }
}