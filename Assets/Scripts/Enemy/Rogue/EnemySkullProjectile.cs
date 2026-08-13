using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemySkullProjectile :
    MonoBehaviour,
    IReflectableProjectile
{
    [Header("Projectile")]
    [SerializeField] private float defaultSpeed = 10f;
    [SerializeField] private int defaultDamage = 1;
    [SerializeField] private float lifetime = 6f;

    [Header("Homing")]
    [Tooltip(
        "How quickly the Skull rotates toward the player."
    )]
    [SerializeField] private float homingTurnSpeed = 120f;

    [Tooltip(
        "Height above the target's root position that " +
        "the Skull tries to follow."
    )]
    [SerializeField] private float targetHeightOffset = 1f;

    [Header("Visual")]
    [Tooltip(
        "Rotation correction required by the Skull particle prefab."
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
    private bool isPlayerOwned;

    // =========================================================
    // INITIALIZATION
    // =========================================================

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
        Transform target
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

    // =========================================================
    // MOVEMENT
    // =========================================================

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
            targetHeightOffset;

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
         * A projectile already reflected by the player
         * cannot be reflected repeatedly.
         */
        if (isPlayerOwned)
        {
            return;
        }

        Vector3 incomingDirection;

        if (
            rb.linearVelocity.sqrMagnitude >
            0.001f
        )
        {
            incomingDirection =
                rb.linearVelocity.normalized;
        }
        else
        {
            incomingDirection =
                transform.forward;
        }

        Vector3 reflectedDirection =
            -incomingDirection;

        /*
         * Restore collision with the Rogue that
         * originally fired the Skull.
         */
        IgnoreOwnerCollisions(
            false
        );

        /*
         * Ownership transfers to the player.
         */
        owner =
            newOwner;

        IgnoreOwnerCollisions(
            true
        );

        /*
         * Once reflected, stop homing toward
         * the player completely.
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
            $"{name}: Skull reflected by Shield.",
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

        if (IsOwnerCollider(other))
        {
            return;
        }

        /*
         * Skull projectiles ignore one another.
         */
        if (
            other.GetComponentInParent
                <EnemySkullProjectile>() != null
        )
        {
            return;
        }

        /*
         * Shield owns the reflection logic.
         *
         * Because the Shield is parented beneath
         * the Player, do not accidentally interpret
         * the Shield collider itself as PlayerStatsNew.
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
         * Enemy-owned Skulls cannot hurt enemies.
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
         * Reflected Skulls ignore the Player.
         */
        PlayerStatsNew playerStats =
            other.GetComponentInParent
                <PlayerStatsNew>();

        if (playerStats != null)
        {
            return;
        }

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

        if (other.isTrigger)
        {
            return;
        }

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

    // =========================================================
    // VALIDATION
    // =========================================================

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

        targetHeightOffset =
            Mathf.Max(
                0f,
                targetHeightOffset
            );
    }
}