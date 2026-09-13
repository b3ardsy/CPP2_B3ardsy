using UnityEngine;
using UnityEngine.UI;

public class DemoBoundaryWarning : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Collider demoBoundaryBlocker;

    [Header("Screen Vignette")]
    [SerializeField] private Image warningVignette;
    [SerializeField] private float maximumVignetteAlpha = 0.5f;

    [Header("Boundary X Pool")]
    [Tooltip("The 5 SpriteRenderers used to fake an infinite row of Xs.")]
    [SerializeField] private SpriteRenderer[] boundaryXs;

    [Tooltip("Distance between each imaginary X along the boundary.")]
    [SerializeField] private float xSpacing = 6f;

    [Tooltip("Height of the Xs above the boundary.")]
    [SerializeField] private float xHeight = 2.5f;

    [Tooltip("Moves the Xs slightly toward the playable side.")]
    [SerializeField] private float xForwardOffset = 0.5f;

    [Header("X Alpha")]
    [Tooltip("Maximum alpha when the player is directly in front of an X.")]
    [SerializeField] private float xMaximumAlpha = 1f;

    [Tooltip("How quickly alpha changes as the player moves.")]
    [SerializeField] private float xFadeSpeed = 5f;

    [Tooltip("Controls how quickly Xs fade with sideways distance.")]
    [SerializeField] private float xFadeDistance = 13f;

    [Header("Warning Message")]
    [SerializeField] private CanvasGroup warningMessage;
    [SerializeField] private float messageShowDistance = 3f;

    [Header("Warning Settings")]
    [SerializeField] private float warningDistance = 8f;
    [SerializeField] private float fadeSpeed = 5f;

    private bool playerInsideWarningZone;

    private float targetVignetteAlpha;
    private float targetMessageAlpha;

    // Direction running along the blocker.
    private Vector3 boundaryDirection;

    // Fixed grid origin for our imaginary infinite row.
    private Vector3 boundaryGridOrigin;

    private void Start()
    {
        SetVignetteAlpha(0f);
        SetAllXAlpha(0f);

        if (warningMessage != null)
        {
            warningMessage.alpha = 0f;
        }

        if (demoBoundaryBlocker != null)
        {
            boundaryDirection =
                demoBoundaryBlocker.transform.right.normalized;

            /*
             * IMPORTANT:
             *
             * This is a FIXED point in the world.
             * Every imaginary X is placed at:
             *
             * origin + direction * (slotNumber * spacing)
             *
             * So the X grid never slides with the player.
             */
            boundaryGridOrigin =
                demoBoundaryBlocker.bounds.center;
        }
    }

    private void Update()
    {
        if (playerInsideWarningZone)
        {
            UpdateBoundaryWarning();
            UpdateInfiniteXStrip();
        }
        else
        {
            targetVignetteAlpha = 0f;
            targetMessageAlpha = 0f;

            FadeXsOut();
        }

        UpdateVignette();
        UpdateMessage();
    }

    private void UpdateBoundaryWarning()
    {
        if (player == null || demoBoundaryBlocker == null)
            return;

        Vector3 closestPoint =
            demoBoundaryBlocker.ClosestPoint(player.position);

        float distanceToBoundary =
            Vector3.Distance(
                player.position,
                closestPoint
            );

        float boundaryStrength =
            1f - Mathf.Clamp01(
                distanceToBoundary / warningDistance
            );

        targetVignetteAlpha =
            boundaryStrength * maximumVignetteAlpha;

        targetMessageAlpha =
            distanceToBoundary <= messageShowDistance
                ? 1f
                : 0f;
    }

    private void UpdateInfiniteXStrip()
    {
        if (boundaryXs == null ||
            boundaryXs.Length != 5 ||
            player == null ||
            demoBoundaryBlocker == null)
        {
            return;
        }

        /*
         * Find the player's position ALONG the boundary.
         *
         * We don't care how far toward/away from the wall
         * they are here.
         */
        float playerAlongBoundary =
            Vector3.Dot(
                player.position - boundaryGridOrigin,
                boundaryDirection
            );

        /*
         * Find which imaginary X slot the player is
         * currently closest to.
         *
         * Example:
         *
         * ... -2, -1, 0, 1, 2, 3, 4 ...
         */
        int centerSlot =
            Mathf.RoundToInt(
                playerAlongBoundary / xSpacing
            );

        /*
         * We want the five slots surrounding the player:
         *
         * center - 2
         * center - 1
         * center
         * center + 1
         * center + 2
         *
         * Crucially, each world slot maps consistently
         * to one of our five SpriteRenderers using modulo.
         *
         * Therefore when we advance one slot, four Xs stay
         * EXACTLY where they were.
         *
         * Only the farthest X gets recycled to the new
         * position on the opposite side.
         */
        for (int slot = centerSlot - 2;
             slot <= centerSlot + 2;
             slot++)
        {
            int rendererIndex =
                PositiveModulo(
                    slot,
                    boundaryXs.Length
                );

            SpriteRenderer x =
                boundaryXs[rendererIndex];

            if (x == null)
                continue;

            Vector3 slotPosition =
                GetSlotWorldPosition(slot);

            /*
             * This is NOT lerped.
             *
             * Xs represent fixed locations in the world.
             * We want them absolutely stationary.
             */
            x.transform.position = slotPosition;

            UpdateSingleXAlpha(
                x,
                slotPosition
            );
        }
    }

    private Vector3 GetSlotWorldPosition(int slot)
    {
        Vector3 position =
            boundaryGridOrigin +
            boundaryDirection *
            (slot * xSpacing);

        /*
         * Keep X height constant.
         */
        position.y =
            boundaryGridOrigin.y + xHeight;

        /*
         * Find which side of the blocker the player is on
         * and push the marker slightly toward that side.
         */
        Vector3 closestPoint =
            demoBoundaryBlocker.ClosestPoint(
                player.position
            );

        Vector3 towardPlayer =
            player.position - closestPoint;

        towardPlayer.y = 0f;

        if (towardPlayer.sqrMagnitude > 0.001f)
        {
            position +=
                towardPlayer.normalized *
                xForwardOffset;
        }

        return position;
    }

    private void UpdateSingleXAlpha(
        SpriteRenderer x,
        Vector3 xPosition)
    {
        /*
         * Measure ONLY the player's sideways distance
         * from this fixed X.
         */
        float playerAlongBoundary =
            Vector3.Dot(
                player.position - boundaryGridOrigin,
                boundaryDirection
            );

        float xAlongBoundary =
            Vector3.Dot(
                xPosition - boundaryGridOrigin,
                boundaryDirection
            );

        float lateralDistance =
            Mathf.Abs(
                playerAlongBoundary -
                xAlongBoundary
            );

        /*
         * 0 distance:
         * alpha = 1
         *
         * farther away:
         * alpha smoothly drops.
         */
        float proximity =
            1f - Mathf.Clamp01(
                lateralDistance /
                xFadeDistance
            );

        /*
         * Slightly smooth the curve.
         *
         * This makes the brightest marker more obvious
         * and the outer markers fade more naturally.
         */
        proximity =
            Mathf.SmoothStep(
                0f,
                1f,
                proximity
            );

        /*
         * Distance toward the actual boundary still
         * controls the overall effect.
         */
        Vector3 closestPoint =
            demoBoundaryBlocker.ClosestPoint(
                player.position
            );

        float distanceToBoundary =
            Vector3.Distance(
                player.position,
                closestPoint
            );

        float boundaryStrength =
            1f - Mathf.Clamp01(
                distanceToBoundary /
                warningDistance
            );

        float targetAlpha =
            proximity *
            boundaryStrength *
            xMaximumAlpha;

        Color color = x.color;

        color.a =
            Mathf.Lerp(
                color.a,
                targetAlpha,
                1f - Mathf.Exp(
                    -xFadeSpeed *
                    Time.deltaTime
                )
            );

        x.color = color;
    }

    private int PositiveModulo(int value, int modulus)
    {
        return
            ((value % modulus) + modulus)
            % modulus;
    }

    private void FadeXsOut()
    {
        if (boundaryXs == null)
            return;

        foreach (SpriteRenderer x in boundaryXs)
        {
            if (x == null)
                continue;

            Color color = x.color;

            color.a =
                Mathf.Lerp(
                    color.a,
                    0f,
                    1f - Mathf.Exp(
                        -xFadeSpeed *
                        Time.deltaTime
                    )
                );

            x.color = color;
        }
    }

    private void UpdateVignette()
    {
        if (warningVignette == null)
            return;

        Color color =
            warningVignette.color;

        color.a =
            Mathf.Lerp(
                color.a,
                targetVignetteAlpha,
                1f - Mathf.Exp(
                    -fadeSpeed *
                    Time.deltaTime
                )
            );

        warningVignette.color = color;
    }

    private void UpdateMessage()
    {
        if (warningMessage == null)
            return;

        warningMessage.alpha =
            Mathf.Lerp(
                warningMessage.alpha,
                targetMessageAlpha,
                1f - Mathf.Exp(
                    -fadeSpeed *
                    Time.deltaTime
                )
            );
    }

    private void SetVignetteAlpha(float alpha)
    {
        if (warningVignette == null)
            return;

        Color color =
            warningVignette.color;

        color.a = alpha;

        warningVignette.color = color;
    }

    private void SetAllXAlpha(float alpha)
    {
        if (boundaryXs == null)
            return;

        foreach (SpriteRenderer x in boundaryXs)
        {
            if (x == null)
                continue;

            Color color = x.color;

            color.a = alpha;

            x.color = color;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInsideWarningZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInsideWarningZone = false;
    }
}