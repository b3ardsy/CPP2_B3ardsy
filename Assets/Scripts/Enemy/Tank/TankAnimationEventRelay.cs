using UnityEngine;

public class TankAnimationEventRelay : MonoBehaviour
{
    private TankEnemy tankEnemy;

    private void Awake()
    {
        tankEnemy = GetComponentInParent<TankEnemy>();

        if (tankEnemy == null)
        {
            Debug.LogError(
                $"{name}: Could not find TankEnemy on a parent object.",
                this
            );
        }
    }

    public void EnableAxeHitbox()
    {
        if (tankEnemy != null)
        {
            tankEnemy.EnableAxeHitbox();
        }
    }

    public void DisableAxeHitbox()
    {
        if (tankEnemy != null)
        {
            tankEnemy.DisableAxeHitbox();
        }
    }

    public void EndAttack()
    {
        if (tankEnemy != null)
        {
            tankEnemy.EndAttack();
        }
    }
}