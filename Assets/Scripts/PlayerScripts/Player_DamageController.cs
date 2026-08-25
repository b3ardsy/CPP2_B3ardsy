using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Health))]
public class Player_DamageController :
    MonoBehaviour,
    IDamageable,
    IAxeDamageable
{
    // =========================================================
    // HEALTH
    // =========================================================

    public const int HealthPerHeart = 4;

    // =========================================================
    // DAMAGE PROTECTION
    // =========================================================

    [Header("Damage Protection")]
    [SerializeField]
    private float invulnerabilityDuration = 0.4f;

    // =========================================================
    // HIT REACTION
    // =========================================================

    [Header("Hit Reaction")]
    [Tooltip(
        "How long movement, combat, and dodge stay disabled " +
        "after a non-lethal hit."
    )]
    [SerializeField]
    private float hitReactionDuration = 0.4f;

    [SerializeField]
    private float axeHitReactionDuration = 0.65f;

    // =========================================================
    // DEATH
    // =========================================================

    [Header("Death")]
    [Tooltip(
        "How long the death animation plays before " +
        "the Game Over banner appears."
    )]
    [SerializeField]
    private float deathAnimationDelay = 2f;

    [Tooltip(
        "How long the Game Over banner remains before " +
        "returning to the Main Menu."
    )]
    [SerializeField]
    private float gameOverDelay = 3f;

    [Tooltip(
        "Scene loaded after the Game Over sequence."
    )]
    [SerializeField]
    private string mainMenuSceneName =
        "Game_Start";

    [TextArea]
    [Tooltip(
        "Message displayed after the player dies."
    )]
    [SerializeField]
    private string gameOverMessage =
        "GAME OVER";

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private Animator animator;
    [SerializeField] private Player_Controller playerController;
    [SerializeField] private Player_Combat playerCombat;
    [SerializeField] private Player_Dodge playerDodge;
    [SerializeField] private Player_LockOn playerLockOn;
    [SerializeField] private HUDNotificationBanner notificationBanner;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

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

    private bool isInHitReaction;

    /*
     * Health.IsDead becomes true before Health.OnDied is raised.
     *
     * This separate flag prevents the player-specific death
     * sequence from running more than once.
     */
    private bool deathHandled;

    private Coroutine invulnerabilityCoroutine;
    private Coroutine hitReactionCoroutine;
    private Coroutine deathCoroutine;

    // =========================================================
    // PUBLIC PROPERTIES
    // =========================================================

    public int CurrentHealth =>
        health != null
            ? health.CurrentHealth
            : 0;

    public int MaxHealth =>
        health != null
            ? health.MaxHealth
            : 0;

    public bool IsDead =>
        health != null &&
        health.IsDead;

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

    // =========================================================
    // HEALTH EVENTS
    // =========================================================

    /*
     * Temporary compatibility event.
     *
     * Health remains the authoritative source of health data,
     * but existing listeners can continue subscribing here
     * during the migration.
     */
    public event Action<int, int> OnHealthChanged;

    // =========================================================
    // ANIMATOR PARAMETERS
    // =========================================================

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

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        FindReferences();

        shieldProtectionSources = 0;
        deathHandled = false;

        if (health == null)
        {
            Debug.LogError(
                $"{name}: Player_DamageController requires " +
                "a Health component.",
                this
            );

            enabled = false;
            return;
        }

        health.OnHealthChanged +=
            HandleHealthChanged;

        health.OnDied +=
            HandleHealthDepleted;
    }

    private void Start()
    {
        /*
         * Supply listeners with the player's initial health.
         */
        OnHealthChanged?.Invoke(
            CurrentHealth,
            MaxHealth
        );
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void FindReferences()
    {
        if (health == null)
        {
            health =
                GetComponent<Health>();
        }

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (playerController == null)
        {
            playerController =
                GetComponent<Player_Controller>();
        }

        if (playerController == null)
        {
            playerController =
                GetComponentInParent<Player_Controller>();
        }

        if (playerCombat == null)
        {
            playerCombat =
                GetComponent<Player_Combat>();
        }

        if (playerCombat == null)
        {
            playerCombat =
                GetComponentInParent<Player_Combat>();
        }

        if (playerDodge == null)
        {
            playerDodge =
                GetComponent<Player_Dodge>();
        }

        if (playerDodge == null)
        {
            playerDodge =
                GetComponentInParent<Player_Dodge>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<Player_LockOn>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponentInParent<Player_LockOn>();
        }

        if (notificationBanner == null)
        {
            notificationBanner =
                FindAnyObjectByType<HUDNotificationBanner>();
        }
    }

    // =========================================================
    // HEALTH EVENT RELAY
    // =========================================================

    private void HandleHealthChanged(
        int currentHealth,
        int maxHealth
    )
    {
        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );
    }

    private void HandleHealthDepleted()
    {
        Die();
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    public void TakeDamage(
        int damage
    )
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

    public void TakeAxeDamage(
        int damage
    )
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
            IsDead ||
            isInvulnerable ||
            IsShieldProtected;
    }

    private void ApplyDamage(
        int damage,
        bool useAxeHitReaction
    )
    {
        if (health == null)
        {
            return;
        }

        bool damageApplied =
            health.TakeDamage(
                damage
            );

        if (!damageApplied)
        {
            return;
        }

        /*
         * Health.OnDied invokes Die() before execution
         * returns here if this was a lethal hit.
         */
        if (IsDead)
        {
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
        if (IsDead)
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

    public void Heal(
        int amount
    )
    {
        if (
            health == null ||
            IsDead ||
            amount <= 0
        )
        {
            return;
        }

        health.Heal(
            amount
        );
    }

    public void RestoreFullHealth()
    {
        if (
            health == null ||
            IsDead
        )
        {
            return;
        }

        health.RestoreFullHealth();
    }

    public void IncreaseMaxHealth(
        int amount,
        bool restoreFullHealth = true
    )
    {
        if (
            health == null ||
            IsDead ||
            amount <= 0
        )
        {
            return;
        }

        health.IncreaseMaxHealth(
            amount,
            restoreFullHealth
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

        if (!IsDead)
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

        if (!IsDead)
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

        if (!IsDead)
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
        if (deathHandled)
        {
            return;
        }

        deathHandled = true;

        isInvulnerable = true;
        isInHitReaction = false;

        /*
         * Shield protection is no longer relevant
         * once the player dies.
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

        if (playerController != null)
        {
            playerController.StopMovementImmediately();

            playerController.AddMovementLock(
                this
            );
        }

        if (playerCombat != null)
        {
            playerCombat.CancelCombat();
            playerCombat.enabled = false;
        }
    }

    private void EnableTemporaryPlayerActions()
    {
        if (playerController != null)
        {
            playerController.RemoveMovementLock(
                this
            );
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
            playerCombat.CancelCombat();
            playerCombat.enabled = false;
        }

        if (playerController != null)
        {
            playerController.StopMovementImmediately();

            playerController.AddMovementLock(
                this
            );
        }
    }

    // =========================================================
    // GAME OVER
    // =========================================================

    private IEnumerator DeathCoroutine()
    {
        /*
         * TEMPORARY LEGACY DEATH BEHAVIOUR.
         *
         * This will be replaced by checkpoint respawning
         * during the checkpoint architecture refactor.
         */

        yield return new WaitForSeconds(
            deathAnimationDelay
        );

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

        yield return new WaitForSeconds(
            gameOverDelay
        );

        SceneManager.LoadScene(
            mainMenuSceneName
        );
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDisable()
    {
        if (
            !IsDead &&
            playerController != null
        )
        {
            playerController.RemoveMovementLock(
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -=
                HandleHealthChanged;

            health.OnDied -=
                HandleHealthDepleted;
        }
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
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