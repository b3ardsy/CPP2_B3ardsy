using UnityEngine;

public class StaffPickup : MonoBehaviour, IInteract
{
    private bool hasBeenCollected;

    public void Interact(PlayerInteraction interactor)
    {
        if (hasBeenCollected)
        {
            return;
        }

        if (interactor == null)
        {
            return;
        }

        PlayerWeaponManager weaponManager =
            interactor.GetWeaponManager();

        if (weaponManager == null)
        {
            Debug.LogError(
                $"{name}: Player does not have a PlayerWeaponManager.",
                this
            );

            return;
        }

        hasBeenCollected = true;

        weaponManager.UnlockStaff();

        interactor.ClearCurrentInteractable();

        Debug.Log(
            $"{name}: Staff picked up.",
            this
        );

        Destroy(gameObject);
    }
}