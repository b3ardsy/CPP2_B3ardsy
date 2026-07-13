using System.Collections.Generic;
using UnityEngine;

public class RandomPotionSpawner : MonoBehaviour
{
    [Header("Potion Prefabs")]
    [SerializeField] private GameObject[] potionPrefabs;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        SpawnPotions();
    }

    private void SpawnPotions()
    {
        // Make a temporary list so the same spawn point
        // cannot be selected more than once.
        List<Transform> availableSpawnPoints =
            new List<Transform>(spawnPoints);

        for (int i = 0; i < potionPrefabs.Length; i++)
        {
            int randomIndex =
                Random.Range(0, availableSpawnPoints.Count);

            Transform selectedSpawnPoint =
                availableSpawnPoints[randomIndex];

            Instantiate(
                potionPrefabs[i],
                selectedSpawnPoint.position,
                selectedSpawnPoint.rotation
            );

            // Remove the selected point so another potion
            // cannot spawn at the same location.
            availableSpawnPoints.RemoveAt(randomIndex);
        }
    }
}