using UnityEngine;

[RequireComponent(typeof(EnemyController))]
public class MageLogic : MonoBehaviour
{
    private enum MageState
    {
        Patrolling,
        Engaged,
        Attacking,
        ReturningHome
    }

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

    // =========================================================
    // HOMING
    // =========================================================

    [Header("Black Hole Homing")]
    [Range(0f, 1f)]
    [Tooltip(
        "Chance that one randomly selected Black Hole " +
        "will home toward the player."
    )]
    [SerializeField]
    private float homingChance = 0.5f;

    // =========================================================
    // ATTACK TIMING
    // =========================================================

    [Header("Attack Timing")]
    [SerializeField]
    private float minimumAttackCooldown = 1.75f;

    [SerializeField]
    private float maximumAttackCooldown = 2.75f;

    // =========================================================
    // ENTANGLE RECOVERY
    // =========================================================

    [Header("Entangle Recovery")]
    [Tooltip(
        "Animator state used as the safe re-entry point " +
        "after Entangle ends."
    )]
    [SerializeField]
    private string entangleRecoveryStateName = "Idle_B";

    [Tooltip(
        "How quickly the Animator blends out of Entangle " +
        "and back to locomotion."
    )]
    [SerializeField]
    private float entangleRecoveryTransitionDuration = 0.05f;

    // =========================================================
    // REFERENCES
    // =========================================================

    private EnemyController enemyController;
    private Animator animator;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private MageState currentState;

    private float attackCooldownTimer;

    private bool isPerformingAttack;
    private bool wasEntangledLastFrame;

    // =========================================================
    // ANIMATOR PARAMETERS
    // =========================================================

    private static readonly int SpeedFloat =
        Animator.StringToHash("Speed");

    private static readonly int AttackTrigger =
        Animator.StringToHash("Attack");

