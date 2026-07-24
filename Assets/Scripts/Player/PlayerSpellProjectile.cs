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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();

        rb.useGravity = false;
        rb.isKinematic = false;

        projectileCollider.isTrigger = true;

        damage = defaultDamage;
        speed = defaultSpeed;
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
        owner = projectileOwner;

        damage = Mathf.Max(
            1,
            projectileDamage
        );

        speed = Mathf.Max(
            0f,
            projectileSpeed
        );

        Vector3 normalizedDirection =
            direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : transform.forward;

        transform.rotation =
            Quaternion.LookRotation(
                normalizedDirection
            );

        IgnoreOwnerCollisions();

        rb.linearVelocity =
            normalizedDirection * speed;

        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized)
        {
            return;
        }

        /*
         * Maintaining the velocity prevents the projectile
         * from slowing down after minor physics interactions.
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

        foreach (Collider ownerCollider in ownerColliders)
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

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit)
        {
            return;
        }

        if (owner != null)
        {
            if (
                other.gameObject == owner ||
                other.transform.IsChildOf(
                    owner.transform
                )
            )
            {
                return;
            }
        }

        Enemy enemy =
            other.GetComponentInParent<Enemy>();

        if (enemy != null)
        {
            if (enemy.IsDead)
            {
                HandleImpact();
                return;
            }

            enemy.TakeDamage(damage);

            HandleImpact();
            return;
        }

        /*
         * Ignore other player projectiles so spells do not
         * destroy each other immediately.
         */
        if (
            other.GetComponentInParent
                <PlayerSpellProjectile>() != null
        )
        {
            return;
        }

        HandleImpact();
    }

    private void HandleImpact()
    {
        if (hasHit)
        {
            return;
        }

        hasHit = true;

        rb.linearVelocity = Vector3.zero;

        if (impactEffectPrefab != null)
        {
            Instantiate(
                impactEffectPrefab,
                transform.position,
                transform.rotation
            );
        }

        Destroy(gameObject);
    }
}