using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyBlackHoleProjectile :
    MonoBehaviour,
    IReflectableProjectile
{
    [Header("Projectile")]
    [SerializeField] private float defaultSpeed = 12f;
    [SerializeField] private int defaultDamage = 1;
    [SerializeField] private float lifetime = 5f;

    [Header("Homing")]
    [Tooltip(
        "How quickly a homing Black Hole can rotate " +
        "toward its target."
    )]
    [SerializeField] private float homingTurnSpeed = 120f;

    [Tooltip(
        "Height above the target's root position that " +
        "the homing projectile aims toward."
    )]
    [SerializeField] private float homingTargetHeightOffset = 1f;

    [Tooltip(
        "Visual rotation correction for the Black Hole prefab."
    )]
    [SerializeField]
    private Vector3 visualRotationOffset =
        new Vector3(-90f, 0f, 0f);

    private Rigidbody rb;
    private Collider projectileCollider;

    private GameObject owner;
    private Transform homingTarget;

    private float speed;
    private int damage;

    private bool initialized;
    private bool hasHit;
    private bool isHoming;

    /*
     * False when initially fired by an enemy.
     *
     * Becomes true when the player's Shield
     * reflects this projectile.
     */
    private bool isPlayerOwned;

    private void Awake()
    {
        rb =
            GetComponent<Rigidbody>();

        projectileCollider =
            GetComponent<Collider>();

        rb.useGravity =
            false;

        rb.isKinematic =
            false;

        projectileCollider.isTrigger =
            true;

        speed =
            defaultSpeed;

        damage =
            defaultDamage;
    }

    private void Start()
    {
        Destroy(
            gameObject,
            lifetime
        );
    }

    public void Initialize(
        GameObject projectileOwner,
        Vector3 direction,
        int projectileDamage,
        float projectileSpeed,
        Transform target,
        bool enableHoming
    )
    {
        owner =
            projectileOwner;

        damage =
            Mathf.Max(
                1,
                projectileDamage
            );

        speed =
            Mathf.Max(
                0f,
                projectileSpeed
            );

        homingTarget =
            target;

        isHoming =
            enableHoming &&
            homingTarget != null;

        isPlayerOwned =
            false;

        Vector3 normalizedDirection =
            direction.sqrMagnitude >
            0.001f
                ? direction.normalized
                : transform.forward;

        IgnoreOwnerCollisions(
            true
        );

        SetProjectileDirection(
            normalizedDirection
        );

        initialized =
            true;
    }

    private void FixedUpdate()
    {
        if (!initialized)
        {
            return;
        }

        if (
            isHoming &&
            homingTarget != null
        )
        {
            UpdateHomingDirection();
        }
        else
        {
            MaintainVelocity();
        }
    }

    private void UpdateHomingDirection()
    {
        Vector3 targetPosition =
            homingTarget.position +
            Vector3.up *
            homingTargetHeightOffset;

        Vector3 directionToTarget =
            targetPosition -
            transform.position;

        if (
            directionToTarget.sqrMagnitude <=
            0.001f
        )
        {
            MaintainVelocity();
            return;
        }

        directionToTarget.Normalize();

        Vector3 currentDirection;

        if (
            rb.linearVelocity.sqrMagnitude >
            0.001f
        )
        {
            currentDirection =
                rb.linearVelocity.normalized;
        }
        else
        {
            currentDirection =
                directionToTarget;
        }

        Vector3 newDirection =
            Vector3.RotateTowards(
                currentDirection,
                directionToTarget,
                homingTurnSpeed *
                Mathf.Deg2Rad *
                Time.fixedDeltaTime,
                0f
            );

        SetProjectileDirection(
            newDirection.normalized
        );
    }

    private void MaintainVelocity()
    {
        if (
            rb.linearVelocity.sqrMagnitude <=
            0.001f
        )
        {
            return;
        }

        Vector3 direction =
            rb.linearVelocity.normalized;

        SetProjectileDirection(
            direction
        );
    }

    private void SetProjectileDirection(
        Vector3 direction
    )
    {
        rb.linearVelocity =
            direction *
            speed;

        Quaternion directionRotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );

        Quaternion rotationOffset =
            Quaternion.Euler(
                visualRotationOffset
            );

        transform.rotation =
            directionRotation *
            rotationOffset;
    }

    // =========================================================
    // REFLECTION
    // =========================================================

    public void Reflect(
        GameObject newOwner
    )
    {
        if (
            !initialized ||
            hasHit ||
            newOwner == null
        )
        {
            return;
        }

        /*
         * Don't reflect the same projectile repeatedly
         * from the player's own Shield.
         */
        if (isPlayerOwned)
        {
            return;
        }

        Vector3 incomingDirection =
            rb.linearVelocity.sqrMagnitude >
            0.001f
                ? rb.linearVelocity.normalized
                : transform.forward;

        Vector3 reflectedDirection =
            -incomingDirection;

        /*
         * The projectile must be allowed to collide with
         * its original enemy owner again.
         */
        IgnoreOwnerCollisions(
            false
        );

        owner =
            newOwner;

        /*
         * The reflected projectile must ignore the
         * player who now owns it.
         */
        IgnoreOwnerCollisions(
            true
        );

        /*
         * Reflected missiles no longer home.
         *
         * Otherwise the original homing missile would
         * immediately try turning back toward the player.
         */
        isHoming =
            false;

        homingTarget =
            null;

        isPlayerOwned =
            true;

        SetProjectileDirection(
            reflectedDirection
        );

        Debug.Log(
            $"{name}: Black Hole reflected by Shield.",
            this
        );
    }

    // =========================================================
    // COLLISION
    // =========================================================

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (
            !initialized ||
            hasHit
        )
        {
            return;
        }

        /*
         * Never collide with the current owner.
         */
        if (IsOwnerCollider(other))
        {
            return;
        }

        /*
         * Other Black Hole projectiles are ignored.
         */
        if (
            other.GetComponentInParent
                <EnemyBlackHoleProjectile>() != null
        )
        {
            return;
        }

        /*
         * IMPORTANT:
         *
         * The Shield is parented underneath the player.
         * Without this check, GetComponentInParent
         * <PlayerStatsNew>() could interpret the Shield
         * collider as the Player collider.
         *
         * PlayerShieldEffect owns the actual reflection.
         */
        PlayerShieldEffect shield =
            other.GetComponentInParent
                <PlayerShieldEffect>();

        if (shield != null)
        {
            return;
        }

        if (isPlayerOwned)
        {
            HandlePlayerOwnedCollision(
                other
            );

            return;
        }

        HandleEnemyOwnedCollision(
            other
        );
    }

    private void HandleEnemyOwnedCollision(
        Collider other
    )
    {
        /*
         * Enemy-owned projectile:
         * damage the Player.
         */
        PlayerStatsNew playerStats =
            other.GetComponentInParent
                <PlayerStatsNew>();

        if (playerStats != null)
        {
            if (!playerStats.IsDead)
            {
                playerStats.TakeDamage(
                    damage
                );
            }

            HandleImpact();
            return;
        }

        /*
         * Ignore enemies while enemy-owned.
         */
        if (
            other.GetComponentInParent
                <Enemy>() != null
        )
        {
            return;
        }

        /*
         * Ignore unrelated trigger volumes.
         */
        if (other.isTrigger)
        {
            return;
        }

        HandleImpact();
    }

    private void HandlePlayerOwnedCollision(
        Collider other
    )
    {
        /*
         * Reflected projectile:
         * ignore the Player entirely.
         */
        PlayerStatsNew playerStats =
            other.GetComponentInParent
                <PlayerStatsNew>();

        if (playerStats != null)
        {
            return;
        }

        /*
         * Reflected projectiles may damage enemies.
         */
        Enemy enemy =
            other.GetComponentInParent
                <Enemy>();

        if (enemy != null)
        {
            if (!enemy.IsDead)
            {
                enemy.TakeDamage(
                    damage
                );
            }

            HandleImpact();
            return;
        }

        /*
         * Ignore unrelated trigger volumes.
         */
        if (other.isTrigger)
        {
            return;
        }

        /*
         * Solid scenery destroys the reflected
         * projectile normally.
         */
        HandleImpact();
    }

    private bool IsOwnerCollider(
        Collider other
    )
    {
        if (owner == null)
        {
            return false;
        }

        return
            other.gameObject ==
            owner ||
            other.transform.IsChildOf(
                owner.transform
            );
    }

    private void IgnoreOwnerCollisions(
        bool shouldIgnore
    )
    {
        if (
            owner == null ||
            projectileCollider == null
        )
        {
            return;
        }

        Collider[] ownerColliders =
            owner.GetComponentsInChildren
                <Collider>();

        foreach (
            Collider ownerCollider
            in ownerColliders
        )
        {
            if (
                ownerCollider == null ||
                ownerCollider ==
                projectileCollider
            )
            {
                continue;
            }

            Physics.IgnoreCollision(
                projectileCollider,
                ownerCollider,
                shouldIgnore
            );
        }
    }

    private void HandleImpact()
    {
        if (hasHit)
        {
            return;
        }

        hasHit =
            true;

        rb.linearVelocity =
            Vector3.zero;

        Destroy(
            gameObject
        );
    }

    private void OnValidate()
    {
        defaultSpeed =
            Mathf.Max(
                0f,
                defaultSpeed
            );

        defaultDamage =
            Mathf.Max(
                1,
                defaultDamage
            );

        lifetime =
            Mathf.Max(
                0.1f,
                lifetime
            );

        homingTurnSpeed =
            Mathf.Max(
                0f,
                homingTurnSpeed
            );

        homingTargetHeightOffset =
            Mathf.Max(
                0f,
                homingTargetHeightOffset
            );
    }
}