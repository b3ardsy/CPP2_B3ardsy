using System;
using System.Collections;
using UnityEngine;

public abstract class Enemy : MonoBehaviour
{
    // =========================================================
    // ENEMY STATS
    // =========================================================

    [Header("Enemy Stats")]
    [SerializeField] protected int maxHealth = 3;

    // =========================================================
    // DEATH
    // =========================================================

    [Header("Death")]
    [Tooltip(
        "How long the death animation plays before " +
        "the enemy is destroyed."
    )]
    [SerializeField]
    protected float destroyDelay = 2f;

    [Tooltip(
        "Particle effect spawned when the enemy disappears " +
        "at the end of its death animation."
    )]
    [SerializeField]
    private GameObject deathEffectPrefab;

    [Tooltip(
        "Offset from the enemy's visual body centre where " +
        "the death effect is spawned."
    )]
    [SerializeField]
    private Vector3 deathEffectSpawnOffset =
        Vector3.zero;

    [Tooltip(
        "How long the spawned death effect remains before " +
        "being destroyed."
    )]
    [SerializeField]
    private float deathEffectLifetime = 3f;

    // =========================================================
    // PICKUP DROP
    // =========================================================

    [Header("Pickup Drop")]
    [SerializeField]
    private PickupType pickupToDrop =
        PickupType.Health;

    [SerializeField]
    private GameObject healthPickupPrefab;

    [SerializeField]
    private GameObject ammoPickupPrefab;

    [SerializeField]
    private GameObject specialPickupPrefab;

    [Tooltip(
        "Height above the enemy where the pickup spawns."
    )]
    [SerializeField]
    private float pickupSpawnHeight = 0.5f;

    // =========================================================
    // SHARED REFERENCES
    // =========================================================

    protected Animator animator;
    protected CapsuleCollider capsuleCollider;
    protected Transform player;

    // =========================================================
    // HEALTH
    // =========================================================

    protected int currentHealth;
    protected bool isDead;

    // =========================================================
    // ENTANGLE
    // =========================================================

    private bool isEntangled;
    private Coroutine entangleRoutine;
    private GameObject activeEntangleEffect;

    // =========================================================
    // PUBLIC PROPERTIES
    // =========================================================

    public bool IsDead =>
        isDead;

    public bool IsEntangled =>
        isEntangled;

    public int CurrentHealth =>
        currentHealth;

    public int MaxHealth =>
        maxHealth;

    // =========================================================
    // EVENTS
    // =========================================================

    public event Action<int, int> OnHealthChanged;

    public event Action OnDied;

    public event Action OnEntangled;

    public event Action OnEntangleEnded;

    // =========================================================
    // PICKUP TYPES
    // =========================================================

    public enum PickupType
    {
        None,
        Health,
        Ammo,
        Special
    }

    // =========================================================
    // LOCK-ON
    // =========================================================

    public Vector3 LockOnPoint
    {
        get
        {
            if (capsuleCollider == null)
            {
                return transform.position;
            }

            return
                capsuleCollider.transform.TransformPoint(
                    capsuleCollider.center
                );
        }
    }

    // =========================================================
    // ANIMATOR PARAMETERS
    // =========================================================

    protected static readonly int HitTrigger =
        Animator.StringToHash("Hit");

    protected static readonly int DeathTrigger =
        Animator.StringToHash("Death");

    protected static readonly int EntangleTrigger =
        Animator.StringToHash("Entangle");

    // =========================================================
    // INITIALIZATION
    // =========================================================

    protected virtual void Awake()
    {
        currentHealth =
            maxHealth;

        animator =
            GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError(
                $"{name}: No Animator component was found.",
                this
            );
        }

        capsuleCollider =
            GetComponentInChildren<CapsuleCollider>();

        if (capsuleCollider == null)
        {
            Debug.LogWarning(
                $"{name}: No CapsuleCollider was found. " +
                "The enemy's transform position will be used " +
                "as the lock-on point.",
                this
            );
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (playerObject != null)
        {
            player =
                playerObject.transform;
        }
        else
        {
            Debug.LogError(
                $"{name}: No GameObject with the Player tag was found.",
                this
            );
        }
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    public virtual void TakeDamage(
        int damage
    )
    {
        if (isDead)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        currentHealth -=
            damage;

        currentHealth =
            Mathf.Clamp(
                currentHealth,
                0,
                maxHealth
            );

        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(
                HitTrigger
            );
        }
    }

