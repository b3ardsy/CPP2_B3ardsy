using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class RogueEnemy : Enemy
{
    private enum RogueState
    {
        Patrolling,
        Engaged,
        Attacking,
        ReturningHome
    }

    private enum RogueSpell
    {
        None,
        BonePrison,
        BoneCleave
    }

    [Header("Patrol")]
    [Tooltip("How far the rogue may wander from its starting position.")]
    [SerializeField] private float patrolRadius = 10f;

    [Tooltip("How long the rogue waits after reaching a patrol destination.")]
    [SerializeField] private float patrolWaitTime = 2f;

    [Tooltip("Extra distance allowed when checking whether a destination was reached.")]
    [SerializeField] private float destinationTolerance = 0.25f;

    [Tooltip("How far Unity searches for a valid NavMesh point.")]
    [SerializeField] private float navMeshSampleDistance = 3f;

    [Tooltip("How many attempts are made to find a patrol destination.")]
    [SerializeField] private int patrolSearchAttempts = 10;

    [Header("Detection")]
    [Tooltip("The player must enter this distance before the rogue engages.")]
    [SerializeField] private float detectionRange = 12f;

    [Tooltip("The rogue disengages when the player moves beyond this distance.")]
    [SerializeField] private float loseTargetRange = 17f;

    [Tooltip("Maximum distance from which the rogue can cast.")]
    [SerializeField] private float attackRange = 12f;

    [Header("Combat")]
    [Tooltip("Minimum delay between completed attacks.")]
    [SerializeField] private float minimumAttackCooldown = 2f;

    [Tooltip("Maximum delay between completed attacks.")]
    [SerializeField] private float maximumAttackCooldown = 3f;

    [Tooltip("How quickly the rogue turns toward the player.")]
    [SerializeField] private float attackRotationSpeed = 8f;

    [Range(0f, 1f)]
    [Tooltip("Chance that the rogue selects Bone Prison. The remaining chance selects Bone Cleave.")]
    [SerializeField] private float bonePrisonChance = 0.4f;

    [Header("Bone Prison")]
    [SerializeField] private GameObject bonePrisonPrefab;

    [Tooltip("Layers that the spell can use to find the ground.")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("How far above the targeted position the ground check begins.")]
    [SerializeField] private float groundCheckHeight = 5f;

    [Tooltip("Maximum distance used when finding the ground.")]
    [SerializeField] private float groundCheckDistance = 15f;

    [Header("Bone Cleave")]
    [SerializeField] private BoneCleave boneCleavePrefab;

    [Tooltip("Point from which Bone Cleave begins.")]
    [SerializeField] private Transform castPoint;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float returnSpeed = 3.5f;

    private NavMeshAgent agent;
    private PlayerStats playerStats;

    private RogueState currentState;
    private RogueSpell pendingSpell;

    private Vector3 homePosition;
    private Vector3 storedTargetPosition;
    private Vector3 storedCleaveDirection;

    private float patrolWaitTimer;
    private float attackCooldownTimer;

    private bool isWaitingAtPatrolPoint;
    private bool hasPatrolDestination;
    private bool isCasting;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    protected override void Awake()
    {
        base.Awake();

        agent = GetComponent<NavMeshAgent>();

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

        if (castPoint == null)
        {
            Debug.LogError(
                $"{name}: Cast Point has not been assigned.",
                this
            );
        }

        if (bonePrisonPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Bone Prison Prefab has not been assigned.",
                this
            );
        }

        if (boneCleavePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Bone Cleave Prefab has not been assigned.",
                this
            );
        }
    }

    private void Start()
    {
        homePosition = transform.position;
        currentState = RogueState.Patrolling;

        attackCooldownTimer = Random.Range(
            minimumAttackCooldown,
            maximumAttackCooldown
        );

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                $"{name}: RogueEnemy is not positioned on a baked NavMesh.",
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

        if (IsPlayerDead())
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
            case RogueState.Patrolling:
            case RogueState.ReturningHome:
                if (distanceToPlayer <= detectionRange &&
                    !IsPlayerDead())
                {
                    BeginEngagement();
                }
                break;

            case RogueState.Engaged:
            case RogueState.Attacking:
                if (distanceToPlayer > loseTargetRange ||
                    IsPlayerDead())
                {
                    ReturnHome();
                }
                break;
        }
    }

    private void RunCurrentState(float distanceToPlayer)
    {
        switch (currentState)
        {
            case RogueState.Patrolling:
                Patrol();
                break;

            case RogueState.Engaged:
                EngagePlayer(distanceToPlayer);
                break;

            case RogueState.Attacking:
                AttackPlayer();
                break;

            case RogueState.ReturningHome:
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
            $"{name}: Could not find a valid patrol destination.",
            this
        );

        hasPatrolDestination = false;
        isWaitingAtPatrolPoint = true;
        patrolWaitTimer = patrolWaitTime;
    }

    // =========================================================
    // ENGAGEMENT
    // =========================================================

    private void BeginEngagement()
    {
        currentState = RogueState.Engaged;

        isWaitingAtPatrolPoint = false;
        hasPatrolDestination = false;

        StopAgent();
    }

    private void EngagePlayer(float distanceToPlayer)
    {
        StopAgent();
        FacePlayer();

        if (isCasting)
        {
            return;
        }

        if (distanceToPlayer > attackRange)
        {
            return;
        }

        if (attackCooldownTimer > 0f)
        {
            return;
        }

        BeginAttack();
    }

    // =========================================================
    // ATTACK
    // =========================================================

    private void BeginAttack()
    {
        if (isCasting || player == null || IsPlayerDead())
        {
            return;
        }

        currentState = RogueState.Attacking;
        isCasting = true;

        StopAgent();
        FacePlayer();

        SelectSpell();
        StoreAttackTarget();

        if (animator != null)
        {
            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(AttackHash);
        }
        else
        {
            CastSpell();
            EndAttack();
        }
    }

    private void AttackPlayer()
    {
        StopAgent();
        FacePlayer();
    }

    private void SelectSpell()
    {
        bool canUsePrison = bonePrisonPrefab != null;
        bool canUseCleave =
            boneCleavePrefab != null && castPoint != null;

        if (canUsePrison && canUseCleave)
        {
            pendingSpell =
                Random.value <= bonePrisonChance
                    ? RogueSpell.BonePrison
                    : RogueSpell.BoneCleave;

            return;
        }

        if (canUsePrison)
        {
            pendingSpell = RogueSpell.BonePrison;
            return;
        }

        if (canUseCleave)
        {
            pendingSpell = RogueSpell.BoneCleave;
            return;
        }

        pendingSpell = RogueSpell.None;

        Debug.LogWarning(
            $"{name}: No rogue spell prefabs have been assigned.",
            this
        );
    }

    private void StoreAttackTarget()
    {
        storedTargetPosition = player.position;

        storedCleaveDirection =
            player.position - transform.position;

        storedCleaveDirection.y = 0f;

        if (storedCleaveDirection.sqrMagnitude <= 0.001f)
        {
            storedCleaveDirection = transform.forward;
        }

        storedCleaveDirection.Normalize();
    }

    /// <summary>
    /// Called through an Animation Event relay at the casting frame.
    /// </summary>
    public void CastSpell()
    {
        if (isDead || !isCasting)
        {
            return;
        }

        switch (pendingSpell)
        {
            case RogueSpell.BonePrison:
                CastBonePrison();
                break;

            case RogueSpell.BoneCleave:
                CastBoneCleave();
                break;
        }

        pendingSpell = RogueSpell.None;
    }

    private void CastBonePrison()
    {
        if (bonePrisonPrefab == null || player == null)
        {
            return;
        }

        Vector3 playerPosition = player.position;

        Vector3 spawnPosition =
            FindGroundPosition(playerPosition);

        Instantiate(
            bonePrisonPrefab,
            spawnPosition,
            bonePrisonPrefab.transform.rotation
        );
    }

    private void CastBoneCleave()
    {
        if (boneCleavePrefab == null || castPoint == null)
        {
            return;
        }

        Vector3 flatDirection = storedCleaveDirection;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude <= 0.001f)
        {
            flatDirection = transform.forward;
        }

        flatDirection.Normalize();

        Quaternion rotation =
            Quaternion.LookRotation(flatDirection, Vector3.up);

        BoneCleave cleave = Instantiate(
            boneCleavePrefab,
            castPoint.position,
            rotation
        );

        cleave.Initialize(
            flatDirection,
            gameObject
        );
    }

    private Vector3 FindGroundPosition(Vector3 targetPosition)
    {
        Vector3 rayOrigin =
            targetPosition + Vector3.up * groundCheckHeight;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return targetPosition;
    }

    /// <summary>
    /// Add this as an Animation Event near the end of the attack.
    /// </summary>
    public void EndAttack()
    {
        if (isDead)
        {
            return;
        }

        isCasting = false;
        pendingSpell = RogueSpell.None;

        attackCooldownTimer = Random.Range(
            minimumAttackCooldown,
            maximumAttackCooldown
        );

        float distanceToPlayer =
            player != null
                ? GetFlatDistance(
                    transform.position,
                    player.position
                )
                : Mathf.Infinity;

        if (player != null &&
            !IsPlayerDead() &&
            distanceToPlayer <= loseTargetRange)
        {
            currentState = RogueState.Engaged;
        }
        else
        {
            ReturnHome();
        }
    }

    // =========================================================
    // ROTATION
    // =========================================================

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
            Quaternion.LookRotation(direction.normalized);

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
        if (currentState == RogueState.ReturningHome)
        {
            return;
        }

        currentState = RogueState.ReturningHome;

        isCasting = false;
        pendingSpell = RogueSpell.None;
        isWaitingAtPatrolPoint = false;
        hasPatrolDestination = false;

        if (animator != null)
        {
            animator.ResetTrigger(AttackHash);
        }

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
        agent.isStopped = false;

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

        currentState = RogueState.Patrolling;
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

            currentState = RogueState.Patrolling;
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
                case RogueState.Patrolling:
                    animationSpeed = 0.5f;
                    break;

                case RogueState.ReturningHome:
                    animationSpeed = 1f;
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
        return playerStats != null && playerStats.IsDead;
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
        isCasting = false;
        pendingSpell = RogueSpell.None;

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

        detectionRange = Mathf.Max(0.5f, detectionRange);

        loseTargetRange = Mathf.Max(
            detectionRange + 0.5f,
            loseTargetRange
        );

        attackRange = Mathf.Clamp(
            attackRange,
            0.1f,
            loseTargetRange
        );

        minimumAttackCooldown = Mathf.Max(
            0.1f,
            minimumAttackCooldown
        );

        maximumAttackCooldown = Mathf.Max(
            minimumAttackCooldown,
            maximumAttackCooldown
        );

        attackRotationSpeed = Mathf.Max(
            0f,
            attackRotationSpeed
        );

        groundCheckHeight = Mathf.Max(
            0f,
            groundCheckHeight
        );

        groundCheckDistance = Mathf.Max(
            0.1f,
            groundCheckDistance
        );

        patrolSpeed = Mathf.Max(0f, patrolSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 centre =
            Application.isPlaying
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