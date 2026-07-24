using UnityEngine;

public class RogueAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private RogueEnemy rogueEnemy;

    private void Awake()
    {
        if (rogueEnemy == null)
        {
            rogueEnemy = GetComponentInParent<RogueEnemy>();
        }

        if (rogueEnemy == null)
        {
            Debug.LogError(
                $"{name}: No RogueEnemy was found in the parent hierarchy.",
                this
            );
        }
    }

    public void CastSpell()
    {
        if (rogueEnemy != null)
        {
            rogueEnemy.CastSpell();
        }
    }

    public void EndAttack()
    {
        if (rogueEnemy != null)
        {
            rogueEnemy.EndAttack();
        }
    }
}