    // =========================================================
    // ENTANGLE
    // =========================================================

    public void ApplyEntangle(
        float duration,
        GameObject entangleEffectPrefab,
        Vector3 localOffset
    )
    {
        if (isDead)
        {
            return;
        }

        duration =
            Mathf.Max(
                0.1f,
                duration
            );

        if (entangleRoutine != null)
        {
            StopCoroutine(
                entangleRoutine
            );

            entangleRoutine =
                null;
        }

        DestroyEntangleEffect();

        isEntangled =
            true;

        SpawnEntangleEffect(
            entangleEffectPrefab,
            localOffset
        );

        if (animator != null)
        {
            animator.ResetTrigger(
                HitTrigger
            );

            animator.ResetTrigger(
                EntangleTrigger
            );

            animator.SetTrigger(
                EntangleTrigger
            );
        }

        OnEntangled?.Invoke();

        entangleRoutine =
            StartCoroutine(
                EntangleRoutine(
                    duration
                )
            );

        Debug.Log(
            $"{name}: Entangled for {duration:0.0} seconds.",
            this
        );
    }

    private IEnumerator EntangleRoutine(
        float duration
    )
    {
        yield return new WaitForSeconds(
            duration
        );

        entangleRoutine =
            null;

        EndEntangle();
    }

    private void SpawnEntangleEffect(
        GameObject entangleEffectPrefab,
        Vector3 localOffset
    )
    {
        if (entangleEffectPrefab == null)
        {
            return;
        }

        activeEntangleEffect =
            Instantiate(
                entangleEffectPrefab,
                transform
            );

        activeEntangleEffect.transform.localPosition =
            localOffset;

        activeEntangleEffect.transform.localRotation =
            entangleEffectPrefab.transform.localRotation;
    }

    public void EndEntangle()
    {
        if (!isEntangled)
        {
            return;
        }

        isEntangled =
            false;

        if (entangleRoutine != null)
        {
            StopCoroutine(
                entangleRoutine
            );

            entangleRoutine =
                null;
        }

        DestroyEntangleEffect();

        OnEntangleEnded?.Invoke();

        Debug.Log(
            $"{name}: Entangle ended.",
            this
        );
    }

    private void DestroyEntangleEffect()
    {
        if (activeEntangleEffect == null)
        {
            return;
        }

        Destroy(
            activeEntangleEffect
        );

        activeEntangleEffect =
            null;
    }

    // =========================================================
    // DEATH
    // =========================================================

    protected virtual void Die()
    {
        if (isDead)
        {
            return;
        }

        if (entangleRoutine != null)
        {
            StopCoroutine(
                entangleRoutine
            );

            entangleRoutine =
                null;
        }

        isEntangled =
            false;

        DestroyEntangleEffect();

        isDead =
            true;

        OnDied?.Invoke();

        if (animator != null)
        {
            animator.ResetTrigger(
                EntangleTrigger
            );

            animator.SetTrigger(
                DeathTrigger
            );
        }

        DropPickup();

        StartCoroutine(
            DestroyAfterDelay()
        );
    }

    // =========================================================
    // DEATH EFFECT
    // =========================================================

    private Vector3 GetVisualBodyCentre()
    {
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>();

        if (
            renderers == null ||
            renderers.Length == 0
        )
        {
            /*
             * Fallback if this enemy somehow has
             * no visible Renderer.
             */
            return LockOnPoint;
        }

        bool foundRenderer =
            false;

        Bounds combinedBounds =
            new Bounds(
                transform.position,
                Vector3.zero
            );

        foreach (Renderer enemyRenderer in renderers)
        {
            if (
                enemyRenderer == null ||
                !enemyRenderer.enabled ||
                !enemyRenderer.gameObject.activeInHierarchy
            )
            {
                continue;
            }

            /*
             * Don't allow an active Entangle visual
             * to affect the calculated enemy centre.
             */
            if (
                activeEntangleEffect != null &&
                enemyRenderer.transform.IsChildOf(
                    activeEntangleEffect.transform
                )
            )
            {
                continue;
            }

            if (!foundRenderer)
            {
                combinedBounds =
                    enemyRenderer.bounds;

                foundRenderer =
                    true;

                continue;
            }

            combinedBounds.Encapsulate(
                enemyRenderer.bounds
            );
        }

        if (!foundRenderer)
        {
            return LockOnPoint;
        }

        return combinedBounds.center;
    }

