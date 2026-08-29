using System;
using UnityEngine;

public class Player_StaffCombat : MonoBehaviour
{
    public enum StaffSpell
    {
        Entangle,
        Flamethrower,
        IceTornado,
        LightningStrike
    }

    [Serializable]
    public struct SpellProgressionState
    {
        public bool lightningUnlocked;
        public bool iceTornadoUnlocked;
        public bool entangleUnlocked;

        public SpellProgressionState(
            bool lightning,
            bool iceTornado,
            bool entangle
        )
        {
            lightningUnlocked = lightning;
            iceTornadoUnlocked = iceTornado;
            entangleUnlocked = entangle;
        }
    }

    public event Action<StaffSpell> OnSpellUnlocked;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Player_Controller playerController;
    [SerializeField] private Player_LockOn playerLockOn;

    [Header("Staff")]
    [Tooltip(
        "Spawn point used by Staff spells that originate " +
        "directly from the Staff."
    )]
    [SerializeField] private Transform staffFirePoint;

    // =========================================================
    // SPELL SLOTS
    // =========================================================

    [Header("Spell Slots")]
    [Tooltip("Spell cast immediately when the player presses 1.")]
    [SerializeField]
    private StaffSpell slot1Spell =
        StaffSpell.LightningStrike;

    [Tooltip("Spell cast immediately when the player presses 2.")]
    [SerializeField]
    private StaffSpell slot2Spell =
        StaffSpell.IceTornado;

    [Tooltip("Spell cast immediately when the player presses 3.")]
    [SerializeField]
    private StaffSpell slot3Spell =
        StaffSpell.Entangle;

    // =========================================================
    // SPELL PROGRESSION
    // =========================================================

    [Header("Spell Progression")]
    [Tooltip("Useful for testing. Normally leave these disabled.")]
    [SerializeField] private bool startWithLightningUnlocked;

    [SerializeField] private bool startWithIceTornadoUnlocked;

    [SerializeField] private bool startWithEntangleUnlocked;

    private bool lightningUnlocked;
    private bool iceTornadoUnlocked;
    private bool entangleUnlocked;

    // =========================================================
    // COOLDOWNS
    // =========================================================

    [Header("Spell Cooldowns")]
    [SerializeField] private float entangleCooldown = 8f;
    [SerializeField] private float flamethrowerCooldown = 5f;
    [SerializeField] private float iceTornadoCooldown = 5f;
    [SerializeField] private float lightningStrikeCooldown = 5f;

    // =========================================================
    // ENTANGLE
    // =========================================================

    [Header("Entangle")]
    [Tooltip("Vine visual spawned on the target while Entangled.")]
    [SerializeField]
    private GameObject entanglePrefab;

    [Tooltip("How long the enemy remains immobilized.")]
    [SerializeField]
    private float entangleDuration = 5f;

    [Tooltip(
        "Local position adjustment for the vine effect " +
        "after it is parented to the enemy."
    )]
    [SerializeField]
    private Vector3 entangleLocalOffset =
        Vector3.zero;

    [Tooltip("How far in front of the player a missed Entangle appears.")]
    [SerializeField]
    private float entangleMissDistance = 6f;

    [Tooltip("Height used when checking the ground for a missed Entangle.")]
    [SerializeField]
    private float entangleGroundCheckHeight = 5f;

    [Tooltip("Small vertical offset above the ground for a missed Entangle.")]
    [SerializeField]
    private float entangleGroundOffset = 0.05f;

    // =========================================================
    // ICE TORNADO
    // =========================================================

    [Header("Ice Tornado")]
    [SerializeField]
    private IceTornadoProjectile iceTornadoPrefab;

    [SerializeField]
    private int iceTornadoDamage = 1;

    [SerializeField]
    private float iceTornadoSpeed = 10f;

    [SerializeField]
    private float iceTornadoSpawnDistance = 1.5f;

    [SerializeField]
    private float iceTornadoGroundCheckHeight = 3f;

    [SerializeField]
    private float iceTornadoGroundOffset = 0.05f;

    [SerializeField]
    private Vector3 iceTornadoRotationOffset =
        new Vector3(-90f, 0f, 0f);

    // =========================================================
    // LIGHTNING STRIKE
    // =========================================================

    [Header("Lightning Strike")]
    [SerializeField]
    private LightningStrikeEffect lightningStrikePrefab;

    [SerializeField]
    private int lightningStrikeDamage = 2;

