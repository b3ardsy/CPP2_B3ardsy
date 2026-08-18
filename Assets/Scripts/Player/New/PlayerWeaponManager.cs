using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerWeaponManager : MonoBehaviour
{
    [Header("Weapon Models")]
    [Tooltip("The Wand object attached to the player's hand.")]
    [SerializeField] private GameObject wandObject;

    [Tooltip("The Staff object attached to the player's other hand.")]
    [SerializeField] private GameObject staffObject;

    [Header("Waiting Fade")]
    [Tooltip("How long weapons take to fade in or out.")]
    [SerializeField] private float waitingFadeDuration = 0.25f;

    private bool hasWand = true;
    private bool hasStaff;
    private bool weaponsTemporarilyHidden;

    private Coroutine weaponFadeCoroutine;

    private readonly List<Renderer> weaponRenderers =
        new List<Renderer>();

    private readonly List<Material[]> weaponMaterials =
        new List<Material[]>();

    public bool HasWand => hasWand;
    public bool HasStaff => hasStaff;

    /*
     * Useful later for HUD updates when the Staff is collected.
     */
    public event Action OnStaffUnlocked;

    private void Awake()
    {
        hasWand = true;
        hasStaff = false;

        CacheWeaponRenderers();

        ApplyWeaponVisibility();
        ValidateReferences();
    }

    public void UnlockStaff()
    {
        if (hasStaff)
        {
            return;
        }

        hasStaff = true;

        ApplyWeaponVisibility();

        OnStaffUnlocked?.Invoke();

        Debug.Log(
            $"{name}: Staff unlocked. Wand and Staff are now available.",
            this
        );
    }

    public void HideWeaponsForWaiting()
    {
        if (weaponsTemporarilyHidden)
        {
            return;
        }

        weaponsTemporarilyHidden = true;

        StartWeaponFade(
            0f,
            true
        );
    }

    public void RestoreWeaponsAfterWaiting()
    {
        if (!weaponsTemporarilyHidden)
        {
            return;
        }

        weaponsTemporarilyHidden = false;

        /*
         * Restore the correct active objects before fading in.
         */
        ApplyWeaponVisibility();

        StartWeaponFade(
            1f,
            false
        );
    }

    private void StartWeaponFade(
        float targetAlpha,
        bool disableAfterFade
    )
    {
        if (weaponFadeCoroutine != null)
        {
            StopCoroutine(
                weaponFadeCoroutine
            );
        }

        weaponFadeCoroutine =
            StartCoroutine(
                FadeWeaponsCoroutine(
                    targetAlpha,
                    disableAfterFade
                )
            );
    }

    private IEnumerator FadeWeaponsCoroutine(
        float targetAlpha,
        bool disableAfterFade
    )
    {
        /*
         * Ensure the currently available weapons are active while
         * fading out so the fade can actually be seen.
         */
        if (disableAfterFade)
        {
            if (wandObject != null)
            {
                wandObject.SetActive(
                    hasWand
                );
            }

            if (staffObject != null)
            {
                staffObject.SetActive(
                    hasStaff
                );
            }
        }

        float startAlpha =
            GetCurrentWeaponAlpha();

        float elapsedTime = 0f;

        while (
            elapsedTime <
            waitingFadeDuration
        )
        {
            elapsedTime +=
                Time.deltaTime;

            float normalizedTime =
                waitingFadeDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        elapsedTime /
                        waitingFadeDuration
                    );

            float alpha =
                Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    normalizedTime
                );

            SetWeaponAlpha(
                alpha
            );

            yield return null;
        }

        SetWeaponAlpha(
            targetAlpha
        );

        if (disableAfterFade)
        {
            if (wandObject != null)
            {
                wandObject.SetActive(
                    false
                );
            }

            if (staffObject != null)
            {
                staffObject.SetActive(
                    false
                );
            }
        }

        weaponFadeCoroutine =
            null;
    }

    private void ApplyWeaponVisibility()
    {
        if (wandObject != null)
        {
            wandObject.SetActive(
                hasWand &&
                !weaponsTemporarilyHidden
            );
        }

        if (staffObject != null)
        {
            staffObject.SetActive(
                hasStaff &&
                !weaponsTemporarilyHidden
            );
        }

        /*
         * Normal gameplay visibility should always be fully opaque.
         */
        if (!weaponsTemporarilyHidden)
        {
            SetWeaponAlpha(
                1f
            );
        }
    }

    private void CacheWeaponRenderers()
    {
        weaponRenderers.Clear();
        weaponMaterials.Clear();

        CacheRenderersFrom(
            wandObject
        );

        CacheRenderersFrom(
            staffObject
        );
    }

    private void CacheRenderersFrom(
        GameObject weaponObject
    )
    {
        if (weaponObject == null)
        {
            return;
        }

        Renderer[] renderers =
            weaponObject.GetComponentsInChildren<Renderer>(
                true
            );

        foreach (Renderer weaponRenderer in renderers)
        {
            if (weaponRenderer == null)
            {
                continue;
            }

            weaponRenderers.Add(
                weaponRenderer
            );

            /*
             * renderer.materials creates per-renderer material instances.
             * This prevents weapon fading from changing the shared Druid
             * material used elsewhere on the character.
             */
            weaponMaterials.Add(
                weaponRenderer.materials
            );
        }
    }

    private float GetCurrentWeaponAlpha()
    {
        foreach (Material[] materials in weaponMaterials)
        {
            foreach (Material material in materials)
            {
                if (
                    material != null &&
                    material.HasProperty("_BaseColor")
                )
                {
                    return material.GetColor(
                        "_BaseColor"
                    ).a;
                }

                if (
                    material != null &&
                    material.HasProperty("_Color")
                )
                {
                    return material.color.a;
                }
            }
        }

        return 1f;
    }

    private void SetWeaponAlpha(
        float alpha
    )
    {
        alpha =
            Mathf.Clamp01(
                alpha
            );

        foreach (Material[] materials in weaponMaterials)
        {
            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    Color color =
                        material.GetColor(
                            "_BaseColor"
                        );

                    color.a =
                        alpha;

                    material.SetColor(
                        "_BaseColor",
                        color
                    );

                    continue;
                }

                if (material.HasProperty("_Color"))
                {
                    Color color =
                        material.color;

                    color.a =
                        alpha;

                    material.color =
                        color;
                }
            }
        }
    }

    private void ValidateReferences()
    {
        if (wandObject == null)
        {
            Debug.LogError(
                $"{name}: PlayerWeaponManager is missing the Wand object.",
                this
            );
        }

        if (staffObject == null)
        {
            Debug.LogError(
                $"{name}: PlayerWeaponManager is missing the Staff object.",
                this
            );
        }
    }

    private void OnValidate()
    {
        waitingFadeDuration =
            Mathf.Max(
                0f,
                waitingFadeDuration
            );
    }
}