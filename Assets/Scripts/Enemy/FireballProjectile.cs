using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FireballProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private int damage = 1;

    [Header("Polygon Arsenal Effects")]
    [SerializeField] private GameObject impactParticlePrefab;
    [SerializeField] private GameObject projectileParticlePrefab;
    [SerializeField] private GameObject muzzleParticlePrefab;

    [Header("Effect Lifetimes")]
    [SerializeField] private float muzzleLifetime = 1.5f;
    [SerializeField] private float impactLifetime = 5f;
    [SerializeField] private float trailCleanupDelay = 3f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    private Vector3 moveDirection;
    private GameObject owner;

    private GameObject projectileParticleInstance;

    private bool hasBeenInitialized;
    private bool hasHitSomething;

    private void Start()
    {
        if (!hasBeenInitialized)
        {
            moveDirection = transform.forward;
        }

        SpawnProjectileEffect();
        SpawnMuzzleEffect();

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (hasHitSomething)
        {
            return;
        }

        transform.position +=
            moveDirection *
            speed *
            Time.deltaTime;

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            transform.forward = moveDirection;
        }
    }

    public void Initialize(
        Vector3 direction,
        GameObject projectileOwner
    )
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward;
        }

        moveDirection = direction.normalized;
        owner = projectileOwner;
        hasBeenInitialized = true;

        transform.forward = moveDirection;
    }

    private void SpawnProjectileEffect()
    {
        if (projectileParticlePrefab == null)
        {
            return;
        }

        projectileParticleInstance = Instantiate(
            projectileParticlePrefab,
            transform.position,
            transform.rotation
        );

        projectileParticleInstance.transform.SetParent(
            transform,
            true
        );
    }

    private void SpawnMuzzleEffect()
    {
        if (muzzleParticlePrefab == null)
        {
            return;
        }

        GameObject muzzleEffect = Instantiate(
            muzzleParticlePrefab,
            transform.position,
            transform.rotation
        );

        Destroy(
            muzzleEffect,
            muzzleLifetime
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHitSomething)
        {
            return;
        }

        if (IsOwnerCollider(other))
        {
            return;
        }

        if (!IsInCollisionLayers(other.gameObject.layer))
        {
            return;
        }

        hasHitSomething = true;

        PlayerStats playerStats =
            other.GetComponentInParent<PlayerStats>();

        if (playerStats != null)
        {
            playerStats.TakeDamage(damage);
        }

        SpawnImpactEffect(other);
        DetachProjectileEffect();

        Destroy(gameObject);
    }

    private void SpawnImpactEffect(Collider other)
    {
        if (impactParticlePrefab == null)
        {
            return;
        }

        Vector3 impactPosition =
            other.ClosestPoint(transform.position);

        Vector3 impactNormal =
            transform.position - impactPosition;

        if (impactNormal.sqrMagnitude <= 0.001f)
        {
            impactNormal = -moveDirection;
        }

        impactNormal.Normalize();

        Vector3 spawnPosition =
            impactPosition +
            impactNormal * 0.15f;

        Quaternion impactRotation =
            Quaternion.FromToRotation(
                Vector3.up,
                impactNormal
            );

        GameObject impactEffect = Instantiate(
            impactParticlePrefab,
            spawnPosition,
            impactRotation
        );

        Destroy(
            impactEffect,
            impactLifetime
        );
    }

    private void DetachProjectileEffect()
    {
        if (projectileParticleInstance == null)
        {
            return;
        }

        projectileParticleInstance.transform.SetParent(
            null,
            true
        );

        StopParticleEmission(
            projectileParticleInstance
        );

        Destroy(
            projectileParticleInstance,
            trailCleanupDelay
        );

        projectileParticleInstance = null;
    }

    private void StopParticleEmission(GameObject effectObject)
    {
        ParticleSystem[] particleSystems =
            effectObject.GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            ParticleSystem.EmissionModule emission =
                particleSystem.emission;

            emission.enabled = false;
        }
    }

    private bool IsOwnerCollider(Collider other)
    {
        if (owner == null)
        {
            return false;
        }

        return
            other.gameObject == owner ||
            other.transform.IsChildOf(owner.transform);
    }

    private bool IsInCollisionLayers(int objectLayer)
    {
        return
            (collisionLayers.value & (1 << objectLayer)) != 0;
    }

    private void OnDestroy()
    {
        if (!hasHitSomething)
        {
            DetachProjectileEffect();
        }
    }
}