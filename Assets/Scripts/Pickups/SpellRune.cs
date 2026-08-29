using System.Collections;
using UnityEngine;

public class SpellRune : MonoBehaviour, IInteract, ICheckpointResettable
{
    [Header("Rune Unlock")]
    [Tooltip("The Staff spell unlocked when this rune is activated.")]
    [SerializeField]
    private Player_StaffCombat.StaffSpell spellToUnlock =
        Player_StaffCombat.StaffSpell.LightningStrike;

    [Header("Interaction")]
    [Tooltip(
        "If enabled, the rune plays its activation sequence " +
        "and is destroyed after unlocking its spell."
    )]
    [SerializeField] private bool destroyAfterUnlock = true;

    [Header("Activation Animation")]
    [Tooltip("How long the rune shakes in place before sinking begins.")]
    [SerializeField] private float shakeDuration = 2f;

    [Tooltip("How far the rune moves horizontally while shaking.")]
    [SerializeField] private float shakeStrength = 0.04f;

    [Tooltip("How quickly the shake changes direction.")]
    [SerializeField] private float shakeSpeed = 25f;

    [Tooltip("How long the rune takes to sink into the ground.")]
    [SerializeField] private float sinkDuration = 1.5f;

    [Tooltip("How far downward the rune sinks before being destroyed.")]
    [SerializeField] private float sinkDistance = 2f;

    private bool hasBeenActivated;

    private Vector3 startingPosition;

    private Quaternion startingRotation;

    public bool IsCheckpointAvailable =>
        !hasBeenActivated;

    private void Awake()
    {
        startingPosition =
            transform.position;

        startingRotation =
            transform.rotation;
    }

    public void Interact(PlayerInteraction interactor)
    {
        if (hasBeenActivated)
        {
            return;
        }

        if (interactor == null)
        {
            return;
        }

        Player_StaffCombat staffCombat =
            interactor.GetComponent<Player_StaffCombat>();

        if (staffCombat == null)
        {
            staffCombat =
                interactor.GetComponentInParent<Player_StaffCombat>();
        }

        if (staffCombat == null)
        {
            Debug.LogError(
                $"{name}: Could not find Player_StaffCombat on the player.",
                this
            );

            return;
        }

        /*
         * Staff spells should not be learned before
         * the player has actually collected the Staff.
         */
        Player_WeaponManager weaponManager =
            interactor.GetWeaponManager();

        if (
            weaponManager == null ||
            !weaponManager.HasStaff
        )
        {
            Debug.Log(
                $"{name}: The Staff must be collected before " +
                $"{spellToUnlock} can be unlocked.",
                this
            );

            return;
        }

        /*
         * Ignore interaction if this spell was already unlocked.
         */
        if (staffCombat.IsSpellUnlocked(spellToUnlock))
        {
            hasBeenActivated = true;

            interactor.ClearCurrentInteractable();

            if (destroyAfterUnlock)
            {
                StartCoroutine(
                    PlayActivationSequence()
                );
            }

            return;
        }

        hasBeenActivated = true;

        staffCombat.UnlockSpell(
            spellToUnlock
        );

        interactor.ClearCurrentInteractable();

        Debug.Log(
            $"{name}: Rune activated. " +
            $"{spellToUnlock} unlocked.",
            this
        );

        if (destroyAfterUnlock)
        {
            StartCoroutine(
                PlayActivationSequence()
            );
        }
    }

    private IEnumerator PlayActivationSequence()
    {
        /*
         * Phase 1:
         * Shake in place before sinking.
         */
        float elapsedTime = 0f;

        while (elapsedTime < shakeDuration)
        {
            elapsedTime +=
                Time.deltaTime;

            ApplyShake(
                startingPosition,
                elapsedTime
            );

            yield return null;
        }

        /*
         * Phase 2:
         * Sink into the ground while continuing to shake.
         */
        Vector3 sinkStartPosition =
            startingPosition;

        Vector3 sinkEndPosition =
            sinkStartPosition +
            Vector3.down *
            sinkDistance;

        elapsedTime = 0f;

        while (elapsedTime < sinkDuration)
        {
            elapsedTime +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsedTime /
                    sinkDuration
                );

            Vector3 currentSinkPosition =
                Vector3.Lerp(
                    sinkStartPosition,
                    sinkEndPosition,
                    progress
                );

            ApplyShake(
                currentSinkPosition,
                elapsedTime
            );

            yield return null;
        }

        /*
         * Place the rune fully underground before destroying it.
         */
        transform.position =
            sinkEndPosition;

        gameObject.SetActive(
            false
        );
    }

    private void ApplyShake(
        Vector3 basePosition,
        float elapsedTime
    )
    {
        float shakeX =
            Mathf.Sin(
                elapsedTime *
                shakeSpeed
            ) *
            shakeStrength;

        float shakeZ =
            Mathf.Cos(
                elapsedTime *
                shakeSpeed
            ) *
            shakeStrength;

        transform.position =
            basePosition +
            new Vector3(
                shakeX,
                0f,
                shakeZ
            );
    }

    // =========================================================
    // CHECKPOINT RESTORE
    // =========================================================

    public void RestoreCheckpointState(
        bool wasAvailable
    )
    {
        StopAllCoroutines();

        hasBeenActivated =
            !wasAvailable;

        transform.SetPositionAndRotation(
            startingPosition,
            startingRotation
        );

        gameObject.SetActive(
            wasAvailable
        );
    }

    private void OnValidate()
    {
        shakeDuration =
            Mathf.Max(
                0f,
                shakeDuration
            );

        shakeStrength =
            Mathf.Max(
                0f,
                shakeStrength
            );

        shakeSpeed =
            Mathf.Max(
                0f,
                shakeSpeed
            );

        sinkDuration =
            Mathf.Max(
                0.01f,
                sinkDuration
            );

        sinkDistance =
            Mathf.Max(
                0f,
                sinkDistance
            );
    }
}