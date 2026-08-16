using System.Collections;
using TMPro;
using UnityEngine;

public class HUDNotificationBanner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The complete banner object that is shown and hidden.")]
    [SerializeField] private GameObject bannerObject;

    [Tooltip("Text displayed inside the banner.")]
    [SerializeField] private TMP_Text bannerText;

    [Header("Timing")]
    [Tooltip("How long the banner remains fully visible.")]
    [SerializeField] private float displayDuration = 2.5f;

    [Tooltip("How long the fade in takes.")]
    [SerializeField] private float fadeInDuration = 0.25f;

    [Tooltip("How long the fade out takes.")]
    [SerializeField] private float fadeOutDuration = 0.4f;

    private CanvasGroup canvasGroup;
    private Coroutine activeRoutine;

    private void Awake()
    {
        FindReferences();
        ValidateReferences();

        if (bannerObject != null)
        {
            bannerObject.SetActive(false);
        }
    }

    public void ShowMessage(string message)
    {
        if (
            bannerObject == null ||
            bannerText == null ||
            canvasGroup == null
        )
        {
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine =
            StartCoroutine(
                ShowMessageRoutine(message)
            );
    }

    private IEnumerator ShowMessageRoutine(
        string message
    )
    {
        bannerText.text =
            message;

        bannerObject.SetActive(true);

        canvasGroup.alpha = 0f;

        yield return FadeCanvasGroup(
            0f,
            1f,
            fadeInDuration
        );

        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(
            displayDuration
        );

        yield return FadeCanvasGroup(
            1f,
            0f,
            fadeOutDuration
        );

        canvasGroup.alpha = 0f;

        bannerObject.SetActive(false);

        activeRoutine = null;
    }

    private IEnumerator FadeCanvasGroup(
        float startAlpha,
        float endAlpha,
        float duration
    )
    {
        if (duration <= 0f)
        {
            canvasGroup.alpha =
                endAlpha;

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsedTime /
                    duration
                );

            canvasGroup.alpha =
                Mathf.Lerp(
                    startAlpha,
                    endAlpha,
                    progress
                );

            yield return null;
        }

        canvasGroup.alpha =
            endAlpha;
    }

    private void FindReferences()
    {
        if (bannerObject == null)
        {
            Transform bannerTransform =
                transform.Find("BannerBG");

            if (bannerTransform != null)
            {
                bannerObject =
                    bannerTransform.gameObject;
            }
        }

        if (
            bannerText == null &&
            bannerObject != null
        )
        {
            bannerText =
                bannerObject
                    .GetComponentInChildren<TMP_Text>();
        }

        if (bannerObject != null)
        {
            canvasGroup =
                bannerObject.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup =
                    bannerObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void ValidateReferences()
    {
        if (bannerObject == null)
        {
            Debug.LogError(
                $"{name}: HUDNotificationBanner could not find BannerBG.",
                this
            );

            enabled = false;
            return;
        }

        if (bannerText == null)
        {
            Debug.LogError(
                $"{name}: HUDNotificationBanner could not find banner text.",
                this
            );

            enabled = false;
        }
    }

    private void OnValidate()
    {
        displayDuration =
            Mathf.Max(
                0f,
                displayDuration
            );

        fadeInDuration =
            Mathf.Max(
                0f,
                fadeInDuration
            );

        fadeOutDuration =
            Mathf.Max(
                0f,
                fadeOutDuration
            );
    }
}