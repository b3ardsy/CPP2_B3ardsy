using UnityEngine;

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

    [Header("Ground Movement")]
    [SerializeField] private bool keepStartingHeight = true;

    [Header("Fireball Attack")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Transform firePoint;

    [Tooltip("Minimum delay between fireball attacks.")]
    [SerializeField] private float minimumAttackCooldown = 1.75f;

    [Tooltip("Maximum delay between fireball attacks.")]
    [SerializeField] private float maximumAttackCooldown = 2.75f;

    [Tooltip("Minimum delay before attacking when the player first looks at this enemy.")]
    [SerializeField] private float minimumReactionDelay = 0.1f;

    [Tooltip("Maximum delay before attacking when the player first looks at this enemy.")]
    [SerializeField] private float maximumReactionDelay = 0.35f;

    [SerializeField] private float attackRange = 10f;
    [SerializeField] private float aimHeightOffset = 1f;
    [SerializeField] private bool rotateWhileAttacking = true;

    private float startingHeight;
    private float nextAttackTime;

    private bool isSneaking;
    private bool wasPlayerFacingEnemy;

    private static readonly int IsSneakingBool =
        Animator.StringToHash("IsSneaking");

    private static readonly int AttackTrigger =
        Animator.StringToHash("Attack");

    protected override void Awake()
    {
        base.Awake();

        startingHeight = transform.position.y;

        // Gives every Boo a slightly different starting attack time.
        nextAttackTime = Time.time + GetRandomReactionDelay();
    }

    private void Update()
    {
        if (isDead || player == null)
        {
            StopSneaking();
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
            wasPlayerFacingEnemy = false;
            return;
        }

        bool playerIsFacingEnemy = IsPlayerFacingEnemy();

        // The Boo freezes and attacks when the player looks at it.
        if (playerIsFacingEnemy)
        {
            StopSneaking();

            if (rotateWhileAttacking)
            {
                RotateTowardPlayer();
            }

            // This only runs on the frame the player first turns toward the Boo.
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

        // Player has looked away, allowing the next look to trigger
        // a new reaction delay.
        wasPlayerFacingEnemy = false;

        // The player is not looking, so the Boo resumes sneaking.
        if (distanceToPlayer <= stoppingDistance)
        {
            StopSneaking();
            return;
        }

        SneakTowardPlayer();
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

        Vector3 playerForward = player.forward;
        playerForward.y = 0f;

        if (playerForward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        playerForward.Normalize();

        float facingDotProduct = Vector3.Dot(
            playerForward,
            directionFromPlayerToEnemy
        );

        return facingDotProduct >= facingThreshold;
    }

    private void SneakTowardPlayer()
    {
        isSneaking = true;

        SetSneakingAnimation(true);
        SetAnimatorSpeed(sneakAnimationSpeed);

        Vector3 directionToPlayer =
            player.position - transform.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude <= 0.001f)
        {
            return;
        }

        directionToPlayer.Normalize();

        RotateTowardDirection(directionToPlayer);

        Vector3 movement =
            directionToPlayer *
            moveSpeed *
            Time.deltaTime;

        transform.position += movement;

        if (keepStartingHeight)
        {
            Vector3 correctedPosition = transform.position;
            correctedPosition.y = startingHeight;
            transform.position = correctedPosition;
        }
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

        // Randomized cooldown prevents multiple Boos from staying synchronized.
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
            fireDirection = transform.forward;
        }

        fireDirection.Normalize();

        Quaternion fireRotation =
            Quaternion.LookRotation(
                fireDirection,
                Vector3.up
            );

        GameObject fireballObject = Instantiate(
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

    private void RotateTowardPlayer()
    {
        Vector3 directionToPlayer =
            player.position - transform.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude <= 0.001f)
        {
            return;
        }

        RotateTowardDirection(directionToPlayer.normalized);
    }

    private void RotateTowardDirection(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(
            direction,
            Vector3.up
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void StopSneaking()
    {
        if (!isSneaking)
        {
            return;
        }

        isSneaking = false;

        SetSneakingAnimation(false);
        SetAnimatorSpeed(1f);
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

    private void OnValidate()
    {
        detectionRadius = Mathf.Max(0f, detectionRadius);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);

        sneakAnimationSpeed = Mathf.Max(
            0.01f,
            sneakAnimationSpeed
        );

        attackRange = Mathf.Max(0f, attackRange);
        aimHeightOffset = Mathf.Max(0f, aimHeightOffset);

        minimumAttackCooldown = Mathf.Max(
            0.01f,
            minimumAttackCooldown
        );

        maximumAttackCooldown = Mathf.Max(
            minimumAttackCooldown,
            maximumAttackCooldown
        );

        minimumReactionDelay = Mathf.Max(
            0f,
            minimumReactionDelay
        );

        maximumReactionDelay = Mathf.Max(
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