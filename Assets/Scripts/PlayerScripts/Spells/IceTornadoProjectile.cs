using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class IceTornadoProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float defaultSpeed = 10f;
    [SerializeField] private int defaultDamage = 1;

    [Tooltip("How long the Ice Tornado remains active.")]
    [SerializeField] private float lifetime = 5f;

    private Rigidbody rb;
    private Collider projectileCollider;

    private GameObject owner;

    private float speed;
    private int damage;

    private bool initialized;

    /*
     * Prevent the Tornado from damaging the same damageable
     * object multiple times if it has several colliders.
     *
     * We store the MonoBehaviour that implements IDamageable
     * rather than a concrete Enemy type so this works with:
     *
     * - EnemyController (new Mage architecture)
     * - Enemy (legacy Rogue/Tank during migration)
     * - future IDamageable targets
     */
    private readonly HashSet<MonoBehaviour> damagedTargets =
        new HashSet<MonoBehaviour>();

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
        float projectileSpeed
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
         * Do not change transform.rotation here.
         *
         * PlayerStaffCombat already gives the Tornado
         * the correct visual rotation when it spawns.
         */
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
         * Keep the Tornado moving at a consistent speed.
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
    // COLLISION
    // =========================================================

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (
            !initialized ||
            other == null
        )
        {
            return;
        }

        if (IsOwnerCollider(other))
        {
            return;
        }

        if (
            !TryFindDamageable(
                other,
                out IDamageable damageable,
                out MonoBehaviour damageableBehaviour
            )
        )
        {
            /*
             * Terrain, scenery, pickups, and unrelated objects
             * do not affect the Tornado. It lasts until its
             * normal lifetime expires.
             */
            return;
        }

        /*
         * Only damage each target once per Tornado even if
         * that target exposes multiple colliders.
         */
        if (
            damagedTargets.Contains(
                damageableBehaviour
            )
        )
        {
            return;
        }

        damagedTargets.Add(
            damageableBehaviour
        );

        damageable.TakeDamage(
            damage
        );
    }

    // =========================================================
    // DAMAGEABLE LOOKUP
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

    // =========================================================
    // OWNER COLLISION
    // =========================================================

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