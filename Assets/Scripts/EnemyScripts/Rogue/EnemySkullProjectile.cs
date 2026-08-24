using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemySkullProjectile :
    MonoBehaviour,
    IReflectableProjectile
{
    // =========================================================
    // PROJECTILE
    // =========================================================

    [Header("Projectile")]
    [SerializeField] private float defaultSpeed = 10f;
    [SerializeField] private int defaultDamage = 1;
    [SerializeField] private float lifetime = 6f;

    // =========================================================
    // HOMING
    // =========================================================

    [Header("Homing")]
    [Tooltip(
        "How quickly the Skull rotates toward the player."
    )]
    [SerializeField]
    private float homingTurnSpeed = 120f;

    [Tooltip(
        "Height above the target's root position that " +
        "the Skull tries to follow."
    )]
    [SerializeField]
    private float targetHeightOffset = 1f;

    // =========================================================
    // VISUAL
    // =========================================================

    [Header("Visual")]
    [Tooltip(
        "Rotation correction required by the Skull particle prefab."
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
         * the Player, handle it before checking
         * for IDamageable.
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
        if (
            TryFindDamageable(
                other,
                out IDamageable damageable,
                out MonoBehaviour damageableBehaviour
            )
        )
        {
            /*
             * Enemy-owned Skulls may damage only the Player.
             */
            if (
                IsPlayerDamageable(
                    damageableBehaviour
                )
            )
            {
                damageable.TakeDamage(
                    damage
                );

                HandleImpact();
                return;
            }

            /*
             * Ignore other damageable objects,
             * such as enemies.
             */
            return;
        }

        /*
         * Enemy-owned Skulls also ignore enemies
         * even if no IDamageable is found.
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
        if (
            TryFindDamageable(
                other,
                out IDamageable damageable,
                out MonoBehaviour damageableBehaviour
            )
        )
        {
            /*
             * Reflected Skulls ignore the Player.
             */
            if (
                IsPlayerDamageable(
                    damageableBehaviour
                )
            )
            {
                return;
            }

            /*
             * Reflected Skulls may damage any
             * non-player IDamageable target.
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

        targetHeightOffset =
            Mathf.Max(
                0f,
                targetHeightOffset
            );
    }
}