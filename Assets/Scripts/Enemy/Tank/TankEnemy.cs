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
    private PlayerStatsNew playerStats;

    private TankState currentState;

    private Vector3 homePosition;

    private float patrolWaitTimer;
    private float attackCooldownTimer;

    private bool isWaitingAtPatrolPoint;
    private bool hasPatrolDestination;
    private bool isPerformingAttack;

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

        FindPlayerStats();
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

    private void FindPlayerStats()
    {
        if (player == null)
        {
            return;
        }

        playerStats =
            player.GetComponent<PlayerStatsNew>();

        if (playerStats == null)
        {
            playerStats =
                player.GetComponentInParent<PlayerStatsNew>();
        }

        if (playerStats == null)
        {
            playerStats =
                player.GetComponentInChildren<PlayerStatsNew>();
        }

        if (playerStats == null)
        {
            Debug.LogError(
                $"{name}: The Player does not have " +
                "a PlayerStatsNew component.",
                this
            );
        }
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

        if (playerStats == null)
        {
            FindPlayerStats();
        }

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -=
                Time.deltaTime;
        }

        if (
            playerStats != null &&
            playerStats.IsDead
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
        currentState =
            TankState.Attacking;

        StopAgent();
    }

    private void AttackPlayer(
        float distanceToPlayer
    )
    {
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
         * IMPORTANT:
         *
         * Attack Exit Range is only a state buffer.
         * It does NOT grant permission to start another attack.
         *
         * If the player is outside the real Attack Range,
         * the Tank remains stationary and faces the player
         * without swinging.
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
            playerStats == null ||
            playerStats.IsDead
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
        PlayerStatsNew targetPlayer
    )
    {
        if (isDead)
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
            targetPlayer.IsDead
        )
        {
            return;
        }

        if (
            playerStats != null &&
            targetPlayer != playerStats
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
         * Stay in Attacking state.
         *
         * AttackPlayer() will only permit another
         * swing once the player is also inside
         * the actual Attack Range.
         */
    }

    private void FacePlayer()
    {
        if (player == null)
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
            switch (currentState)
            {
                case TankState.Patrolling:
                case TankState.ReturningHome:

                    animationSpeed =
                        0.5f;

                    break;

                case TankState.Chasing:

                    animationSpeed =
                        1f;

                    break;
            }
        }

        animator.SetFloat(
            SpeedHash,
            animationSpeed,
            0.08f,
            Time.deltaTime
        );
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
            playerStats != null &&
            playerStats.IsDead;
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