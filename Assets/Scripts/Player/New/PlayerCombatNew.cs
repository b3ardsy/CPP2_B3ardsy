using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatNew : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement3DNew playerMovement;
    [SerializeField] private PlayerSpellcastingNew playerSpellcasting;

    [Header("Attack Settings")]
    [SerializeField] private float attackDuration = 0.7f;
    [SerializeField] private float attackCooldown = 0.15f;

    private bool isAttacking;
    private float nextAttackTime;

    private Coroutine attackCoroutine;

    public bool IsAttacking => isAttacking;

    private static readonly int PunchTrigger =
        Animator.StringToHash("Punch");

    private static readonly int KickTrigger =
        Animator.StringToHash("Kick");

    private void Awake()
    {
        FindReferences();
        ValidateReferences();
    }

    private void FindReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                GetComponent<PlayerMovement3DNew>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                GetComponentInParent<PlayerMovement3DNew>();
        }

        if (playerSpellcasting == null)
        {
            playerSpellcasting =
                GetComponent<PlayerSpellcastingNew>();
        }

        if (playerSpellcasting == null)
        {
            playerSpellcasting =
                GetComponentInParent<PlayerSpellcastingNew>();
        }
    }

    private void ValidateReferences()
    {
        if (animator == null)
        {
            Debug.LogError(
                $"{name}: PlayerCombatNew could not find an Animator.",
                this
            );

            enabled = false;
            return;
        }

        if (playerMovement == null)
        {
            Debug.LogError(
                $"{name}: PlayerCombatNew could not find PlayerMovement3DNew.",
                this
            );

            enabled = false;
        }
    }

    private void Update()
    {
        /*
         * Cancel an attack immediately if the player becomes
         * movement locked (Bone Prison, hit, death, etc.).
         */
        if (
            isAttacking &&
            IsPlayerActionLocked()
        )
        {
            CancelAttack();
            return;
        }

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
        if (IsPlayerActionLocked())
        {
            return;
        }

        if (isAttacking)
        {
            return;
        }

        if (
            playerSpellcasting != null &&
            playerSpellcasting.IsCasting
        )
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        attackCoroutine =
            StartCoroutine(
                PerformRandomAttack()
            );
    }

    private IEnumerator PerformRandomAttack()
    {
        isAttacking = true;

        animator.ResetTrigger(PunchTrigger);
        animator.ResetTrigger(KickTrigger);

        if (Random.Range(0, 2) == 0)
        {
            animator.SetTrigger(PunchTrigger);
        }
        else
        {
            animator.SetTrigger(KickTrigger);
        }

        yield return new WaitForSeconds(
            attackDuration
        );

        FinishAttack();
    }

    private void FinishAttack()
    {
        if (!isAttacking)
        {
            return;
        }

        isAttacking = false;
        attackCoroutine = null;

        nextAttackTime =
            Time.time + attackCooldown;
    }

    public void CancelAttack()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (animator != null)
        {
            animator.ResetTrigger(PunchTrigger);
            animator.ResetTrigger(KickTrigger);
        }

        isAttacking = false;
    }

    private bool IsPlayerActionLocked()
    {
        return
            playerMovement != null &&
            playerMovement.IsMovementLocked;
    }

    private void OnDisable()
    {
        CancelAttack();
    }

    private void OnValidate()
    {
        attackDuration =
            Mathf.Max(
                0.01f,
                attackDuration
            );

        attackCooldown =
            Mathf.Max(
                0f,
                attackCooldown
            );
    }
}