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
        "How long the visual remains before being destroyed."
    )]
    [SerializeField]
    private float lifetime = 2.5f;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private int damage;
    private float damageRadius;

    private bool initialized;
    private bool damageApplied;

    // =========================================================
    // INITIALIZATION
    // =========================================================

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

        StartCoroutine(
            EffectRoutine()
        );
    }

    // =========================================================
    // EFFECT ROUTINE
    // =========================================================

    private IEnumerator EffectRoutine()
    {
        /*
         * DeathEvil now activates immediately when spawned.
         * The player's warning window comes from the Rogue's
         * attack animation rather than an additional effect delay.
         */
        ApplyDamage();

        if (lifetime > 0f)
        {
            yield return new WaitForSeconds(
                lifetime
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
        lifetime =
            Mathf.Max(
                0.1f,
                lifetime
            );
    }
}