    [SerializeField]
    private float lightningStrikeDamageRadius = 1.25f;

    [SerializeField]
    private float lightningStrikeRange = 6f;

    [SerializeField]
    private float lightningGroundCheckHeight = 5f;

    [SerializeField]
    private float lightningGroundOffset = 0.05f;

    [SerializeField]
    private LayerMask enemyLayer;

    // =========================================================
    // SHARED
    // =========================================================

    [Header("Ground Detection")]
    [SerializeField]
    private LayerMask groundLayer;

    [Header("Aiming")]
    [SerializeField]
    private bool usePlayerForwardWhenUnlocked = true;

    private bool isCasting;

    /*
     * The spell that began the current cast.
     * This is captured before the animation plays.
     */
    private StaffSpell activeSpell;

    /*
     * Entangle captures its target when casting begins,
     * preventing lock-on changes during the animation
     * from redirecting the spell.
     */
    /*
     * Temporary migration support:
     *
     * Mage now uses EnemyController.
     * Rogue and Tank still use the legacy Enemy component
     * until their migrations are complete.
     */
    private EnemyController pendingEntangleController;
    private Enemy pendingLegacyEntangleTarget;

    private float nextEntangleTime;
    private float nextFlamethrowerTime;
    private float nextIceTornadoTime;
    private float nextLightningStrikeTime;

    public bool IsCasting =>
        isCasting;

    public StaffSpell Slot1Spell =>
        slot1Spell;

    public StaffSpell Slot2Spell =>
        slot2Spell;

    public StaffSpell Slot3Spell =>
        slot3Spell;

    private static readonly int MagicSummonTrigger =
        Animator.StringToHash("MagicSummon");

    private void Awake()
    {
        FindReferences();
        ValidateReferences();

        lightningUnlocked =
            startWithLightningUnlocked;

        iceTornadoUnlocked =
            startWithIceTornadoUnlocked;

        entangleUnlocked =
            startWithEntangleUnlocked;
    }

    // =========================================================
    // SPELL UNLOCKING
    // =========================================================

    public bool IsSpellUnlocked(StaffSpell spell)
    {
        switch (spell)
        {
            case StaffSpell.LightningStrike:
                return lightningUnlocked;

            case StaffSpell.IceTornado:
                return iceTornadoUnlocked;

            case StaffSpell.Entangle:
                return entangleUnlocked;

            case StaffSpell.Flamethrower:
                return false;

            default:
                return false;
        }
    }

    public void UnlockSpell(StaffSpell spell)
    {
        if (IsSpellUnlocked(spell))
        {
            return;
        }

        switch (spell)
        {
            case StaffSpell.LightningStrike:
                lightningUnlocked = true;
                break;

            case StaffSpell.IceTornado:
                iceTornadoUnlocked = true;
                break;

            case StaffSpell.Entangle:
                entangleUnlocked = true;
                break;

            case StaffSpell.Flamethrower:
                Debug.LogWarning(
                    $"{name}: Flamethrower cannot be unlocked " +
                    "because it is not currently implemented.",
                    this
                );

                return;

            default:
                return;
        }

        OnSpellUnlocked?.Invoke(spell);

        Debug.Log(
            $"{name}: {spell} unlocked.",
            this
        );
    }

    public SpellProgressionState CaptureProgressionState()
    {
        return new SpellProgressionState(
            lightningUnlocked,
            iceTornadoUnlocked,
            entangleUnlocked
        );
    }

    public void RestoreProgressionState(
        SpellProgressionState state
    )
    {
        lightningUnlocked =
            state.lightningUnlocked;

        iceTornadoUnlocked =
            state.iceTornadoUnlocked;

        entangleUnlocked =
            state.entangleUnlocked;
    }

    // =========================================================
    // SPELL SLOT CASTING
    // =========================================================

    public void TryCastSpellSlot(int slotNumber)
    {
        StaffSpell spell;

        switch (slotNumber)
        {
            case 1:
                spell = slot1Spell;
                break;

            case 2:
                spell = slot2Spell;
                break;

            case 3:
                spell = slot3Spell;
                break;

            default:
                Debug.LogWarning(
                    $"{name}: Invalid Staff spell slot {slotNumber}.",
                    this
                );

                return;
        }

        TryCastSpell(spell);
    }

    public StaffSpell GetSpellForSlot(int slotNumber)
    {
        switch (slotNumber)
        {
            case 1:
                return slot1Spell;

            case 2:
                return slot2Spell;

            case 3:
                return slot3Spell;

            default:
                return slot1Spell;
        }
    }

