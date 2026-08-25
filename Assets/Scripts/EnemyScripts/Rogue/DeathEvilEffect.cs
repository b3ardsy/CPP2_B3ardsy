using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathEvilEffect : MonoBehaviour
{
    // =========================================================
    // TIMING
    // =========================================================

    [Header("Timing")]
    [Tooltip(
        "Delay before DeathEvil actually deals damage. " +
        "This gives the player a brief chance to escape."
    )]
    [SerializeField]
    private float activationDelay = 0.5f;

    [Tooltip(
        "How long the visual remains before being destroyed."
    )]
    [SerializeField]
    private float lifetime = 2.5f;

    // =========================================================
    // DAMAGE
    // =========================================================

    [Header("Damage")]
    [SerializeField]
    private int damage = 1;

    [Tooltip(
        "Radius around the centre of DeathEvil that " +
        "damages the player."
    )]
    [SerializeField]
    private float damageRadius = 2f;

    // =========================================================
    // GROUND
    // =========================================================

    [Header("Ground")]
    [Tooltip(
        "Optional vertical offset above the ground."
    )]
    [SerializeField]
    private float groundOffset = 0.05f;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private bool initialized;
    private bool damageApplied;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Start()
    {
        if (!initialized)
        {
            Initialize(
                damage,
                damageRadius
            );
        }
    }

    public void Initialize(
        int effectDamage,
        float effectRadius
    )
    {
        if (initialized)
        {
            return;
        }

        initialized =
            true;

        damage =
            Mathf.Max(
                1,
                effectDamage
            );

        damageRadius =
            Mathf.Max(
                0.1f,
                effectRadius
            );

        /*
         * Keep the gas slightly above the ground
         * to avoid visual clipping.
         */
        transform.position +=
            Vector3.up *
            groundOffset;

        StartCoroutine(
            EffectRoutine()
        );
    }

    // =========================================================
    // EFFECT ROUTINE
    // =========================================================

    private IEnumerator EffectRoutine()
    {
        if (activationDelay > 0f)
        {
            yield return new WaitForSeconds(
                activationDelay
            );
        }

        ApplyDamage();

        float remainingLifetime =
            Mathf.Max(
                0f,
                lifetime -
                activationDelay
            );

        if (remainingLifetime > 0f)
        {
            yield return new WaitForSeconds(
                remainingLifetime
            );
        }

        Destroy(
            gameObject
        );
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    private void ApplyDamage()
    {
        if (damageApplied)
        {
            return;
        }

        damageApplied =
            true;

        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                damageRadius,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore
            );

        /*
         * The player may have several colliders.
         *
         * Store each IDamageable target that has already
         * received damage so the same target cannot be
         * damaged more than once by this effect.
         */
        HashSet<IDamageable> damagedTargets =
            new HashSet<IDamageable>();

        foreach (
            Collider hit
            in hits
        )
        {
            if (hit == null)
            {
                continue;
            }

            if (
                !TryFindDamageable(
                    hit,
                    out IDamageable damageable,
                    out MonoBehaviour damageableBehaviour
                )
            )
            {
                continue;
            }

            /*
             * DeathEvil is an enemy attack and should
             * damage only the Player.
             */
            if (
                !IsPlayerDamageable(
                    damageableBehaviour
                )
            )
            {
                continue;
            }

            /*
             * Multiple player colliders may resolve to
             * the same IDamageable component.
             */
            if (
                damagedTargets.Contains(
                    damageable
                )
            )
            {
                continue;
            }

            damagedTargets.Add(
                damageable
            );

            damageable.TakeDamage(
                damage
            );

            Debug.Log(
                $"{name}: DeathEvil damaged " +
                $"{damageableBehaviour.name}.",
                this
            );
        }
    }

    // =========================================================
    // DAMAGEABLE HELPERS
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

        /*
         * Search the collider and its parent hierarchy
         * for a MonoBehaviour implementing IDamageable.
         *
         * For the Player this currently resolves to
         * PlayerStatsNew.
         *
         * After we rename/refactor that component,
         * DeathEvil will not need to change.
         */
        MonoBehaviour[] behaviours =
            other.GetComponentsInParent<MonoBehaviour>(
                true
            );

        foreach (
            MonoBehaviour behaviour
            in behaviours
        )
        {
            if (behaviour == null)
            {
                continue;
            }

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

    private bool IsPlayerDamageable(
        MonoBehaviour damageableBehaviour
    )
    {
        if (damageableBehaviour == null)
        {
            return false;
        }

        Transform currentTransform =
            damageableBehaviour.transform;

        /*
         * Walk upward so this remains valid even if
         * the IDamageable component is later moved
         * onto a child object.
         */
        while (currentTransform != null)
        {
            if (
                currentTransform.CompareTag(
                    "Player"
                )
            )
            {
                return true;
            }

            currentTransform =
                currentTransform.parent;
        }

        return false;
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        activationDelay =
            Mathf.Max(
                0f,
                activationDelay
            );

        lifetime =
            Mathf.Max(
                activationDelay + 0.1f,
                lifetime
            );

        damage =
            Mathf.Max(
                1,
                damage
            );

        damageRadius =
            Mathf.Max(
                0.1f,
                damageRadius
            );

        groundOffset =
            Mathf.Max(
                0f,
                groundOffset
            );
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            damageRadius
        );
    }
}