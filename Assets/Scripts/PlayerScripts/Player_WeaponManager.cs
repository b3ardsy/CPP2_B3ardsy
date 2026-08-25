using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_WeaponManager : MonoBehaviour
{
    // =========================================================
    // WEAPON MODELS
    // =========================================================

    [Header("Weapon Models")]
    [Tooltip("The Wand object attached to the player's hand.")]
    [SerializeField]
    private GameObject wandObject;

    [Tooltip("The Staff object attached to the player's other hand.")]
    [SerializeField]
    private GameObject staffObject;

    // =========================================================
    // WAITING FADE
    // =========================================================

    [Header("Waiting Fade")]
    [Tooltip("How long weapons take to fade in or out.")]
    [SerializeField]
    private float waitingFadeDuration = 0.25f;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private bool hasWand = true;
    private bool hasStaff;
    private bool weaponsTemporarilyHidden;

    private Coroutine weaponFadeCoroutine;

    private readonly List<Renderer> weaponRenderers =
        new List<Renderer>();

    private readonly List<Material[]> weaponMaterials =
        new List<Material[]>();

    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool HasWand =>
        hasWand;

    public bool HasStaff =>
        hasStaff;

    /*
     * Raised once when the Staff is collected.
     *
     * Other systems such as Shield and HUD can listen
     * for this without Player_WeaponManager needing to
     * know anything about those systems.
     */
    public event Action OnStaffUnlocked;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        /*
         * Current progression:
         *
         * - Player always begins with the Wand.
         * - Staff must be collected during gameplay.
         */
        hasWand = true;
        hasStaff = false;

        CacheWeaponRenderers();

        ApplyWeaponVisibility();

        ValidateReferences();
    }

    // =========================================================
    // STAFF PROGRESSION
    // =========================================================

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
            $"{name}: Staff unlocked. " +
            "Wand and Staff are now available.",
            this
        );
    }

    // =========================================================
    // WAITING / IDLE VISIBILITY
    // =========================================================

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

    // =========================================================
    // FADING
    // =========================================================

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
         * Ensure currently-owned weapons are active while
         * fading out so the fade is actually visible.
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

        float elapsedTime =
            0f;

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

    // =========================================================
    // VISIBILITY
    // =========================================================

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
         * Normal gameplay visibility should always
         * restore full opacity.
         */
        if (!weaponsTemporarilyHidden)
        {
            SetWeaponAlpha(
                1f
            );
        }
    }

    // =========================================================
    // RENDERER CACHE
    // =========================================================

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

        foreach (
            Renderer weaponRenderer
            in renderers
        )
        {
            if (weaponRenderer == null)
            {
                continue;
            }

            weaponRenderers.Add(
                weaponRenderer
            );

            /*
             * renderer.materials creates per-renderer
             * material instances.
             *
             * This prevents fading a weapon from modifying
             * a shared character material elsewhere.
             */
            weaponMaterials.Add(
                weaponRenderer.materials
            );
        }
    }

    // =========================================================
    // MATERIAL ALPHA
    // =========================================================

    private float GetCurrentWeaponAlpha()
    {
        foreach (
            Material[] materials
            in weaponMaterials
        )
        {
            foreach (
                Material material
                in materials
            )
            {
                if (
                    material != null &&
                    material.HasProperty(
                        "_BaseColor"
                    )
                )
                {
                    return
                        material.GetColor(
                            "_BaseColor"
                        ).a;
                }

                if (
                    material != null &&
                    material.HasProperty(
                        "_Color"
                    )
                )
                {
                    return
                        material.color.a;
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

        foreach (
            Material[] materials
            in weaponMaterials
        )
        {
            foreach (
                Material material
                in materials
            )
            {
                if (material == null)
                {
                    continue;
                }

                if (
                    material.HasProperty(
                        "_BaseColor"
                    )
                )
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

                if (
                    material.HasProperty(
                        "_Color"
                    )
                )
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

    // =========================================================
    // VALIDATION
    // =========================================================

    private void ValidateReferences()
    {
        if (wandObject == null)
        {
            Debug.LogError(
                $"{name}: Player_WeaponManager is missing " +
                "the Wand object.",
                this
            );
        }

        if (staffObject == null)
        {
            Debug.LogError(
                $"{name}: Player_WeaponManager is missing " +
                "the Staff object.",
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