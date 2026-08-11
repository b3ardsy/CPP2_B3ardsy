using UnityEngine;

public class PlayerStaffCombat : MonoBehaviour
{
    public enum StaffSpell
    {
        Flamethrower,
        IceTornado,
        LightningStrike,
        Shield
    }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement3DNew playerMovement;
    [SerializeField] private PlayerLockOn playerLockOn;

    [Header("Staff")]
    [Tooltip(
        "Spawn point used by Staff spells that originate " +
        "directly from the Staff, such as Flamethrower."
    )]
    [SerializeField] private Transform staffFirePoint;

    [Header("Selected Spell")]
    [SerializeField]
    private StaffSpell selectedSpell =
        StaffSpell.IceTornado;

    [Header("Spell Cooldowns")]
    [SerializeField] private float flamethrowerCooldown = 5f;
    [SerializeField] private float iceTornadoCooldown = 5f;
    [SerializeField] private float lightningStrikeCooldown = 5f;
    [SerializeField] private float shieldCooldown = 5f;

    [Header("Ice Tornado")]
    [SerializeField]
    private IceTornadoProjectile iceTornadoPrefab;

    [SerializeField]
    private int iceTornadoDamage = 1;

    [SerializeField]
    private float iceTornadoSpeed = 10f;

    [Tooltip(
        "How far in front of the player the Ice Tornado " +
        "attempts to spawn."
    )]
    [SerializeField]
    private float iceTornadoSpawnDistance = 1.5f;

    [Tooltip(
        "How high above the intended spawn point the " +
        "ground check begins."
    )]
    [SerializeField]
    private float iceTornadoGroundCheckHeight = 3f;

    [Tooltip(
        "Small vertical offset above the detected ground."
    )]
    [SerializeField]
    private float iceTornadoGroundOffset = 0.05f;

    [Tooltip(
        "Layers considered valid ground for Ice Tornado spawning."
    )]
    [SerializeField]
    private LayerMask groundLayer;

    [Tooltip(
        "Visual rotation correction for the Ice Tornado prefab."
    )]
    [SerializeField]
    private Vector3 iceTornadoRotationOffset =
        new Vector3(-90f, 0f, 0f);

    [Header("Aiming")]
    [Tooltip(
        "When not locked on, Ice Tornado travels " +
        "in the direction the player is facing."
    )]
    [SerializeField]
    private bool usePlayerForwardWhenUnlocked = true;

    private bool isCasting;

    private StaffSpell activeSpell;

    /*
     * Each Staff spell owns its own cooldown timer.
     */
    private float nextFlamethrowerTime;
    private float nextIceTornadoTime;
    private float nextLightningStrikeTime;
    private float nextShieldTime;

    public StaffSpell SelectedSpell =>
        selectedSpell;

    public bool IsCasting =>
        isCasting;

    private static readonly int MagicSummonTrigger =
        Animator.StringToHash("MagicSummon");

    private void Awake()
    {
        FindReferences();
        ValidateReferences();
    }

