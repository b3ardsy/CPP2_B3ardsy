using System.Collections;
using UnityEngine;

public class BoneCleave : MonoBehaviour
{
    [Header("Segments")]
    [SerializeField] private BoneCleaveSegment segmentPrefab;

    [Tooltip("Number of bone eruptions in the line.")]
    [SerializeField] private int segmentCount = 7;

    [Tooltip("Distance between each bone eruption.")]
    [SerializeField] private float segmentSpacing = 1.25f;

    [Tooltip("Delay between each eruption.")]
    [SerializeField] private float delayBetweenSegments = 0.08f;

    [Tooltip("Distance in front of the caster where the first segment appears.")]
    [SerializeField] private float startingDistance = 1f;

    [Header("Ground Placement")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float groundCheckHeight = 5f;
    [SerializeField] private float groundCheckDistance = 15f;

    private Vector3 travelDirection;
    private GameObject owner;
    private bool hasInitialized;

    public void Initialize(
        Vector3 direction,
        GameObject spellOwner)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward;
        }

        travelDirection = direction.normalized;
        owner = spellOwner;
        hasInitialized = true;

        StartCoroutine(SpawnSegments());
    }

    private IEnumerator SpawnSegments()
    {
        if (!hasInitialized || segmentPrefab == null)
        {
            Destroy(gameObject);
            yield break;
        }

        for (int i = 0; i < segmentCount; i++)
        {
            float distance =
                startingDistance +
                i * segmentSpacing;

            Vector3 desiredPosition =
                transform.position +
                travelDirection * distance;

            Vector3 groundPosition =
                FindGroundPosition(desiredPosition);

            Quaternion rotation =
                Quaternion.LookRotation(
                    travelDirection,
                    Vector3.up
                );

            BoneCleaveSegment segment = Instantiate(
                segmentPrefab,
                groundPosition,
                rotation
            );

            segment.Initialize(owner);

            if (delayBetweenSegments > 0f)
            {
                yield return new WaitForSeconds(
                    delayBetweenSegments
                );
            }
        }

        Destroy(gameObject);
    }

    private Vector3 FindGroundPosition(Vector3 desiredPosition)
    {
        Vector3 rayOrigin =
            desiredPosition + Vector3.up * groundCheckHeight;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return desiredPosition;
    }

    private void OnValidate()
    {
        segmentCount = Mathf.Max(1, segmentCount);
        segmentSpacing = Mathf.Max(0.1f, segmentSpacing);

        delayBetweenSegments = Mathf.Max(
            0f,
            delayBetweenSegments
        );

        startingDistance = Mathf.Max(
            0f,
            startingDistance
        );

        groundCheckHeight = Mathf.Max(
            0f,
            groundCheckHeight
        );

        groundCheckDistance = Mathf.Max(
            0.1f,
            groundCheckDistance
        );
    }
}