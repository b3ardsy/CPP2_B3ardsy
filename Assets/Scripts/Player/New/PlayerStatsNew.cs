using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStatsNew : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 5;

    [Header("Damage Protection")]
    [SerializeField] private float invulnerabilityDuration = 0.4f;

    [Header("Hit Reaction")]
    [Tooltip(
        "How long movement, combat, and dodge stay disabled " +
        "after a non-lethal hit."
    )]
    [SerializeField] private float hitReactionDuration = 0.4f;

    [Tooltip(
        "Exact Animator state name used for the hit animation."
    )]
    [SerializeField]
    private string hitAnimationStateName =
        "Player_Hit";

    [Header("Death")]
    [SerializeField] private float deathRestartDelay = 2f;

    [Tooltip(
        "Reloads the active scene after the player dies."
    )]
    [SerializeField] private bool restartSceneOnDeath = true;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement3DNew playerMovement;
    [SerializeField] private PlayerCombatNew playerCombat;
    [SerializeField] private PlayerDodgeNew playerDodge;
    [SerializeField] private PlayerLockOn playerLockOn;

    private int currentHealth;

    private bool isInvulnerable;
    private bool isDead;
    private bool isInHitReaction;

    private Coroutine invulnerabilityCoroutine;
    private Coroutine hitReactionCoroutine;
    private Coroutine deathCoroutine;

    private int hitAnimationStateHash;

    public int CurrentHealth =>
        currentHealth;

    public int MaxHealth =>
        maxHealth;

    public bool IsDead =>
        isDead;

    public bool IsInvulnerable =>
        isInvulnerable;

    public bool IsInHitReaction =>
        isInHitReaction;

    private static readonly int HitTrigger =
        Animator.StringToHash("Hit");

    private static readonly int DeathTrigger =
        Animator.StringToHash("Death");

    private static readonly int PunchTrigger =
        Animator.StringToHash("Punch");

    private static readonly int KickTrigger =
        Animator.StringToHash("Kick");

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
                1,
                maxHealth
            );

        currentHealth =
            maxHealth;

        FindReferences();

        hitAnimationStateHash =
            Animator.StringToHash(
                hitAnimationStateName
            );
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
    }

    public void TakeDamage(int damage)
    {
        if (
            isDead ||
            isInvulnerable ||
            damage <= 0
        )
        {
            return;
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

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        StartHitReaction();
        StartInvulnerability();
    }

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
    }

    public void RestoreFullHealth()
    {
        if (isDead)
        {
            return;
        }

        currentHealth =
            maxHealth;
    }

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
            HitTrigger
        );

        /*
         * Restart the hit state immediately. This allows another
         * valid hit to restart the reaction instead of waiting for
         * the current hit animation to finish.
         */
        if (
            animator.HasState(
                0,
                hitAnimationStateHash
            )
        )
        {
            animator.Play(
                hitAnimationStateHash,
                0,
                0f
            );

            animator.Update(
                0f
            );
        }
        else
        {
            Debug.LogWarning(
                $"Animator state '{hitAnimationStateName}' " +
                "was not found on layer 0. " +
                "Falling back to the Hit trigger.",
                this
            );

            animator.SetTrigger(
                HitTrigger
            );
        }
    }

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

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        isInvulnerable = true;
        isInHitReaction = false;

        StopActiveGameplayCoroutines();
        StopPlayerActions();

        if (animator != null)
        {
            ClearActionTriggers();

            animator.ResetTrigger(
                HitTrigger
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

    private void ClearActionTriggers()
    {
        if (animator == null)
        {
            return;
        }

        animator.ResetTrigger(
            PunchTrigger
        );

        animator.ResetTrigger(
            KickTrigger
        );

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
        /*
         * Stop any active dodge before disabling its script.
         */
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

    private IEnumerator DeathCoroutine()
    {
        yield return new WaitForSeconds(
            deathRestartDelay
        );

        if (!restartSceneOnDeath)
        {
            deathCoroutine = null;
            yield break;
        }

        Scene activeScene =
            SceneManager.GetActiveScene();

        SceneManager.LoadScene(
            activeScene.buildIndex
        );
    }

    private void OnDisable()
    {
        /*
         * Avoid leaving a permanent movement lock if this component
         * is disabled independently during play.
         */
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
                1,
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

        deathRestartDelay =
            Mathf.Max(
                0f,
                deathRestartDelay
            );

        if (
            string.IsNullOrWhiteSpace(
                hitAnimationStateName
            )
        )
        {
            hitAnimationStateName =
                "Player_Hit";
        }

        hitAnimationStateHash =
            Animator.StringToHash(
                hitAnimationStateName
            );
    }
}