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

    private enum RogueAttack
    {
        Skull,
        DeathEvil
    }

    // =========================================================
    // PATROL
    // =========================================================

    [Header("Patrol")]
    [SerializeField] private float patrolRadius = 10f;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private float destinationTolerance = 0.25f;
    [SerializeField] private float navMeshSampleDistance = 3f;
    [SerializeField] private int patrolSearchAttempts = 10;

    // =========================================================
    // DETECTION
    // =========================================================

    [Header("Detection")]
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float loseTargetRange = 17f;

    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float returnSpeed = 3.5f;
    [SerializeField] private float engagedRotationSpeed = 8f;

    // =========================================================
    // SKULL ATTACK
    // =========================================================

    [Header("Skull Attack")]
    [SerializeField]
    private EnemySkullProjectile skullProjectilePrefab;

    [Tooltip(
        "Spawn point positioned near the Rogue's face."
    )]
    [SerializeField]
    private Transform firePoint;

    [SerializeField] private int skullDamage = 1;
    [SerializeField] private float skullSpeed = 10f;

    [Tooltip(
        "Vertical offset used when initially aiming " +
        "toward the player."
    )]
    [SerializeField]
    private float aimHeightOffset = 1f;

    // =========================================================
    // DEATH EVIL ATTACK
    // =========================================================

    [Header("Death Evil Attack")]
    [SerializeField]
    private DeathEvilEffect deathEvilPrefab;

    [SerializeField]
    private int deathEvilDamage = 1;

    [SerializeField]
    private float deathEvilDamageRadius = 2f;

    [Tooltip(
        "Maximum horizontal distance from the Rogue where " +
        "DeathEvil may be placed."
    )]
    [SerializeField]
    private float deathEvilMaximumCastRange = 10f;

    [Tooltip(
        "Height above the intended position used to search " +
        "for the ground."
    )]
    [SerializeField]
    private float deathEvilGroundCheckHeight = 5f;

    [Tooltip(
        "Small vertical adjustment after finding the ground."
    )]
    [SerializeField]
    private float deathEvilGroundOffset = 0.05f;

    [SerializeField]
    private LayerMask groundLayer;

    // =========================================================
    // ATTACK TIMING
    // =========================================================

    [Header("Attack Timing")]
    [SerializeField]
    private float minimumAttackCooldown = 2f;

    [SerializeField]
    private float maximumAttackCooldown = 3f;

    [Tooltip(
        "Time after ReleaseAttack before the attack is " +
        "considered finished."
    )]
    [SerializeField]
    private float attackRecoveryDuration = 0.6f;

    // =========================================================
    // ATTACK ORDER
    // =========================================================

    [Header("Attack Order")]
    [Tooltip(
        "Attack used the first time the Rogue engages."
    )]
    [SerializeField]
    private RogueAttack startingAttack =
        RogueAttack.Skull;

    // =========================================================
    // RUNTIME
    // =========================================================

    private NavMeshAgent agent;
    private PlayerStatsNew playerStats;

    private RogueState currentState;

    private RogueAttack nextAttack;
    private RogueAttack activeAttack;

    private Vector3 homePosition;

    private float patrolWaitTimer;
    private float attackCooldownTimer;
    private float attackRecoveryTimer;

    private bool isWaitingAtPatrolPoint;
    private bool hasPatrolDestination;
    private bool isPerformingAttack;
    private bool attackReleased;

    private bool wasEntangledLastFrame;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int AttackTrigger =
        Animator.StringToHash("Attack");

    // =========================================================
    // INITIALIZATION
    // =========================================================

    protected override void Awake()
    {
        base.Awake();

        agent =
            GetComponent<NavMeshAgent>();

        FindPlayerStats();

        if (agent == null)
        {
            Debug.LogError(
                $"{name}: RogueEnemy requires a NavMeshAgent.",
                this
            );

            enabled = false;
            return;
        }

        if (skullProjectilePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Skull Projectile Prefab is missing.",
                this
            );
        }

        if (firePoint == null)
        {
            Debug.LogWarning(
                $"{name}: Fire Point is missing.",
                this
            );
        }

        if (deathEvilPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: DeathEvil Prefab is missing.",
                this
            );
        }

        if (groundLayer.value == 0)
        {
            Debug.LogWarning(
                $"{name}: Ground Layer is not assigned.",
                this
            );
        }
    }

    private void Start()
    {
        homePosition =
            transform.position;

        currentState =
            RogueState.Patrolling;

        nextAttack =
            startingAttack;

        ResetAttackCooldown();

        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            Debug.LogError(
                $"{name}: RogueEnemy is not positioned " +
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
            Debug.LogWarning(
                $"{name}: PlayerStatsNew could not be found.",
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
            UpdateMovementAnimation();
            return;
        }

        if (player == null)
        {
            StopAgent();
            UpdateMovementAnimation();
            return;
        }

        if (playerStats == null)
        {
            FindPlayerStats();
        }

        // =====================================================
        // ENTANGLE
        // =====================================================

        if (IsEntangled)
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
        // ATTACK RECOVERY
        // =====================================================

        if (
            isPerformingAttack &&
            attackReleased
        )
        {
            attackRecoveryTimer -=
                Time.deltaTime;

            if (
                attackRecoveryTimer <=
                0f
            )
            {
                FinishAttack();
            }
        }

        // =====================================================
        // COOLDOWN
        // =====================================================

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -=
                Time.deltaTime;
        }

        if (IsPlayerDead())
        {
            ReturnHome();

            UpdateMovementAnimation();
            return;
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
            case RogueState.Patrolling:
            case RogueState.ReturningHome:

                if (
                    distanceToPlayer <=
                    detectionRange
                )
                {
                    BeginEngagement();
                }

                break;

            case RogueState.Engaged:

                if (
                    distanceToPlayer >
                    loseTargetRange
                )
                {
                    ReturnHome();
                }

                break;

            case RogueState.Attacking:

                /*
                 * FinishAttack() controls leaving
                 * this state.
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
            case RogueState.Patrolling:

                Patrol();
                break;

            case RogueState.Engaged:

                EngagePlayer(
                    distanceToPlayer
                );

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

    private void Patrol()
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
            IsEntangled ||
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
            $"{name}: Rogue could not find " +
            "a valid patrol destination.",
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
    // ENGAGEMENT
    // =========================================================

    private void BeginEngagement()
    {
        if (IsEntangled)
        {
            return;
        }

        currentState =
            RogueState.Engaged;

        isWaitingAtPatrolPoint =
            false;

        hasPatrolDestination =
            false;

        StopAgent();
    }

    private void EngagePlayer(
        float distanceToPlayer
    )
    {
        if (IsEntangled)
        {
            return;
        }

        StopAgent();

        FacePlayer();

        if (
            distanceToPlayer >
            detectionRange
        )
        {
            return;
        }

        if (
            attackCooldownTimer >
            0f
        )
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
        if (
            IsEntangled ||
            isPerformingAttack ||
            player == null
        )
        {
            return;
        }

        if (!CanUseAttack(nextAttack))
        {
            /*
             * If one attack happens to be missing its
             * prefab/reference, use the other one instead.
             */
            nextAttack =
                GetOppositeAttack(
                    nextAttack
                );

            if (!CanUseAttack(nextAttack))
            {
                Debug.LogWarning(
                    $"{name}: Rogue has no usable attacks.",
                    this
                );

                ResetAttackCooldown();

                return;
            }
        }

        currentState =
            RogueState.Attacking;

        isPerformingAttack =
            true;

        attackReleased =
            false;

        attackRecoveryTimer =
            0f;

        activeAttack =
            nextAttack;

        StopAgent();

        FacePlayer();

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackTrigger
            );

            animator.SetTrigger(
                AttackTrigger
            );
        }
        else
        {
            ReleaseAttack();
        }
    }

    private void AttackPlayer()
    {
        if (IsEntangled)
        {
            return;
        }

        StopAgent();

        FacePlayer();
    }

    // =========================================================
    // ATTACK ANIMATION EVENT
    // =========================================================

    /*
     * This is the ONLY required combat Animation Event.
     *
     * It replaces the old ShootSkull event.
     */
    public void ReleaseAttack()
    {
        if (
            isDead ||
            IsEntangled ||
            !isPerformingAttack ||
            attackReleased
        )
        {
            return;
        }

        bool attackSucceeded =
            false;

        switch (activeAttack)
        {
            case RogueAttack.Skull:

                attackSucceeded =
                    ReleaseSkull();

                break;

            case RogueAttack.DeathEvil:

                attackSucceeded =
                    ReleaseDeathEvil();

                break;
        }

        if (!attackSucceeded)
        {
            Debug.LogWarning(
                $"{name}: {activeAttack} could not be released.",
                this
            );
        }

        /*
         * The attack is considered released even if a
         * prefab/reference error prevented the effect.
         *
         * This prevents the Rogue from becoming stuck.
         */
        attackReleased =
            true;

        attackRecoveryTimer =
            attackRecoveryDuration;
    }

    // =========================================================
    // SKULL
    // =========================================================

    private bool ReleaseSkull()
    {
        if (
            skullProjectilePrefab == null ||
            firePoint == null ||
            player == null
        )
        {
            return false;
        }

        Vector3 targetPosition =
            player.position +
            Vector3.up *
            aimHeightOffset;

        Vector3 direction =
            targetPosition -
            firePoint.position;

        if (
            direction.sqrMagnitude <=
            0.001f
        )
        {
            direction =
                transform.forward;
        }

        direction.Normalize();

        Quaternion spawnRotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );

        EnemySkullProjectile skull =
            Instantiate(
                skullProjectilePrefab,
                firePoint.position,
                spawnRotation
            );

        skull.Initialize(
            gameObject,
            direction,
            skullDamage,
            skullSpeed,
            player
        );

        Debug.Log(
            $"{name}: Skull projectile spawned.",
            skull
        );

        return true;
    }

    // =========================================================
    // DEATH EVIL
    // =========================================================

    private bool ReleaseDeathEvil()
    {
        if (
            deathEvilPrefab == null ||
            player == null
        )
        {
            return false;
        }

        Vector3 targetPosition =
            CalculateDeathEvilTargetPosition();

        Vector3 groundPosition =
            FindDeathEvilGroundPosition(
                targetPosition
            );

        DeathEvilEffect effect =
            Instantiate(
                deathEvilPrefab,
                groundPosition,
                deathEvilPrefab.transform.rotation
            );

        effect.Initialize(
            deathEvilDamage,
            deathEvilDamageRadius
        );

        Debug.Log(
            $"{name}: DeathEvil spawned at " +
            $"{groundPosition}.",
            effect
        );

        return true;
    }

    private Vector3 CalculateDeathEvilTargetPosition()
    {
        Vector3 roguePosition =
            transform.position;

        Vector3 playerPosition =
            player.position;

        /*
         * Only consider horizontal distance.
         */
        Vector3 direction =
            playerPosition -
            roguePosition;

        direction.y =
            0f;

        float distance =
            direction.magnitude;

        if (
            distance <=
            deathEvilMaximumCastRange
        )
        {
            return playerPosition;
        }

        if (
            direction.sqrMagnitude <=
            0.001f
        )
        {
            return roguePosition;
        }

        /*
         * If the player is unusually far away,
         * place the gas at the maximum allowed
         * casting distance instead.
         */
        direction.Normalize();

        return
            roguePosition +
            direction *
            deathEvilMaximumCastRange;
    }

    private Vector3 FindDeathEvilGroundPosition(
        Vector3 targetPosition
    )
    {
        Vector3 rayOrigin =
            targetPosition +
            Vector3.up *
            deathEvilGroundCheckHeight;

        float rayDistance =
            deathEvilGroundCheckHeight *
            2f;

        if (
            Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            return
                hit.point +
                Vector3.up *
                deathEvilGroundOffset;
        }

        return targetPosition;
    }

    // =========================================================
    // ATTACK RECOVERY
    // =========================================================

    private void FinishAttack()
    {
        if (!isPerformingAttack)
        {
            return;
        }

        isPerformingAttack =
            false;

        attackReleased =
            false;

        attackRecoveryTimer =
            0f;

        /*
         * Strict alternation.
         */
        nextAttack =
            GetOppositeAttack(
                activeAttack
            );

        ResetAttackCooldown();

        if (
            isDead ||
            player == null
        )
        {
            return;
        }

        if (IsEntangled)
        {
            StopAgent();
            return;
        }

        float distanceToPlayer =
            GetFlatDistance(
                transform.position,
                player.position
            );

        if (
            distanceToPlayer >
            loseTargetRange ||
            IsPlayerDead()
        )
        {
            ReturnHome();
            return;
        }

        currentState =
            RogueState.Engaged;
    }

    private RogueAttack GetOppositeAttack(
        RogueAttack attack
    )
    {
        return
            attack == RogueAttack.Skull
                ? RogueAttack.DeathEvil
                : RogueAttack.Skull;
    }

    private bool CanUseAttack(
        RogueAttack attack
    )
    {
        switch (attack)
        {
            case RogueAttack.Skull:

                return
                    skullProjectilePrefab != null &&
                    firePoint != null;

            case RogueAttack.DeathEvil:

                return
                    deathEvilPrefab != null;

            default:

                return false;
        }
    }

    private void ResetAttackCooldown()
    {
        attackCooldownTimer =
            Random.Range(
                minimumAttackCooldown,
                maximumAttackCooldown
            );
    }

    // =========================================================
    // ENTANGLE
    // =========================================================

    private void HandleEntangledState()
    {
        StopAgent();

        if (!isPerformingAttack)
        {
            return;
        }

        isPerformingAttack =
            false;

        attackReleased =
            false;

        attackRecoveryTimer =
            0f;

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackTrigger
            );
        }
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

        ResetAttackCooldown();

        float distanceToPlayer =
            GetFlatDistance(
                transform.position,
                player.position
            );

        if (
            !IsPlayerDead() &&
            distanceToPlayer <=
            detectionRange
        )
        {
            BeginEngagement();
            return;
        }

        ReturnHome();
    }

    // =========================================================
    // FACING
    // =========================================================

    private void FacePlayer()
    {
        if (
            player == null ||
            IsEntangled
        )
        {
            return;
        }

        Vector3 directionToPlayer =
            player.position -
            transform.position;

        directionToPlayer.y =
            0f;

        if (
            directionToPlayer.sqrMagnitude <=
            0.001f
        )
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                directionToPlayer.normalized,
                Vector3.up
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                engagedRotationSpeed *
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
            RogueState.ReturningHome
        )
        {
            return;
        }

        currentState =
            RogueState.ReturningHome;

        isPerformingAttack =
            false;

        attackReleased =
            false;

        attackRecoveryTimer =
            0f;

        isWaitingAtPatrolPoint =
            false;

        hasPatrolDestination =
            false;

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackTrigger
            );
        }

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

        StopAgent();

        currentState =
            RogueState.Patrolling;

        isWaitingAtPatrolPoint =
            true;

        hasPatrolDestination =
            false;

        patrolWaitTimer =
            patrolWaitTime;
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
        }
    }

    // =========================================================
    // NAVMESH HELPERS
    // =========================================================

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

        if (
            agent.remainingDistance >
            agent.stoppingDistance +
            destinationTolerance
        )
        {
            return false;
        }

        return
            !agent.hasPath ||
            agent.velocity.sqrMagnitude <=
            0.01f;
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

    private float GetFlatDistance(
        Vector3 positionA,
        Vector3 positionB
    )
    {
        positionA.y =
            0f;

        positionB.y =
            0f;

        return
            Vector3.Distance(
                positionA,
                positionB
            );
    }

    private bool IsPlayerDead()
    {
        return
            playerStats != null &&
            playerStats.IsDead;
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private void UpdateMovementAnimation()
    {
        if (
            animator == null ||
            agent == null
        )
        {
            return;
        }

        float animationSpeed =
            0f;

        if (
            !IsEntangled &&
            agent.enabled &&
            agent.isOnNavMesh &&
            !agent.isStopped &&
            agent.velocity.sqrMagnitude >
            0.01f
        )
        {
            switch (currentState)
            {
                case RogueState.Patrolling:

                    animationSpeed =
                        0.5f;

                    break;

                case RogueState.ReturningHome:

                    animationSpeed =
                        1f;

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
    // DAMAGE
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

            attackReleased =
                false;

            attackRecoveryTimer =
                0f;

            if (animator != null)
            {
                animator.ResetTrigger(
                    AttackTrigger
                );
            }

            ResetAttackCooldown();

            if (!IsEntangled)
            {
                currentState =
                    RogueState.Engaged;
            }
        }

        base.TakeDamage(
            damage
        );
    }

    // =========================================================
    // DEATH
    // =========================================================

    protected override void Die()
    {
        isPerformingAttack =
            false;

        attackReleased =
            false;

        attackRecoveryTimer =
            0f;

        wasEntangledLastFrame =
            false;

        StopAgent();

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackTrigger
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
                0f,
                patrolRadius
            );

        patrolWaitTime =
            Mathf.Max(
                0f,
                patrolWaitTime
            );

        destinationTolerance =
            Mathf.Max(
                0f,
                destinationTolerance
            );

        navMeshSampleDistance =
            Mathf.Max(
                0.1f,
                navMeshSampleDistance
            );

        patrolSearchAttempts =
            Mathf.Max(
                1,
                patrolSearchAttempts
            );

        detectionRange =
            Mathf.Max(
                0f,
                detectionRange
            );

        loseTargetRange =
            Mathf.Max(
                detectionRange,
                loseTargetRange
            );

        patrolSpeed =
            Mathf.Max(
                0f,
                patrolSpeed
            );

        returnSpeed =
            Mathf.Max(
                0f,
                returnSpeed
            );

        engagedRotationSpeed =
            Mathf.Max(
                0f,
                engagedRotationSpeed
            );

        skullDamage =
            Mathf.Max(
                1,
                skullDamage
            );

        skullSpeed =
            Mathf.Max(
                0f,
                skullSpeed
            );

        aimHeightOffset =
            Mathf.Max(
                0f,
                aimHeightOffset
            );

        deathEvilDamage =
            Mathf.Max(
                1,
                deathEvilDamage
            );

        deathEvilDamageRadius =
            Mathf.Max(
                0.1f,
                deathEvilDamageRadius
            );

        deathEvilMaximumCastRange =
            Mathf.Max(
                0.1f,
                deathEvilMaximumCastRange
            );

        deathEvilGroundCheckHeight =
            Mathf.Max(
                0.1f,
                deathEvilGroundCheckHeight
            );

        deathEvilGroundOffset =
            Mathf.Max(
                0f,
                deathEvilGroundOffset
            );

        minimumAttackCooldown =
            Mathf.Max(
                0.01f,
                minimumAttackCooldown
            );

        maximumAttackCooldown =
            Mathf.Max(
                minimumAttackCooldown,
                maximumAttackCooldown
            );

        attackRecoveryDuration =
            Mathf.Max(
                0.05f,
                attackRecoveryDuration
            );
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            Application.isPlaying
                ? homePosition
                : transform.position,
            patrolRadius
        );

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );

        Gizmos.DrawWireSphere(
            transform.position,
            loseTargetRange
        );

        Gizmos.DrawWireSphere(
            transform.position,
            deathEvilMaximumCastRange
        );
    }
}