using UnityEngine;

[RequireComponent(typeof(EnemyController))]
public class RogueLogic : MonoBehaviour
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
    // SKULL ATTACK
    // =========================================================

    [Header("Skull Attack")]
    [SerializeField]
    private EnemySkullProjectile skullProjectilePrefab;

    [SerializeField]
    private Transform firePoint;

    [SerializeField]
    private int skullDamage = 1;

    [SerializeField]
    private float skullSpeed = 10f;

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

    [SerializeField]
    private float deathEvilGroundCheckHeight = 5f;

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

    [SerializeField]
    private float attackRecoveryDuration = 0.6f;

    // =========================================================
    // ENTANGLE RECOVERY
    // =========================================================

    [Header("Entangle Recovery")]
    [SerializeField]
    private string entangleRecoveryStateName = "Idle";

    [SerializeField]
    private float entangleRecoveryTransitionDuration = 0.05f;

    // =========================================================
    // REFERENCES
    // =========================================================

    private EnemyController enemyController;
    private Animator animator;

    // =========================================================
    // RUNTIME
    // =========================================================

    private RogueState currentState;

    private RogueAttack activeAttack;

    private float attackCooldownTimer;
    private float attackRecoveryTimer;

    private bool isPerformingAttack;
    private bool attackReleased;
    private bool wasEntangledLastFrame;

    // =========================================================
    // ANIMATOR PARAMETERS
    // =========================================================

    private static readonly int SpeedHash =
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
                $"{name}: RogueLogic requires an EnemyController.",
                this
            );

            enabled = false;
            return;
        }

        /*
         * Do NOT read EnemyController.Animator here.
         *
         * EnemyController and RogueLogic are separate MonoBehaviours,
         * so Unity does not guarantee which Awake() runs first.
         * EnemyController may not have initialized its Animator yet.
         */
        ValidateReferences();
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

        /*
         * Prevent duplicate subscriptions if the component
         * is disabled and enabled again.
         */
        enemyController.OnDamaged -=
            HandleDamaged;

        enemyController.OnDamaged +=
            HandleDamaged;
    }

    private void Start()
    {
        /*
         * All Awake() methods have completed before Start() runs,
         * so EnemyController has now initialized its shared Animator.
         */
        animator =
            enemyController.Animator;

        if (animator == null)
        {
            /*
             * Safety fallback in case the shared controller could not
             * resolve the Animator for some reason.
             */
            animator =
                GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError(
                $"{name}: RogueLogic could not find an Animator.",
                this
            );

            enabled = false;
            return;
        }

        currentState =
            RogueState.Patrolling;

        ResetAttackCooldown();

        if (!enemyController.IsOnNavMesh)
        {
            Debug.LogError(
                $"{name}: RogueLogic cannot start because " +
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

                if (enemyController.IsPlayerDetected())
                {
                    BeginEngagement();
                }

                break;

            case RogueState.Engaged:

                if (enemyController.HasLostPlayer())
                {
                    ReturnHome();
                }

                break;

            case RogueState.Attacking:
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

                enemyController.UpdatePatrol();

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
    // ENGAGEMENT
    // =========================================================

    private void BeginEngagement()
    {
        if (enemyController.IsEntangled)
        {
            return;
        }

        currentState =
            RogueState.Engaged;

        enemyController.ClearPatrolState();

        enemyController.StopAgent();
    }

    private void EngagePlayer(
        float distanceToPlayer
    )
    {
        enemyController.StopAgent();

        enemyController.FacePlayer();

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
            enemyController.Player == null
        )
        {
            return;
        }

        if (
            !TryChooseRandomAttack(
                out RogueAttack chosenAttack
            )
        )
        {
            Debug.LogWarning(
                $"{name}: Rogue has no usable attacks.",
                this
            );

            ResetAttackCooldown();

            return;
        }

        activeAttack =
            chosenAttack;

        currentState =
            RogueState.Attacking;

        isPerformingAttack =
            true;

        attackReleased =
            false;

        attackRecoveryTimer =
            0f;

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
            ReleaseAttack();
        }
    }

    private void AttackPlayer()
    {
        enemyController.StopAgent();

        enemyController.FacePlayer();
    }

    // =========================================================
    // RANDOM ATTACK
    // =========================================================

    private bool TryChooseRandomAttack(
        out RogueAttack chosenAttack
    )
    {
        bool canUseSkull =
            skullProjectilePrefab != null &&
            firePoint != null;

        bool canUseDeathEvil =
            deathEvilPrefab != null;

        if (
            canUseSkull &&
            canUseDeathEvil
        )
        {
            chosenAttack =
                UnityEngine.Random.value <
                0.5f
                    ? RogueAttack.Skull
                    : RogueAttack.DeathEvil;

            return true;
        }

        if (canUseSkull)
        {
            chosenAttack =
                RogueAttack.Skull;

            return true;
        }

        if (canUseDeathEvil)
        {
            chosenAttack =
                RogueAttack.DeathEvil;

            return true;
        }

        chosenAttack =
            default;

        return false;
    }

    // =========================================================
    // ATTACK ANIMATION EVENT
    // =========================================================

    public void ReleaseAttack()
    {
        if (
            enemyController.IsDead ||
            enemyController.IsEntangled ||
            !isPerformingAttack ||
            attackReleased
        )
        {
            return;
        }

        bool attackSucceeded;

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

            default:

                attackSucceeded =
                    false;

                break;
        }

        if (!attackSucceeded)
        {
            Debug.LogWarning(
                $"{name}: {activeAttack} could not be released.",
                this
            );
        }

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
            enemyController.Player == null
        )
        {
            return false;
        }

        Vector3 targetPosition =
            enemyController.Player.position +
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
            enemyController.Player
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
            enemyController.Player == null
        )
        {
            return false;
        }

        /*
         * Unlike the old implementation, DeathEvil always
         * targets the player's CURRENT horizontal position.
         *
         * We then raycast straight down from that point so
         * the effect appears centered beneath the player.
         */
        Vector3 playerPosition =
            enemyController.Player.position;

        Vector3 groundPosition =
            GetGroundPositionUnderPlayer(
                playerPosition
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

        return true;
    }

    private Vector3 GetGroundPositionUnderPlayer(
        Vector3 playerPosition
    )
    {
        Vector3 rayOrigin =
            playerPosition +
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

        /*
         * Fallback:
         * keep the player's X/Z even if no ground is found.
         */
        return
            new Vector3(
                playerPosition.x,
                playerPosition.y +
                deathEvilGroundOffset,
                playerPosition.z
            );
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
            RogueState.Engaged;
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
    // ENTANGLE
    // =========================================================

    private void HandleEntangledState()
    {
        enemyController.StopAgent();

        if (!isPerformingAttack)
        {
            return;
        }

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
            SpeedHash,
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
         * Preserve the behaviour from the old RogueEnemy:
         * getting hit during an attack interrupts that attack.
         *
         * This prevents the Rogue from being left permanently
         * in Attacking if the Hit animation interrupts the
         * attack animation before ReleaseAttack can fire.
         */
        CancelCurrentAttack();

        ResetAttackCooldown();

        if (!enemyController.IsEntangled)
        {
            currentState =
                RogueState.Engaged;
        }
    }

    // =========================================================
    // RETURN HOME
    // =========================================================

    private void ReturnHome()
    {
        if (
            currentState ==
            RogueState.ReturningHome
        )
        {
            return;
        }

        currentState =
            RogueState.ReturningHome;

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
            RogueState.Patrolling;

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

        float animationSpeed =
            0f;

        if (
            !enemyController.IsDead &&
            !enemyController.IsEntangled &&
            enemyController.IsOnNavMesh &&
            !enemyController.Agent.isStopped &&
            enemyController.Agent.velocity.sqrMagnitude >
            0.01f
        )
        {
            /*
             * Rogue's Animator already uses a Speed float.
             *
             * Patrol = walk
             * Return home = run
             */
            animationSpeed =
                currentState ==
                RogueState.ReturningHome
                    ? 1f
                    : 0.5f;
        }

        animator.SetFloat(
            SpeedHash,
            animationSpeed,
            0.1f,
            Time.deltaTime
        );
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void CancelCurrentAttack()
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
    }

    private void OnDisable()
    {
        if (enemyController != null)
        {
            enemyController.OnDamaged -=
                HandleDamaged;
        }

        CancelCurrentAttack();

        if (enemyController != null)
        {
            enemyController.StopAgent();
        }

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

    private void ValidateReferences()
    {
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

    private void OnValidate()
    {
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

        entangleRecoveryTransitionDuration =
            Mathf.Max(
                0f,
                entangleRecoveryTransitionDuration
            );
    }
}