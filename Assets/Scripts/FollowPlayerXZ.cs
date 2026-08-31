using UnityEngine;

public class FollowPlayerXZ : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float heightOffset = 20f;

    private void LateUpdate()
    {
        if (player == null)
            return;

        transform.position = new Vector3(
            player.position.x,
            player.position.y + heightOffset,
            player.position.z
        );
    }
}