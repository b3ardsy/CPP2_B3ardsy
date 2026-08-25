using UnityEngine;

[RequireComponent(typeof(EnemyController))]
public class TankLogic : MonoBehaviour
{
    private enum TankState
    {
        Patrolling,
        Chasing,
        Attacking,
        ReturningHome
    }

    // =========================================================
    // COMBAT
    // =========================================================

    [Header("Combat")]
    [Tooltip(
        "Maximum distance from which a new axe attack may begin."
    )]
    [SerializeField]
    private float attackRange = 4f;

    [Tooltip(
        "How close the NavMeshAgent attempts to get while chasing. " +
        "Keep this slightly smaller than Attack Range."
    )]
    [SerializeField]
    private float chaseStoppingDistance = 3.25f;

    [Tooltip(
        "Distance the player must exceed before the Tank leaves " +
        "the attacking state and resumes chasing."
    )]
    [SerializeField]
    private float attackExitRange = 4.75f;

    [Tooltip(
        "Damage dealt each time the axe connects."
    )]
    [SerializeField]
    private int attackDamage = 1;

    [Tooltip(
        "Time between complete axe attacks."
    )]
    [SerializeField]
    private float attackCooldown = 3f;

    [Tooltip(
        "How quickly the Tank turns toward the player while attacking."
    )]
    [SerializeField]
    private float attackRotationSpeed = 25f;

    [Tooltip(
        "Trigger hitbox attached to the axe."
    )]
    [SerializeField]
    private TankWeaponHitbox axeHitbox;

    // =========================================================
    // CHASE
    // =========================================================

    [Header("Chase")]
    [SerializeField]
    private float chaseSpeed = 10f;

    // =========================================================
    // ENTANGLE RECOVERY
    // =========================================================

    [Header("Entangle Recovery")]
    [Tooltip(
        "Animator state used as a safe locomotion re-entry point " +
        "after Entangle ends."
    )]
    [SerializeField]
    private string locomotionRecoveryStateName = "Idle";

    [Tooltip(
        "How quickly the Animator crossfades out of Entangle."
    )]
    [SerializeField]
    private float entangleRecoveryTransitionDuration = 0.05f;

    [Tooltip(
        "For a few frames after Entangle, movement animation is " +
        "driven by AI state while NavMesh velocity wakes back up."
    )]
    [SerializeField]
    private int entangleRecoveryAnimationFrames = 3;

    // =========================================================
    // REFERENCES
    // =========================================================

    private EnemyController enemyController;
    private Animator animator;

    private IAxeDamageable playerAxeDamageable;

    // =========================================================
    // RUNTIME
    // =========================================================

    private TankState currentState;

    private float attackCooldownTimer;

    private bool isPerformingAttack;
    private bool wasEntangledLastFrame;

    private int movementAnimationRecoveryFrames;

    // =========================================================
    // ANIMATOR PARAMETERS
    // =========================================================

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private static readonly int EntangleHash =
        Animator.StringToHash("Entangle");

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        enemyController =
            GetComponent<EnemyController>();

        if (enemyController == null)
        {
            Debug.LogError(
                $"{name}: TankLogic requires an EnemyController.",
                this
            );

            enabled = false;
            return;
        }

        if (axeHitbox == null)
        {
            axeHitbox =
                GetComponentInChildren<TankWeaponHitbox>(
                    true
                );
        }

