using System.Collections;
using UnityEngine;

public class BonePrison : MonoBehaviour
{
    [Header("Capture Timing")]
    [Tooltip(
        "Delay before the prison attempts to capture the player."
    )]
    [SerializeField] private float activationDelay = 0.1f;

    [Tooltip(
        "Brief window during which the prison can capture the player."
    )]
    [SerializeField] private float captureWindow = 0.15f;

    [Tooltip(
        "How long a captured player remains unable to move."
    )]
    [SerializeField] private float trapDuration = 1.75f;

    [Header("References")]
    [Tooltip(
        "Trigger collider covering the inside of the prison."
    )]
    [SerializeField] private Collider captureTrigger;

    [Tooltip(
        "Particle system responsible for the prison effect."
    )]
    [SerializeField] private ParticleSystem prisonParticles;

    [Header("Damage")]
    [SerializeField] private bool dealDamageOnCapture;
    [SerializeField] private int captureDamage = 1;

    private PlayerMovement3DNew trappedPlayer;
    private PlayerStatsNew trappedPlayerStats;

    private bool canCapture;
    private bool hasDamagedPlayer;
    private bool isEnding;
    private bool visualLifetimeFinished;

    private Coroutine releaseCoroutine;

    private void Awake()
    {
        FindReferences();

        if (captureTrigger == null)
        {
            Debug.LogError(
                $"{name}: Bone Prison requires a capture trigger collider.",
                this
            );

            enabled =
                false;

            return;
        }

        if (prisonParticles == null)
        {
            Debug.LogError(
                $"{name}: Bone Prison requires a Particle System.",
                this
            );

            enabled =
                false;

            return;
        }

        captureTrigger.isTrigger =
            true;

        captureTrigger.enabled =
            false;
    }

    private void Start()
    {
        StartCoroutine(
            PrisonRoutine()
        );
    }

    private void FindReferences()
    {
        if (captureTrigger == null)
        {
            captureTrigger =
                GetComponent<Collider>();

            if (captureTrigger == null)
            {
                captureTrigger =
                    GetComponentInChildren<Collider>();
            }
        }

        if (prisonParticles == null)
        {
            prisonParticles =
                GetComponent<ParticleSystem>();

            if (prisonParticles == null)
            {
                prisonParticles =
                    GetComponentInChildren<ParticleSystem>();
            }
        }
    }

    // =========================================================
    // PRISON LIFETIME
    // =========================================================

    private IEnumerator PrisonRoutine()
    {
        if (!prisonParticles.isPlaying)
        {
            prisonParticles.Play();
        }

        if (activationDelay > 0f)
        {
            yield return new WaitForSeconds(
                activationDelay
            );
        }

        canCapture =
            true;

        captureTrigger.enabled =
            true;

        /*
         * Make Unity acknowledge the newly enabled
         * trigger before checking its bounds.
         */
        Physics.SyncTransforms();

        /*
         * IMPORTANT:
         *
         * Bone Prison normally spawns around the player,
         * meaning the player may already be inside the
         * collider when it becomes active.
         *
         * Don't rely only on OnTriggerEnter.
         */
        CheckForPlayerImmediately();

        yield return new WaitForSeconds(
            captureWindow
        );

        canCapture =
            false;

        captureTrigger.enabled =
            false;

        ParticleSystem.MainModule main =
            prisonParticles.main;

        float remainingParticleTime =
            Mathf.Max(
                0f,
                main.duration -
                activationDelay -
                captureWindow
            );

        if (remainingParticleTime > 0f)
        {
            yield return new WaitForSeconds(
                remainingParticleTime
            );
        }

        visualLifetimeFinished =
            true;

        /*
         * If nobody was captured, we're done.
         *
         * If somebody IS trapped, wait until their full
         * trap duration finishes before destroying this
         * object.
         */
        if (trappedPlayer == null)
        {
            EndPrison();
        }
    }

    // =========================================================
    // IMMEDIATE OVERLAP CHECK
    // =========================================================

