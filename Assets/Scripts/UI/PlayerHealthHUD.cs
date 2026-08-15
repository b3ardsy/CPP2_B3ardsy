using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealthHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStatsNew playerStats;
    [SerializeField] private HeartUI heartPrefab;
    [SerializeField] private Transform heartsContainer;

    [Header("Heart Container Animation")]
    [Tooltip(
        "How long each individual heart takes to refill " +
        "during a maximum-health upgrade."
    )]
    [SerializeField] private float heartRefillDuration = 0.3f;

    [Tooltip(
        "Small delay between each heart filling."
    )]
    [SerializeField] private float delayBetweenHearts = 0.05f;

    [Tooltip(
        "Small amount of extra time added after the final heart fills."
    )]
    [SerializeField] private float animationEndPadding = 0.15f;

    private readonly List<HeartUI> hearts =
        new List<HeartUI>();

    private Coroutine healthUpgradeCoroutine;

    private int previousMaxHealth;

    private void Start()
    {
        if (playerStats == null)
        {
            playerStats =
                FindAnyObjectByType<PlayerStatsNew>();
        }

        if (playerStats == null)
        {
            Debug.LogError(
                $"{name}: PlayerHealthHUD could not find PlayerStatsNew.",
                this
            );

            enabled = false;
            return;
        }

        if (
            heartPrefab == null ||
            heartsContainer == null
        )
        {
            Debug.LogError(
                $"{name}: Heart Prefab or Hearts Container is missing.",
                this
            );

            enabled = false;
            return;
        }

        playerStats.OnHealthChanged +=
            HandleHealthChanged;

        previousMaxHealth =
            playerStats.MaxHealth;

        EnsureHeartCount(
            playerStats.MaxHealth
        );

        UpdateHeartsImmediately(
            playerStats.CurrentHealth
        );
    }

    private void HandleHealthChanged(
        int currentHealth,
        int maxHealth
    )
    {
        bool maxHealthIncreased =
            maxHealth > previousMaxHealth;

        if (maxHealthIncreased)
        {
            /*
             * Add any newly earned hearts as empty containers.
             * Existing hearts keep their current fill amounts.
             */
            EnsureHeartCount(
                maxHealth,
                true
            );

            if (healthUpgradeCoroutine != null)
            {
                StopCoroutine(
                    healthUpgradeCoroutine
                );
            }

            healthUpgradeCoroutine =
                StartCoroutine(
                    AnimateHealthUpgrade(
                        currentHealth
                    )
                );
        }
        else
        {
            /*
             * Normal damage and healing update immediately.
             */
            if (healthUpgradeCoroutine != null)
            {
                StopCoroutine(
                    healthUpgradeCoroutine
                );

                healthUpgradeCoroutine = null;
            }

            EnsureHeartCount(
                maxHealth
            );

            UpdateHeartsImmediately(
                currentHealth
            );
        }

        previousMaxHealth =
            maxHealth;
    }

    // =========================================================
    // HEART CREATION
    // =========================================================

    private void EnsureHeartCount(
        int maxHealth,
        bool newHeartsStartEmpty = false
    )
    {
        int requiredHeartCount =
            Mathf.CeilToInt(
                maxHealth /
                (float)PlayerStatsNew.HealthPerHeart
            );

        /*
         * Add only the missing hearts.
         *
         * Existing hearts stay untouched so their current
         * radial fill can animate naturally.
         */
        while (hearts.Count < requiredHeartCount)
        {
            HeartUI newHeart =
                Instantiate(
                    heartPrefab,
                    heartsContainer
                );

            newHeart.name =
                $"Heart{hearts.Count + 1:00}";

            if (newHeartsStartEmpty)
            {
                newHeart.SetFillAmount(
                    0f
                );
            }

            hearts.Add(
                newHeart
            );
        }

        /*
         * Also supports future situations where maximum
         * health might decrease.
         */
        while (hearts.Count > requiredHeartCount)
        {
            int lastIndex =
                hearts.Count - 1;

            HeartUI heartToRemove =
                hearts[lastIndex];

            hearts.RemoveAt(
                lastIndex
            );

            if (heartToRemove != null)
            {
                Destroy(
                    heartToRemove.gameObject
                );
            }
        }
    }

    // =========================================================
    // NORMAL HEALTH UPDATES
    // =========================================================

    private void UpdateHeartsImmediately(
        int currentHealth
    )
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            int healthForThisHeart =
                CalculateHealthForHeart(
                    currentHealth,
                    i
                );

            hearts[i].SetHeartHealth(
                healthForThisHeart
            );
        }
    }

    // =========================================================
    // HEART CONTAINER ANIMATION
    // =========================================================

    private IEnumerator AnimateHealthUpgrade(
        int targetHealth
    )
    {
        /*
         * Fill hearts sequentially from left to right.
         *
         * Hearts already at their target value are skipped.
         * The newly-added heart begins empty, so it naturally
         * becomes the final heart in the sequence.
         */

        List<int> heartsToAnimate =
            new List<int>();

        for (int i = 0; i < hearts.Count; i++)
        {
            int targetHealthForHeart =
                CalculateHealthForHeart(
                    targetHealth,
                    i
                );

            float targetFill =
                (float)targetHealthForHeart /
                PlayerStatsNew.HealthPerHeart;

            float startingFill =
                hearts[i].CurrentFillAmount;

            if (targetFill > startingFill)
            {
                heartsToAnimate.Add(
                    i
                );
            }
            else if (targetFill < startingFill)
            {
                hearts[i].SetFillAmount(
                    targetFill
                );
            }
        }

        for (int i = 0; i < heartsToAnimate.Count; i++)
        {
            int heartIndex =
                heartsToAnimate[i];

            HeartUI heart =
                hearts[heartIndex];

            int targetHealthForHeart =
                CalculateHealthForHeart(
                    targetHealth,
                    heartIndex
                );

            float targetFill =
                (float)targetHealthForHeart /
                PlayerStatsNew.HealthPerHeart;

            float startingFill =
                heart.CurrentFillAmount;

            yield return StartCoroutine(
                AnimateSingleHeart(
                    heart,
                    startingFill,
                    targetFill
                )
            );

            /*
             * Only delay if another heart still needs
             * to animate after this one.
             */
            if (
                i < heartsToAnimate.Count - 1 &&
                delayBetweenHearts > 0f
            )
            {
                yield return new WaitForSeconds(
                    delayBetweenHearts
                );
            }
        }

        /*
         * Guarantee exact values after animation.
         */
        UpdateHeartsImmediately(
            targetHealth
        );

        healthUpgradeCoroutine = null;
    }

    private IEnumerator AnimateSingleHeart(
        HeartUI heart,
        float startingFill,
        float targetFill
    )
    {
        if (heart == null)
        {
            yield break;
        }

        if (heartRefillDuration <= 0f)
        {
            heart.SetFillAmount(
                targetFill
            );

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < heartRefillDuration)
        {
            elapsedTime +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsedTime /
                    heartRefillDuration
                );

            float fillAmount =
                Mathf.Lerp(
                    startingFill,
                    targetFill,
                    t
                );

            heart.SetFillAmount(
                fillAmount
            );

            yield return null;
        }

        heart.SetFillAmount(
            targetFill
        );
    }

    // =========================================================
    // ANIMATION TIMING
    // =========================================================

    public float GetHealthUpgradeAnimationDuration(
        int startingHealth,
        int startingMaxHealth,
        int targetHealth,
        int targetMaxHealth
    )
    {
        int targetHeartCount =
            Mathf.CeilToInt(
                targetMaxHealth /
                (float)PlayerStatsNew.HealthPerHeart
            );

        int heartsThatNeedRefilling = 0;

        for (int i = 0; i < targetHeartCount; i++)
        {
            int startingHealthForHeart;

            /*
             * Hearts that did not exist before the upgrade
             * are treated as completely empty.
             */
            int startingHeartCount =
                Mathf.CeilToInt(
                    startingMaxHealth /
                    (float)PlayerStatsNew.HealthPerHeart
                );

            if (i >= startingHeartCount)
            {
                startingHealthForHeart = 0;
            }
            else
            {
                startingHealthForHeart =
                    CalculateHealthForHeart(
                        startingHealth,
                        i
                    );
            }

            int targetHealthForHeart =
                CalculateHealthForHeart(
                    targetHealth,
                    i
                );

            if (
                targetHealthForHeart >
                startingHealthForHeart
            )
            {
                heartsThatNeedRefilling++;
            }
        }

        if (heartsThatNeedRefilling <= 0)
        {
            return animationEndPadding;
        }

        float refillTime =
            heartsThatNeedRefilling *
            heartRefillDuration;

        float delayTime =
            Mathf.Max(
                0,
                heartsThatNeedRefilling - 1
            ) *
            delayBetweenHearts;

        return
            refillTime +
            delayTime +
            animationEndPadding;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private int CalculateHealthForHeart(
        int currentHealth,
        int heartIndex
    )
    {
        int healthForThisHeart =
            currentHealth -
            (
                heartIndex *
                PlayerStatsNew.HealthPerHeart
            );

        return
            Mathf.Clamp(
                healthForThisHeart,
                0,
                PlayerStatsNew.HealthPerHeart
            );
    }

    private void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -=
                HandleHealthChanged;
        }
    }

    private void OnValidate()
    {
        heartRefillDuration =
            Mathf.Max(
                0f,
                heartRefillDuration
            );

        delayBetweenHearts =
            Mathf.Max(
                0f,
                delayBetweenHearts
            );

        animationEndPadding =
            Mathf.Max(
                0f,
                animationEndPadding
            );
    }
}