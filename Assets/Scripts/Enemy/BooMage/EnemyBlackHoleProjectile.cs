using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyBlackHoleProjectile : MonoBehaviour
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

    private void Awake()
    {
        rb =
            GetComponent<Rigidbody>();

        projectileCollider =
            GetComponent<Collider>();

        rb.useGravity = false;
        rb.isKinematic = false;

        projectileCollider.isTrigger = true;

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

        Vector3 normalizedDirection =
            direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : transform.forward;

        IgnoreOwnerCollisions();

        SetProjectileDirection(
            normalizedDirection
        );

        initialized = true;
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
        /*
         * Aim above the target's root position.
         *
         * The player Transform is located close to the
         * ground, so aiming directly at transform.position
         * causes the missile to curve downward and hit
         * the terrain before reaching the player.
         */
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

        /*
         * Gradually curve toward the player rather
         * than snapping directly toward them.
         */
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
         * Ignore the Boo that fired the projectile.
         */
        if (IsOwnerCollider(other))
        {
            return;
        }

        /*
         * Black Hole projectiles spawn from the same
         * FirePoint and must never destroy each other.
         */
        if (
            other.GetComponentInParent
                <EnemyBlackHoleProjectile>() != null
        )
        {
            return;
        }

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
         * Ignore other enemies.
         */
        if (
            other.GetComponentInParent
                <Enemy>() != null
        )
        {
            return;
        }

        /*
         * Ignore unrelated trigger volumes such as
         * interaction zones, hitboxes, and pickups.
         */
        if (other.isTrigger)
        {
            return;
        }

        /*
         * Solid terrain or scenery destroys
         * the projectile.
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
            other.gameObject == owner ||
            other.transform.IsChildOf(
                owner.transform
            );
    }

    private void IgnoreOwnerCollisions()
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
            if (ownerCollider == null)
            {
                continue;
            }

            Physics.IgnoreCollision(
                projectileCollider,
                ownerCollider,
                true
            );
        }
    }

    private void HandleImpact()
    {
        if (hasHit)
        {
            return;
        }

        hasHit = true;

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