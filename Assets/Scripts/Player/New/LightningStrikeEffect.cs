using System.Collections.Generic;
using UnityEngine;

public class LightningStrikeEffect : MonoBehaviour
{
    [Header("Effect")]
    [Tooltip("How long the Lightning Strike object remains in the scene.")]
    [SerializeField] private float lifetime = 2f;

    private bool initialized;

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

        initialized = true;

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
         * Only damage each enemy once per strike.
         */
        HashSet<Enemy> damagedEnemies =
            new HashSet<Enemy>();

        foreach (Collider hitCollider in hitColliders)
        {
            Enemy enemy =
                hitCollider.GetComponentInParent<Enemy>();

            if (
                enemy == null ||
                enemy.IsDead ||
                damagedEnemies.Contains(enemy)
            )
            {
                continue;
            }

            damagedEnemies.Add(
                enemy
            );

            enemy.TakeDamage(
                damage
            );
        }
    }

    private void OnValidate()
    {
        lifetime =
            Mathf.Max(
                0.1f,
                lifetime
            );
    }
}