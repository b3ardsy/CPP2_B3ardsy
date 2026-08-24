using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyBlackHoleProjectile :
    MonoBehaviour,
    IReflectableProjectile
{
    // =========================================================
    // PROJECTILE
    // =========================================================

    [Header("Projectile")]
    [SerializeField] private float defaultSpeed = 12f;
    [SerializeField] private int defaultDamage = 1;
    [SerializeField] private float lifetime = 5f;

    // =========================================================
    // HOMING
    // =========================================================

    [Header("Homing")]
    [Tooltip(
        "How quickly a homing Black Hole can rotate " +
        "toward its target."
    )]
    [SerializeField]
    private float homingTurnSpeed = 120f;

    [Tooltip(
        "Height above the target's root position that " +
        "the homing projectile aims toward."
    )]
    [SerializeField]
    private float homingTargetHeightOffset = 1f;

    // =========================================================
    // VISUAL
    // =========================================================

    [Tooltip(
        "Visual rotation correction for the Black Hole prefab."
    )]
    [SerializeField]
    private Vector3 visualRotationOffset =
        new Vector3(-90f, 0f, 0f);

    // =========================================================
    // REFERENCES
    // =========================================================

    private Rigidbody rb;
    private Collider projectileCollider;

    private GameObject owner;
    private Transform homingTarget;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

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
         * The Shield owns projectile reflection.
         *
         * Because the Shield is parented underneath the Player,
         * it must be handled before searching the hierarchy for
         * an IDamageable target.
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

    // =========================================================
    // ENEMY-OWNED COLLISION
    // =========================================================

    private void HandleEnemyOwnedCollision(
        Collider other
    )
    {
        /*
         * Enemy-owned Black Holes may damage the Player.
         *
         * The projectile no longer needs to know about
         * PlayerStatsNew specifically. It only needs an
         * IDamageable component belonging to the Player.
         */
        if (
            TryFindDamageable(
                other,
                out IDamageable damageable,
                out MonoBehaviour damageableBehaviour
            )
        )
        {
            if (IsPlayerDamageable(
                damageableBehaviour
            ))
            {
                damageable.TakeDamage(
                    damage
                );

                HandleImpact();
                return;
            }

            /*
             * Enemy-owned projectiles ignore other
             * damageable objects such as enemies.
             */
            return;
        }

        /*
         * Enemy-owned projectiles also ignore enemies even
         * if that enemy somehow does not expose IDamageable.
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

    // =========================================================
    // PLAYER-OWNED COLLISION
    // =========================================================

    private void HandlePlayerOwnedCollision(
        Collider other
    )
    {
        /*
         * Reflected projectiles use the same IDamageable
         * contract as enemy-owned projectiles.
         */
        if (
            TryFindDamageable(
                other,
                out IDamageable damageable,
                out MonoBehaviour damageableBehaviour
            )
        )
        {
            /*
             * Reflected projectiles must never hurt the Player.
             */
            if (IsPlayerDamageable(
                damageableBehaviour
            ))
            {
                return;
            }

            /*
             * Any non-player object exposing IDamageable may
             * receive damage from a reflected projectile.
             *
             * Today that means enemies. Later this could also
             * support destructible objects without changing
             * this projectile.
             */
            damageable.TakeDamage(
                damage
            );

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

    // =========================================================
    // DAMAGEABLE HELPERS
    // =========================================================

    private bool TryFindDamageable(
        Collider other,
        out IDamageable damageable,
        out MonoBehaviour damageableBehaviour
    )
    {
        damageable =
            null;

        damageableBehaviour =
            null;

        if (other == null)
        {
            return false;
        }

        /*
         * Search the collider and its parents for any
         * MonoBehaviour that implements IDamageable.
         *
         * Health itself no longer implements IDamageable.
         *
         * This means:
         *
         * Player -> PlayerStatsNew / future PlayerDamageController
         * Enemy  -> Enemy / TankEnemy / RogueEnemy / BooEnemy
         */
        MonoBehaviour[] behaviours =
            other.GetComponentsInParent<MonoBehaviour>(
                true
            );

        foreach (
            MonoBehaviour behaviour
            in behaviours
        )
        {
            if (behaviour == null)
            {
                continue;
            }

            if (
                behaviour is
                IDamageable foundDamageable
            )
            {
                damageable =
                    foundDamageable;

                damageableBehaviour =
                    behaviour;

                return true;
            }
        }

        return false;
    }

    private bool IsPlayerDamageable(
        MonoBehaviour damageableBehaviour
    )
    {
        if (damageableBehaviour == null)
        {
            return false;
        }

        Transform currentTransform =
            damageableBehaviour.transform;

        /*
         * Walk upward in case the IDamageable implementation
         * ever moves onto a child object later.
         */
        while (currentTransform != null)
        {
            if (
                currentTransform.CompareTag(
                    "Player"
                )
            )
            {
                return true;
            }

            currentTransform =
                currentTransform.parent;
        }

        return false;
    }

    // =========================================================
    // OWNER COLLISION
    // =========================================================

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

    // =========================================================
    // IMPACT
    // =========================================================

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

        homingTargetHeightOffset =
            Mathf.Max(
                0f,
                homingTargetHeightOffset
            );
    }
}