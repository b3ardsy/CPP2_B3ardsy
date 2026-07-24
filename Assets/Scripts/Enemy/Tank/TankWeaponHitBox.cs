using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TankWeaponHitbox : MonoBehaviour
{
    private TankEnemy owner;
    private Collider hitboxCollider;

    private bool hitboxActive;

    // Prevents multiple player colliders or repeated physics frames
    // from dealing damage more than once during one hit window.
    private readonly HashSet<PlayerStats> playersHitThisWindow =
        new HashSet<PlayerStats>();

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();

        if (!hitboxCollider.isTrigger)
        {
            Debug.LogWarning(
                $"{name}: The weapon collider was not marked as a trigger. " +
                "Is Trigger has now been enabled.",
                this
            );

            hitboxCollider.isTrigger = true;
        }

        owner = GetComponentInParent<TankEnemy>();

        if (owner == null)
        {
            Debug.LogError(
                $"{name}: Could not find TankEnemy on a parent object.",
                this
            );
        }

        DisableHitbox();
    }

    public void SetOwner(TankEnemy tankEnemy)
    {
        owner = tankEnemy;
    }

    public void EnableHitbox()
    {
        playersHitThisWindow.Clear();
        hitboxActive = true;
    }

    public void DisableHitbox()
    {
        hitboxActive = false;
        playersHitThisWindow.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHitPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Handles the case where the player is already overlapping
        // when the animation enables the hit window.
        TryHitPlayer(other);
    }

    private void TryHitPlayer(Collider other)
    {
        if (!hitboxActive || owner == null)
        {
            return;
        }

        PlayerStats targetPlayer =
            other.GetComponentInParent<PlayerStats>();

        if (targetPlayer == null)
        {
            return;
        }

        if (playersHitThisWindow.Contains(targetPlayer))
        {
            return;
        }

        playersHitThisWindow.Add(targetPlayer);
        owner.TryDamagePlayer(targetPlayer);
    }

    private void OnDisable()
    {
        DisableHitbox();
    }
}