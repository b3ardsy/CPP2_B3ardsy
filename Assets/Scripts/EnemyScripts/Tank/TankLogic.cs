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

    private enum TankAttack
    {
        Spin = 0,
        Slash = 1
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

    [Header("Spin Attack")]
    [SerializeField]
    private int spinDamage = 1;

    [Header("Slash Attack")]
    [SerializeField]
    private int slashDamage = 1;

    [Header("Attack Timing")]
    [Tooltip(
        "Time between complete attacks."
    )]
    [SerializeField]
    private float attackCooldown = 3f;

    [Tooltip(
        "How quickly the Tank turns toward the player while attacking."
    )]
    [SerializeField]
    private float attackRotationSpeed = 25f;

    [Tooltip(
        "Maximum planar NavMesh velocity allowed before a new attack begins. " +
        "This gives the Tank time to plant its feet instead of sliding " +
        "straight from the chase into an attack."
    )]
    [SerializeField]
    private float attackStartVelocityThreshold = 0.15f;

    [Header("Melee Hit Detection")]
    [Tooltip(
        "SphereCollider on the axe used as the melee hit volume. " +
        "The collider is disabled for normal physics and sampled " +
        "when PerformMeleeHit is called by an Animation Event."
    )]
    [SerializeField]
    private SphereCollider axeHitbox;

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
    private TankAttack activeAttack;

    private float attackCooldownTimer;

    private bool isPerformingAttack;
    private int hitsDuringCurrentAttack;
    private bool wasEntangledLastFrame;

    private int movementAnimationRecoveryFrames;

    // =========================================================
    // ANIMATOR PARAMETERS
    // =========================================================

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private static readonly int AttackTypeHash =
        Animator.StringToHash("AttackType");

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
                GetComponentInChildren<SphereCollider>(
                    true
                );
        }

        if (axeHitbox == null)
        {
            Debug.LogError(
                $"{name}: TankLogic could not find the axe SphereCollider.",
                this
            );
        }
        else
        {
            /*
             * The axe collider is only used as a shape reference for
             * Physics.OverlapBox. It should not participate in normal
             * collision/trigger callbacks.
             */
            axeHitbox.enabled =
                false;
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


            UpdateMovementAnimation();

            return;
        }

        if (enemyController.Player == null)
        {
            enemyController.StopAgent();


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

        /*
         * Wait until the NavMeshAgent has actually settled before
         * committing to the attack animation. This prevents the
         * Tank from visibly sliding into the first attack pose.
         */
        Vector3 currentVelocity =
            enemyController.Agent != null
                ? enemyController.Agent.velocity
                : Vector3.zero;

        currentVelocity.y =
            0f;

        if (
            currentVelocity.magnitude >
            attackStartVelocityThreshold
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

        activeAttack =
            UnityEngine.Random.value < 0.5f
                ? TankAttack.Spin
                : TankAttack.Slash;

        isPerformingAttack =
            true;

        hitsDuringCurrentAttack =
            0;

        attackCooldownTimer =
            attackCooldown;

        enemyController.StopAgent();

        enemyController.FacePlayer(
            attackRotationSpeed
        );

        if (animator != null)
        {
            animator.SetInteger(
                AttackTypeHash,
                (int)activeAttack
            );

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
    // MELEE DAMAGE
    // =========================================================

    /*
     * Animation Event used by both attack clips.
     *
     * Place PerformMeleeHit on the impact frame of Spin and Slash.
     * The axe SphereCollider defines the overlap volume, but remains
     * disabled for normal physics.
     */
    public void PerformMeleeHit()
    {
        if (
            enemyController.IsDead ||
            enemyController.IsEntangled ||
            !isPerformingAttack ||
            hitsDuringCurrentAttack >=
                GetMaximumHitsForActiveAttack() ||
            currentState != TankState.Attacking ||
            axeHitbox == null ||
            playerAxeDamageable == null ||
            enemyController.IsPlayerDead
        )
        {
            return;
        }

        Vector3 worldCenter =
            axeHitbox.transform.TransformPoint(
                axeHitbox.center
            );

        Vector3 scale =
            axeHitbox.transform.lossyScale;

        float largestScale =
            Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z)
            );

        float worldRadius =
            axeHitbox.radius *
            largestScale;

        Collider[] hits =
            Physics.OverlapSphere(
                worldCenter,
                worldRadius,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore
            );

        foreach (Collider hit in hits)
        {
            if (!IsTrackedPlayerCollider(hit))
            {
                continue;
            }

            int damage =
                GetDamageForActiveAttack();

            bool isSpinFollowUpHit =
                activeAttack == TankAttack.Spin &&
                hitsDuringCurrentAttack > 0;

            if (
                isSpinFollowUpHit &&
                playerAxeDamageable is
                    Player_DamageController playerDamageController
            )
            {
                playerDamageController.TakeAxeDamage(
                    damage,
                    true
                );
            }
            else
            {
                playerAxeDamageable.TakeAxeDamage(
                    damage
                );
            }

            hitsDuringCurrentAttack++;

            return;
        }
    }

    private int GetDamageForActiveAttack()
    {
        switch (activeAttack)
        {
            case TankAttack.Slash:
                return slashDamage;

            case TankAttack.Spin:
            default:
                return spinDamage;
        }
    }

    private int GetMaximumHitsForActiveAttack()
    {
        switch (activeAttack)
        {
            case TankAttack.Spin:
                return 2;

            case TankAttack.Slash:
            default:
                return 1;
        }
    }

    private bool IsTrackedPlayerCollider(
        Collider other
    )
    {
        if (
            other == null ||
            enemyController.Player == null
        )
        {
            return false;
        }

        Transform playerTransform =
            enemyController.Player;

        return
            other.transform == playerTransform ||
            other.transform.IsChildOf(
                playerTransform
            );
    }

    // =========================================================
    // ANIMATION EVENTS
    // =========================================================

    public void EndAttack()
    {
        isPerformingAttack =
            false;

        hitsDuringCurrentAttack =
            0;

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
         * AttackPlayer waits for the cooldown and only starts
         * another attack while the player remains in AttackRange.
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
            GetFallbackLocomotionBlendValue()
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

        hitsDuringCurrentAttack =
            0;

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackHash
            );
        }
    }

    // =========================================================
    // RESPAWN RESET
    // =========================================================

    public void ResetForRespawn()
    {
        CancelCurrentAttack();

        currentState =
            TankState.Patrolling;

        attackCooldownTimer =
            0f;

        activeAttack =
            TankAttack.Spin;

        hitsDuringCurrentAttack =
            0;

        wasEntangledLastFrame =
            false;

        movementAnimationRecoveryFrames =
            0;

        if (animator != null)
        {
            animator.ResetTrigger(
                AttackHash
            );

            animator.SetInteger(
                AttackTypeHash,
                0
            );

            animator.ResetTrigger(
                EntangleHash
            );

            animator.SetFloat(
                SpeedHash,
                0f
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

        Vector3 movementVelocity =
            enemyController.Agent.velocity;

        movementVelocity.y =
            0f;

        float animationSpeed =
            GetLocomotionBlendValue(
                movementVelocity.magnitude
            );

        /*
         * During the first few frames after Entangle, the NavMeshAgent
         * can report zero velocity while its path wakes back up.
         * Preserve a sensible locomotion value for that brief recovery.
         */
        if (
            movementAnimationRecoveryFrames >
            0
        )
        {
            movementAnimationRecoveryFrames--;

            animationSpeed =
                Mathf.Max(
                    animationSpeed,
                    GetFallbackLocomotionBlendValue()
                );
        }

        animator.SetFloat(
            SpeedHash,
            animationSpeed,
            0.08f,
            Time.deltaTime
        );
    }

    private float GetLocomotionBlendValue(
        float planarSpeed
    )
    {
        if (planarSpeed <= 0.01f)
        {
            return 0f;
        }

        float walkSpeed =
            Mathf.Max(
                0.01f,
                enemyController.PatrolSpeed
            );

        float runSpeed =
            Mathf.Max(
                walkSpeed + 0.01f,
                chaseSpeed
            );

        if (planarSpeed <= walkSpeed)
        {
            return
                Mathf.InverseLerp(
                    0f,
                    walkSpeed,
                    planarSpeed
                ) *
                0.5f;
        }

        return
            Mathf.Lerp(
                0.5f,
                1f,
                Mathf.InverseLerp(
                    walkSpeed,
                    runSpeed,
                    planarSpeed
                )
            );
    }

    private float GetFallbackLocomotionBlendValue()
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

            enemyController.OnRespawned -=
                ResetForRespawn;

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

        spinDamage =
            Mathf.Max(
                1,
                spinDamage
            );

        slashDamage =
            Mathf.Max(
                1,
                slashDamage
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

        attackStartVelocityThreshold =
            Mathf.Max(
                0f,
                attackStartVelocityThreshold
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