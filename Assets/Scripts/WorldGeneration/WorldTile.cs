using UnityEngine;

public class WorldTile : MonoBehaviour
{
    [Header("Grid Position")]
    [SerializeField] private int gridX;
    [SerializeField] private int gridZ;

    [Header("Biome")]
    [SerializeField] private string biomeName = "Green";

    public int GridX => gridX;
    public int GridZ => gridZ;
    public string BiomeName => biomeName;

    public void Initialize(int x, int z, string startingBiome)
    {
        gridX = x;
        gridZ = z;
        biomeName = startingBiome;
    }

    public void SetBiome(string newBiomeName)
    {
        biomeName = newBiomeName;
    }
}