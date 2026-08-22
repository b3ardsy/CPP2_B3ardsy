using System.Collections.Generic;
using UnityEngine;

public class LightningStrikeEffect : MonoBehaviour
{
    [Header("Effect")]
    [Tooltip(
        "How long the Lightning Strike object remains in the scene."
    )]
    [SerializeField]
    private float lifetime = 2f;

    private bool initialized;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    public void Initialize(
        int damage,
        float damageRadius,
        LayerMask enemyLayer
    )
    {
        if (initialized)
        {
            return;
        }

        initialized =
            true;

        ApplyDamage(
            damage,
            damageRadius,
            enemyLayer
        );

        Destroy(
            gameObject,
            lifetime
        );
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    private void ApplyDamage(
        int damage,
        float damageRadius,
        LayerMask enemyLayer
    )
    {
        Collider[] hitColliders =
            Physics.OverlapSphere(
                transform.position,
                damageRadius,
                enemyLayer,
                QueryTriggerInteraction.Ignore
            );

        /*
         * An enemy may contain multiple colliders.
         *
         * Store the MonoBehaviour implementing IDamageable
         * so each target only receives one hit per strike.
         *
         * This supports both:
         * - EnemyController (new architecture)
         * - Enemy (legacy Rogue/Tank during migration)
         */
        HashSet<MonoBehaviour> damagedTargets =
            new HashSet<MonoBehaviour>();

        foreach (
            Collider hitCollider
            in hitColliders
        )
        {
            if (
                !TryFindDamageable(
                    hitCollider,
                    out IDamageable damageable,
                    out MonoBehaviour damageableBehaviour
                )
            )
            {
                continue;
            }

            if (
                damagedTargets.Contains(
                    damageableBehaviour
                )
            )
            {
                continue;
            }

            damagedTargets.Add(
                damageableBehaviour
            );

            damageable.TakeDamage(
                damage
            );
        }
    }

    // =========================================================
    // DAMAGEABLE LOOKUP
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

        MonoBehaviour[] behaviours =
            other.GetComponentsInParent<MonoBehaviour>(
                true
            );

        foreach (
            MonoBehaviour behaviour
            in behaviours
        )
        {
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