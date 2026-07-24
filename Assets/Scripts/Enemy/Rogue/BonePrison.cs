using System.Collections;
using UnityEngine;

public class BonePrison : MonoBehaviour
{
    [Header("Capture Timing")]
    [Tooltip("Delay before the prison attempts to capture the player.")]
    [SerializeField] private float activationDelay = 0.1f;

    [Tooltip("Brief window during which the prison can capture the player.")]
    [SerializeField] private float captureWindow = 0.15f;

    [Tooltip("How long a captured player remains unable to move.")]
    [SerializeField] private float trapDuration = 1.75f;

    [Header("References")]
    [Tooltip("Trigger collider covering the inside of the prison.")]
    [SerializeField] private Collider captureTrigger;

    [Tooltip("Particle system responsible for the prison effect.")]
    [SerializeField] private ParticleSystem prisonParticles;

    [Header("Damage")]
    [SerializeField] private bool dealDamageOnCapture;
    [SerializeField] private int captureDamage = 1;

    private PlayerMovement3D trappedPlayer;

    private bool canCapture;
    private bool hasDamagedPlayer;
    private bool isEnding;

    private void Awake()
    {
        FindReferences();

        if (captureTrigger == null)
        {
            Debug.LogError(
                $"{name}: Bone Prison requires a capture trigger collider.",
                this
            );

            enabled = false;
            return;
        }

        if (prisonParticles == null)
        {
            Debug.LogError(
                $"{name}: Bone Prison requires a Particle System.",
                this
            );

            enabled = false;
            return;
        }

        captureTrigger.isTrigger = true;
        captureTrigger.enabled = false;
    }

    private void Start()
    {
        StartCoroutine(PrisonRoutine());
    }

    private void FindReferences()
    {
        if (captureTrigger == null)
        {
            captureTrigger = GetComponent<Collider>();

            if (captureTrigger == null)
            {
                captureTrigger = GetComponentInChildren<Collider>();
            }
        }

        if (prisonParticles == null)
        {
            prisonParticles = GetComponent<ParticleSystem>();

            if (prisonParticles == null)
            {
                prisonParticles = GetComponentInChildren<ParticleSystem>();
            }
        }
    }

    private IEnumerator PrisonRoutine()
    {
        if (!prisonParticles.isPlaying)
        {
            prisonParticles.Play();
        }

        if (activationDelay > 0f)
        {
            yield return new WaitForSeconds(activationDelay);
        }

        canCapture = true;
        captureTrigger.enabled = true;

        yield return new WaitForSeconds(captureWindow);

        canCapture = false;
        captureTrigger.enabled = false;

        ParticleSystem.MainModule main =
            prisonParticles.main;

        float remainingParticleTime =
            Mathf.Max(
                0f,
                main.duration -
                activationDelay -
                captureWindow
            );

        yield return new WaitForSeconds(remainingParticleTime);

        EndPrison();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCapturePlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        /*
         * Needed because the capture trigger may activate while
         * the player is already standing inside it.
         */
        TryCapturePlayer(other);
    }

    private void TryCapturePlayer(Collider other)
    {
        if (!canCapture || isEnding || trappedPlayer != null)
        {
            return;
        }

        PlayerMovement3D playerMovement =
            other.GetComponentInParent<PlayerMovement3D>();

        if (playerMovement == null)
        {
            return;
        }

        trappedPlayer = playerMovement;
        trappedPlayer.AddMovementLock(this);

        StartCoroutine(ReleasePlayerAfterDelay());

        if (dealDamageOnCapture && !hasDamagedPlayer)
        {
            PlayerStats playerStats =
                other.GetComponentInParent<PlayerStats>();

            if (playerStats != null)
            {
                hasDamagedPlayer = true;
                playerStats.TakeDamage(captureDamage);
            }
        }
    }

    private IEnumerator ReleasePlayerAfterDelay()
    {
        yield return new WaitForSeconds(trapDuration);

        ReleasePlayer();
    }

    private void ReleasePlayer()
    {
        if (trappedPlayer == null)
        {
            return;
        }

        trappedPlayer.RemoveMovementLock(this);
        trappedPlayer = null;
    }

    private void EndPrison()
    {
        if (isEnding)
        {
            return;
        }

        isEnding = true;
        canCapture = false;

        StopAllCoroutines();

        if (captureTrigger != null)
        {
            captureTrigger.enabled = false;
        }

        ReleasePlayer();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        /*
         * Safety release if the prison is destroyed externally
         * before its normal routine finishes.
         */
        ReleasePlayer();
    }

    private void OnValidate()
    {
        activationDelay =
            Mathf.Max(0f, activationDelay);

        captureWindow =
            Mathf.Max(0.02f, captureWindow);

        trapDuration =
            Mathf.Max(0.1f, trapDuration);

        captureDamage =
            Mathf.Max(1, captureDamage);
    }
}