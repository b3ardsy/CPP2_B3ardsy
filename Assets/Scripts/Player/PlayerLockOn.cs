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

    public Enemy CurrentTarget => currentTarget;

    public Transform CurrentTargetTransform
    {
        get
        {
            if (!IsLockedOn)
            {
                return null;
            }

            return currentTarget.LockOnTarget;
        }
    }

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
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
        Collider[] nearbyColliders =
            Physics.OverlapSphere(
                transform.position,
                detectionRadius,
                enemyLayer,
                QueryTriggerInteraction.Ignore
            );

        Enemy bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (Collider nearbyCollider in nearbyColliders)
        {
            Enemy enemy =
                nearbyCollider.GetComponentInParent<Enemy>();

            if (enemy == null || enemy.IsDead)
            {
                continue;
            }

            Vector3 directionToEnemy =
                enemy.LockOnTarget.position -
                cameraTransform.position;

            directionToEnemy.y = 0f;

            if (directionToEnemy.sqrMagnitude <= 0.001f)
            {
                continue;
            }

            directionToEnemy.Normalize();

            Vector3 cameraForward =
                cameraTransform.forward;

            cameraForward.y = 0f;
            cameraForward.Normalize();

            float angleFromCamera =
                Vector3.Angle(
                    cameraForward,
                    directionToEnemy
                );

            if (angleFromCamera > maximumLockAngle)
            {
                continue;
            }

            float distanceToEnemy =
                Vector3.Distance(
                    transform.position,
                    enemy.LockOnTarget.position
                );

            // Prefer enemies near the centre of the camera view.
            // Distance acts as a smaller secondary factor.
            float targetScore =
                angleFromCamera +
                distanceToEnemy * 0.1f;

            if (targetScore < bestScore)
            {
                bestScore = targetScore;
                bestTarget = enemy;
            }
        }

        currentTarget = bestTarget;

        if (currentTarget != null)
        {
            Debug.Log(
                $"Locked onto {currentTarget.name}."
            );
        }
        else
        {
            Debug.Log(
                "No enemy was available for lock-on."
            );
        }
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
                currentTarget.LockOnTarget.position
            );

        if (distanceToTarget > breakLockDistance)
        {
            ClearTarget();
        }
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