    private void FindReferences()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                GetComponent<PlayerMovement3DNew>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                GetComponentInParent<PlayerMovement3DNew>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponent<PlayerLockOn>();
        }

        if (playerLockOn == null)
        {
            playerLockOn =
                GetComponentInParent<PlayerLockOn>();
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

        if (playerMovement == null)
        {
            Debug.LogError(
                $"{name}: PlayerStaffCombat could not find " +
                "PlayerMovement3DNew.",
                this
            );

            enabled = false;
            return;
        }

        if (staffFirePoint == null)
        {
            Debug.LogWarning(
                $"{name}: Staff Fire Point has not been assigned.",
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

        if (groundLayer.value == 0)
        {
            Debug.LogWarning(
                $"{name}: Ground Layer has not been assigned " +
                "for Ice Tornado spawning.",
                this
            );
        }
    }

    public void SelectSpell(
        StaffSpell spell
    )
    {
        if (isCasting)
        {
            return;
        }

        if (selectedSpell == spell)
        {
            return;
        }

        selectedSpell =
            spell;

        Debug.Log(
            $"{name}: Selected Staff spell: {selectedSpell}.",
            this
        );
    }

    public void TryCastSelectedSpell()
    {
        if (IsPlayerActionLocked())
        {
            return;
        }

        if (isCasting)
        {
            return;
        }

        if (!IsSpellReady(selectedSpell))
        {
            Debug.Log(
                $"{name}: {selectedSpell} is on cooldown for " +
                $"{GetRemainingCooldown(selectedSpell):0.0} more seconds.",
                this
            );

            return;
        }

        switch (selectedSpell)
        {
            case StaffSpell.Flamethrower:
                LogSpellNotImplemented();
                break;

            case StaffSpell.IceTornado:
                TryBeginIceTornado();
                break;

            case StaffSpell.LightningStrike:
                LogSpellNotImplemented();
                break;

            case StaffSpell.Shield:
                LogSpellNotImplemented();
                break;
        }
    }

    private void TryBeginIceTornado()
    {
        if (iceTornadoPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Cannot cast Ice Tornado because " +
                "no Ice Tornado Prefab is assigned.",
                this
            );

            return;
        }

        BeginStaffCast(
            StaffSpell.IceTornado
        );
    }

    private void BeginStaffCast(
        StaffSpell spell
    )
    {
        if (animator == null)
        {
            return;
        }

        activeSpell =
            spell;

        isCasting = true;

        animator.ResetTrigger(
            MagicSummonTrigger
        );

        animator.SetTrigger(
            MagicSummonTrigger
        );
    }

    /*
     * Animation Event on Magic Summon.
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
                break;

            case StaffSpell.Shield:
                break;
        }
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
                transform.forward;

            fireDirection.y = 0f;
        }

        if (
            fireDirection.sqrMagnitude <=
            0.001f
        )
        {
            return false;
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

        Quaternion spawnRotation =
            directionRotation *
            rotationOffset;

        IceTornadoProjectile tornado =
            Instantiate(
                iceTornadoPrefab,
                spawnPosition,
                spawnRotation
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
        Vector3 forwardDirection =
            transform.forward;

        forwardDirection.y = 0f;

        if (
            forwardDirection.sqrMagnitude <=
            0.001f
        )
        {
            forwardDirection =
                Vector3.forward;
        }

        forwardDirection.Normalize();

        Vector3 intendedPosition =
            transform.position +
            forwardDirection *
            iceTornadoSpawnDistance;

        Vector3 rayStart =
            intendedPosition +
            Vector3.up *
            iceTornadoGroundCheckHeight;

        float rayDistance =
            iceTornadoGroundCheckHeight *
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
                iceTornadoGroundOffset;
        }

        return intendedPosition;
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
            Vector3 forwardDirection =
                transform.forward;

            forwardDirection.y = 0f;

            return
                forwardDirection.normalized;
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
            transform.forward.normalized;
    }

    public bool IsSpellReady(
        StaffSpell spell
    )
    {
        return
            GetRemainingCooldown(spell) <= 0f;
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
                readyTime - Time.time
            );
    }

    private float GetNextReadyTime(
        StaffSpell spell
    )
    {
        switch (spell)
        {
            case StaffSpell.Flamethrower:
                return nextFlamethrowerTime;

            case StaffSpell.IceTornado:
                return nextIceTornadoTime;

            case StaffSpell.LightningStrike:
                return nextLightningStrikeTime;

            case StaffSpell.Shield:
                return nextShieldTime;

            default:
                return 0f;
        }
    }

    private float GetCooldownDuration(
        StaffSpell spell
    )
    {
        switch (spell)
        {
            case StaffSpell.Flamethrower:
                return flamethrowerCooldown;

            case StaffSpell.IceTornado:
                return iceTornadoCooldown;

            case StaffSpell.LightningStrike:
                return lightningStrikeCooldown;

            case StaffSpell.Shield:
                return shieldCooldown;

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

            case StaffSpell.Shield:
                nextShieldTime =
                    readyTime;
                break;
        }
    }

    /*
     * Animation Event on Magic Summon.
     */
    public void EndStaffCast()
    {
        isCasting = false;
    }

    public void CancelStaffCast()
    {
        isCasting = false;

        if (animator != null)
        {
            animator.ResetTrigger(
                MagicSummonTrigger
            );
        }
    }

    private bool IsPlayerActionLocked()
    {
        return
            playerMovement != null &&
            playerMovement.IsMovementLocked;
    }

    private void LogSpellNotImplemented()
    {
        Debug.Log(
            $"{name}: {selectedSpell} is not implemented yet.",
            this
        );
    }

    private void OnDisable()
    {
        CancelStaffCast();
    }

    private void OnValidate()
    {
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

        shieldCooldown =
            Mathf.Max(
                0f,
                shieldCooldown
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
    }
}