using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLockOn : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Target Detection")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float maximumLockAngle = 60f;

    [Header("Lock-On Limits")]
    [SerializeField] private float breakLockDistance = 20f;

    private Enemy currentTarget;

    public bool IsLockedOn =>
        currentTarget != null &&
        !currentTarget.IsDead;

    public Enemy CurrentTarget =>
        IsLockedOn ? currentTarget : null;

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
                "PlayerLockOn: Camera Transform reference is missing."
            );

            enabled = false;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            ToggleLockOn();
        }

        if (
            Keyboard.current.tabKey.wasPressedThisFrame &&
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

    private void FindBestTarget()
    {
        List<Enemy> validTargets =
            FindValidTargets();

        if (validTargets.Count == 0)
        {
            Debug.Log(
                "No enemy was available for lock-on."
            );

            return;
        }

        Enemy bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (Enemy enemy in validTargets)
        {
            float angleFromCamera =
                GetUnsignedCameraAngle(enemy);

            float distanceToEnemy =
                Vector3.Distance(
                    transform.position,
                    enemy.LockOnPoint
                );

            // Prefer enemies closer to the centre of the camera.
            // Distance is used as a smaller secondary factor.
            float targetScore =
                angleFromCamera +
                distanceToEnemy * 0.1f;

            if (targetScore < bestScore)
            {
                bestScore = targetScore;
                bestTarget = enemy;
            }
        }

        SetTarget(bestTarget);
    }

    private void CycleTarget()
    {
        List<Enemy> validTargets =
            FindValidTargets();

        if (validTargets.Count <= 1)
        {
            return;
        }

        // Sort targets from the left side of the camera
        // to the right side of the camera.
        validTargets.Sort(
            (enemyA, enemyB) =>
                GetSignedCameraAngle(enemyA).CompareTo(
                    GetSignedCameraAngle(enemyB)
                )
        );

        int currentIndex =
            validTargets.IndexOf(currentTarget);

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

    private List<Enemy> FindValidTargets()
    {
        Collider[] nearbyColliders =
            Physics.OverlapSphere(
                transform.position,
                detectionRadius,
                enemyLayer,
                QueryTriggerInteraction.Ignore
            );

        List<Enemy> validTargets =
            new List<Enemy>();

        foreach (Collider nearbyCollider in nearbyColliders)
        {
            Enemy enemy =
                nearbyCollider.GetComponentInParent<Enemy>();

            if (
                enemy == null ||
                enemy.IsDead ||
                validTargets.Contains(enemy)
            )
            {
                continue;
            }

            Vector3 directionToEnemy =
                enemy.LockOnPoint -
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
                GetUnsignedCameraAngle(enemy);

            if (
                angleFromCamera >
                maximumLockAngle
            )
            {
                continue;
            }

            validTargets.Add(enemy);
        }

        return validTargets;
    }

    private float GetUnsignedCameraAngle(
        Enemy enemy
    )
    {
        Vector3 cameraForward =
            cameraTransform.forward;

        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 directionToEnemy =
            enemy.LockOnPoint -
            cameraTransform.position;

        directionToEnemy.y = 0f;
        directionToEnemy.Normalize();

        return Vector3.Angle(
            cameraForward,
            directionToEnemy
        );
    }

    private float GetSignedCameraAngle(
        Enemy enemy
    )
    {
        Vector3 cameraForward =
            cameraTransform.forward;

        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 directionToEnemy =
            enemy.LockOnPoint -
            cameraTransform.position;

        directionToEnemy.y = 0f;
        directionToEnemy.Normalize();

        return Vector3.SignedAngle(
            cameraForward,
            directionToEnemy,
            Vector3.up
        );
    }

    private void ValidateCurrentTarget()
    {
        if (currentTarget == null)
        {
            return;
        }

        if (currentTarget.IsDead)
        {
            ClearTarget();
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

    private void SetTarget(
        Enemy newTarget
    )
    {
        if (newTarget == null)
        {
            return;
        }

        currentTarget = newTarget;

        Debug.Log(
            $"Locked onto {currentTarget.name}."
        );
    }

    public void ClearTarget()
    {
        if (currentTarget != null)
        {
            Debug.Log(
                $"Lock-on released from {currentTarget.name}."
            );
        }

        currentTarget = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius
        );
    }
}