    private void TryCastSpell(StaffSpell spell)
    {
        if (
            IsPlayerActionLocked() ||
            isCasting
        )
        {
            return;
        }

        if (!IsSpellUnlocked(spell))
        {
            Debug.Log(
                $"{name}: {spell} has not been unlocked yet.",
                this
            );

            return;
        }

        if (!IsSpellReady(spell))
        {
            Debug.Log(
                $"{name}: {spell} is on cooldown for " +
                $"{GetRemainingCooldown(spell):0.0} more seconds.",
                this
            );

            return;
        }

        switch (spell)
        {
            case StaffSpell.Entangle:
                TryBeginEntangle();
                break;

            case StaffSpell.Flamethrower:
                LogSpellNotImplemented(spell);
                break;

            case StaffSpell.IceTornado:
                TryBeginIceTornado();
                break;

            case StaffSpell.LightningStrike:
                TryBeginLightningStrike();
                break;
        }
    }

    // =========================================================
    // CASTING
    // =========================================================

    private void BeginStaffCast(StaffSpell spell)
    {
        if (animator == null)
        {
            return;
        }

        activeSpell = spell;
        isCasting = true;

        animator.ResetTrigger(
            MagicSummonTrigger
        );

        animator.SetTrigger(
            MagicSummonTrigger
        );
    }

    /*
     * Animation Event on the player's Magic Summon animation.
     */
    public void StaffSpellActivate()
    {
        if (!isCasting)
        {
            return;
        }

        if (IsPlayerActionLocked())
        {
            CancelStaffCast();
            return;
        }

        switch (activeSpell)
        {
            case StaffSpell.Entangle:

                if (ReleaseEntangle())
                {
                    StartCooldown(
                        StaffSpell.Entangle
                    );
                }

                break;

            case StaffSpell.Flamethrower:
                break;

            case StaffSpell.IceTornado:

                if (ReleaseIceTornado())
                {
                    StartCooldown(
                        StaffSpell.IceTornado
                    );
                }

                break;

            case StaffSpell.LightningStrike:

                if (ReleaseLightningStrike())
                {
                    StartCooldown(
                        StaffSpell.LightningStrike
                    );
                }

                break;
        }
    }

    public void EndStaffCast()
    {
        isCasting = false;
        pendingEntangleController = null;
        pendingLegacyEntangleTarget = null;
    }

    public void CancelStaffCast()
    {
        isCasting = false;
        pendingEntangleController = null;
        pendingLegacyEntangleTarget = null;

        if (animator != null)
        {
            animator.ResetTrigger(
                MagicSummonTrigger
            );
        }
    }

    // =========================================================
    // ENTANGLE
    // =========================================================

    private void TryBeginEntangle()
    {
        if (entanglePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Entangle cannot cast because " +
                "the Entangle Prefab is missing.",
                this
            );

            return;
        }

        /*
         * Clear any previous target before beginning
         * a new Entangle cast.
         */
        pendingEntangleController = null;
        pendingLegacyEntangleTarget = null;

        /*
         * Capture the current locked target if one exists.
         *
         * During the enemy migration, lock-on can point at:
         *
         * - EnemyController for migrated enemies such as Mage
         * - Enemy for legacy Rogue/Tank enemies
         *
         * Capture whichever one is currently active so changing
         * lock-on during the cast animation cannot redirect Entangle.
         */
        if (
            playerLockOn != null &&
            playerLockOn.IsLockedOn
        )
        {
            EnemyController controllerTarget =
                playerLockOn.CurrentTargetController;

            if (
                controllerTarget != null &&
                !controllerTarget.IsDead
            )
            {
                pendingEntangleController =
                    controllerTarget;
            }
            else
            {
                Enemy legacyTarget =
                    playerLockOn.CurrentTarget;

                if (
                    legacyTarget != null &&
                    !legacyTarget.IsDead
                )
                {
                    pendingLegacyEntangleTarget =
                        legacyTarget;
                }
            }
        }

