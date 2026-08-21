using UnityEngine;
using UnityEngine.UI;

public class HeartUI : MonoBehaviour
{
    // =========================================================
    // HEART SETTINGS
    // =========================================================

    private const int HealthPerHeart = 4;

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Heart Images")]
    [SerializeField]
    private Image fullHeartImage;

    // =========================================================
    // PUBLIC PROPERTIES
    // =========================================================

    public float CurrentFillAmount
    {
        get
        {
            if (fullHeartImage == null)
            {
                return 0f;
            }

            return
                fullHeartImage.fillAmount;
        }
    }

    // =========================================================
    // HEALTH DISPLAY
    // =========================================================

    /*
     * Sets this heart using quarter-heart health units.
     *
     * 4 = 100%
     * 3 = 75%
     * 2 = 50%
     * 1 = 25%
     * 0 = Empty
     */
    public void SetHeartHealth(
        int healthInHeart
    )
    {
        if (fullHeartImage == null)
        {
            Debug.LogWarning(
                $"{name}: Full Heart Image has not been assigned.",
                this
            );

            return;
        }

        healthInHeart =
            Mathf.Clamp(
                healthInHeart,
                0,
                HealthPerHeart
            );

        float fillAmount =
            (float)healthInHeart /
            HealthPerHeart;

        SetFillAmount(
            fillAmount
        );
    }

    /*
     * Allows the HUD to smoothly animate
     * the radial fill.
     */
    public void SetFillAmount(
        float fillAmount
    )
    {
        if (fullHeartImage == null)
        {
            return;
        }

        fullHeartImage.fillAmount =
            Mathf.Clamp01(
                fillAmount
            );
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        if (fullHeartImage == null)
        {
            fullHeartImage =
                transform.Find(
                    "FullHeart"
                )
                ?.GetComponent<Image>();
        }
    }
}