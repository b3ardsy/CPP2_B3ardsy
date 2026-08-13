using System;
using System.Collections;
using UnityEngine;

public abstract class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] protected int maxHealth = 3;

    [Header("Death")]
    [SerializeField] protected float destroyDelay = 2f;

    [Header("Pickup Drop")]
    [SerializeField] private PickupType pickupToDrop = PickupType.Health;
    [SerializeField] private GameObject healthPickupPrefab;
    [SerializeField] private GameObject ammoPickupPrefab;
    [SerializeField] private GameObject specialPickupPrefab;

    [Tooltip("Height above the enemy where the pickup spawns.")]
    [SerializeField] private float pickupSpawnHeight = 0.5f;

    protected Animator animator;
    protected CapsuleCollider capsuleCollider;
    protected Transform player;

    protected int currentHealth;
    protected bool isDead;

    private bool isEntangled;
    private Coroutine entangleRoutine;
    private GameObject activeEntangleEffect;

    public bool IsDead =>
        isDead;

    public bool IsEntangled =>
        isEntangled;

    public int CurrentHealth =>
        currentHealth;

    public int MaxHealth =>
        maxHealth;

    // Fired whenever the enemy's health changes.
    public event Action<int, int> OnHealthChanged;

    // Fired immediately when the enemy dies.
    public event Action OnDied;

    // Fired when Entangle begins.
    public event Action OnEntangled;

    // Fired when Entangle ends.
    public event Action OnEntangleEnded;

    public enum PickupType
    {
        None,
        Health,
        Ammo,
        Special
    }

    public Vector3 LockOnPoint
    {
        get
        {
            if (capsuleCollider == null)
            {
                return transform.position;
            }

            return capsuleCollider.transform.TransformPoint(
                capsuleCollider.center
            );
        }
    }

    protected static readonly int HitTrigger =
        Animator.StringToHash("Hit");

    protected static readonly int DeathTrigger =
        Animator.StringToHash("Death");

    protected static readonly int EntangleTrigger =
        Animator.StringToHash("Entangle");

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

        /*
         * If the enemy is already Entangled,
         * restart the duration rather than creating
         * multiple overlapping status effects.
         */
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

        /*
         * Parent the visual to the enemy so it remains
         * aligned with the target while Entangled.
         *
         * The prefab's own rotation is preserved.
         */
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

        /*
         * Death immediately overrides Entangle.
         */
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
                return healthPickupPrefab;

            case PickupType.Ammo:
                return ammoPickupPrefab;

            case PickupType.Special:
                return specialPickupPrefab;

            default:
                return null;
        }
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(
            destroyDelay
        );

        Destroy(
            gameObject
        );
    }

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
}