        BeginStaffCast(
            StaffSpell.Entangle
        );
    }

    private bool ReleaseEntangle()
    {
        /*
         * First support the new EnemyController architecture.
         */
        if (
            pendingEntangleController != null &&
            !pendingEntangleController.IsDead &&
            entanglePrefab != null
        )
        {
            pendingEntangleController.ApplyEntangle(
                entangleDuration,
                entanglePrefab,
                entangleLocalOffset
            );

            Debug.Log(
                $"{name}: Entangle cast on " +
                $"{pendingEntangleController.name}.",
                this
            );

            pendingEntangleController = null;
            pendingLegacyEntangleTarget = null;

            return true;
        }

        /*
         * Temporary fallback for Rogue/Tank while they still
         * use the legacy Enemy component.
         */
        if (
            pendingLegacyEntangleTarget != null &&
            !pendingLegacyEntangleTarget.IsDead &&
            entanglePrefab != null
        )
        {
            pendingLegacyEntangleTarget.ApplyEntangle(
                entangleDuration,
                entanglePrefab,
                entangleLocalOffset
            );

            Debug.Log(
                $"{name}: Entangle cast on " +
                $"{pendingLegacyEntangleTarget.name}.",
                this
            );

            pendingEntangleController = null;
            pendingLegacyEntangleTarget = null;

            return true;
        }

        /*
         * No valid locked target:
         * spawn the Entangle visual on the ground in front
         * of the player so the spell still visibly fires.
         *
         * This missed visual does not affect an enemy.
         */
        if (entanglePrefab != null)
        {
            Vector3 intendedPosition =
                transform.position +
                GetFlatForwardDirection() *
                entangleMissDistance;

            Vector3 spawnPosition =
                GetGroundPosition(
                    intendedPosition,
                    entangleGroundCheckHeight,
                    entangleGroundOffset
                );

            GameObject missedEntangle =
                Instantiate(
                    entanglePrefab,
                    spawnPosition,
                    entanglePrefab.transform.rotation
                );

            Destroy(
                missedEntangle,
                entangleDuration
            );
        }

        pendingEntangleController = null;
        pendingLegacyEntangleTarget = null;

        Debug.Log(
            $"{name}: Entangle missed because there was " +
            "no valid locked target.",
            this
        );

        /*
         * The spell was still used, so return true.
         * StaffSpellActivate will consume the cooldown.
         */
        return true;
    }

    // =========================================================
    // ICE TORNADO
    // =========================================================

    private void TryBeginIceTornado()
    {
        if (iceTornadoPrefab == null)
        {
            return;
        }

        BeginStaffCast(
            StaffSpell.IceTornado
        );
    }

    private bool ReleaseIceTornado()
    {
        if (iceTornadoPrefab == null)
        {
            return false;
        }

        Vector3 spawnPosition =
            CalculateIceTornadoSpawnPosition();

        Vector3 fireDirection =
            CalculateIceTornadoDirection(
                spawnPosition
            );

        fireDirection.y = 0f;

        if (
            fireDirection.sqrMagnitude <=
            0.001f
        )
        {
            fireDirection =
                GetFlatForwardDirection();
        }

        fireDirection.Normalize();

        Quaternion directionRotation =
            Quaternion.LookRotation(
                fireDirection,
                Vector3.up
            );

        Quaternion rotationOffset =
            Quaternion.Euler(
                iceTornadoRotationOffset
            );

        IceTornadoProjectile tornado =
            Instantiate(
                iceTornadoPrefab,
                spawnPosition,
                directionRotation *
                rotationOffset
            );

        tornado.Initialize(
            gameObject,
            fireDirection,
            iceTornadoDamage,
            iceTornadoSpeed
        );

        return true;
    }

    private Vector3 CalculateIceTornadoSpawnPosition()
    {
        Vector3 intendedPosition =
            transform.position +
            GetFlatForwardDirection() *
            iceTornadoSpawnDistance;

        return GetGroundPosition(
            intendedPosition,
            iceTornadoGroundCheckHeight,
            iceTornadoGroundOffset
        );
    }

    private Vector3 CalculateIceTornadoDirection(
        Vector3 spawnPosition
    )
    {
        if (
            playerLockOn != null &&
            playerLockOn.IsLockedOn
        )
        {
            Vector3 directionToTarget =
                playerLockOn.CurrentTargetPosition -
                spawnPosition;

            directionToTarget.y = 0f;

            if (
                directionToTarget.sqrMagnitude >
                0.001f
            )
            {
                return
                    directionToTarget.normalized;
            }
        }

        if (usePlayerForwardWhenUnlocked)
        {
            return
                GetFlatForwardDirection();
        }

        if (staffFirePoint != null)
        {
            Vector3 staffDirection =
                staffFirePoint.forward;

            staffDirection.y = 0f;

            if (
                staffDirection.sqrMagnitude >
                0.001f
            )
            {
                return
                    staffDirection.normalized;
            }
        }

        return
            GetFlatForwardDirection();
    }

    // =========================================================
    // LIGHTNING STRIKE
    // =========================================================

    private void TryBeginLightningStrike()
    {
        if (lightningStrikePrefab == null)
        {
            return;
        }

        BeginStaffCast(
            StaffSpell.LightningStrike
        );
    }

    private bool ReleaseLightningStrike()
    {
        if (lightningStrikePrefab == null)
        {
            return false;
        }

        Vector3 strikePosition =
            CalculateLightningStrikePosition();

        Quaternion strikeRotation =
            lightningStrikePrefab.transform.rotation;

        LightningStrikeEffect strike =
            Instantiate(
                lightningStrikePrefab,
                strikePosition,
                strikeRotation
            );

        strike.Initialize(
            lightningStrikeDamage,
            lightningStrikeDamageRadius,
            enemyLayer
        );

        return true;
    }

    private Vector3 CalculateLightningStrikePosition()
    {
        if (
            playerLockOn != null &&
            playerLockOn.IsLockedOn
        )
        {
            return GetGroundPosition(
                playerLockOn.CurrentTargetPosition,
                lightningGroundCheckHeight,
                lightningGroundOffset
            );
        }

        Vector3 intendedPosition =
            transform.position +
            GetFlatForwardDirection() *
            lightningStrikeRange;

        return GetGroundPosition(
            intendedPosition,
            lightningGroundCheckHeight,
            lightningGroundOffset
        );
    }

    // =========================================================
    // GROUND HELPERS
    // =========================================================

    private Vector3 GetGroundPosition(
        Vector3 intendedPosition,
        float groundCheckHeight,
        float groundOffset
    )
    {
        Vector3 rayStart =
            intendedPosition +
            Vector3.up *
            groundCheckHeight;

        float rayDistance =
            groundCheckHeight *
            2f;

        if (
            Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            return
                hit.point +
                Vector3.up *
                groundOffset;
        }

        return intendedPosition;
    }

    private Vector3 GetFlatForwardDirection()
    {
        Vector3 forwardDirection =
            transform.forward;

        forwardDirection.y = 0f;

        if (
            forwardDirection.sqrMagnitude <=
            0.001f
        )
        {
            return
                Vector3.forward;
        }

        return
            forwardDirection.normalized;
    }

    // =========================================================
    // COOLDOWNS
    // =========================================================

    public bool IsSpellReady(StaffSpell spell)
    {
        return
            GetRemainingCooldown(spell) <=
            0f;
    }

    public float GetRemainingCooldown(
        StaffSpell spell
    )
    {
        float readyTime =
            GetNextReadyTime(
                spell
            );

        return
            Mathf.Max(
                0f,
                readyTime -
                Time.time
            );
    }

    public float GetCooldownDuration(
        StaffSpell spell
    )
    {
        switch (spell)
        {
            case StaffSpell.Entangle:
                return entangleCooldown;

            case StaffSpell.Flamethrower:
                return flamethrowerCooldown;

            case StaffSpell.IceTornado:
                return iceTornadoCooldown;

            case StaffSpell.LightningStrike:
                return lightningStrikeCooldown;

            default:
                return 0f;
        }
    }

    private float GetNextReadyTime(
        StaffSpell spell
    )
    {
        switch (spell)
        {
            case StaffSpell.Entangle:
                return nextEntangleTime;

            case StaffSpell.Flamethrower:
                return nextFlamethrowerTime;

            case StaffSpell.IceTornado:
                return nextIceTornadoTime;

            case StaffSpell.LightningStrike:
                return nextLightningStrikeTime;

            default:
                return 0f;
        }
    }

    private void StartCooldown(
        StaffSpell spell
    )
    {
        float readyTime =
            Time.time +
            GetCooldownDuration(
                spell
            );

        switch (spell)
        {
            case StaffSpell.Entangle:
                nextEntangleTime =
                    readyTime;
                break;

            case StaffSpell.Flamethrower:
                nextFlamethrowerTime =
                    readyTime;
                break;

            case StaffSpell.IceTornado:
                nextIceTornadoTime =
                    readyTime;
                break;

            case StaffSpell.LightningStrike:
                nextLightningStrikeTime =
                    readyTime;
                break;
        }
    }

    // =========================================================
    // RESPAWN RESET
    // =========================================================

    /*
     * Clears only temporary Staff combat state.
     * Spell unlock progression is restored separately.
     */
    public void ResetForRespawn()
    {
        CancelStaffCast();

        activeSpell =
            default;

        nextEntangleTime = 0f;
        nextFlamethrowerTime = 0f;
        nextIceTornadoTime = 0f;
        nextLightningStrikeTime = 0f;
    }

    // =========================================================
    // GENERAL
    // =========================================================

    private void FindReferences()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (playerController == null)
        {
            playerController =
                GetComponent<Player_Controller>();
        }

        if (playerController == null)
        {
            playerController =
                GetComponentInParent<Player_Controller>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<Player_LockOn>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponentInParent<Player_LockOn>();
        }
    }

    private void ValidateReferences()
    {
        if (animator == null)
        {
            Debug.LogError(
                $"{name}: PlayerStaffCombat could not find an Animator.",
                this
            );
        }

        if (playerController == null)
        {
            Debug.LogError(
                $"{name}: Player_StaffCombat could not find " +
                "Player_Controller.",
                this
            );

            enabled = false;
            return;
        }

        if (playerLockOn == null)
        {
            Debug.LogWarning(
                 $"{name}: Player_StaffCombat could not find " +
                 "Player_LockOn. Entangle requires lock-on.",
    this
);
        }

        if (staffFirePoint == null)
        {
            Debug.LogWarning(
                $"{name}: Staff Fire Point has not been assigned.",
                this
            );
        }

        if (entanglePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Entangle Prefab has not been assigned.",
                this
            );
        }

        if (iceTornadoPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Ice Tornado Prefab has not been assigned.",
                this
            );
        }

        if (lightningStrikePrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Lightning Strike Prefab has not been assigned.",
                this
            );
        }

        if (groundLayer.value == 0)
        {
            Debug.LogWarning(
                $"{name}: Ground Layer has not been assigned.",
                this
            );
        }

        if (enemyLayer.value == 0)
        {
            Debug.LogWarning(
                $"{name}: Enemy Layer has not been assigned.",
                this
            );
        }
    }

    private bool IsPlayerActionLocked()
    {
        return
            playerController != null &&
            playerController.IsMovementLocked;
    }

    private void LogSpellNotImplemented(
        StaffSpell spell
    )
    {
        Debug.Log(
            $"{name}: {spell} is not implemented yet.",
            this
        );
    }

    private void OnDisable()
    {
        CancelStaffCast();
    }

    private void OnValidate()
    {
        entangleDuration =
            Mathf.Max(
                0.1f,
                entangleDuration
            );

        entangleMissDistance =
            Mathf.Max(
                0f,
                entangleMissDistance
            );

        entangleGroundCheckHeight =
            Mathf.Max(
                0.1f,
                entangleGroundCheckHeight
            );

        entangleGroundOffset =
            Mathf.Max(
                0f,
                entangleGroundOffset
            );

        entangleCooldown =
            Mathf.Max(
                0f,
                entangleCooldown
            );

        flamethrowerCooldown =
            Mathf.Max(
                0f,
                flamethrowerCooldown
            );

        iceTornadoCooldown =
            Mathf.Max(
                0f,
                iceTornadoCooldown
            );

        lightningStrikeCooldown =
            Mathf.Max(
                0f,
                lightningStrikeCooldown
            );

        iceTornadoDamage =
            Mathf.Max(
                1,
                iceTornadoDamage
            );

        iceTornadoSpeed =
            Mathf.Max(
                0f,
                iceTornadoSpeed
            );

        iceTornadoSpawnDistance =
            Mathf.Max(
                0f,
                iceTornadoSpawnDistance
            );

        iceTornadoGroundCheckHeight =
            Mathf.Max(
                0.1f,
                iceTornadoGroundCheckHeight
            );

        iceTornadoGroundOffset =
            Mathf.Max(
                0f,
                iceTornadoGroundOffset
            );

        lightningStrikeDamage =
            Mathf.Max(
                1,
                lightningStrikeDamage
            );

        lightningStrikeDamageRadius =
            Mathf.Max(
                0.01f,
                lightningStrikeDamageRadius
            );

        lightningStrikeRange =
            Mathf.Max(
                0f,
                lightningStrikeRange
            );

        lightningGroundCheckHeight =
            Mathf.Max(
                0.1f,
                lightningGroundCheckHeight
            );

        lightningGroundOffset =
            Mathf.Max(
                0f,
                lightningGroundOffset
            );
    }
}