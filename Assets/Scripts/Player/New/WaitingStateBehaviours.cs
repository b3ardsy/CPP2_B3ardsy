using UnityEngine;

public class WaitingStateBehaviour : StateMachineBehaviour
{
    private PlayerWeaponManager weaponManager;

    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex
    )
    {
        FindWeaponManager(
            animator
        );

        if (weaponManager != null)
        {
            weaponManager.HideWeaponsForWaiting();
        }
    }

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex
    )
    {
        FindWeaponManager(
            animator
        );

        if (weaponManager != null)
        {
            weaponManager.RestoreWeaponsAfterWaiting();
        }
    }

    private void FindWeaponManager(
        Animator animator
    )
    {
        if (
            weaponManager != null ||
            animator == null
        )
        {
            return;
        }

        weaponManager =
            animator.GetComponent<PlayerWeaponManager>();

        if (weaponManager == null)
        {
            weaponManager =
                animator.GetComponentInParent<PlayerWeaponManager>();
        }
    }
}