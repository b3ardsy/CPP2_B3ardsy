using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TankWeaponHitbox : MonoBehaviour
{
    /*
     * Temporary migration support:
     *
     * New Tank uses TankLogic.
     * Legacy TankEnemy remains supported until the prefab swap
     * is complete so the project can compile safely.
     */
    private TankLogic owner;
    private TankEnemy legacyOwner;

    private Collider hitboxCollider;

    private bool hitboxActive;

    /*
     * Prevent multiple Player colliders or repeated physics frames
     * from dealing damage more than once during one hit window.
     */
    private readonly HashSet<IAxeDamageable> playersHitThisWindow =
        new HashSet<IAxeDamageable>();

    private void Awake()
    {
        hitboxCollider =
            GetComponent<Collider>();

        if (!hitboxCollider.isTrigger)
        {
            Debug.LogWarning(
                $"{name}: Tank weapon collider was not marked " +
                "as a trigger. Is Trigger has now been enabled.",
                this
            );

            hitboxCollider.isTrigger =
                true;
        }

        owner =
            GetComponentInParent<TankLogic>();

        if (owner == null)
        {
            legacyOwner =
                GetComponentInParent<TankEnemy>();
        }

        /*
         * During migration, either TankLogic or TankEnemy may own
         * this hitbox. The new TankLogic will explicitly call SetOwner.
         */
        DisableHitbox();
    }

    public void SetOwner(
        TankLogic tankLogic
    )
    {
        owner =
            tankLogic;

        legacyOwner =
            null;
    }

    /*
     * Temporary overload for the old TankEnemy script.
     *
     * Remove this overload after TankEnemy has been retired.
     */
    public void SetOwner(
        TankEnemy tankEnemy
    )
    {
        legacyOwner =
            tankEnemy;

        owner =
            null;
    }

    public void EnableHitbox()
    {
        playersHitThisWindow.Clear();

        hitboxActive =
            true;
    }

    public void DisableHitbox()
    {
        hitboxActive =
            false;

        playersHitThisWindow.Clear();
    }

    private void OnTriggerEnter(
        Collider other
    )
    {
        TryHitPlayer(
            other
        );
    }

    private void OnTriggerStay(
        Collider other
    )
    {
        /*
         * Handles the case where the Player is already
         * overlapping when the hit window opens.
         */
        TryHitPlayer(
            other
        );
    }

    private void TryHitPlayer(
        Collider other
    )
    {
        if (
            !hitboxActive ||
            (
                owner == null &&
                legacyOwner == null
            )
        )
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        IAxeDamageable targetPlayer =
            FindAxeDamageable(
                other
            );

        if (targetPlayer == null)
        {
            return;
        }

        if (
            playersHitThisWindow.Contains(
                targetPlayer
            )
        )
        {
            return;
        }

        playersHitThisWindow.Add(
            targetPlayer
        );

        if (owner != null)
        {
            owner.TryDamagePlayer(
                targetPlayer
            );

            return;
        }

        if (legacyOwner != null)
        {
            legacyOwner.TryDamagePlayer(
                targetPlayer
            );
        }
    }

    private IAxeDamageable FindAxeDamageable(
        Collider other
    )
    {
        if (other == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours =
            other.GetComponentsInParent<MonoBehaviour>(
                true
            );

        foreach (
            MonoBehaviour behaviour
            in behaviours
        )
        {
            if (
                behaviour is
                IAxeDamageable axeDamageable
            )
            {
                return axeDamageable;
            }
        }

        return null;
    }

    private bool IsPlayerCollider(
        Collider other
    )
    {
        if (other == null)
        {
            return false;
        }

        Transform currentTransform =
            other.transform;

        while (currentTransform != null)
        {
            if (
                currentTransform.CompareTag(
                    "Player"
                )
            )
            {
                return true;
            }

            currentTransform =
                currentTransform.parent;
        }

        return false;
    }

    private void OnDisable()
    {
        DisableHitbox();
    }
}