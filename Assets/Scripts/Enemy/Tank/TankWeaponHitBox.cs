using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TankWeaponHitbox : MonoBehaviour
{
    private TankEnemy owner;
    private Collider hitboxCollider;

    private bool hitboxActive;

    /*
     * Prevents multiple Player colliders or repeated physics frames
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
                $"{name}: The weapon collider was not marked as a trigger. " +
                "Is Trigger has now been enabled.",
                this
            );

            hitboxCollider.isTrigger =
                true;
        }

        owner =
            GetComponentInParent<TankEnemy>();

        if (owner == null)
        {
            Debug.LogError(
                $"{name}: Could not find TankEnemy on a parent object.",
                this
            );
        }

        DisableHitbox();
    }

    public void SetOwner(
        TankEnemy tankEnemy
    )
    {
        owner =
            tankEnemy;
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
         * Handles the case where the Player is already overlapping
         * when the animation enables the hit window.
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
            owner == null
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

        owner.TryDamagePlayer(
            targetPlayer
        );
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