using System.Collections;
using UnityEngine;

public class BoneField : MonoBehaviour
{
    [Header("Damage Timing")]
    [Tooltip(
        "Delay before the spikes become dangerous. " +
        "Match this to when the spikes emerge."
    )]
    [SerializeField] private float activationDelay = 0.1f;

    [Tooltip(
        "How long the spikes can damage the player."
    )]
    [SerializeField] private float activeDuration = 0.5f;

    [Tooltip(
        "How long the complete Bone Field object remains alive."
    )]
    [SerializeField] private float lifetime = 2f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;

    [Header("References")]
    [Tooltip(
        "Trigger collider covering the dangerous spike radius."
    )]
    [SerializeField] private Collider damageTrigger;

    [Tooltip(
        "Optional particle system responsible for the Bone Field effect."
    )]
    [SerializeField] private ParticleSystem boneFieldParticles;

    private bool canDamage;
    private bool hasDamagedPlayer;
    private bool isEnding;

    private void Awake()
    {
        FindReferences();

        if (damageTrigger == null)
        {
            Debug.LogError(
                $"{name}: BoneField requires a damage trigger collider.",
                this
            );

            enabled = false;
            return;
        }

        damageTrigger.isTrigger = true;
        damageTrigger.enabled = false;
    }

    private void Start()
    {
        StartCoroutine(
            BoneFieldRoutine()
        );
    }

    private void FindReferences()
    {
        if (damageTrigger == null)
        {
            damageTrigger =
                GetComponent<Collider>();

            if (damageTrigger == null)
            {
                damageTrigger =
                    GetComponentInChildren<Collider>();
            }
        }

        if (boneFieldParticles == null)
        {
            boneFieldParticles =
                GetComponent<ParticleSystem>();

            if (boneFieldParticles == null)
            {
                boneFieldParticles =
                    GetComponentInChildren<ParticleSystem>();
            }
        }
    }

    private IEnumerator BoneFieldRoutine()
    {
        if (
            boneFieldParticles != null &&
            !boneFieldParticles.isPlaying
        )
        {
            boneFieldParticles.Play();
        }

        if (activationDelay > 0f)
        {
            yield return new WaitForSeconds(
                activationDelay
            );
        }

        canDamage = true;
        damageTrigger.enabled = true;

        if (activeDuration > 0f)
        {
            yield return new WaitForSeconds(
                activeDuration
            );
        }

        canDamage = false;
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

        EndBoneField();
    }

    private void OnTriggerEnter(
        Collider other
    )
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay(
        Collider other
    )
    {
        /*
         * Required because the trigger may activate while
         * the player is already standing inside the field.
         */
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(
        Collider other
    )
    {
        if (
            !canDamage ||
            hasDamagedPlayer ||
            isEnding
        )
        {
            return;
        }

        PlayerStats playerStats =
            other.GetComponentInParent<PlayerStats>();

        if (playerStats == null)
        {
            return;
        }

        hasDamagedPlayer = true;

        playerStats.TakeDamage(
            damage
        );
    }

    private void EndBoneField()
    {
        if (isEnding)
        {
            return;
        }

        isEnding = true;
        canDamage = false;

        StopAllCoroutines();

        if (damageTrigger != null)
        {
            damageTrigger.enabled = false;
        }

        Destroy(gameObject);
    }

    private void OnValidate()
    {
        activationDelay =
            Mathf.Max(
                0f,
                activationDelay
            );

        activeDuration =
            Mathf.Max(
                0.02f,
                activeDuration
            );

        lifetime =
            Mathf.Max(
                activationDelay +
                activeDuration,
                lifetime
            );

        damage =
            Mathf.Max(
                1,
                damage
            );
    }
}