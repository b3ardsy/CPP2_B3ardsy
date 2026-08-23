using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerSpellProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float defaultSpeed = 12f;
    [SerializeField] private int defaultDamage = 1;
    [SerializeField] private float lifetime = 5f;

    [Header("Impact")]
    [SerializeField] private GameObject impactEffectPrefab;

    private Rigidbody rb;
    private Collider projectileCollider;

    private GameObject owner;
    private int damage;
    private float speed;

    private bool hasHit;
    private bool initialized;

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

        damage =
            defaultDamage;

        speed =
            defaultSpeed;
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
        bool preserveSpawnRotation = false
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

        Vector3 normalizedDirection =
            direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : transform.forward;

        /*
         * Most projectiles should rotate to face their
         * travel direction.
         *
         * Some effects use a custom authored rotation
         * and should preserve the spawn rotation.
         */
        if (!preserveSpawnRotation)
        {
            transform.rotation =
                Quaternion.LookRotation(
                    normalizedDirection
                );
        }

        IgnoreOwnerCollisions();

        rb.linearVelocity =
            normalizedDirection *
            speed;

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

        /*
         * Maintain projectile speed after minor
         * physics interactions.
         */
        if (
            rb.linearVelocity.sqrMagnitude >
            0.001f
        )
        {
            rb.linearVelocity =
                rb.linearVelocity.normalized *
                speed;
        }
    }

    // =========================================================
    // OWNER COLLISION
    // =========================================================

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
            owner.GetComponentsInChildren<Collider>();

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
                true
            );
        }
    }

    private bool IsOwnerCollider(
        Collider other
    )
    {
        if (
            owner == null ||
            other == null
        )
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

    // =========================================================
    // COLLISION
    // =========================================================

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (
            !initialized ||
            hasHit ||
            other == null
        )
        {
            return;
        }

        if (IsOwnerCollider(other))
        {
            return;
        }

        /*
         * Player projectiles should not destroy each other.
         */
        if (
            other.GetComponentInParent
                <PlayerSpellProjectile>() != null
        )
        {
            return;
        }

        /*
         * Damage is now interface-based.
         *
         * Legacy enemies expose IDamageable through Enemy.
         * Migrated enemies expose IDamageable through
         * EnemyController.
         *
         * This projectile no longer needs to know which
         * concrete enemy class it hit.
         */
        if (
            TryFindDamageable(
                other,
                out IDamageable damageable
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
         * Ignore unrelated trigger volumes.
         *
         * This prevents invisible trigger zones from consuming
         * the player's projectile.
         */
        if (other.isTrigger)
        {
            return;
        }

        /*
         * Solid scenery consumes the projectile normally.
         */
        HandleImpact();
    }

    // =========================================================
    // DAMAGEABLE LOOKUP
    // =========================================================

    private bool TryFindDamageable(
        Collider other,
        out IDamageable damageable
    )
    {
        damageable =
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

                return true;
            }
        }

        return false;
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

        if (impactEffectPrefab != null)
        {
            Instantiate(
                impactEffectPrefab,
                transform.position,
                transform.rotation
            );
        }

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
    }
}