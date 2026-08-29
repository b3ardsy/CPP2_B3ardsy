using System;
using UnityEngine;

public class Health : MonoBehaviour
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
    // REVIVAL
    // =========================================================

    /*
     * Explicitly restores a dead Health component to an alive state.
     *
     * Normal Heal() and RestoreFullHealth() intentionally do not
     * revive dead entities. Systems such as checkpoints, respawns,
     * or enemy reset logic should use this method when resurrection
     * is deliberate.
     */
    public void Revive(
        int healthAmount
    )
    {
        if (!IsDead)
        {
            return;
        }

        currentHealth =
            Mathf.Clamp(
                healthAmount,
                1,
                maxHealth
            );

        NotifyHealthChanged();
    }

    // =========================================================
    // STATE RESTORATION
    // =========================================================

    /*
     * Restores an exact health state.
     *
     * This is intentionally separate from Heal(), Revive(), and
     * IncreaseMaxHealth() because state restoration may need to
     * move maximum health either upward or downward.
     */
    public void RestoreHealthState(
        int healthAmount,
        int maxHealthAmount
    )
    {
        maxHealth =
            Mathf.Max(
                1,
                maxHealthAmount
            );

        currentHealth =
            Mathf.Clamp(
                healthAmount,
                1,
                maxHealth
            );

        NotifyHealthChanged();
    }

    /*
     * Restores exact serialized health, including 0 HP.
     *
     * Unlike TakeDamage(), this deliberately does not invoke OnDied.
     * The owning load/restoration system is responsible for putting
     * a dead entity into its correct runtime presentation/state.
     */
    public void RestoreSavedHealthState(
        int healthAmount,
        int maxHealthAmount
    )
    {
        maxHealth =
            Mathf.Max(
                1,
                maxHealthAmount
            );

        currentHealth =
            Mathf.Clamp(
                healthAmount,
                0,
                maxHealth
            );

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