    private void DisableEnemyColliders()
    {
        Collider[] enemyColliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider enemyCollider in enemyColliders)
        {
            if (enemyCollider == null)
            {
                continue;
            }

            enemyCollider.enabled =
                false;
        }
    }

    private void SpawnDeathEffect(
        Vector3 spawnPosition
    )
    {
        if (deathEffectPrefab == null)
        {
            return;
        }

        GameObject deathEffect =
            Instantiate(
                deathEffectPrefab,
                spawnPosition,
                deathEffectPrefab.transform.rotation
            );

        /*
         * Explicitly start all Particle Systems in case
         * Play On Awake is disabled.
         */
        ParticleSystem[] particleSystems =
            deathEffect.GetComponentsInChildren
                <ParticleSystem>();

        foreach (
            ParticleSystem particleSystem
            in particleSystems
        )
        {
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Play();
        }

        Destroy(
            deathEffect,
            deathEffectLifetime
        );

        Debug.Log(
            $"{name}: Spawned death effect at visual body centre.",
            deathEffect
        );
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(
            destroyDelay
        );

        /*
         * Calculate the centre BEFORE disabling anything.
         *
         * Renderer bounds are based on the visible model,
         * so this works better for differently shaped
         * enemies than the CapsuleCollider centre.
         */
        Vector3 deathEffectPosition =
            GetVisualBodyCentre() +
            deathEffectSpawnOffset;

        /*
         * Remove all enemy collision before creating
         * the particle burst.
         *
         * This prevents large enemy colliders, such as
         * the Tank's, from deflecting or disturbing
         * newly spawned particles.
         */
        DisableEnemyColliders();

        SpawnDeathEffect(
            deathEffectPosition
        );

        Destroy(
            gameObject
        );
    }

    // =========================================================
    // PICKUPS
    // =========================================================

    private void DropPickup()
    {
        GameObject pickupPrefab =
            GetPickupPrefab();

        if (pickupPrefab == null)
        {
            if (
                pickupToDrop !=
                PickupType.None
            )
            {
                Debug.LogWarning(
                    $"{name}: No prefab assigned for " +
                    $"{pickupToDrop} pickup.",
                    this
                );
            }

            return;
        }

        Vector3 spawnPosition =
            transform.position +
            Vector3.up *
            pickupSpawnHeight;

        Instantiate(
            pickupPrefab,
            spawnPosition,
            Quaternion.identity
        );

        Debug.Log(
            $"{name}: Dropped {pickupToDrop} pickup.",
            this
        );
    }

    private GameObject GetPickupPrefab()
    {
        switch (pickupToDrop)
        {
            case PickupType.Health:

                return
                    healthPickupPrefab;

            case PickupType.Ammo:

                return
                    ammoPickupPrefab;

            case PickupType.Special:

                return
                    specialPickupPrefab;

            default:

                return
                    null;
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    protected virtual void OnDestroy()
    {
        if (entangleRoutine != null)
        {
            StopCoroutine(
                entangleRoutine
            );

            entangleRoutine =
                null;
        }

        DestroyEntangleEffect();
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    protected virtual void OnValidate()
    {
        maxHealth =
            Mathf.Max(
                1,
                maxHealth
            );

        destroyDelay =
            Mathf.Max(
                0f,
                destroyDelay
            );

        deathEffectLifetime =
            Mathf.Max(
                0.1f,
                deathEffectLifetime
            );

        pickupSpawnHeight =
            Mathf.Max(
                0f,
                pickupSpawnHeight
            );
    }
}