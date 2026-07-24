using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoneCleaveSegment : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;

    [Tooltip("Delay before this segment can deal damage.")]
    [SerializeField] private float activationDelay = 0.05f;

    [Tooltip("How long the damage collider remains active.")]
    [SerializeField] private float activeDuration = 0.35f;

    [Tooltip("Total time before the segment destroys itself.")]
    [SerializeField] private float lifetime = 1.5f;

    private readonly HashSet<PlayerStats> damagedPlayers =
        new HashSet<PlayerStats>();

    private Collider damageTrigger;
    private GameObject owner;
    private bool canDealDamage;

    private void Awake()
    {
        damageTrigger = GetComponent<Collider>();

        if (damageTrigger == null)
        {
            damageTrigger = GetComponentInChildren<Collider>();
        }

        if (damageTrigger == null)
        {
            Debug.LogError(
                $"{name}: Bone Cleave Segment requires a Collider.",
                this
            );

            enabled = false;
            return;
        }

        damageTrigger.isTrigger = true;
        damageTrigger.enabled = false;
    }

    public void Initialize(GameObject spellOwner)
    {
        owner = spellOwner;
        StartCoroutine(SegmentRoutine());
    }

    private IEnumerator SegmentRoutine()
    {
        yield return new WaitForSeconds(activationDelay);

        canDealDamage = true;
        damageTrigger.enabled = true;

        yield return new WaitForSeconds(activeDuration);

        canDealDamage = false;
        damageTrigger.enabled = false;

        float remainingLifetime =
            lifetime -
            activationDelay -
            activeDuration;

        if (remainingLifetime > 0f)
        {
            yield return new WaitForSeconds(
                remainingLifetime
            );
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canDealDamage)
        {
            return;
        }

        if (owner != null &&
            other.transform.root.gameObject == owner)
        {
            return;
        }

        PlayerStats playerStats =
            other.GetComponentInParent<PlayerStats>();

        if (playerStats == null || playerStats.IsDead)
        {
            return;
        }

        if (!damagedPlayers.Add(playerStats))
        {
            return;
        }

        playerStats.TakeDamage(damage);
    }

    private void OnValidate()
    {
        damage = Mathf.Max(1, damage);
        activationDelay = Mathf.Max(0f, activationDelay);
        activeDuration = Mathf.Max(0.01f, activeDuration);

        lifetime = Mathf.Max(
            activationDelay + activeDuration,
            lifetime
        );
    }
}