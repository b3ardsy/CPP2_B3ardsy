using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStatsNew : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("Maximum health in quarter-heart units. 4 health = 1 full heart.")]
    [SerializeField] private int maxHealth = 12;

    public const int HealthPerHeart = 4;

    [Header("Damage Protection")]
    [SerializeField] private float invulnerabilityDuration = 0.4f;

    [Header("Hit Reaction")]
    [Tooltip(
        "How long movement, combat, and dodge stay disabled " +
        "after a non-lethal hit."
    )]
    [SerializeField] private float hitReactionDuration = 0.4f;
    [SerializeField] private float axeHitReactionDuration = 0.65f;

    [Header("Death")]
    [Tooltip(
    "How long the death animation plays before the Game Over banner appears."
)]
    [SerializeField] private float deathAnimationDelay = 2f;

    [Tooltip(
        "How long the Game Over banner remains before returning to the Main Menu."
    )]
    [SerializeField] private float gameOverDelay = 3f;

    [Tooltip(
        "Scene loaded after the Game Over sequence."
    )]
    [SerializeField]
    private string mainMenuSceneName =
        "Game_Start";

    [TextArea]
    [Tooltip("Message displayed after the player dies.")]
    [SerializeField]
    private string gameOverMessage =
        "GAME OVER";

    [Header("Death UI")]
    [Tooltip(
        "Optional HUD notification banner. " +
        "If left empty, it will be found automatically."
    )]

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement3DNew playerMovement;
    [SerializeField] private PlayerCombatNew playerCombat;
    [SerializeField] private PlayerDodgeNew playerDodge;
    [SerializeField] private PlayerLockOn playerLockOn;
    [SerializeField] private HUDNotificationBanner notificationBanner;

    private int currentHealth;

    /*
     * Short post-hit invulnerability.
     */
    private bool isInvulnerable;

    /*
     * Number of active Shield protection sources.
     *
     * Using a counter instead of a bool prevents one Shield
     * instance from accidentally removing protection supplied
     * by another source.
     */
    private int shieldProtectionSources;

    private bool isDead;
    private bool isInHitReaction;

    private Coroutine invulnerabilityCoroutine;
    private Coroutine hitReactionCoroutine;
    private Coroutine deathCoroutine;

    public int CurrentHealth =>
        currentHealth;

    public int MaxHealth =>
        maxHealth;

    /*
     * Broadcast whenever current or maximum health changes.
     * The HUD can subscribe to this instead of polling every frame.
     */
    public event System.Action<int, int> OnHealthChanged;

    public bool IsDead =>
        isDead;

    /*
     * Reports all current forms of damage invulnerability.
     */
    public bool IsInvulnerable =>
        isInvulnerable ||
        IsShieldProtected;

    public bool IsShieldProtected =>
        shieldProtectionSources > 0;

    public bool IsInHitReaction =>
        isInHitReaction;

    private static readonly int HitTrigger =
        Animator.StringToHash("Hit");

    private static readonly int AxeHitTrigger =
        Animator.StringToHash("AxeHit");

    private static readonly int DeathTrigger =
        Animator.StringToHash("Death");

    private static readonly int DodgeForwardTrigger =
        Animator.StringToHash("DodgeForward");

    private static readonly int DodgeBackwardTrigger =
        Animator.StringToHash("DodgeBackward");

    private static readonly int DodgeLeftTrigger =
        Animator.StringToHash("DodgeLeft");

    private static readonly int DodgeRightTrigger =
        Animator.StringToHash("DodgeRight");

    private static readonly int JumpTrigger =
        Animator.StringToHash("Jump");

    private static readonly int LandTrigger =
        Animator.StringToHash("Land");

    private static readonly int IsRunningBool =
        Animator.StringToHash("IsRunning");

    private static readonly int IsGroundedBool =
        Animator.StringToHash("IsGrounded");

    private static readonly int IsLockedOnBool =
        Animator.StringToHash("IsLockedOn");

    private static readonly int SpeedFloat =
        Animator.StringToHash("Speed");

    private static readonly int LockOnHorizontalFloat =
        Animator.StringToHash("LockOnHorizontal");

    private static readonly int LockOnVerticalFloat =
        Animator.StringToHash("LockOnVertical");

    private void Awake()
    {
        maxHealth =
            Mathf.Max(
                HealthPerHeart,
                maxHealth
            );

        currentHealth =
            maxHealth;

        shieldProtectionSources = 0;

        FindReferences();
    }

    private void Start()
    {
        /*
         * Notify listeners once all scene objects have completed Awake().
         * This gives the HUD an initial health value when the scene starts.
         */
        NotifyHealthChanged();
    }

    private void FindReferences()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                GetComponent<PlayerMovement3DNew>();
        }

        if (playerCombat == null)
        {
            playerCombat =
                GetComponent<PlayerCombatNew>();
        }

        if (playerDodge == null)
        {
            playerDodge =
                GetComponent<PlayerDodgeNew>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<PlayerLockOn>();
        }

        if (notificationBanner == null)
        {
            notificationBanner =
                FindAnyObjectByType<HUDNotificationBanner>();
        }
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    public void TakeDamage(int damage)
    {
        if (
            IsDamageBlocked() ||
            damage <= 0
        )
        {
            return;
        }

        ApplyDamage(
            damage,
            false
        );
    }

    public void TakeAxeDamage(int damage)
    {
        if (
            IsDamageBlocked() ||
            damage <= 0
        )
        {
            return;
        }

        ApplyDamage(
            damage,
            true
        );
    }

    private bool IsDamageBlocked()
    {
        return
            isDead ||
            isInvulnerable ||
            IsShieldProtected;
    }

    private void ApplyDamage(
        int damage,
        bool useAxeHitReaction
    )
    {
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

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (useAxeHitReaction)
        {
            StartAxeHitReaction();
        }
        else
        {
            StartHitReaction();
        }

        StartInvulnerability();
    }

    // =========================================================
    // SHIELD PROTECTION
    // =========================================================

    public void AddShieldProtection()
    {
        if (isDead)
        {
            return;
        }

        shieldProtectionSources++;

        Debug.Log(
            $"{name}: Shield protection activated.",
            this
        );
    }

    public void RemoveShieldProtection()
    {
        shieldProtectionSources =
            Mathf.Max(
                0,
                shieldProtectionSources - 1
            );

        if (!IsShieldProtected)
        {
            Debug.Log(
                $"{name}: Shield protection ended.",
                this
            );
        }
    }

    // =========================================================
    // HEALTH
    // =========================================================

    public void Heal(int amount)
    {
        if (
            isDead ||
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
        if (isDead)
        {
            return;
        }

        currentHealth =
            maxHealth;

        Debug.Log(
            $"{name} restored to full health. " +
            $"Health: {currentHealth}/{maxHealth}",
            this
        );

        NotifyHealthChanged();
    }

    /*
     * Adds permanent maximum health.
     *
     * For a full new heart, pass HealthPerHeart (4).
     * By default the new health capacity is also filled.
     */
    public void IncreaseMaxHealth(
    int amount,
    bool restoreFullHealth = true
)
    {
        if (
            isDead ||
            amount <= 0
        )
        {
            return;
        }

        maxHealth =
            Mathf.Max(
                1,
                maxHealth + amount
            );

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

        Debug.Log(
            $"{name} maximum health increased by {amount}. " +
            $"Health: {currentHealth}/{maxHealth}",
            this
        );

        NotifyHealthChanged();
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );
    }

    // =========================================================
    // NORMAL HIT REACTION
    // =========================================================

    private void StartHitReaction()
    {
        if (hitReactionCoroutine != null)
        {
            StopCoroutine(
                hitReactionCoroutine
            );

            hitReactionCoroutine = null;
        }

        hitReactionCoroutine =
            StartCoroutine(
                HitReactionCoroutine()
            );
    }

    private IEnumerator HitReactionCoroutine()
    {
        isInHitReaction = true;

        DisableTemporaryPlayerActions();
        PlayHitAnimationImmediately();

        yield return new WaitForSeconds(
            hitReactionDuration
        );

        if (!isDead)
        {
            EnableTemporaryPlayerActions();
        }

        isInHitReaction = false;
        hitReactionCoroutine = null;
    }

    private void PlayHitAnimationImmediately()
    {
        if (animator == null)
        {
            return;
        }

        ClearActionTriggers();

        animator.ResetTrigger(
            DeathTrigger
        );

        animator.ResetTrigger(
            AxeHitTrigger
        );

        animator.ResetTrigger(
            HitTrigger
        );

        animator.SetTrigger(
            HitTrigger
        );
    }

    // =========================================================
    // AXE HIT REACTION
    // =========================================================

    private void StartAxeHitReaction()
    {
        if (hitReactionCoroutine != null)
        {
            StopCoroutine(
                hitReactionCoroutine
            );

            hitReactionCoroutine = null;
        }

        hitReactionCoroutine =
            StartCoroutine(
                AxeHitReactionCoroutine()
            );
    }

    private IEnumerator AxeHitReactionCoroutine()
    {
        isInHitReaction = true;

        DisableTemporaryPlayerActions();
        PlayAxeHitAnimationImmediately();

        yield return new WaitForSeconds(
            axeHitReactionDuration
        );

        if (!isDead)
        {
            EnableTemporaryPlayerActions();
        }

        isInHitReaction = false;
        hitReactionCoroutine = null;
    }

    private void PlayAxeHitAnimationImmediately()
    {
        if (animator == null)
        {
            return;
        }

        ClearActionTriggers();

        animator.ResetTrigger(
            DeathTrigger
        );

        animator.ResetTrigger(
            HitTrigger
        );

        animator.ResetTrigger(
            AxeHitTrigger
        );

        animator.SetTrigger(
            AxeHitTrigger
        );
    }

    // =========================================================
    // INVULNERABILITY
    // =========================================================

    private void StartInvulnerability()
    {
        if (invulnerabilityCoroutine != null)
        {
            StopCoroutine(
                invulnerabilityCoroutine
            );

            invulnerabilityCoroutine = null;
        }

        invulnerabilityCoroutine =
            StartCoroutine(
                InvulnerabilityCoroutine()
            );
    }

    private IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;

        yield return new WaitForSeconds(
            invulnerabilityDuration
        );

        if (!isDead)
        {
            isInvulnerable = false;
        }

        invulnerabilityCoroutine = null;
    }

    // =========================================================
    // DEATH
    // =========================================================

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        isInvulnerable = true;
        isInHitReaction = false;

        /*
         * Shield is no longer relevant once the player dies.
         */
        shieldProtectionSources = 0;

        StopActiveGameplayCoroutines();
        StopPlayerActions();

        if (animator != null)
        {
            ClearActionTriggers();

            animator.ResetTrigger(
                HitTrigger
            );

            animator.ResetTrigger(
                AxeHitTrigger
            );

            animator.ResetTrigger(
                DeathTrigger
            );

            animator.SetFloat(
                SpeedFloat,
                0f
            );

            animator.SetFloat(
                LockOnHorizontalFloat,
                0f
            );

            animator.SetFloat(
                LockOnVerticalFloat,
                0f
            );

            animator.SetBool(
                IsRunningBool,
                false
            );

            animator.SetBool(
                IsGroundedBool,
                true
            );

            animator.SetBool(
                IsLockedOnBool,
                false
            );

            animator.SetTrigger(
                DeathTrigger
            );
        }

        Debug.Log(
            $"{name} has died.",
            this
        );

        deathCoroutine =
            StartCoroutine(
                DeathCoroutine()
            );
    }

    private void StopActiveGameplayCoroutines()
    {
        if (invulnerabilityCoroutine != null)
        {
            StopCoroutine(
                invulnerabilityCoroutine
            );

            invulnerabilityCoroutine = null;
        }

        if (hitReactionCoroutine != null)
        {
            StopCoroutine(
                hitReactionCoroutine
            );

            hitReactionCoroutine = null;
        }
    }

    // =========================================================
    // ANIMATOR / ACTION HELPERS
    // =========================================================

    private void ClearActionTriggers()
    {
        if (animator == null)
        {
            return;
        }

        animator.ResetTrigger(
            DodgeForwardTrigger
        );

        animator.ResetTrigger(
            DodgeBackwardTrigger
        );

        animator.ResetTrigger(
            DodgeLeftTrigger
        );

        animator.ResetTrigger(
            DodgeRightTrigger
        );

        animator.ResetTrigger(
            JumpTrigger
        );

        animator.ResetTrigger(
            LandTrigger
        );
    }

    private void DisableTemporaryPlayerActions()
    {
        if (playerDodge != null)
        {
            playerDodge.CancelDodge();
            playerDodge.enabled = false;
        }

        if (playerMovement != null)
        {
            playerMovement.StopMovementImmediately();
            playerMovement.AddMovementLock(this);
        }

        if (playerCombat != null)
        {
            playerCombat.enabled = false;
        }
    }

    private void EnableTemporaryPlayerActions()
    {
        if (playerMovement != null)
        {
            playerMovement.RemoveMovementLock(this);
        }

        if (playerCombat != null)
        {
            playerCombat.enabled = true;
        }

        if (playerDodge != null)
        {
            playerDodge.enabled = true;
        }
    }

    private void StopPlayerActions()
    {
        if (playerLockOn != null)
        {
            playerLockOn.enabled = false;
        }

        if (playerDodge != null)
        {
            playerDodge.CancelDodge();
            playerDodge.enabled = false;
        }

        if (playerCombat != null)
        {
            playerCombat.enabled = false;
        }

        if (playerMovement != null)
        {
            playerMovement.StopMovementImmediately();
            playerMovement.AddMovementLock(this);
        }
    }

    // =========================================================
    // SCENE RESTART
    // =========================================================

    private IEnumerator DeathCoroutine()
    {
        /*
         * Give the death animation time to play
         * before displaying Game Over.
         */
        yield return new WaitForSeconds(
            deathAnimationDelay
        );

        /*
         * Display the shared HUD notification.
         */
        if (notificationBanner != null)
        {
            notificationBanner.ShowMessage(
                gameOverMessage
            );
        }
        else
        {
            Debug.LogWarning(
                $"{name}: HUDNotificationBanner could not be found.",
                this
            );
        }

        /*
         * Leave Game Over on screen briefly before
         * returning to the Main Menu.
         */
        yield return new WaitForSeconds(
            gameOverDelay
        );

        SceneManager.LoadScene(
            mainMenuSceneName
        );
    }

    private void OnDisable()
    {
        if (
            !isDead &&
            playerMovement != null
        )
        {
            playerMovement.RemoveMovementLock(
                this
            );
        }
    }

    private void OnValidate()
    {
        maxHealth =
            Mathf.Max(
                HealthPerHeart,
                maxHealth
            );

        invulnerabilityDuration =
            Mathf.Max(
                0f,
                invulnerabilityDuration
            );

        hitReactionDuration =
            Mathf.Max(
                0f,
                hitReactionDuration
            );

        axeHitReactionDuration =
            Mathf.Max(
                0f,
                axeHitReactionDuration
            );

        deathAnimationDelay =
    Mathf.Max(
        0f,
        deathAnimationDelay
    );

        gameOverDelay =
            Mathf.Max(
                0f,
                gameOverDelay
            );
    }
}