using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathEvilEffect : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip(
        "Delay before DeathEvil actually deals damage. " +
        "This gives the player a brief chance to escape."
    )]
    [SerializeField] private float activationDelay = 0.5f;

    [Tooltip(
        "How long the visual remains before being destroyed."
    )]
    [SerializeField] private float lifetime = 2.5f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;

    [Tooltip(
        "Radius around the centre of DeathEvil that " +
        "damages the player."
    )]
    [SerializeField] private float damageRadius = 2f;

    [Header("Ground")]
    [Tooltip(
        "Optional vertical offset above the ground."
    )]
    [SerializeField] private float groundOffset = 0.05f;

    private bool initialized;
    private bool damageApplied;

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
         * A CharacterController/player may have several
         * colliders, so make sure the same PlayerStatsNew
         * is only damaged once.
         */
        HashSet<PlayerStatsNew> damagedPlayers =
            new HashSet<PlayerStatsNew>();

        foreach (Collider hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            PlayerStatsNew playerStats =
                hit.GetComponentInParent<PlayerStatsNew>();

            if (
                playerStats == null ||
                playerStats.IsDead ||
                damagedPlayers.Contains(playerStats)
            )
            {
                continue;
            }

            damagedPlayers.Add(
                playerStats
            );

            playerStats.TakeDamage(
                damage
            );

            Debug.Log(
                $"{name}: DeathEvil damaged {playerStats.name}.",
                this
            );
        }
    }

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            damageRadius
        );
    }
}