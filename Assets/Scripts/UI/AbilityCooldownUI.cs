using UnityEngine;
using UnityEngine.UI;

public class AbilityCooldownUI : MonoBehaviour
{
    [Header("Cooldown Overlay")]
    [SerializeField] private Image cooldownOverlay;

    public void SetCooldown(
        float remainingCooldown,
        float totalCooldown
    )
    {
        if (cooldownOverlay == null)
        {
            return;
        }

        if (totalCooldown <= 0f)
        {
            cooldownOverlay.fillAmount = 0f;
            return;
        }

        float normalizedCooldown =
            Mathf.Clamp01(
                remainingCooldown /
                totalCooldown
            );

        cooldownOverlay.fillAmount =
            normalizedCooldown;
    }

    public void SetReady()
    {
        if (cooldownOverlay == null)
        {
            return;
        }

        cooldownOverlay.fillAmount = 0f;
    }

    private void OnValidate()
    {
        if (cooldownOverlay == null)
        {
            Transform overlayTransform =
                transform.Find("CooldownOverlay");

            if (overlayTransform != null)
            {
                cooldownOverlay =
                    overlayTransform.GetComponent<Image>();
            }
        }
    }
}