    private void CheckForPlayerImmediately()
    {
        if (
            !canCapture ||
            captureTrigger == null ||
            trappedPlayer != null
        )
        {
            return;
        }

        Bounds triggerBounds =
            captureTrigger.bounds;

        Collider[] overlappingColliders =
            Physics.OverlapBox(
                triggerBounds.center,
                triggerBounds.extents,
                Quaternion.identity,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide
            );

        foreach (
            Collider overlappingCollider
            in overlappingColliders
        )
        {
            TryCapturePlayer(
                overlappingCollider
            );

            if (trappedPlayer != null)
            {
                return;
            }
        }
    }

    // =========================================================
    // TRIGGER CAPTURE
    // =========================================================

    private void OnTriggerEnter(
        Collider other
    )
    {
        TryCapturePlayer(
            other
        );
    }

    private void OnTriggerStay(
        Collider other
    )
    {
        TryCapturePlayer(
            other
        );
    }

    private void TryCapturePlayer(
        Collider other
    )
    {
        if (
            !canCapture ||
            isEnding ||
            trappedPlayer != null
        )
        {
            return;
        }

        PlayerMovement3DNew playerMovement =
            other.GetComponentInParent<PlayerMovement3DNew>();

        if (playerMovement == null)
        {
            return;
        }

        trappedPlayer =
            playerMovement;

        trappedPlayerStats =
            other.GetComponentInParent<PlayerStatsNew>();

        if (trappedPlayerStats == null)
        {
            trappedPlayerStats =
                trappedPlayer.GetComponent<PlayerStatsNew>();
        }

        /*
         * Stop the Player immediately.
         */
        trappedPlayer.StopMovementImmediately();

        /*
         * Bone Prison owns this movement lock.
         */
        trappedPlayer.AddMovementLock(
            this
        );

        if (
            dealDamageOnCapture &&
            !hasDamagedPlayer &&
            trappedPlayerStats != null
        )
        {
            hasDamagedPlayer =
                true;

            trappedPlayerStats.TakeDamage(
                captureDamage
            );
        }

        if (trappedPlayerStats != null)
        {
            trappedPlayerStats.StartBonePrisonReaction();
        }

        if (releaseCoroutine != null)
        {
            StopCoroutine(
                releaseCoroutine
            );
        }

        releaseCoroutine =
            StartCoroutine(
                ReleasePlayerAfterDelay()
            );

        Debug.Log(
            $"{name}: Player captured by Bone Prison.",
            this
        );
    }

    // =========================================================
    // RELEASE
    // =========================================================

    private IEnumerator ReleasePlayerAfterDelay()
    {
        yield return new WaitForSeconds(
            trapDuration
        );

        releaseCoroutine =
            null;

        ReleasePlayer();

        /*
         * If the visual portion has already finished,
         * the Prison can now clean itself up.
         */
        if (visualLifetimeFinished)
        {
            EndPrison();
        }
    }

    private void ReleasePlayer()
    {
        if (trappedPlayer == null)
        {
            return;
        }

        if (trappedPlayerStats != null)
        {
            trappedPlayerStats.EndBonePrisonReaction();
        }

        trappedPlayer.RemoveMovementLock(
            this
        );

        Debug.Log(
            $"{name}: Player released from Bone Prison.",
            this
        );

        trappedPlayerStats =
            null;

        trappedPlayer =
            null;
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void EndPrison()
    {
        if (isEnding)
        {
            return;
        }

        isEnding =
            true;

        canCapture =
            false;

        if (captureTrigger != null)
        {
            captureTrigger.enabled =
                false;
        }

        /*
         * Safety cleanup.
         *
         * Under normal circumstances the trapped player
         * has already completed their trap duration.
         */
        ReleasePlayer();

        Destroy(
            gameObject
        );
    }

    private void OnDestroy()
    {
        /*
         * Never allow a destroyed Prison object to leave
         * its movement lock behind.
         */
        ReleasePlayer();
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

        captureWindow =
            Mathf.Max(
                0.02f,
                captureWindow
            );

        trapDuration =
            Mathf.Max(
                0.1f,
                trapDuration
            );

        captureDamage =
            Mathf.Max(
                1,
                captureDamage
            );
    }
}