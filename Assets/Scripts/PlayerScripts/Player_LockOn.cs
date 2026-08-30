using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player_LockOn : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Target Detection")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float maximumLockAngle = 60f;

    [Header("Lock-On Limits")]
    [SerializeField] private float breakLockDistance = 20f;

    // =========================================================
    // TARGET WRAPPER
    // =========================================================

    /*
     * Temporary migration wrapper.
     *
     * New enemies use EnemyController.
     * Rogue/Tank still use the legacy Enemy component until
     * their individual migrations are complete.
     *
     * Once all enemies use EnemyController, this wrapper and the
     * legacy Enemy branch can be removed.
     */
    private sealed class LockOnTarget
    {
        public EnemyController Controller { get; }
        public Enemy LegacyEnemy { get; }

        public LockOnTarget(
            EnemyController controller
        )
        {
            Controller = controller;
            LegacyEnemy = null;
        }

        public LockOnTarget(
            Enemy enemy
        )
        {
            Controller = null;
            LegacyEnemy = enemy;
        }

        public bool IsValid =>
            Controller != null ||
            LegacyEnemy != null;

        public bool IsDead
        {
            get
            {
                if (Controller != null)
                {
                    return Controller.IsDead;
                }

                return
                    LegacyEnemy == null ||
                    LegacyEnemy.IsDead;
            }
        }

        public Vector3 LockOnPoint
        {
            get
            {
                if (Controller != null)
                {
                    return Controller.LockOnPoint;
                }

                if (LegacyEnemy != null)
                {
                    return LegacyEnemy.LockOnPoint;
                }

                return Vector3.zero;
            }
        }

        public Transform TargetTransform
        {
            get
            {
                if (Controller != null)
                {
                    return Controller.transform;
                }

                if (LegacyEnemy != null)
                {
                    return LegacyEnemy.transform;
                }

                return null;
            }
        }

        public string TargetName
        {
            get
            {
                Transform targetTransform =
                    TargetTransform;

                return targetTransform != null
                    ? targetTransform.name
                    : "Unknown";
            }
        }

        public bool Matches(
            LockOnTarget other
        )
        {
            if (other == null)
            {
                return false;
            }

            if (
                Controller != null ||
                other.Controller != null
            )
            {
                return
                    Controller != null &&
                    Controller ==
                    other.Controller;
            }

            return
                LegacyEnemy != null &&
                LegacyEnemy ==
                other.LegacyEnemy;
        }
    }

    private LockOnTarget currentTarget;

    // =========================================================
    // PUBLIC PROPERTIES
    // =========================================================

    public bool IsLockedOn =>
        currentTarget != null &&
        currentTarget.IsValid &&
        !currentTarget.IsDead;

    /*
     * Legacy compatibility property.
     *
     * This continues to expose the old Enemy target while Rogue
     * and Tank still use Enemy.cs. A migrated EnemyController
     * target will return null here.
     *
     * Player systems should prefer IsLockedOn and
     * CurrentTargetPosition.
     */
    public Enemy CurrentTarget =>
        IsLockedOn
            ? currentTarget.LegacyEnemy
            : null;

    public EnemyController CurrentTargetController =>
        IsLockedOn
            ? currentTarget.Controller
            : null;

    public Transform CurrentTargetTransform =>
        IsLockedOn
            ? currentTarget.TargetTransform
            : null;

    public Vector3 CurrentTargetPosition
    {
        get
        {
            if (!IsLockedOn)
            {
                return transform.position;
            }

            return currentTarget.LockOnPoint;
        }
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (
            cameraTransform == null &&
            Camera.main != null
        )
        {
            cameraTransform =
                Camera.main.transform;
        }

        if (cameraTransform == null)
        {
            Debug.LogError(
                "Player_LockOn: Camera Transform reference is missing."
            );

            enabled = false;
        }
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (
            Keyboard.current.qKey
                .wasPressedThisFrame
        )
        {
            ToggleLockOn();
        }

        if (
            Keyboard.current.tabKey
                .wasPressedThisFrame &&
            IsLockedOn
        )
        {
            CycleTarget();
        }

        ValidateCurrentTarget();
    }

    private void ToggleLockOn()
    {
        if (IsLockedOn)
        {
            ClearTarget();
            return;
        }

        FindBestTarget();
    }

    // =========================================================
    // TARGET SELECTION
    // =========================================================

    private void FindBestTarget()
    {
        List<LockOnTarget> validTargets =
            FindValidTargets();

        if (validTargets.Count == 0)
        {
            Debug.Log(
                "No enemy was available for lock-on."
            );

            return;
        }

        LockOnTarget bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (
            LockOnTarget target
            in validTargets
        )
        {
            float angleFromCamera =
                GetUnsignedCameraAngle(
                    target
                );

            float distanceToEnemy =
                Vector3.Distance(
                    transform.position,
                    target.LockOnPoint
                );

            /*
             * Prefer enemies near the centre of the camera.
             * Distance remains a smaller secondary factor.
             */
            float targetScore =
                angleFromCamera +
                distanceToEnemy * 0.1f;

            if (
                targetScore <
                bestScore
            )
            {
                bestScore =
                    targetScore;

                bestTarget =
                    target;
            }
        }

        SetTarget(
            bestTarget
        );
    }

    private void CycleTarget()
    {
        List<LockOnTarget> validTargets =
            FindValidTargets();

        if (
            validTargets.Count <=
            1
        )
        {
            return;
        }

        /*
         * Sort targets from the left side of the camera
         * to the right side of the camera.
         */
        validTargets.Sort(
            (targetA, targetB) =>
                GetSignedCameraAngle(
                    targetA
                ).CompareTo(
                    GetSignedCameraAngle(
                        targetB
                    )
                )
        );

        int currentIndex =
            FindTargetIndex(
                validTargets,
                currentTarget
            );

        int nextIndex;

        if (currentIndex < 0)
        {
            nextIndex = 0;
        }
        else
        {
            nextIndex =
                (currentIndex + 1) %
                validTargets.Count;
        }

        SetTarget(
            validTargets[nextIndex]
        );
    }

    private int FindTargetIndex(
        List<LockOnTarget> targets,
        LockOnTarget targetToFind
    )
    {
        if (targetToFind == null)
        {
            return -1;
        }

        for (
            int index = 0;
            index < targets.Count;
            index++
        )
        {
            if (
                targets[index].Matches(
                    targetToFind
                )
            )
            {
                return index;
            }
        }

        return -1;
    }

    // =========================================================
    // TARGET DISCOVERY
    // =========================================================

    private List<LockOnTarget> FindValidTargets()
    {
        Collider[] nearbyColliders =
            Physics.OverlapSphere(
                transform.position,
                detectionRadius,
                enemyLayer,
                QueryTriggerInteraction.Ignore
            );

        List<LockOnTarget> validTargets =
            new List<LockOnTarget>();

        foreach (
            Collider nearbyCollider
            in nearbyColliders
        )
        {
            LockOnTarget target =
                FindLockOnTarget(
                    nearbyCollider
                );

            if (
                target == null ||
                !target.IsValid ||
                target.IsDead ||
                ContainsTarget(
                    validTargets,
                    target
                )
            )
            {
                continue;
            }

            Vector3 directionToEnemy =
                target.LockOnPoint -
                cameraTransform.position;

            directionToEnemy.y = 0f;

            if (
                directionToEnemy.sqrMagnitude <=
                0.001f
            )
            {
                continue;
            }

            float angleFromCamera =
                GetUnsignedCameraAngle(
                    target
                );

            if (
                angleFromCamera >
                maximumLockAngle
            )
            {
                continue;
            }

            validTargets.Add(
                target
            );
        }

        return validTargets;
    }

    private LockOnTarget FindLockOnTarget(
        Collider nearbyCollider
    )
    {
        if (nearbyCollider == null)
        {
            return null;
        }

        /*
         * Prefer the new shared EnemyController.
         */
        EnemyController controller =
            nearbyCollider
                .GetComponentInParent<EnemyController>();

        if (controller != null)
        {
            return new LockOnTarget(
                controller
            );
        }

        /*
         * Temporary fallback for Rogue/Tank while they still
         * use the legacy Enemy hierarchy.
         */
        Enemy legacyEnemy =
            nearbyCollider
                .GetComponentInParent<Enemy>();

        if (legacyEnemy != null)
        {
            return new LockOnTarget(
                legacyEnemy
            );
        }

        return null;
    }

    private bool ContainsTarget(
        List<LockOnTarget> targets,
        LockOnTarget candidate
    )
    {
        foreach (
            LockOnTarget target
            in targets
        )
        {
            if (target.Matches(candidate))
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // CAMERA ANGLES
    // =========================================================

    private float GetUnsignedCameraAngle(
        LockOnTarget target
    )
    {
        Vector3 cameraForward =
            cameraTransform.forward;

        cameraForward.y = 0f;

        if (
            cameraForward.sqrMagnitude <=
            0.001f
        )
        {
            return 180f;
        }

        cameraForward.Normalize();

        Vector3 directionToEnemy =
            target.LockOnPoint -
            cameraTransform.position;

        directionToEnemy.y = 0f;

        if (
            directionToEnemy.sqrMagnitude <=
            0.001f
        )
        {
            return 180f;
        }

        directionToEnemy.Normalize();

        return Vector3.Angle(
            cameraForward,
            directionToEnemy
        );
    }

    private float GetSignedCameraAngle(
        LockOnTarget target
    )
    {
        Vector3 cameraForward =
            cameraTransform.forward;

        cameraForward.y = 0f;

        if (
            cameraForward.sqrMagnitude <=
            0.001f
        )
        {
            return 0f;
        }

        cameraForward.Normalize();

        Vector3 directionToEnemy =
            target.LockOnPoint -
            cameraTransform.position;

        directionToEnemy.y = 0f;

        if (
            directionToEnemy.sqrMagnitude <=
            0.001f
        )
        {
            return 0f;
        }

        directionToEnemy.Normalize();

        return Vector3.SignedAngle(
            cameraForward,
            directionToEnemy,
            Vector3.up
        );
    }

    // =========================================================
    // TARGET VALIDATION
    // =========================================================

    private void ValidateCurrentTarget()
    {
        if (currentTarget == null)
        {
            return;
        }

        /*
         * If the current target dies, immediately try
         * to transfer lock-on to the nearest remaining
         * valid enemy.
         */
        if (
            !currentTarget.IsValid ||
            currentTarget.IsDead
        )
        {
            TryTransferToNearestTarget();
            return;
        }

        float distanceToTarget =
            Vector3.Distance(
                transform.position,
                currentTarget.LockOnPoint
            );

        if (
            distanceToTarget >
            breakLockDistance
        )
        {
            ClearTarget();
        }
    }

    private void TryTransferToNearestTarget()
    {
        LockOnTarget previousTarget =
            currentTarget;

        List<LockOnTarget> validTargets =
            FindValidTargets();

        RemoveTarget(
            validTargets,
            previousTarget
        );

        if (
            validTargets.Count ==
            0
        )
        {
            ClearTarget();
            return;
        }

        LockOnTarget nearestTarget = null;
        float nearestDistance =
            float.MaxValue;

        foreach (
            LockOnTarget target
            in validTargets
        )
        {
            float distanceToEnemy =
                Vector3.Distance(
                    transform.position,
                    target.LockOnPoint
                );

            if (
                distanceToEnemy <
                nearestDistance
            )
            {
                nearestDistance =
                    distanceToEnemy;

                nearestTarget =
                    target;
            }
        }

        if (nearestTarget == null)
        {
            ClearTarget();
            return;
        }

        Debug.Log(
            $"Lock-on transferred from " +
            $"{previousTarget?.TargetName ?? "Unknown"} to " +
            $"{nearestTarget.TargetName}."
        );

        SetTarget(
            nearestTarget
        );
    }

    private void RemoveTarget(
        List<LockOnTarget> targets,
        LockOnTarget targetToRemove
    )
    {
        if (targetToRemove == null)
        {
            return;
        }

        for (
            int index = targets.Count - 1;
            index >= 0;
            index--
        )
        {
            if (
                targets[index].Matches(
                    targetToRemove
                )
            )
            {
                targets.RemoveAt(
                    index
                );
            }
        }
    }

    private void SetTarget(
        LockOnTarget newTarget
    )
    {
        if (
            newTarget == null ||
            !newTarget.IsValid ||
            newTarget.IsDead
        )
        {
            return;
        }

        currentTarget =
            newTarget;

        Debug.Log(
            $"Locked onto {currentTarget.TargetName}."
        );
    }

    public void ClearTarget()
    {
        if (currentTarget != null)
        {
            Debug.Log(
                $"Lock-on released from {currentTarget.TargetName}."
            );
        }

        currentTarget =
            null;
    }

    // =========================================================
    // RESPAWN RESET
    // =========================================================

    /*
     * Lock-on is transient runtime state and is never restored
     * from a checkpoint. Respawning always begins without a target.
     */
    public void ResetForRespawn()
    {
        ClearTarget();
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius
        );
    }
}