using UnityEngine;

[RequireComponent(typeof(PersistentID))]
public class StaffPickup : MonoBehaviour, IInteract, ICheckpointResettable
{
    [Header("Notification")]
    [Tooltip(
        "Optional HUD banner used to display the Staff unlock message. " +
        "If left empty, it will be found automatically."
    )]
    [SerializeField]
    private HUDNotificationBanner notificationBanner;

    [TextArea]
    [Tooltip("Message displayed when the Staff is collected.")]
    [SerializeField]
    private string unlockMessage =
        "The ancient staff answers your call: Shield Unlocked";

    private bool hasBeenCollected;

    public bool IsCheckpointAvailable =>
        !hasBeenCollected;

    private void Awake()
    {
        /*
         * The Staff is a prefab, so a scene HUD reference
         * usually cannot be assigned directly to the prefab asset.
         *
         * Find the notification banner automatically instead.
         */
        if (notificationBanner == null)
        {
            notificationBanner =
                FindAnyObjectByType<HUDNotificationBanner>();
        }
    }

    public void Interact(
        PlayerInteraction interactor
    )
    {
        if (hasBeenCollected)
        {
            return;
        }

        if (interactor == null)
        {
            return;
        }

        Player_WeaponManager weaponManager =
            interactor.GetWeaponManager();

        if (weaponManager == null)
        {
            Debug.LogError(
                $"{name}: Player does not have a " +
                "Player_WeaponManager.",
                this
            );

            return;
        }

        hasBeenCollected = true;

        /*
         * Unlock the Staff first.
         *
         * Player_WeaponManager owns Staff progression
         * and raises OnStaffUnlocked for listening systems.
         */
        weaponManager.UnlockStaff();

        AudioManager.Instance?.Play(
            SoundId.StaffUnlock,
            transform.position
        );

        /*
         * Remove this pickup from the player's current
         * interaction target.
         */
        interactor.ClearCurrentInteractable();

        /*
         * Display the acquisition message using the
         * shared HUD notification system.
         */
        if (notificationBanner != null)
        {
            notificationBanner.ShowMessage(
                unlockMessage
            );
        }
        else
        {
            Debug.LogWarning(
                $"{name}: HUDNotificationBanner " +
                "could not be found.",
                this
            );
        }

        Debug.Log(
            $"{name}: Staff picked up.",
            this
        );

        gameObject.SetActive(
            false
        );
    }
    // =========================================================
    // CHECKPOINT RESTORE
    // =========================================================

    public void RestoreCheckpointState(
        bool wasAvailable
    )
    {
        hasBeenCollected =
            !wasAvailable;

        gameObject.SetActive(
            wasAvailable
        );
    }

}