using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Attack Settings")]
    [SerializeField] private float attackDuration = 0.7f;
    [SerializeField] private float attackCooldown = 0.15f;

    private bool isAttacking;
    private float nextAttackTime;

    public bool IsAttacking => isAttacking;

    private static readonly int PunchTrigger =
        Animator.StringToHash("Punch");

    private static readonly int KickTrigger =
        Animator.StringToHash("Kick");

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError(
                $"{name}: No Animator component was found."
            );

            enabled = false;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (isAttacking)
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        StartCoroutine(PerformRandomAttack());
    }

    private IEnumerator PerformRandomAttack()
    {
        isAttacking = true;

        animator.ResetTrigger(PunchTrigger);
        animator.ResetTrigger(KickTrigger);

        int randomAttack =
            Random.Range(0, 2);

        if (randomAttack == 0)
        {
            animator.SetTrigger(PunchTrigger);
        }
        else
        {
            animator.SetTrigger(KickTrigger);
        }

        yield return new WaitForSeconds(attackDuration);

        isAttacking = false;

        nextAttackTime =
            Time.time + attackCooldown;
    }
}