        if (axeHitbox == null)
        {
            Debug.LogError(
                $"{name}: TankLogic could not find TankWeaponHitbox.",
                this
            );
        }
        else
        {
            axeHitbox.SetOwner(
                this
            );

            axeHitbox.DisableHitbox();
        }
    }

    private void OnEnable()
    {
        if (enemyController == null)
        {
            enemyController =
                GetComponent<EnemyController>();
        }

        if (enemyController == null)
        {
            return;
        }

        enemyController.OnDamaged -=
            HandleDamaged;

        enemyController.OnDamaged +=
            HandleDamaged;

        enemyController.OnDied -=
            HandleDied;

        enemyController.OnDied +=
            HandleDied;
    }

    private void Start()
    {
        /*
         * Fetch the Animator in Start rather than Awake.
         *
         * This avoids the same component initialization-order
         * issue we found during the Rogue migration.
         */
        animator =
            enemyController.Animator;

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError(
                $"{name}: TankLogic could not find an Animator.",
                this
            );

            enabled = false;
            return;
        }

        FindPlayerAxeDamageable();

        currentState =
            TankState.Patrolling;

        if (!enemyController.IsOnNavMesh)
        {
            Debug.LogError(
                $"{name}: TankLogic cannot start because " +
                "EnemyController is not positioned on a baked NavMesh.",
                this
            );

            enabled = false;
            return;
        }

        enemyController.BeginPatrol();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (enemyController == null)
        {
            return;
        }

        if (enemyController.IsDead)
        {
            enemyController.StopAgent();

            DisableAxeHitbox();

            UpdateMovementAnimation();

            return;
        }

        if (enemyController.Player == null)
        {
            enemyController.StopAgent();

            DisableAxeHitbox();

            UpdateMovementAnimation();

            return;
        }

        if (playerAxeDamageable == null)
        {
            FindPlayerAxeDamageable();
        }

        // =====================================================
        // ENTANGLE
        // =====================================================

        if (enemyController.IsEntangled)
        {
            HandleEntangledState();

            wasEntangledLastFrame =
                true;

            UpdateMovementAnimation();

            return;
        }

        if (wasEntangledLastFrame)
        {
            wasEntangledLastFrame =
                false;

            ResumeAfterEntangle();
        }

        // =====================================================
        // COOLDOWN
        // =====================================================

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -=
                Time.deltaTime;
        }

        if (enemyController.IsPlayerDead)
        {
            ReturnHome();

            UpdateMovementAnimation();

            return;
        }

        float distanceToPlayer =
            enemyController.DistanceToPlayer;

        UpdateState(
            distanceToPlayer
        );

        RunCurrentState(
            distanceToPlayer
        );

        UpdateMovementAnimation();
    }

    // =========================================================
    // PLAYER DAMAGE INTERFACE
    // =========================================================

    private void FindPlayerAxeDamageable()
    {
        playerAxeDamageable =
            null;

        Transform player =
            enemyController != null
                ? enemyController.Player
                : null;

        if (player == null)
        {
            return;
        }

        MonoBehaviour[] behaviours =
            player.GetComponentsInChildren<MonoBehaviour>(
                true
            );

        foreach (
            MonoBehaviour behaviour
            in behaviours
        )
        {
            if (
                behaviour is
                IAxeDamageable axeDamageable
            )
            {
                playerAxeDamageable =
                    axeDamageable;

                return;
            }
        }

        Debug.LogWarning(
            $"{name}: Player does not contain an IAxeDamageable.",
            this
        );
    }

    // =========================================================
    // STATE
    // =========================================================

    private void UpdateState(
        float distanceToPlayer
    )
    {
        switch (currentState)
        {
            case TankState.Patrolling:
            case TankState.ReturningHome:

                if (enemyController.IsPlayerDetected())
                {
                    BeginChasing();
                }

                break;

            case TankState.Chasing:

                if (enemyController.HasLostPlayer())
                {
                    ReturnHome();
                }
                else if (
                    distanceToPlayer <=
                    attackRange
                )
                {
                    BeginAttacking();
                }

                break;

            case TankState.Attacking:

                /*
                 * Do not leave the attack state while the
                 * current axe animation is still active.
                 */
                if (isPerformingAttack)
                {
                    break;
                }

                if (enemyController.HasLostPlayer())
                {
                    ReturnHome();
                }
                else if (
                    distanceToPlayer >
                    attackExitRange
                )
                {
                    BeginChasing();
                }

                break;
        }
    }

    private void RunCurrentState(
        float distanceToPlayer
    )
    {
        switch (currentState)
        {
            case TankState.Patrolling:

                enemyController.UpdatePatrol();

                break;

            case TankState.Chasing:

                ChasePlayer();

                break;

            case TankState.Attacking:

                AttackPlayer(
                    distanceToPlayer
                );

                break;

            case TankState.ReturningHome:

                ReturnToPatrolZone();

                break;
        }
    }

    // =========================================================
    // CHASE
    // =========================================================

    private void BeginChasing()
    {
        if (enemyController.IsEntangled)
        {
            return;
        }

        currentState =
            TankState.Chasing;

        CancelCurrentAttack();

        enemyController.ClearPatrolState();

        if (
            enemyController.Player ==
            null
        )
        {
            return;
        }

        enemyController.SetDestination(
            enemyController.Player.position,
            chaseSpeed,
            chaseStoppingDistance
        );
    }

    private void ChasePlayer()
    {
        if (
            enemyController.IsEntangled ||
            enemyController.Player == null
        )
        {
            return;
        }

        enemyController.SetDestination(
            enemyController.Player.position,
            chaseSpeed,
            chaseStoppingDistance
        );
    }

    // =========================================================
    // ATTACK
    // =========================================================

    private void BeginAttacking()
    {
        if (enemyController.IsEntangled)
        {
            return;
        }

        currentState =
            TankState.Attacking;

        enemyController.StopAgent();
    }

    private void AttackPlayer(
        float distanceToPlayer
    )
    {
        enemyController.StopAgent();

        enemyController.FacePlayer(
            attackRotationSpeed
        );

        /*
         * Let the current axe attack finish first.
         */
        if (isPerformingAttack)
        {
            return;
        }

        if (
            distanceToPlayer >
            attackExitRange
        )
        {
            BeginChasing();

            return;
        }

        /*
         * AttackExitRange is only a state buffer.
         * A new attack only begins inside AttackRange.
         */
        if (
            distanceToPlayer >
            attackRange
        )
        {
            return;
        }

        if (attackCooldownTimer > 0f)
        {
            return;
        }

        if (
            enemyController.IsPlayerDead ||
            playerAxeDamageable == null
        )
        {
            return;
        }

        BeginAttack();
    }

    private void BeginAttack()
    {
        if (
            enemyController.IsDead ||
            enemyController.IsEntangled ||
            enemyController.Player == null ||
            isPerformingAttack
        )
        {
            return;
        }

        isPerformingAttack =
            true;

        attackCooldownTimer =
            attackCooldown;

        DisableAxeHitbox();

        enemyController.StopAgent();

        enemyController.FacePlayer(
            attackRotationSpeed
        );

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackHash
            );

            animator.SetTrigger(
                AttackHash
            );
        }
        else
        {
            /*
             * Safety fallback so the Tank cannot become
             * permanently stuck if the Animator is missing.
             */
            EndAttack();
        }
    }

    // =========================================================
    // AXE DAMAGE
    // =========================================================

    public void TryDamagePlayer(
        IAxeDamageable targetPlayer
    )
    {
        if (
            enemyController.IsDead ||
            enemyController.IsEntangled ||
            !isPerformingAttack ||
            currentState !=
            TankState.Attacking
        )
        {
            return;
        }

        if (
            targetPlayer == null ||
            enemyController.IsPlayerDead
        )
        {
            return;
        }

        /*
         * Only damage the player currently tracked by this Tank.
         */
        if (
            playerAxeDamageable != null &&
            !ReferenceEquals(
                targetPlayer,
                playerAxeDamageable
            )
        )
        {
            return;
        }

        targetPlayer.TakeAxeDamage(
            attackDamage
        );
    }

    // =========================================================
    // ANIMATION EVENTS
    // =========================================================

    /*
     * These methods are intended to be called directly by
     * Animation Events on the Tank's attack clip.
     *
     * If TankLogic and the Animator live on the same GameObject,
     * TankAnimationEventRelay is no longer necessary.
     */

    public void EnableAxeHitbox()
    {
        if (
            enemyController.IsDead ||
            enemyController.IsEntangled ||
            !isPerformingAttack ||
            currentState !=
            TankState.Attacking
        )
        {
            return;
        }

        if (axeHitbox != null)
        {
            axeHitbox.EnableHitbox();
        }
    }

    public void DisableAxeHitbox()
    {
        if (axeHitbox != null)
        {
            axeHitbox.DisableHitbox();
        }
    }

    public void EndAttack()
    {
        DisableAxeHitbox();

        isPerformingAttack =
            false;

        if (
            enemyController.IsDead ||
            enemyController.IsEntangled ||
            enemyController.Player == null ||
            enemyController.IsPlayerDead
        )
        {
            return;
        }

        float distanceToPlayer =
            enemyController.DistanceToPlayer;

        if (enemyController.HasLostPlayer())
        {
            ReturnHome();
        }
        else if (
            distanceToPlayer >
            attackExitRange
        )
        {
            BeginChasing();
        }

        /*
         * Otherwise remain in Attacking.
         *
         * AttackPlayer will wait for the cooldown and only
         * begin another swing when the player is in AttackRange.
         */
    }

    // =========================================================
    // ENTANGLE
    // =========================================================

    private void HandleEntangledState()
    {
        enemyController.StopAgent();

        CancelCurrentAttack();
    }

    private void ResumeAfterEntangle()
    {
        if (
            enemyController.IsDead ||
            enemyController.Player == null
        )
        {
            return;
        }

        if (enemyController.IsPlayerDead)
        {
            ReturnHome();

            RecoverAnimatorAfterEntangle();

            return;
        }

        float distanceToPlayer =
            enemyController.DistanceToPlayer;

        if (
            distanceToPlayer <=
            attackRange
        )
        {
            BeginAttacking();

            RecoverAnimatorAfterEntangle();

            return;
        }

        if (
            distanceToPlayer <=
            enemyController.DetectionRange
        )
        {
            BeginChasing();

            RecoverAnimatorAfterEntangle();

            return;
        }

        ReturnHome();

        RecoverAnimatorAfterEntangle();
    }

    private void RecoverAnimatorAfterEntangle()
    {
        if (animator == null)
        {
            return;
        }

        animator.ResetTrigger(
            EntangleHash
        );

        animator.ResetTrigger(
            AttackHash
        );

        animator.SetFloat(
            SpeedHash,
            GetAnimationSpeedForCurrentState()
        );

        movementAnimationRecoveryFrames =
            entangleRecoveryAnimationFrames;

        if (
            string.IsNullOrWhiteSpace(
                locomotionRecoveryStateName
            )
        )
        {
            return;
        }

        int recoveryStateHash =
            Animator.StringToHash(
                locomotionRecoveryStateName
            );

        if (
            !animator.HasState(
                0,
                recoveryStateHash
            )
        )
        {
            Debug.LogWarning(
                $"{name}: Animator does not contain recovery state " +
                $"'{locomotionRecoveryStateName}'.",
                this
            );

            return;
        }

        animator.CrossFadeInFixedTime(
            recoveryStateHash,
            entangleRecoveryTransitionDuration,
            0,
            0f
        );
    }

    // =========================================================
    // DAMAGE REACTION
    // =========================================================

    private void HandleDamaged()
    {
        if (
            enemyController == null ||
            enemyController.IsDead ||
            !isPerformingAttack
        )
        {
            return;
        }

        /*
         * Preserve the old TankEnemy behaviour:
         * being hit during an axe swing interrupts the attack.
         */
        CancelCurrentAttack();

        if (!enemyController.IsEntangled)
        {
            if (
                enemyController.DistanceToPlayer <=
                attackExitRange
            )
            {
                currentState =
                    TankState.Attacking;
            }
            else
            {
                BeginChasing();
            }
        }
    }

    private void HandleDied()
    {
        wasEntangledLastFrame =
            false;

        movementAnimationRecoveryFrames =
            0;

        CancelCurrentAttack();

        enemyController.StopAgent();
    }

    private void CancelCurrentAttack()
    {
        isPerformingAttack =
            false;

        DisableAxeHitbox();

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackHash
            );
        }
    }

    // =========================================================
    // RETURN HOME
    // =========================================================

    private void ReturnHome()
    {
        if (
            currentState ==
            TankState.ReturningHome
        )
        {
            return;
        }

        currentState =
            TankState.ReturningHome;

        CancelCurrentAttack();

        enemyController.ClearPatrolState();

        enemyController.SetHomeDestination();
    }

    private void ReturnToPatrolZone()
    {
        bool reachedHome =
            enemyController.UpdateReturnHome();

        if (!reachedHome)
        {
            return;
        }

        currentState =
            TankState.Patrolling;

        enemyController.BeginWaitingAtPatrolPoint();
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private void UpdateMovementAnimation()
    {
        if (
            animator == null ||
            enemyController.Agent == null
        )
        {
            return;
        }

        if (
            enemyController.IsDead ||
            enemyController.IsEntangled ||
            isPerformingAttack
        )
        {
            animator.SetFloat(
                SpeedHash,
                0f,
                0.08f,
                Time.deltaTime
            );

            return;
        }

        if (
            movementAnimationRecoveryFrames >
            0
        )
        {
            movementAnimationRecoveryFrames--;

            animator.SetFloat(
                SpeedHash,
                GetAnimationSpeedForCurrentState()
            );

            return;
        }

        Vector3 movementVelocity =
            enemyController.Agent.velocity;

        movementVelocity.y =
            0f;

        bool isActuallyMoving =
            enemyController.IsOnNavMesh &&
            !enemyController.Agent.isStopped &&
            movementVelocity.sqrMagnitude >
            0.01f;

        float animationSpeed =
            isActuallyMoving
                ? GetAnimationSpeedForCurrentState()
                : 0f;

        animator.SetFloat(
            SpeedHash,
            animationSpeed,
            0.08f,
            Time.deltaTime
        );
    }

    private float GetAnimationSpeedForCurrentState()
    {
        switch (currentState)
        {
            case TankState.Patrolling:
            case TankState.ReturningHome:

                return 0.5f;

            case TankState.Chasing:

                return 1f;

            case TankState.Attacking:
            default:

                return 0f;
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDisable()
    {
        if (enemyController != null)
        {
            enemyController.OnDamaged -=
                HandleDamaged;

            enemyController.OnDied -=
                HandleDied;

            enemyController.StopAgent();
        }

        CancelCurrentAttack();

        if (animator != null)
        {
            animator.SetFloat(
                SpeedHash,
                0f
            );
        }
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        attackRange =
            Mathf.Max(
                0f,
                attackRange
            );

        chaseStoppingDistance =
            Mathf.Clamp(
                chaseStoppingDistance,
                0f,
                attackRange
            );

        attackExitRange =
            Mathf.Max(
                attackRange,
                attackExitRange
            );

        attackDamage =
            Mathf.Max(
                1,
                attackDamage
            );

        attackCooldown =
            Mathf.Max(
                0f,
                attackCooldown
            );

        attackRotationSpeed =
            Mathf.Max(
                0f,
                attackRotationSpeed
            );

        chaseSpeed =
            Mathf.Max(
                0f,
                chaseSpeed
            );

        entangleRecoveryTransitionDuration =
            Mathf.Max(
                0f,
                entangleRecoveryTransitionDuration
            );

        entangleRecoveryAnimationFrames =
            Mathf.Max(
                0,
                entangleRecoveryAnimationFrames
            );
    }
}