using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BooEnemy : Enemy
{
    private enum BooState
    {
        Patrolling,
        Engaged,
        Attacking,
        ReturningHome
    }

    [Header("Patrol")]
    [SerializeField] private float patrolRadius = 10f;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private float destinationTolerance = 0.25f;
    [SerializeField] private float navMeshSampleDistance = 3f;
    [SerializeField] private int patrolSearchAttempts = 10;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float loseTargetRange = 25f;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float returnSpeed = 3f;
    [SerializeField] private float engagedRotationSpeed = 8f;

    // =========================================================
    // BLACK HOLE ATTACK
    // =========================================================

    [Header("Black Hole Attack")]
    [SerializeField]
    private EnemyBlackHoleProjectile blackHoleProjectilePrefab;

    [SerializeField]
    private Transform firePoint;

    [SerializeField]
    private int blackHoleDamage = 1;

    [SerializeField]
    private float blackHoleSpeed = 12f;

    [Tooltip(
        "Vertical offset used when aiming toward the player."
    )]
    [SerializeField]
    private float aimHeightOffset = 1f;

    [Tooltip(
        "Number of Black Holes fired in each volley."
    )]
    [SerializeField]
    private int projectileCount = 3;

    [Tooltip(
        "Angle between each projectile in the volley."
    )]
    [SerializeField]
    private float projectileSpreadAngle = 12f;

    [Header("Black Hole Homing")]
    [Range(0f, 1f)]
    [Tooltip(
        "Chance that one randomly selected Black Hole " +
        "will home toward the player."
    )]
    [SerializeField]
    private float homingChance = 0.5f;

    [Header("Attack Timing")]
    [SerializeField]
    private float minimumAttackCooldown = 1.75f;

    [SerializeField]
    private float maximumAttackCooldown = 2.75f;

    [Header("Entangle Recovery")]
    [Tooltip(
        "Animator state used as the safe re-entry point after Entangle ends."
    )]
    [SerializeField]
    private string entangleRecoveryStateName = "Idle_B";

    [Tooltip(
        "How quickly the Animator blends out of Entangle and back to locomotion."
    )]
    [SerializeField]
    private float entangleRecoveryTransitionDuration = 0.05f;

    private NavMeshAgent agent;

    private BooState currentState;

    private Vector3 homePosition;

    private float patrolWaitTimer;
    private float attackCooldownTimer;

    private bool isWaitingAtPatrolPoint;
    private bool hasPatrolDestination;
    private bool isPerformingAttack;

    /*
     * Tracks whether Boo was Entangled on the previous frame.
     * This lets the AI rebuild its state as soon as Entangle ends.
     */
    private bool wasEntangledLastFrame;

    private static readonly int IsMovingBool =
        Animator.StringToHash("IsMoving");

    private static readonly int AttackTrigger =
        Animator.StringToHash("Attack");

    protected override void Awake()
    {
        base.Awake();

        agent =
            GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            Debug.LogError(
                $"{name}: BooEnemy requires a NavMeshAgent.",
                this
            );

            enabled = false;
        }
    }

    private void Start()
    {
        homePosition =
            transform.position;

        currentState =
            BooState.Patrolling;

        ResetAttackCooldown();

        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            Debug.LogError(
                $"{name}: BooEnemy is not positioned " +
                "on a baked NavMesh.",
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
            UpdateMovementAnimation();
            return;
        }

        if (player == null)
        {
            StopAgent();
            UpdateMovementAnimation();
            return;
        }

        // =====================================================
        // ENTANGLE
        // =====================================================

        /*
         * Entangle completely suspends Boo AI.
         *
         * If Entangle interrupts an attack animation, the normal
         * EndAttack Animation Event may never fire. Cancel the
         * attack here so isPerformingAttack cannot remain stuck true.
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
         * Entangle just ended. Rebuild Boo's state using the
         * player's current position and restart the attack cooldown.
         */
        if (wasEntangledLastFrame)
        {
            wasEntangledLastFrame =
                false;

            ResumeAfterEntangle();
        }

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -=
                Time.deltaTime;
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
        StopAgent();

        /*
         * Entangle may interrupt the attack animation before its
         * EndAttack Animation Event fires. Clear the runtime attack
         * state explicitly so Boo can attack again afterward.
         */
        if (isPerformingAttack)
        {
            isPerformingAttack =
                false;

            if (animator != null)
            {
                animator.ResetTrigger(
                    AttackTrigger
                );
            }
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

        /*
         * Entangle can interrupt the current attack state before
         * its EndAttack Animation Event fires.
         *
         * Force the Animator back to the safe locomotion state so
         * future Attack triggers can be received normally.
         */
        RecoverAnimatorAfterEntangle();

        /*
         * Give Boo a normal cooldown after being released rather
         * than immediately firing on the very first free frame.
         */
        ResetAttackCooldown();

        float distanceToPlayer =
            GetFlatDistance(
                transform.position,
                player.position
            );

        if (
            distanceToPlayer <=
            detectionRange
        )
        {
            BeginEngagement();
            return;
        }

        ReturnHome();
    }

    private void RecoverAnimatorAfterEntangle()
    {
        if (animator == null)
        {
            return;
        }

        animator.ResetTrigger(
            EntangleTrigger
        );

        animator.ResetTrigger(
            AttackTrigger
        );

        animator.SetBool(
            IsMovingBool,
            false
        );

        int recoveryStateHash =
            Animator.StringToHash(
                entangleRecoveryStateName
            );

        if (
            !animator.HasState(
                0,
                recoveryStateHash
            )
        )
        {
            Debug.LogWarning(
                $"{name}: Animator does not contain Entangle " +
                $"recovery state '{entangleRecoveryStateName}'.",
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
            case BooState.Patrolling:
            case BooState.ReturningHome:

                if (
                    distanceToPlayer <=
                    detectionRange
                )
                {
                    BeginEngagement();
                }

                break;

            case BooState.Engaged:

                if (
                    distanceToPlayer >
                    loseTargetRange
                )
                {
                    ReturnHome();
                }

                break;

            case BooState.Attacking:
                break;
        }
    }

    private void RunCurrentState(
        float distanceToPlayer
    )
    {
        switch (currentState)
        {
            case BooState.Patrolling:
                Patrol();
                break;

            case BooState.Engaged:
                EngagePlayer(
                    distanceToPlayer
                );
                break;

            case BooState.Attacking:
                AttackPlayer();
                break;

            case BooState.ReturningHome:
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
            $"{name}: BooEnemy could not find " +
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
            BooState.Engaged;

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
            player == null ||
            blackHoleProjectilePrefab == null ||
            firePoint == null
        )
        {
            return;
        }

        currentState =
            BooState.Attacking;

        isPerformingAttack =
            true;

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
            ShootBlackHole();
            EndAttack();
        }
    }

    private void AttackPlayer()
    {
        StopAgent();

        FacePlayer();
    }

    /*
     * Animation Event.
     *
     * Fires the complete Black Hole volley.
     */
    public void ShootBlackHole()
    {
        Debug.Log(
            $"{name}: ShootBlackHole fired | " +
            $"Dead={isDead} | " +
            $"Attacking={isPerformingAttack} | " +
            $"Player={(player != null ? player.name : "NULL")} | " +
            $"Prefab={(blackHoleProjectilePrefab != null ? blackHoleProjectilePrefab.name : "NULL")} | " +
            $"FirePoint={(firePoint != null ? firePoint.name : "NULL")} | " +
            $"Count={projectileCount}",
            this
        );

        if (
            isDead ||
            !isPerformingAttack ||
            player == null ||
            blackHoleProjectilePrefab == null ||
            firePoint == null
        )
        {
            Debug.LogError(
                $"{name}: Black Hole volley aborted.",
                this
            );

            return;
        }

        Vector3 targetPosition =
            player.position +
            Vector3.up *
            aimHeightOffset;

        Vector3 baseDirection =
            targetPosition -
            firePoint.position;

        if (
            baseDirection.sqrMagnitude <=
            0.001f
        )
        {
            baseDirection =
                transform.forward;
        }

        baseDirection.Normalize();

        /*
         * Roll once per volley.
         *
         * If successful, exactly one randomly selected
         * projectile will home toward the player.
         */
        bool volleyHasHomingProjectile =
            Random.value <=
            homingChance;

        int homingProjectileIndex =
            -1;

        if (volleyHasHomingProjectile)
        {
            homingProjectileIndex =
                Random.Range(
                    0,
                    projectileCount
                );
        }

        for (
            int projectileIndex = 0;
            projectileIndex < projectileCount;
            projectileIndex++
        )
        {
            float spreadOffset =
                CalculateSpreadOffset(
                    projectileIndex
                );

            Quaternion spreadRotation =
                Quaternion.AngleAxis(
                    spreadOffset,
                    Vector3.up
                );

            Vector3 projectileDirection =
                spreadRotation *
                baseDirection;

            if (
                projectileDirection.sqrMagnitude <=
                0.001f
            )
            {
                projectileDirection =
                    transform.forward;
            }

            projectileDirection.Normalize();

            bool shouldHome =
                projectileIndex ==
                homingProjectileIndex;

            Quaternion directionRotation =
                Quaternion.LookRotation(
                    projectileDirection,
                    Vector3.up
                );

            /*
             * Instantiate directly inside the volley loop.
             */
            EnemyBlackHoleProjectile projectile =
                Instantiate(
                    blackHoleProjectilePrefab,
                    firePoint.position,
                    directionRotation
                );

            Debug.Log(
                $"{name}: Spawned Black Hole " +
                $"{projectileIndex + 1}/{projectileCount}. " +
                $"Homing={shouldHome}",
                projectile
            );

            projectile.Initialize(
                gameObject,
                projectileDirection,
                blackHoleDamage,
                blackHoleSpeed,
                player,
                shouldHome
            );
        }
    }

    private float CalculateSpreadOffset(
        int projectileIndex
    )
    {
        /*
         * With three projectiles and 12 degrees:
         *
         * Projectile 1 = -12
         * Projectile 2 =   0
         * Projectile 3 = +12
         */
        float centreIndex =
            (projectileCount - 1) *
            0.5f;

        return
            (projectileIndex - centreIndex) *
            projectileSpreadAngle;
    }

    /*
     * Animation Event near the end of
     * the attack animation.
     */
    public void EndAttack()
    {
        if (!isPerformingAttack)
        {
            return;
        }

        isPerformingAttack =
            false;

        ResetAttackCooldown();

        if (
            isDead ||
            player == null
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
            return;
        }

        currentState =
            BooState.Engaged;
    }

    private void ResetAttackCooldown()
    {
        attackCooldownTimer =
            Random.Range(
                minimumAttackCooldown,
                maximumAttackCooldown
            );
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
        if (
            currentState ==
            BooState.ReturningHome
        )
        {
            return;
        }

        currentState =
            BooState.ReturningHome;

        isPerformingAttack =
            false;

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
            BooState.Patrolling;

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

        return Vector3.Distance(
            positionA,
            positionB
        );
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private void UpdateMovementAnimation()
    {
        if (animator == null)
        {
            return;
        }

        bool isMoving =
            agent != null &&
            agent.isOnNavMesh &&
            !agent.isStopped &&
            agent.velocity.sqrMagnitude >
            0.01f;

        animator.SetBool(
            IsMovingBool,
            isMoving
        );
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void OnDisable()
    {
        StopAgent();

        isPerformingAttack =
            false;

        if (animator != null)
        {
            animator.SetBool(
                IsMovingBool,
                false
            );

            animator.ResetTrigger(
                AttackTrigger
            );
        }
    }

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

        blackHoleDamage =
            Mathf.Max(
                1,
                blackHoleDamage
            );

        blackHoleSpeed =
            Mathf.Max(
                0f,
                blackHoleSpeed
            );

        aimHeightOffset =
            Mathf.Max(
                0f,
                aimHeightOffset
            );

        projectileCount =
            Mathf.Max(
                1,
                projectileCount
            );

        projectileSpreadAngle =
            Mathf.Max(
                0f,
                projectileSpreadAngle
            );

        homingChance =
            Mathf.Clamp01(
                homingChance
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

        entangleRecoveryTransitionDuration =
            Mathf.Max(
                0f,
                entangleRecoveryTransitionDuration
            );
    }

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
    }
}