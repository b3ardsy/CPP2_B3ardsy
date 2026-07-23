using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BooEnemy : Enemy
{
    [Header("Boo Movement")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stoppingDistance = 1.5f;

    [Header("Animation")]
    [SerializeField] private float sneakAnimationSpeed = 1f;

    [Header("Player Facing Detection")]
    [Range(-1f, 1f)]
    [SerializeField] private float facingThreshold = 0.5f;

    [Header("Fireball Attack")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Transform firePoint;

    [Tooltip("Minimum delay between fireball attacks.")]
    [SerializeField] private float minimumAttackCooldown = 1.75f;

    [Tooltip("Maximum delay between fireball attacks.")]
    [SerializeField] private float maximumAttackCooldown = 2.75f;

    [Tooltip(
        "Minimum delay before attacking when the player first looks " +
        "at this enemy."
    )]
    [SerializeField] private float minimumReactionDelay = 0.1f;

    [Tooltip(
        "Maximum delay before attacking when the player first looks " +
        "at this enemy."
    )]
    [SerializeField] private float maximumReactionDelay = 0.35f;

    [SerializeField] private float attackRange = 10f;
    [SerializeField] private float aimHeightOffset = 1f;
    [SerializeField] private bool rotateWhileAttacking = true;

    private Rigidbody rb;

    private float nextAttackTime;

    private bool isSneaking;
    private bool wasPlayerFacingEnemy;

    private bool shouldMove;
    private bool shouldRotate;

    private Vector3 desiredMoveDirection;
    private Vector3 desiredFacingDirection;

    private Quaternion lastRotation;

    private static readonly int IsSneakingBool =
        Animator.StringToHash("IsSneaking");

    private static readonly int AttackTrigger =
        Animator.StringToHash("Attack");

    protected override void Awake()
    {
        base.Awake();

        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError(
                $"{name} requires a Rigidbody component.",
                this
            );

            enabled = false;
            return;
        }

        lastRotation = rb.rotation;

        // Gives every Boo a slightly different starting attack time.
        nextAttackTime =
            Time.time + GetRandomReactionDelay();
    }

    private void Update()
    {
        if (isDead || player == null)
        {
            StopSneaking();
            StopRotation();

            wasPlayerFacingEnemy = false;
            return;
        }

        float distanceToPlayer = Vector3.Distance(
            transform.position,
            player.position
        );

        bool playerIsInDetectionRange =
            distanceToPlayer <= detectionRadius;

        if (!playerIsInDetectionRange)
        {
            StopSneaking();
            StopRotation();

            wasPlayerFacingEnemy = false;
            return;
        }

        bool playerIsFacingEnemy =
            IsPlayerFacingEnemy();

        // The Boo freezes and attacks when the player looks at it.
        if (playerIsFacingEnemy)
        {
            StopSneaking();

            // Only rotate while still outside the stopping distance.
            // Once the Boo reaches its destination, it keeps its current
            // rotation instead of turning in place.
            if (rotateWhileAttacking &&
                distanceToPlayer > stoppingDistance)
            {
                SetDesiredRotationTowardPlayer();
            }
            else
            {
                StopRotation();
            }

            // Runs only when the player first turns toward the Boo.
            if (!wasPlayerFacingEnemy)
            {
                nextAttackTime =
                    Time.time + GetRandomReactionDelay();
            }

            wasPlayerFacingEnemy = true;

            if (distanceToPlayer <= attackRange)
            {
                TryAttack();
            }

            return;
        }

        // Player looked away, allowing a new reaction delay next time.
        wasPlayerFacingEnemy = false;

        if (distanceToPlayer <= stoppingDistance)
        {
            StopSneaking();
            StopRotation();
            return;
        }

        SneakTowardPlayer();
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        // Prevent collisions from physically spinning the enemy.
        rb.angularVelocity = Vector3.zero;

        if (isDead)
        {
            StopRigidbodyMovement();
            return;
        }

        ApplyMovement();
        ApplyRotation();
    }

    private void ApplyMovement()
    {
        Vector3 currentVelocity =
            rb.linearVelocity;

        if (!shouldMove)
        {
            currentVelocity.x = 0f;
            currentVelocity.z = 0f;

            rb.linearVelocity =
                currentVelocity;

            return;
        }

        Vector3 horizontalVelocity =
            desiredMoveDirection * moveSpeed;

        rb.linearVelocity = new Vector3(
            horizontalVelocity.x,
            currentVelocity.y,
            horizontalVelocity.z
        );
    }

    private void ApplyRotation()
    {
        if (!shouldRotate ||
            desiredFacingDirection.sqrMagnitude <= 0.001f)
        {
            rb.MoveRotation(lastRotation);
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                desiredFacingDirection,
                Vector3.up
            );

        Quaternion smoothedRotation =
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

        lastRotation = smoothedRotation;

        rb.MoveRotation(smoothedRotation);
    }

    private bool IsPlayerFacingEnemy()
    {
        Vector3 directionFromPlayerToEnemy =
            transform.position - player.position;

        directionFromPlayerToEnemy.y = 0f;

        if (directionFromPlayerToEnemy.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        directionFromPlayerToEnemy.Normalize();

        Vector3 playerForward =
            player.forward;

        playerForward.y = 0f;

        if (playerForward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        playerForward.Normalize();

        float facingDotProduct =
            Vector3.Dot(
                playerForward,
                directionFromPlayerToEnemy
            );

        return facingDotProduct >= facingThreshold;
    }

    private void SneakTowardPlayer()
    {
        Vector3 directionToPlayer =
            player.position - transform.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude <= 0.001f)
        {
            StopSneaking();
            StopRotation();
            return;
        }

        directionToPlayer.Normalize();

        shouldMove = true;
        desiredMoveDirection = directionToPlayer;

        shouldRotate = true;
        desiredFacingDirection = directionToPlayer;

        if (!isSneaking)
        {
            isSneaking = true;
            SetSneakingAnimation(true);
        }

        SetAnimatorSpeed(sneakAnimationSpeed);
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        if (fireballPrefab == null || firePoint == null)
        {
            Debug.LogWarning(
                $"{name} cannot attack because its fireball prefab " +
                "or fire point has not been assigned.",
                this
            );

            return;
        }

        // Randomized cooldown prevents Boos from staying synchronized.
        nextAttackTime =
            Time.time + GetRandomAttackCooldown();

        if (animator != null)
        {
            animator.ResetTrigger(AttackTrigger);
            animator.SetTrigger(AttackTrigger);
        }
        else
        {
            ShootFireball();
        }
    }

    /// <summary>
    /// Call this from an Animation Event during the casting animation.
    /// </summary>
    public void ShootFireball()
    {
        if (isDead ||
            player == null ||
            fireballPrefab == null ||
            firePoint == null)
        {
            return;
        }

        Vector3 targetPosition =
            player.position + Vector3.up * aimHeightOffset;

        Vector3 fireDirection =
            targetPosition - firePoint.position;

        if (fireDirection.sqrMagnitude <= 0.001f)
        {
            fireDirection =
                transform.forward;
        }

        fireDirection.Normalize();

        Quaternion fireRotation =
            Quaternion.LookRotation(
                fireDirection,
                Vector3.up
            );

        GameObject fireballObject =
            Instantiate(
                fireballPrefab,
                firePoint.position,
                fireRotation
            );

        FireballProjectile projectile =
            fireballObject.GetComponent<FireballProjectile>();

        if (projectile != null)
        {
            projectile.Initialize(
                fireDirection,
                gameObject
            );
        }
        else
        {
            Debug.LogWarning(
                $"{fireballObject.name} does not contain a " +
                $"{nameof(FireballProjectile)} component.",
                fireballObject
            );
        }
    }

    private float GetRandomReactionDelay()
    {
        return Random.Range(
            minimumReactionDelay,
            maximumReactionDelay
        );
    }

    private float GetRandomAttackCooldown()
    {
        return Random.Range(
            minimumAttackCooldown,
            maximumAttackCooldown
        );
    }

    private void SetDesiredRotationTowardPlayer()
    {
        Vector3 directionToPlayer =
            player.position - transform.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude <= 0.001f)
        {
            StopRotation();
            return;
        }

        shouldRotate = true;

        desiredFacingDirection =
            directionToPlayer.normalized;
    }

    private void StopSneaking()
    {
        shouldMove = false;
        desiredMoveDirection = Vector3.zero;

        if (!isSneaking)
        {
            return;
        }

        isSneaking = false;

        SetSneakingAnimation(false);
        SetAnimatorSpeed(1f);
    }

    private void StopRotation()
    {
        shouldRotate = false;
        desiredFacingDirection = Vector3.zero;

        if (rb == null)
        {
            return;
        }

        lastRotation = rb.rotation;
        rb.angularVelocity = Vector3.zero;
    }

    private void StopRigidbodyMovement()
    {
        shouldMove = false;
        shouldRotate = false;

        desiredMoveDirection = Vector3.zero;
        desiredFacingDirection = Vector3.zero;

        Vector3 stoppedVelocity =
            rb.linearVelocity;

        stoppedVelocity.x = 0f;
        stoppedVelocity.z = 0f;

        rb.linearVelocity =
            stoppedVelocity;

        rb.angularVelocity = Vector3.zero;
        lastRotation = rb.rotation;
    }

    private void SetSneakingAnimation(bool value)
    {
        if (animator != null)
        {
            animator.SetBool(
                IsSneakingBool,
                value
            );
        }
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (animator != null)
        {
            animator.speed = speed;
        }
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            StopRigidbodyMovement();
        }

        if (animator != null)
        {
            animator.speed = 1f;
        }
    }

    private void OnValidate()
    {
        detectionRadius =
            Mathf.Max(0f, detectionRadius);

        moveSpeed =
            Mathf.Max(0f, moveSpeed);

        rotationSpeed =
            Mathf.Max(0f, rotationSpeed);

        stoppingDistance =
            Mathf.Max(0f, stoppingDistance);

        sneakAnimationSpeed =
            Mathf.Max(
                0.01f,
                sneakAnimationSpeed
            );

        attackRange =
            Mathf.Max(0f, attackRange);

        aimHeightOffset =
            Mathf.Max(0f, aimHeightOffset);

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

        minimumReactionDelay =
            Mathf.Max(
                0f,
                minimumReactionDelay
            );

        maximumReactionDelay =
            Mathf.Max(
                minimumReactionDelay,
                maximumReactionDelay
            );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius
        );

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );
    }
}