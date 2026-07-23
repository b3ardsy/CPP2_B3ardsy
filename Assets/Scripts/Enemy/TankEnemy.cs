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

    [Header("Patrol")]
    [Tooltip("How far the tank may wander from its starting position.")]
    [SerializeField] private float patrolRadius = 10f;

    [Tooltip("How long the tank waits after reaching a patrol destination.")]
    [SerializeField] private float patrolWaitTime = 2f;

    [Tooltip("Extra distance allowed when checking whether a destination was reached.")]
    [SerializeField] private float destinationTolerance = 0.25f;

    [Tooltip("How far Unity searches for a valid NavMesh point near a random position.")]
    [SerializeField] private float navMeshSampleDistance = 3f;

    [Tooltip("How many times the tank tries to find a valid random patrol point.")]
    [SerializeField] private int patrolSearchAttempts = 10;

    [Header("Detection")]
    [Tooltip("The player must enter this distance before the tank begins chasing.")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("The tank stops chasing when the player exceeds this distance.")]
    [SerializeField] private float loseTargetRange = 15f;

    [Header("Combat")]
    [Tooltip("Distance from which the tank begins attacking.")]
    [SerializeField] private float attackRange = 2.5f;

    [Tooltip("Damage dealt each time the axe connects.")]
    [SerializeField] private int attackDamage = 1;

    [Tooltip("Time between complete attack animations.")]
    [SerializeField] private float attackCooldown = 1.25f;

    [Tooltip("How quickly the tank turns toward the player while attacking.")]
    [SerializeField] private float attackRotationSpeed = 8f;

    [Tooltip("The trigger hitbox attached to the axe head.")]
    [SerializeField] private TankWeaponHitbox axeHitbox;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float returnSpeed = 3f;

    private NavMeshAgent agent;
    private PlayerStats playerStats;

    private TankState currentState;
    private Vector3 homePosition;

    private float patrolWaitTimer;
    private float attackCooldownTimer;

    private bool isWaitingAtPatrolPoint;
    private bool hasPatrolDestination;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    protected override void Awake()
    {
        base.Awake();

        agent = GetComponent<NavMeshAgent>();

        if (axeHitbox == null)
        {
            axeHitbox = GetComponentInChildren<TankWeaponHitbox>(true);
        }

        if (axeHitbox == null)
        {
            Debug.LogError(
                $"{name}: No TankWeaponHitbox was found in the tank's children.",
                this
            );
        }
        else
        {
            axeHitbox.SetOwner(this);
            axeHitbox.DisableHitbox();
        }

        if (player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();

            if (playerStats == null)
            {
                playerStats = player.GetComponentInParent<PlayerStats>();
            }

            if (playerStats == null)
            {
                Debug.LogError(
                    $"{name}: The Player does not have a PlayerStats component.",
                    this
                );
            }
        }
    }

    private void Start()
    {
        homePosition = transform.position;
        currentState = TankState.Patrolling;

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                $"{name}: TankEnemy is not positioned on a baked NavMesh.",
                this
            );

            enabled = false;
            return;
        }

        ConfigureAgentForPatrol();
        ChooseRandomPatrolDestination();
    }

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

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        if (playerStats != null && playerStats.IsDead)
        {
            ReturnHome();
        }

        float distanceToPlayer = GetFlatDistance(
            transform.position,
            player.position
        );

        UpdateState(distanceToPlayer);
        RunCurrentState(distanceToPlayer);
        UpdateMovementAnimation();
    }

    private void UpdateState(float distanceToPlayer)
    {
        switch (currentState)
        {
            case TankState.Patrolling:
            case TankState.ReturningHome:

                if (distanceToPlayer <= detectionRange &&
                    !IsPlayerDead())
                {
                    BeginChasing();
                }

                break;

            case TankState.Chasing:
            case TankState.Attacking:

                if (distanceToPlayer > loseTargetRange ||
                    IsPlayerDead())
                {
                    ReturnHome();
                }
                else if (distanceToPlayer <= attackRange)
                {
                    BeginAttacking();
                }
                else
                {
                    BeginChasing();
                }

                break;
        }
    }

    private void RunCurrentState(float distanceToPlayer)
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
                AttackPlayer(distanceToPlayer);
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
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;

        if (isWaitingAtPatrolPoint)
        {
            agent.isStopped = true;

            patrolWaitTimer -= Time.deltaTime;

            if (patrolWaitTimer <= 0f)
            {
                isWaitingAtPatrolPoint = false;
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

        agent.isStopped = true;
        hasPatrolDestination = false;
        isWaitingAtPatrolPoint = true;
        patrolWaitTimer = patrolWaitTime;
    }

    private void ChooseRandomPatrolDestination()
    {
        if (!agent.isOnNavMesh)
        {
            return;
        }

        for (int attempt = 0;
             attempt < patrolSearchAttempts;
             attempt++)
        {
            Vector2 randomCircle =
                Random.insideUnitCircle * patrolRadius;

            Vector3 randomPosition =
                homePosition +
                new Vector3(
                    randomCircle.x,
                    0f,
                    randomCircle.y
                );

            if (NavMesh.SamplePosition(
                    randomPosition,
                    out NavMeshHit hit,
                    navMeshSampleDistance,
                    agent.areaMask))
            {
                float distanceFromHome = GetFlatDistance(
                    homePosition,
                    hit.position
                );

                if (distanceFromHome > patrolRadius)
                {
                    continue;
                }

                agent.speed = patrolSpeed;
                agent.stoppingDistance = 0f;
                agent.isStopped = false;

                hasPatrolDestination =
                    agent.SetDestination(hit.position);

                return;
            }
        }

        Debug.LogWarning(
            $"{name}: Could not find a valid random patrol destination.",
            this
        );

        hasPatrolDestination = false;
        isWaitingAtPatrolPoint = true;
        patrolWaitTimer = patrolWaitTime;
    }

    // =========================================================
    // CHASE
    // =========================================================

    private void BeginChasing()
    {
        if (currentState == TankState.Chasing)
        {
            return;
        }

        currentState = TankState.Chasing;

        DisableAxeHitbox();

        isWaitingAtPatrolPoint = false;
        hasPatrolDestination = false;

        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange;
        agent.isStopped = false;
    }

    private void ChasePlayer()
    {
        if (!agent.isOnNavMesh || player == null)
        {
            return;
        }

        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange;
        agent.isStopped = false;

        agent.SetDestination(player.position);
    }

    // =========================================================
    // ATTACK
    // =========================================================

    private void BeginAttacking()
    {
        currentState = TankState.Attacking;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void AttackPlayer(float distanceToPlayer)
    {
        StopAgent();
        FacePlayer();

        if (distanceToPlayer > attackRange)
        {
            BeginChasing();
            return;
        }

        if (attackCooldownTimer > 0f)
        {
            return;
        }

        if (playerStats == null || playerStats.IsDead)
        {
            return;
        }

        if (animator != null)
        {
            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(AttackHash);
        }

        // No damage is dealt here.
        // The axe trigger handles damage during active hit windows.
        attackCooldownTimer = attackCooldown;
    }

    /// <summary>
    /// Called by the axe hitbox when it overlaps a PlayerStats component.
    /// </summary>
    public void TryDamagePlayer(PlayerStats targetPlayer)
    {
        if (isDead)
        {
            return;
        }

        if (currentState != TankState.Attacking)
        {
            return;
        }

        if (targetPlayer == null || targetPlayer.IsDead)
        {
            return;
        }

        // Only damage the player this enemy is targeting.
        if (playerStats != null && targetPlayer != playerStats)
        {
            return;
        }

        targetPlayer.TakeDamage(attackDamage);
    }

    /// <summary>
    /// Called through an Animation Event relay.
    /// Starts one damage window and clears the previous hit record.
    /// </summary>
    public void EnableAxeHitbox()
    {
        if (isDead || currentState != TankState.Attacking)
        {
            return;
        }

        if (axeHitbox != null)
        {
            axeHitbox.EnableHitbox();
        }
    }

    /// <summary>
    /// Called through an Animation Event relay.
    /// Ends the current damage window.
    /// </summary>
    public void DisableAxeHitbox()
    {
        if (axeHitbox != null)
        {
            axeHitbox.DisableHitbox();
        }
    }

    private void FacePlayer()
    {
        if (player == null)
        {
            return;
        }

        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            attackRotationSpeed * Time.deltaTime
        );
    }

    // =========================================================
    // RETURN HOME
    // =========================================================

    private void ReturnHome()
    {
        if (currentState == TankState.ReturningHome)
        {
            return;
        }

        currentState = TankState.ReturningHome;

        DisableAxeHitbox();

        isWaitingAtPatrolPoint = false;
        hasPatrolDestination = false;

        if (!agent.isOnNavMesh)
        {
            return;
        }

        agent.speed = returnSpeed;
        agent.stoppingDistance = 0f;
        agent.isStopped = false;

        SetHomeDestination();
    }

    private void ReturnToPatrolZone()
    {
        if (!agent.isOnNavMesh)
        {
            return;
        }

        agent.speed = returnSpeed;
        agent.stoppingDistance = 0f;

        if (!agent.hasPath && !agent.pathPending)
        {
            SetHomeDestination();
        }

        if (!HasReachedDestination())
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();

        currentState = TankState.Patrolling;

        isWaitingAtPatrolPoint = true;
        patrolWaitTimer = patrolWaitTime;
        hasPatrolDestination = false;
    }

    private void SetHomeDestination()
    {
        if (NavMesh.SamplePosition(
                homePosition,
                out NavMeshHit hit,
                navMeshSampleDistance,
                agent.areaMask))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            Debug.LogWarning(
                $"{name}: Could not find its home position on the NavMesh.",
                this
            );

            currentState = TankState.Patrolling;
            ChooseRandomPatrolDestination();
        }
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private void UpdateMovementAnimation()
    {
        if (animator == null ||
            agent == null ||
            !agent.enabled)
        {
            return;
        }

        float animationSpeed = 0f;

        if (!agent.isStopped)
        {
            switch (currentState)
            {
                case TankState.Patrolling:
                    animationSpeed = 0.5f;
                    break;

                case TankState.Chasing:
                    animationSpeed = 1f;
                    break;

                case TankState.ReturningHome:
                    animationSpeed = 0.5f;
                    break;

                case TankState.Attacking:
                    animationSpeed = 0f;
                    break;
            }
        }

        animator.SetFloat(
            SpeedHash,
            animationSpeed,
            0.1f,
            Time.deltaTime
        );
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void ConfigureAgentForPatrol()
    {
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;
        agent.isStopped = false;
    }

    private bool HasReachedDestination()
    {
        if (!agent.isOnNavMesh || agent.pathPending)
        {
            return false;
        }

        if (!agent.hasPath)
        {
            return false;
        }

        return agent.remainingDistance <=
               agent.stoppingDistance +
               destinationTolerance;
    }

    private bool IsPlayerDead()
    {
        return playerStats != null &&
               playerStats.IsDead;
    }

    private float GetFlatDistance(
        Vector3 first,
        Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;

        return Vector3.Distance(first, second);
    }

    private void StopAgent()
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
    }

    protected override void Die()
    {
        DisableAxeHitbox();
        StopAgent();

        if (animator != null)
        {
            animator.ResetTrigger(AttackHash);
        }

        if (agent != null && agent.enabled)
        {
            agent.enabled = false;
        }

        base.Die();
    }

    private void OnValidate()
    {
        patrolRadius = Mathf.Max(0.5f, patrolRadius);
        patrolWaitTime = Mathf.Max(0f, patrolWaitTime);
        destinationTolerance = Mathf.Max(
            0.05f,
            destinationTolerance
        );

        navMeshSampleDistance = Mathf.Max(
            0.5f,
            navMeshSampleDistance
        );

        patrolSearchAttempts = Mathf.Max(
            1,
            patrolSearchAttempts
        );

        detectionRange = Mathf.Max(
            0.5f,
            detectionRange
        );

        loseTargetRange = Mathf.Max(
            detectionRange + 0.5f,
            loseTargetRange
        );

        attackRange = Mathf.Clamp(
            attackRange,
            0.1f,
            detectionRange
        );

        attackDamage = Mathf.Max(1, attackDamage);
        attackCooldown = Mathf.Max(0.1f, attackCooldown);

        attackRotationSpeed = Mathf.Max(
            0f,
            attackRotationSpeed
        );

        patrolSpeed = Mathf.Max(0f, patrolSpeed);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 centre = Application.isPlaying
            ? homePosition
            : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(centre, patrolRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            loseTargetRange
        );

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );
    }
}