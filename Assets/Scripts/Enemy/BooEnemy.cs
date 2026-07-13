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

    private float startingHeight;
    private bool isSneaking;

    private static readonly int IsSneakingBool =
        Animator.StringToHash("IsSneaking");

    protected override void Awake()
    {
        base.Awake();

        startingHeight = transform.position.y;
    }

    private void Update()
    {
        if (isDead || player == null)
        {
            StopSneaking();
            return;
        }

        float distanceToPlayer =
            Vector3.Distance(
                transform.position,
                player.position
            );

        bool playerIsInRange =
            distanceToPlayer <= detectionRadius;

        if (!playerIsInRange)
        {
            StopSneaking();
            return;
        }

        bool playerIsFacingEnemy =
            IsPlayerFacingEnemy();

        if (playerIsFacingEnemy)
        {
            StopSneaking();
            return;
        }

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

        Vector3 playerForward =
            player.forward;

        playerForward.y = 0f;
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

        RotateTowardPlayer(directionToPlayer);

        Vector3 movement =
            directionToPlayer *
            moveSpeed *
            Time.deltaTime;

        transform.position += movement;

        if (keepStartingHeight)
        {
            Vector3 correctedPosition =
                transform.position;

            correctedPosition.y = startingHeight;

            transform.position = correctedPosition;
        }
    }

    private void RotateTowardPlayer(
        Vector3 directionToPlayer
    )
    {
        Quaternion targetRotation =
            Quaternion.LookRotation(
                directionToPlayer,
                Vector3.up
            );

        transform.rotation =
            Quaternion.Slerp(
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius
        );
    }
}