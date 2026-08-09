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

    public bool IsDead => isDead;

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

    protected virtual void Awake()
    {
        currentHealth = maxHealth;

        animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError(
                $"{name}: No Animator component was found."
            );
        }

        capsuleCollider =
            GetComponentInChildren<CapsuleCollider>();

        if (capsuleCollider == null)
        {
            Debug.LogWarning(
                $"{name}: No CapsuleCollider was found. " +
                "The enemy's transform position will be used " +
                "as the lock-on point."
            );
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogError(
                $"{name}: No GameObject with the Player tag was found."
            );
        }
    }

    public virtual void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        currentHealth -= damage;

        currentHealth = Mathf.Clamp(
            currentHealth,
            0,
            maxHealth
        );

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(HitTrigger);
        }
    }

    protected virtual void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (animator != null)
        {
            animator.SetTrigger(DeathTrigger);
        }

        DropPickup();

        StartCoroutine(DestroyAfterDelay());
    }

    private void DropPickup()
    {
        GameObject pickupPrefab = GetPickupPrefab();

        if (pickupPrefab == null)
        {
            if (pickupToDrop != PickupType.None)
            {
                Debug.LogWarning(
                    $"{name}: No prefab assigned for {pickupToDrop} pickup."
                );
            }

            return;
        }

        Vector3 spawnPosition =
            transform.position +
            Vector3.up * pickupSpawnHeight;

        Instantiate(
            pickupPrefab,
            spawnPosition,
            Quaternion.identity
        );

        Debug.Log(
            $"{name}: Dropped {pickupToDrop} pickup."
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

        Destroy(gameObject);
    }
}