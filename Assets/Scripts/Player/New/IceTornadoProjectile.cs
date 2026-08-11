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
     * Prevent the Tornado from damaging the same enemy
     * multiple times if that enemy has several colliders.
     */
    private readonly HashSet<Enemy> damagedEnemies =
        new HashSet<Enemy>();

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

        initialized = true;
    }

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

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (!initialized)
        {
            return;
        }

        if (IsOwnerCollider(other))
        {
            return;
        }

        Enemy enemy =
            other.GetComponentInParent<Enemy>();

        /*
         * Ignore terrain, scenery, pickups, and other
         * non-enemy objects. The Tornado lasts until
         * its lifetime expires.
         */
        if (enemy == null)
        {
            return;
        }

        if (enemy.IsDead)
        {
            return;
        }

        /*
         * Only damage each enemy once per Tornado.
         */
        if (damagedEnemies.Contains(enemy))
        {
            return;
        }

        damagedEnemies.Add(
            enemy
        );

        enemy.TakeDamage(
            damage
        );
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
            owner.GetComponentsInChildren<Collider>();

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