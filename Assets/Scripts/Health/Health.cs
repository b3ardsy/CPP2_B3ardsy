using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    // =========================================================
    // HEALTH
    // =========================================================

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    private int currentHealth;

    // =========================================================
    // PUBLIC PROPERTIES
    // =========================================================

    public int CurrentHealth =>
        currentHealth;

    public int MaxHealth =>
        maxHealth;

    public bool IsDead =>
        currentHealth <= 0;

    // =========================================================
    // EVENTS
    // =========================================================

    public event Action<int, int> OnHealthChanged;

    public event Action OnDied;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        currentHealth =
            maxHealth;
    }

    private void Start()
    {
        NotifyHealthChanged();
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    public bool TakeDamage(
        int damage
    )
    {
        if (
            IsDead ||
            damage <= 0
        )
        {
            return false;
        }

        currentHealth =
            Mathf.Clamp(
                currentHealth - damage,
                0,
                maxHealth
            );

        Debug.Log(
            $"{name} took {damage} damage. " +
            $"Health: {currentHealth}/{maxHealth}",
            this
        );

        NotifyHealthChanged();

        if (IsDead)
        {
            OnDied?.Invoke();
        }

        return true;
    }

    // =========================================================
    // HEALING
    // =========================================================

    public void Heal(
        int amount
    )
    {
        if (
            IsDead ||
            amount <= 0
        )
        {
            return;
        }

        currentHealth =
            Mathf.Clamp(
                currentHealth + amount,
                0,
                maxHealth
            );

        Debug.Log(
            $"{name} healed {amount}. " +
            $"Health: {currentHealth}/{maxHealth}",
            this
        );

        NotifyHealthChanged();
    }

    public void RestoreFullHealth()
    {
        if (IsDead)
        {
            return;
        }

        currentHealth =
            maxHealth;

        NotifyHealthChanged();
    }

    // =========================================================
    // MAXIMUM HEALTH
    // =========================================================

    public void IncreaseMaxHealth(
        int amount,
        bool restoreFullHealth = true
    )
    {
        if (
            IsDead ||
            amount <= 0
        )
        {
            return;
        }

        maxHealth +=
            amount;

        if (restoreFullHealth)
        {
            currentHealth =
                maxHealth;
        }
        else
        {
            currentHealth =
                Mathf.Clamp(
                    currentHealth,
                    0,
                    maxHealth
                );
        }

        NotifyHealthChanged();
    }

    // =========================================================
    // EVENTS
    // =========================================================

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        maxHealth =
            Mathf.Max(
                1,
                maxHealth
            );
    }
}