    private static readonly int EntangleTrigger =
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
                $"{name}: MageLogic requires an EnemyController.",
                this
            );

            enabled = false;
            return;
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

        enemyController.OnRespawned -=
            ResetForRespawn;

        enemyController.OnRespawned +=
            ResetForRespawn;
    }

    private void Start()
    {
        /*
         * Fetch the Animator in Start rather than Awake.
         *
         * EnemyController and MageLogic are separate MonoBehaviours,
         * so Unity does not guarantee which Awake runs first.
         * Waiting until Start guarantees EnemyController.Awake()
         * has already assigned its Animator reference.
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
                $"{name}: MageLogic could not find an Animator.",
                this
            );

            enabled = false;
            return;
        }

        currentState =
            MageState.Patrolling;

        ResetAttackCooldown();

        if (!enemyController.IsOnNavMesh)
        {
            Debug.LogError(
                $"{name}: MageLogic cannot start because " +
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

            UpdateMovementAnimation();

            return;
        }

        if (enemyController.Player == null)
        {
            enemyController.StopAgent();

            UpdateMovementAnimation();

            return;
        }

        // =====================================================
        // ENTANGLE
        // =====================================================

        /*
         * EnemyController owns the actual Entangle state.
         *
         * MageLogic only needs to respond to that state
         * so an interrupted attack cannot become stuck.
         */
        if (enemyController.IsEntangled)
        {
            HandleEntangledState();

            wasEntangledLastFrame =
                true;

            UpdateMovementAnimation();

            return;
        }

        /*
         * Entangle ended this frame.
         *
         * Rebuild the Mage's attack state and give it
         * a normal cooldown before attacking again.
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
    // ENTANGLE
    // =========================================================

    private void HandleEntangledState()
    {
        enemyController.StopAgent();

        /*
         * Entangle may interrupt the attack animation before
         * EndAttack fires.
         *
         * Clear the attack state manually so the Mage cannot
         * remain permanently stuck in Attacking.
         */
        if (!isPerformingAttack)
        {
            return;
        }

        isPerformingAttack =
            false;

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
            enemyController.IsDead ||
            enemyController.Player == null
        )
        {
            return;
        }

        RecoverAnimatorAfterEntangle();

        ResetAttackCooldown();

        if (enemyController.IsPlayerDetected())
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

        animator.SetFloat(
            SpeedFloat,
            0f
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
            case MageState.Patrolling:
            case MageState.ReturningHome:

                if (enemyController.IsPlayerDetected())
                {
                    BeginEngagement();
                }

                break;

            case MageState.Engaged:

                if (enemyController.HasLostPlayer())
                {
                    ReturnHome();
                }

                break;

            case MageState.Attacking:
                break;
        }
    }

    private void RunCurrentState(
        float distanceToPlayer
    )
    {
        switch (currentState)
        {
            case MageState.Patrolling:

                enemyController.UpdatePatrol();

                break;

            case MageState.Engaged:

                EngagePlayer(
                    distanceToPlayer
                );

                break;

            case MageState.Attacking:

                AttackPlayer();

                break;

            case MageState.ReturningHome:

                ReturnToPatrolZone();

                break;
        }
    }

    // =========================================================
    // ENGAGEMENT
    // =========================================================

    private void BeginEngagement()
    {
        if (enemyController.IsEntangled)
        {
            return;
        }

        currentState =
            MageState.Engaged;

        enemyController.ClearPatrolState();

        enemyController.StopAgent();
    }

    private void EngagePlayer(
        float distanceToPlayer
    )
    {
        enemyController.StopAgent();

        enemyController.FacePlayer();

        /*
         * Match the old Boo behaviour:
         * the Mage only attacks while the player remains
         * inside its detection range.
         */
        if (
            distanceToPlayer >
            enemyController.DetectionRange
        )
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
        if (
            enemyController.IsEntangled ||
            enemyController.IsDead ||
            isPerformingAttack ||
            enemyController.Player == null ||
            blackHoleProjectilePrefab == null ||
            firePoint == null
        )
        {
            return;
        }

        currentState =
            MageState.Attacking;

        isPerformingAttack =
            true;

        enemyController.StopAgent();

        enemyController.FacePlayer();

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
        enemyController.StopAgent();

        enemyController.FacePlayer();
    }

    // =========================================================
    // BLACK HOLE VOLLEY
    // =========================================================

    /*
     * Animation Event.
     *
     * Fires the complete Black Hole volley.
     */
    public void ShootBlackHole()
    {
        Debug.Log(
            $"{name}: ShootBlackHole fired | " +
            $"Dead={enemyController.IsDead} | " +
            $"Attacking={isPerformingAttack} | " +
            $"Player={(enemyController.Player != null ? enemyController.Player.name : "NULL")} | " +
            $"Prefab={(blackHoleProjectilePrefab != null ? blackHoleProjectilePrefab.name : "NULL")} | " +
            $"FirePoint={(firePoint != null ? firePoint.name : "NULL")} | " +
            $"Count={projectileCount}",
            this
        );

        if (
            enemyController.IsDead ||
            !isPerformingAttack ||
            enemyController.Player == null ||
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
            enemyController.Player.position +
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
            UnityEngine.Random.value <=
            homingChance;

        int homingProjectileIndex =
            -1;

        if (volleyHasHomingProjectile)
        {
            homingProjectileIndex =
                UnityEngine.Random.Range(
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
                enemyController.Player,
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
            enemyController.IsDead ||
            enemyController.Player == null
        )
        {
            return;
        }

        if (enemyController.HasLostPlayer())
        {
            ReturnHome();

            return;
        }

        currentState =
            MageState.Engaged;
    }

    private void ResetAttackCooldown()
    {
        attackCooldownTimer =
            UnityEngine.Random.Range(
                minimumAttackCooldown,
                maximumAttackCooldown
            );
    }

    // =========================================================
    // RETURN HOME
    // =========================================================

    private void ReturnHome()
    {
        if (
            currentState ==
            MageState.ReturningHome
        )
        {
            return;
        }

        currentState =
            MageState.ReturningHome;

        isPerformingAttack =
            false;

        enemyController.ClearPatrolState();

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackTrigger
            );
        }

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
            MageState.Patrolling;

        /*
         * Preserve the old Boo behaviour:
         * reaching home begins with a patrol wait
         * rather than immediately choosing a new point.
         */
        enemyController.BeginWaitingAtPatrolPoint();
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

        float normalizedSpeed =
            0f;

        if (
            enemyController.Agent != null &&
            enemyController.IsOnNavMesh &&
            !enemyController.Agent.isStopped
        )
        {
            normalizedSpeed =
                Mathf.Clamp01(
                    enemyController.Agent.velocity.magnitude /
                    Mathf.Max(
                        enemyController.PatrolSpeed,
                        0.01f
                    )
                );
        }

        animator.SetFloat(
            SpeedFloat,
            normalizedSpeed,
            0.1f,
            Time.deltaTime
        );
    }

    // =========================================================
    // RESPAWN RESET
    // =========================================================

    public void ResetForRespawn()
    {
        currentState =
            MageState.Patrolling;

        isPerformingAttack =
            false;

        wasEntangledLastFrame =
            false;

        ResetAttackCooldown();

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackTrigger
            );

            animator.ResetTrigger(
                EntangleTrigger
            );

            animator.SetFloat(
                SpeedFloat,
                0f
            );
        }
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void OnDisable()
    {
        if (enemyController != null)
        {
            enemyController.OnRespawned -=
                ResetForRespawn;

            enemyController.StopAgent();
        }

        isPerformingAttack =
            false;

        if (animator != null)
        {
            animator.SetFloat(
                SpeedFloat,
                0f
            );

            animator.ResetTrigger(
                AttackTrigger
            );
        }
    }

    private void OnValidate()
    {
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
}