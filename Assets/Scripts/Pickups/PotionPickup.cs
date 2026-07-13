using System;
using UnityEngine;

public class PotionPickup : MonoBehaviour
{
    public enum PotionType
    {
        Red,
        Blue,
        Green
    }

    [Header("Potion Settings")]
    [SerializeField] private PotionType potionType;

    private bool hasBeenCollected;

    private void OnTriggerEnter(Collider other)
    {
        // Only allow the player to collect the potion.
        if (!other.CompareTag("Player"))
        {
            return;
        }

        // Prevent the interaction from running more than once.
        if (hasBeenCollected)
        {
            return;
        }

        hasBeenCollected = true;

        CollectPotion();
    }

    private void CollectPotion()
    {
        switch (potionType)
        {
            case PotionType.Red:
                CollectRedPotion();
                break;

            case PotionType.Blue:
                CollectBluePotion();
                break;

            case PotionType.Green:
                CollectGreenPotion();
                break;
        }

        Destroy(gameObject);
    }

    private void CollectRedPotion()
    {
        try
        {
            throw new InvalidOperationException(
                "The red potion is unstable and cannot be consumed."
            );
        }
        catch (InvalidOperationException ex)
        {
            Debug.LogException(ex);
        }
    }

    private void CollectBluePotion()
    {
        try
        {
            throw new ArgumentException(
                "The blue potion contains an invalid ingredient."
            );
        }
        catch (ArgumentException ex)
        {
            Debug.LogException(ex);
        }
    }

    private void CollectGreenPotion()
    {
        Debug.Log("The player collected the green potion.");
    }
}