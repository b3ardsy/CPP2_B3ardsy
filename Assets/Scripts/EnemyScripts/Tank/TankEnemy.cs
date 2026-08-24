using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class TankEnemy : Enemy
{
    private enum TankState
    {
        Patrolling,
        Chasing,
        Attacking,
        ReturningHome
    }

    // =========================================================
    // PATROL
    // =========================================================

    [Header("Patrol")]
    [Tooltip(
        "How far the Tank may wander from its " +
        "starting position."
    )]
    [SerializeField] private float patrolRadius = 10f;

    [Tooltip(
        "How long the Tank waits after reaching " +
        "a patrol destination."
    )]
    [SerializeField] private float patrolWaitTime = 2f;

    [Tooltip(
        "Extra distance allowed when checking whether " +
        "a destination was reached."
    )]
    [SerializeField] private float destinationTolerance = 0.25f;

    [Tooltip(
        "How far Unity searches for a valid NavMesh point " +
        "near a random position."
    )]
    [SerializeField] private float navMeshSampleDistance = 3f;

    [Tooltip(
        "How many times the Tank tries to find a valid " +
        "random patrol point."
    )]
    [SerializeField] private int patrolSearchAttempts = 10;

    // =========================================================
    // DETECTION
    // =========================================================

    [Header("Detection")]
    [Tooltip(
        "The player must enter this distance before " +
        "the Tank begins chasing."
    )]
    [SerializeField] private float detectionRange = 20f;

    [Tooltip(
        "The Tank stops chasing when the player " +
        "exceeds this distance."
    )]
    [SerializeField] private float loseTargetRange = 30f;

    // =========================================================
    // COMBAT
    // =========================================================

    [Header("Combat")]
    [Tooltip(
        "Maximum distance from which a NEW axe attack " +
        "may actually begin."
    )]
    [SerializeField] private float attackRange = 4f;

    [Tooltip(
        "How close the NavMeshAgent attempts to get " +
        "while chasing. Keep this slightly smaller " +
        "than Attack Range."
    )]
    [SerializeField] private float chaseStoppingDistance = 3.25f;

    [Tooltip(
        "Distance the player must move beyond before " +
        "the Tank leaves the attacking state and resumes chasing. " +
        "This should be slightly larger than Attack Range."
    )]
    [SerializeField] private float attackExitRange = 4.75f;

    [Tooltip(
        "Damage dealt each time the axe connects."
    )]
    [SerializeField] private int attackDamage = 1;

    [Tooltip(
        "Time between complete attack animations."
    )]
    [SerializeField] private float attackCooldown = 3f;

    [Tooltip(
        "How quickly the Tank turns toward the player " +
        "while attacking."
    )]
    [SerializeField] private float attackRotationSpeed = 25f;

    [Tooltip(
        "The trigger hitbox attached to the axe head."
    )]
    [SerializeField] private TankWeaponHitbox axeHitbox;

    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 6f;
    [SerializeField] private float chaseSpeed = 10f;
    [SerializeField] private float returnSpeed = 6f;

    // =========================================================
    // RUNTIME
    // =========================================================

    private NavMeshAgent agent;
    private Health playerHealth;
    private IAxeDamageable playerAxeDamageable;

    private TankState currentState;

    private Vector3 homePosition;

    private float patrolWaitTimer;
    private float attackCooldownTimer;

    private bool isWaitingAtPatrolPoint;
    private bool hasPatrolDestination;
    private bool isPerformingAttack;

    /*
     * Tracks whether the Tank was Entangled on the
     * previous frame.
     *
     * Once Entangle ends, this allows us to restore
     * the AI according to the player's current position.
     */
    private bool wasEntangledLastFrame;

    /*
     * For a few frames after Entangle ends, drive the Animator
     * from the AI state rather than NavMeshAgent.velocity.
     * The agent can report zero velocity briefly after being restarted.
     */
    private int movementAnimationRecoveryFrames;

    [Header("Entangle Recovery")]
    [Tooltip(
        "Animator state used as a safe locomotion re-entry point " +
        "after Entangle ends. Leave blank to skip the forced crossfade."
    )]
    [SerializeField] private string locomotionRecoveryStateName = "Idle";

    [Tooltip(
        "How quickly the Animator crossfades out of Entangle and " +
        "back into locomotion."
    )]
    [SerializeField] private float entangleRecoveryTransitionDuration = 0.05f;

    [Tooltip(
        "How many frames movement animation is driven from AI state " +
        "after Entangle ends, while the NavMeshAgent regains velocity."
    )]
    [SerializeField] private int entangleRecoveryAnimationFrames = 3;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    // =========================================================
    // INITIALIZATION
    // =========================================================

    protected override void Awake()
    {
        base.Awake();

        agent =
            GetComponent<NavMeshAgent>();

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
                $"{name}: No TankWeaponHitbox was found " +
                "in the Tank's children.",
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

        FindPlayerComponents();
    }

    private void Start()
    {
        homePosition =
            transform.position;

        currentState =
            TankState.Patrolling;

        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            Debug.LogError(
                $"{name}: TankEnemy is not positioned " +
                "on a baked NavMesh.",
                this
            );

            enabled = false;
            return;
        }

        ConfigureAgentForPatrol();

        ChooseRandomPatrolDestination();
    }

    private void FindPlayerComponents()
    {
        if (player == null)
        {
            return;
        }

        playerHealth =
            player.GetComponent<Health>();

        if (playerHealth == null)
        {
            playerHealth =
                player.GetComponentInParent<Health>();
        }

        if (playerHealth == null)
        {
            playerHealth =
                player.GetComponentInChildren<Health>();
        }

        playerAxeDamageable =
            FindPlayerAxeDamageable();

        if (playerHealth == null)
        {
            Debug.LogError(
                $"{name}: The Player does not have " +
                "a Health component.",
                this
            );
        }

        if (playerAxeDamageable == null)
        {
            Debug.LogError(
                $"{name}: The Player does not have a component " +
                "implementing IAxeDamageable.",
                this
            );
        }
    }

    private IAxeDamageable FindPlayerAxeDamageable()
    {
        if (player == null)
        {
            return null;
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
                return axeDamageable;
            }
        }

        return null;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (isDead)
        {
            StopAgent();
            return;
        }

        if (player == null)
        {
            StopAgent();
            return;
        }

        if (
            playerHealth == null ||
            playerAxeDamageable == null
        )
        {
            FindPlayerComponents();
        }

        // =====================================================
        // ENTANGLE
        // =====================================================

        /*
         * Entangle completely suspends Tank AI.
         *
         * Nothing below this point runs while the
         * Tank is trapped.
         */
        if (IsEntangled)
        {
            HandleEntangledState();

            wasEntangledLastFrame =
                true;

            UpdateMovementAnimation();

            return;
        }

        /*
         * Entangle just ended.
         *
         * Work out what the Tank should do now based
         * on the player's CURRENT position.
         */
        if (wasEntangledLastFrame)
        {
            wasEntangledLastFrame =
                false;

            ResumeAfterEntangle();
        }

        /*
         * Cooldown intentionally pauses during Entangle
         * because this code is below the Entangle return.
         */
        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -=
                Time.deltaTime;
        }

        if (
            playerHealth != null &&
            playerHealth.IsDead
        )
        {
            ReturnHome();
        }

        float distanceToPlayer =
            GetFlatDistance(
                transform.position,
                player.position
            );

        UpdateState(
            distanceToPlayer
        );

        RunCurrentState(
            distanceToPlayer
        );

        UpdateMovementAnimation();
    }

    // =========================================================
    // ENTANGLE
    // =========================================================

    private void HandleEntangledState()
    {
        /*
         * Completely halt NavMesh movement.
         */
        StopAgent();

        /*
         * If the Tank was caught during an attack,
         * cancel that attack immediately.
         */
        if (isPerformingAttack)
        {
            isPerformingAttack =
                false;

            if (animator != null)
            {
                animator.ResetTrigger(
                    AttackHash
                );
            }
        }

        /*
         * The axe can never remain dangerous while
         * the Tank is trapped.
         */
        DisableAxeHitbox();
    }

    private void ResumeAfterEntangle()
    {
        if (
            isDead ||
            player == null
        )
        {
            return;
        }

        if (IsPlayerDead())
        {
            ReturnHome();

            RecoverAnimatorAfterEntangle();

            return;
        }

        float distanceToPlayer =
            GetFlatDistance(
                transform.position,
                player.position
            );

        /*
         * Player is still within immediate attack range.
         */
        if (
            distanceToPlayer <=
            attackRange
        )
        {
            BeginAttacking();

            RecoverAnimatorAfterEntangle();

            return;
        }

        /*
         * Player is still close enough to remain engaged.
         */
        if (
            distanceToPlayer <=
            detectionRange
        )
        {
            BeginChasing();

            RecoverAnimatorAfterEntangle();

            return;
        }

        /*
         * Otherwise return to the patrol area.
         */
        ReturnHome();

        RecoverAnimatorAfterEntangle();
    }

    private void RecoverAnimatorAfterEntangle()
    {
        if (animator == null)
        {
            return;
        }

        /*
         * Force an immediate locomotion value so the Animator does
         * not wait for NavMeshAgent.velocity to wake back up.
         */
        animator.SetFloat(
            SpeedHash,
            GetAnimationSpeedForCurrentState()
        );

        movementAnimationRecoveryFrames =
            entangleRecoveryAnimationFrames;

        /*
         * Entangle is entered through an Any State transition.
         * Crossfade back to a known locomotion state so a looping
         * Entangle clip cannot trap the Animator after the status ends.
         */
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
                $"{name}: Animator does not contain the " +
                $"Entangle recovery state " +
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

                if (
                    distanceToPlayer <=
                    detectionRange &&
                    !IsPlayerDead()
                )
                {
                    BeginChasing();
                }

                break;

            case TankState.Chasing:

                if (
                    distanceToPlayer >
                    loseTargetRange ||
                    IsPlayerDead()
                )
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
                 * Never interrupt an attack that is
                 * already in progress.
                 */
                if (isPerformingAttack)
                {
                    break;
                }

                if (
                    distanceToPlayer >
                    loseTargetRange ||
                    IsPlayerDead()
                )
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
                 * Between Attack Range and Attack Exit Range,
                 * remain in the Attacking state but do not
                 * begin a new attack.
                 */
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

                Patrol();
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
    // PATROL
    // =========================================================

    private void Patrol()
    {
        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        agent.speed =
            patrolSpeed;

        agent.stoppingDistance =
            0f;

        if (isWaitingAtPatrolPoint)
        {
            agent.isStopped =
                true;

            patrolWaitTimer -=
                Time.deltaTime;

            if (
                patrolWaitTimer <=
                0f
            )
            {
                isWaitingAtPatrolPoint =
                    false;

                ChooseRandomPatrolDestination();
            }

            return;
        }

        if (!hasPatrolDestination)
        {
            ChooseRandomPatrolDestination();
            return;
        }

        if (!HasReachedDestination())
        {
            return;
        }

        agent.isStopped =
            true;

        hasPatrolDestination =
            false;

        isWaitingAtPatrolPoint =
            true;

        patrolWaitTimer =
            patrolWaitTime;
    }

    private void ChooseRandomPatrolDestination()
    {
        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        for (
            int attempt = 0;
            attempt < patrolSearchAttempts;
            attempt++
        )
        {
            Vector2 randomCircle =
                Random.insideUnitCircle *
                patrolRadius;

            Vector3 randomPosition =
                homePosition +
                new Vector3(
                    randomCircle.x,
                    0f,
                    randomCircle.y
                );

            if (
                !NavMesh.SamplePosition(
                    randomPosition,
                    out NavMeshHit hit,
                    navMeshSampleDistance,
                    agent.areaMask
                )
            )
            {
                continue;
            }

            float distanceFromHome =
                GetFlatDistance(
                    homePosition,
                    hit.position
                );

            if (
                distanceFromHome >
                patrolRadius
            )
            {
                continue;
            }

            agent.speed =
                patrolSpeed;

            agent.stoppingDistance =
                0f;

            agent.isStopped =
                false;

            hasPatrolDestination =
                agent.SetDestination(
                    hit.position
                );

            return;
        }

        Debug.LogWarning(
            $"{name}: Could not find a valid " +
            "random patrol destination.",
            this
        );

        hasPatrolDestination =
            false;

        isWaitingAtPatrolPoint =
            true;

        patrolWaitTimer =
            patrolWaitTime;
    }

    // =========================================================
    // CHASE
    // =========================================================

    private void BeginChasing()
    {
        if (IsEntangled)
        {
            return;
        }

        currentState =
            TankState.Chasing;

        isPerformingAttack =
            false;

        DisableAxeHitbox();

        isWaitingAtPatrolPoint =
            false;

        hasPatrolDestination =
            false;

        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        agent.speed =
            chaseSpeed;

        agent.stoppingDistance =
            chaseStoppingDistance;

        agent.isStopped =
            false;
    }

    private void ChasePlayer()
    {
        if (
            IsEntangled ||
            agent == null ||
            !agent.isOnNavMesh ||
            player == null
        )
        {
            return;
        }

        agent.speed =
            chaseSpeed;

        agent.stoppingDistance =
            chaseStoppingDistance;

        agent.isStopped =
            false;

        agent.SetDestination(
            player.position
        );
    }

    // =========================================================
    // ATTACK
    // =========================================================

    private void BeginAttacking()
    {
        if (IsEntangled)
        {
            return;
        }

        currentState =
            TankState.Attacking;

        StopAgent();
    }

    private void AttackPlayer(
        float distanceToPlayer
    )
    {
        if (IsEntangled)
        {
            return;
        }

        StopAgent();

        FacePlayer();

        /*
         * Allow the current attack animation to finish.
         */
        if (isPerformingAttack)
        {
            return;
        }

        /*
         * Leave the attacking state entirely once the
         * player exceeds Attack Exit Range.
         */
        if (
            distanceToPlayer >
            attackExitRange
        )
        {
            BeginChasing();
            return;
        }

        /*
         * Attack Exit Range is only a state buffer.
         *
         * A new attack may only begin inside the
         * actual Attack Range.
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
            playerHealth == null ||
            playerHealth.IsDead ||
            playerAxeDamageable == null
        )
        {
            return;
        }

        isPerformingAttack =
            true;

        attackCooldownTimer =
            attackCooldown;

        DisableAxeHitbox();

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackHash
            );

            animator.SetTrigger(
                AttackHash
            );
        }
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    public void TryDamagePlayer(
        IAxeDamageable targetPlayer
    )
    {
        /*
         * Safety:
         * even if an old axe Animation Event somehow
         * fires during Entangle, no damage is permitted.
         */
        if (
            isDead ||
            IsEntangled
        )
        {
            return;
        }

        if (
            !isPerformingAttack ||
            currentState !=
            TankState.Attacking
        )
        {
            return;
        }

        if (
            targetPlayer == null ||
            playerHealth == null ||
            playerHealth.IsDead
        )
        {
            return;
        }

        /*
         * Only damage the Player this Tank is currently targeting.
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

    public void EnableAxeHitbox()
    {
        if (
            isDead ||
            IsEntangled ||
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

        /*
         * If Entangle interrupted this animation,
         * don't let the old EndAttack event restart AI.
         */
        if (IsEntangled)
        {
            return;
        }

        if (
            isDead ||
            player == null ||
            IsPlayerDead()
        )
        {
            return;
        }

        float distanceToPlayer =
            GetFlatDistance(
                transform.position,
                player.position
            );

        if (
            distanceToPlayer >
            loseTargetRange
        )
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
         * Inside Attack Exit Range:
         *
         * stay in Attacking state.
         *
         * AttackPlayer() will only permit another
         * swing inside the actual Attack Range.
         */
    }

    private void FacePlayer()
    {
        if (
            player == null ||
            IsEntangled
        )
        {
            return;
        }

        Vector3 direction =
            player.position -
            transform.position;

        direction.y =
            0f;

        if (
            direction.sqrMagnitude <=
            0.001f
        )
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                attackRotationSpeed *
                Time.deltaTime
            );
    }

    // =========================================================
    // RETURN HOME
    // =========================================================

    private void ReturnHome()
    {
        if (IsEntangled)
        {
            return;
        }

        if (
            currentState ==
            TankState.ReturningHome
        )
        {
            return;
        }

        currentState =
            TankState.ReturningHome;

        isPerformingAttack =
            false;

        DisableAxeHitbox();

        isWaitingAtPatrolPoint =
            false;

        hasPatrolDestination =
            false;

        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        agent.speed =
            returnSpeed;

        agent.stoppingDistance =
            0f;

        agent.isStopped =
            false;

        SetHomeDestination();
    }

    private void ReturnToPatrolZone()
    {
        if (
            IsEntangled ||
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        agent.speed =
            returnSpeed;

        agent.stoppingDistance =
            0f;

        agent.isStopped =
            false;

        if (
            !agent.hasPath &&
            !agent.pathPending
        )
        {
            SetHomeDestination();
        }

        if (!HasReachedDestination())
        {
            return;
        }

        agent.isStopped =
            true;

        agent.ResetPath();

        currentState =
            TankState.Patrolling;

        isWaitingAtPatrolPoint =
            true;

        patrolWaitTimer =
            patrolWaitTime;

        hasPatrolDestination =
            false;
    }

    private void SetHomeDestination()
    {
        if (
            IsEntangled ||
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        if (
            NavMesh.SamplePosition(
                homePosition,
                out NavMeshHit hit,
                navMeshSampleDistance,
                agent.areaMask
            )
        )
        {
            agent.SetDestination(
                hit.position
            );

            return;
        }

        Debug.LogWarning(
            $"{name}: Could not find its home " +
            "position on the NavMesh.",
            this
        );

        currentState =
            TankState.Patrolling;

        ChooseRandomPatrolDestination();
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private void UpdateMovementAnimation()
    {
        if (
            animator == null ||
            agent == null ||
            !agent.enabled
        )
        {
            return;
        }

        /*
         * While Entangled, explicitly report zero
         * locomotion speed to the Animator.
         */
        if (IsEntangled)
        {
            movementAnimationRecoveryFrames =
                0;

            animator.SetFloat(
                SpeedHash,
                0f,
                0.08f,
                Time.deltaTime
            );

            return;
        }

        /*
         * NavMeshAgent.velocity may remain zero for a few frames
         * after StopAgent() / ResetPath(). During that short window,
         * use the AI state itself to restore the correct locomotion
         * animation immediately.
         */
        if (
            movementAnimationRecoveryFrames > 0 &&
            !isPerformingAttack
        )
        {
            movementAnimationRecoveryFrames--;

            animator.SetFloat(
                SpeedHash,
                GetAnimationSpeedForCurrentState()
            );

            return;
        }

        float animationSpeed =
            0f;

        Vector3 movementVelocity =
            agent.velocity;

        movementVelocity.y =
            0f;

        bool isActuallyMoving =
            !agent.isStopped &&
            movementVelocity.sqrMagnitude >
            0.01f;

        if (
            isActuallyMoving &&
            !isPerformingAttack
        )
        {
            animationSpeed =
                GetAnimationSpeedForCurrentState();
        }

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
    // HELPERS
    // =========================================================

    private void ConfigureAgentForPatrol()
    {
        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        agent.speed =
            patrolSpeed;

        agent.stoppingDistance =
            0f;

        agent.isStopped =
            false;
    }

    private bool HasReachedDestination()
    {
        if (
            agent == null ||
            !agent.isOnNavMesh ||
            agent.pathPending
        )
        {
            return false;
        }

        if (!agent.hasPath)
        {
            return false;
        }

        return
            agent.remainingDistance <=
            agent.stoppingDistance +
            destinationTolerance;
    }

    private bool IsPlayerDead()
    {
        return
            playerHealth != null &&
            playerHealth.IsDead;
    }

    private float GetFlatDistance(
        Vector3 first,
        Vector3 second
    )
    {
        first.y =
            0f;

        second.y =
            0f;

        return Vector3.Distance(
            first,
            second
        );
    }

    private void StopAgent()
    {
        if (
            agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        agent.isStopped =
            true;

        agent.ResetPath();
    }

    // =========================================================
    // DAMAGE REACTION
    // =========================================================

    public override void TakeDamage(
        int damage
    )
    {
        if (
            isDead ||
            damage <= 0
        )
        {
            return;
        }

        /*
         * A hit may interrupt an existing attack.
         */
        if (isPerformingAttack)
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

        base.TakeDamage(
            damage
        );
    }

    protected override void Die()
    {
        isPerformingAttack =
            false;

        wasEntangledLastFrame =
            false;

        movementAnimationRecoveryFrames =
            0;

        DisableAxeHitbox();

        StopAgent();

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackHash
            );
        }

        if (
            agent != null &&
            agent.enabled
        )
        {
            agent.enabled =
                false;
        }

        base.Die();
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        patrolRadius =
            Mathf.Max(
                0.5f,
                patrolRadius
            );

        patrolWaitTime =
            Mathf.Max(
                0f,
                patrolWaitTime
            );

        destinationTolerance =
            Mathf.Max(
                0.05f,
                destinationTolerance
            );

        navMeshSampleDistance =
            Mathf.Max(
                0.5f,
                navMeshSampleDistance
            );

        patrolSearchAttempts =
            Mathf.Max(
                1,
                patrolSearchAttempts
            );

        detectionRange =
            Mathf.Max(
                0.5f,
                detectionRange
            );

        loseTargetRange =
            Mathf.Max(
                detectionRange +
                0.5f,
                loseTargetRange
            );

        attackRange =
            Mathf.Clamp(
                attackRange,
                0.2f,
                detectionRange
            );

        chaseStoppingDistance =
            Mathf.Clamp(
                chaseStoppingDistance,
                0f,
                Mathf.Max(
                    0f,
                    attackRange -
                    0.1f
                )
            );

        attackExitRange =
            Mathf.Clamp(
                attackExitRange,
                attackRange +
                0.1f,
                loseTargetRange
            );

        attackDamage =
            Mathf.Max(
                1,
                attackDamage
            );

        attackCooldown =
            Mathf.Max(
                0.1f,
                attackCooldown
            );

        attackRotationSpeed =
            Mathf.Max(
                0f,
                attackRotationSpeed
            );

        patrolSpeed =
            Mathf.Max(
                0f,
                patrolSpeed
            );

        chaseSpeed =
            Mathf.Max(
                0f,
                chaseSpeed
            );

        returnSpeed =
            Mathf.Max(
                0f,
                returnSpeed
            );

        entangleRecoveryTransitionDuration =
            Mathf.Max(
                0f,
                entangleRecoveryTransitionDuration
            );

        entangleRecoveryAnimationFrames =
            Mathf.Max(
                1,
                entangleRecoveryAnimationFrames
            );
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 centre =
            Application.isPlaying
                ? homePosition
                : transform.position;

        Gizmos.color =
            Color.cyan;

        Gizmos.DrawWireSphere(
            centre,
            patrolRadius
        );

        Gizmos.color =
            Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );

        Gizmos.color =
            Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            loseTargetRange
        );

        Gizmos.color =
            Color.magenta;

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        Gizmos.color =
            Color.white;

        Gizmos.DrawWireSphere(
            transform.position,
            attackExitRange
        );
    }
}