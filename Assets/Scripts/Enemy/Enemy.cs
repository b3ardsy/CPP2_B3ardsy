using System.Collections;
using UnityEngine;

public abstract class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] protected int maxHealth = 3;

    [Header("Death")]
    [SerializeField] protected float destroyDelay = 2f;

    protected Animator animator;
    protected Transform player;
    protected int currentHealth;
    protected bool isDead;

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

        StartCoroutine(DestroyAfterDelay());
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);

        Destroy(gameObject);
    }
}