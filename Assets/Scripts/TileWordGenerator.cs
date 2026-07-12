using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TileWorldGenerator : EditorWindow
{
    private const string WorldParentName = "Generated Tile World";

    private GameObject defaultTilePrefab;

    private string startingBiomeName = "Green";

    private int tilesWide = 167;
    private int tilesDeep = 167;

    private float tileSize = 3f;
    private float worldY = 0f;

    private int chunkSize = 16;

    private bool centerWorld = true;

    [MenuItem("Tools/Tile World Generator")]
    public static void ShowWindow()
    {
        GetWindow<TileWorldGenerator>(
            "Tile World Generator"
        );
    }

    private void OnGUI()
    {
        GUILayout.Label(
            "Tile World Generator V2",
            EditorStyles.boldLabel
        );

        GUILayout.Space(8);

        GUILayout.Label(
            "Default Tile",
            EditorStyles.boldLabel
        );

        defaultTilePrefab =
            (GameObject)EditorGUILayout.ObjectField(
                "Default Tile Prefab",
                defaultTilePrefab,
                typeof(GameObject),
                false
            );

        startingBiomeName =
            EditorGUILayout.TextField(
                "Starting Biome",
                startingBiomeName
            );

        GUILayout.Space(8);

        GUILayout.Label(
            "World Size",
            EditorStyles.boldLabel
        );

        tilesWide =
            EditorGUILayout.IntField(
                "Tiles Wide",
                tilesWide
            );

        tilesDeep =
            EditorGUILayout.IntField(
                "Tiles Deep",
                tilesDeep
            );

        tileSize =
            EditorGUILayout.FloatField(
                "Tile Size",
                tileSize
            );

        worldY =
            EditorGUILayout.FloatField(
                "World Y Position",
                worldY
            );

        GUILayout.Space(8);

        GUILayout.Label(
            "Chunk Settings",
            EditorStyles.boldLabel
        );

        chunkSize =
            EditorGUILayout.IntField(
                "Chunk Size",
                chunkSize
            );

        GUILayout.Space(8);

        GUILayout.Label(
            "Generation Settings",
            EditorStyles.boldLabel
        );

        centerWorld =
            EditorGUILayout.Toggle(
                "Center World At Origin",
                centerWorld
            );

        GUILayout.Space(8);

        long totalTiles =
            (long)Mathf.Max(0, tilesWide) *
            Mathf.Max(0, tilesDeep);

        float worldWidth =
            tilesWide * tileSize;

        float worldDepth =
            tilesDeep * tileSize;

        EditorGUILayout.HelpBox(
            $"World Size: {worldWidth:F1} × " +
            $"{worldDepth:F1} units\n" +
            $"Total Tiles: {totalTiles:N0}\n" +
            $"Starting Biome: {startingBiomeName}",
            MessageType.Info
        );

        if (totalTiles > 50000)
        {
            EditorGUILayout.HelpBox(
                "This world contains more than " +
                "50,000 separate tiles. The Unity Editor " +
                "may slow down while generating or editing it.",
                MessageType.Warning
            );
        }

        GUILayout.Space(10);

        if (GUILayout.Button(
            "Generate World",
            GUILayout.Height(30)
        ))
        {
            GenerateWorld();
        }

        GUILayout.Space(5);

        if (GUILayout.Button(
            "Delete Generated World",
            GUILayout.Height(25)
        ))
        {
            DeleteWorld(true);
        }
    }

    private void GenerateWorld()
    {
        if (!ValidateSettings())
        {
            return;
        }

        int totalTiles =
            tilesWide * tilesDeep;

        if (totalTiles > 50000)
        {
            bool shouldGenerate =
                EditorUtility.DisplayDialog(
                    "Large World Warning",
                    $"You are about to generate " +
                    $"{totalTiles:N0} separate tiles.\n\n" +
                    "This may temporarily slow down Unity.",
                    "Generate",
                    "Cancel"
                );

            if (!shouldGenerate)
            {
                return;
            }
        }

        DeleteWorld(false);

        GameObject worldParent =
            new GameObject(WorldParentName);

        Undo.RegisterCreatedObjectUndo(
            worldParent,
            "Generate Tile World"
        );

        float startX = 0f;
        float startZ = 0f;

        if (centerWorld)
        {
            startX =
                -((tilesWide - 1) * tileSize) / 2f;

            startZ =
                -((tilesDeep - 1) * tileSize) / 2f;
        }

        int tilesCreated = 0;

        try
        {
            for (
                int chunkStartZ = 0;
                chunkStartZ < tilesDeep;
                chunkStartZ += chunkSize
            )
            {
                for (
                    int chunkStartX = 0;
                    chunkStartX < tilesWide;
                    chunkStartX += chunkSize
                )
                {
                    int chunkX =
                        chunkStartX / chunkSize;

                    int chunkZ =
                        chunkStartZ / chunkSize;

                    GameObject chunkParent =
                        new GameObject(
                            $"Chunk_{chunkX}_{chunkZ}"
                        );

                    Undo.RegisterCreatedObjectUndo(
                        chunkParent,
                        "Create Tile Chunk"
                    );

                    chunkParent.transform.SetParent(
                        worldParent.transform
                    );

                    chunkParent.transform.localPosition =
                        Vector3.zero;

                    int chunkEndX =
                        Mathf.Min(
                            chunkStartX + chunkSize,
                            tilesWide
                        );

                    int chunkEndZ =
                        Mathf.Min(
                            chunkStartZ + chunkSize,
                            tilesDeep
                        );

                    for (
                        int z = chunkStartZ;
                        z < chunkEndZ;
                        z++
                    )
                    {
                        for (
                            int x = chunkStartX;
                            x < chunkEndX;
                            x++
                        )
                        {
                            float progress =
                                totalTiles > 0
                                    ? (float)tilesCreated /
                                      totalTiles
                                    : 0f;

                            EditorUtility.DisplayProgressBar(
                                "Generating Tile World",
                                $"Creating tile " +
                                $"{tilesCreated + 1:N0} of " +
                                $"{totalTiles:N0}",
                                progress
                            );

                            GameObject tile =
                                PrefabUtility
                                    .InstantiatePrefab(
                                        defaultTilePrefab
                                    ) as GameObject;

                            if (tile == null)
                            {
                                Debug.LogError(
                                    "Tile World Generator: " +
                                    "Could not instantiate the " +
                                    "default tile prefab."
                                );

                                continue;
                            }

                            Undo.RegisterCreatedObjectUndo(
                                tile,
                                "Create World Tile"
                            );

                            tile.name =
                                $"Tile_{x}_{z}_" +
                                $"{startingBiomeName}";

                            tile.transform.position =
                                new Vector3(
                                    startX + x * tileSize,
                                    worldY,
                                    startZ + z * tileSize
                                );

                            tile.transform.rotation =
                                defaultTilePrefab
                                    .transform
                                    .rotation;

                            tile.transform.localScale =
                                defaultTilePrefab
                                    .transform
                                    .localScale;

                            tile.transform.SetParent(
                                chunkParent.transform,
                                true
                            );

                            WorldTile worldTile =
                                tile.GetComponent<WorldTile>();

                            if (worldTile == null)
                            {
                                worldTile =
                                    Undo.AddComponent<WorldTile>(
                                        tile
                                    );
                            }

                            worldTile.Initialize(
                                x,
                                z,
                                startingBiomeName
                            );

                            EditorUtility.SetDirty(
                                worldTile
                            );

                            tilesCreated++;
                        }
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Selection.activeGameObject =
            worldParent;

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        float generatedWorldWidth =
            tilesWide * tileSize;

        float generatedWorldDepth =
            tilesDeep * tileSize;

        Debug.Log(
            $"Generated {tilesCreated:N0} tiles.\n" +
            $"Grid: {tilesWide} × {tilesDeep}\n" +
            $"World Size: {generatedWorldWidth:F1} × " +
            $"{generatedWorldDepth:F1} units\n" +
            $"Chunk Size: {chunkSize} × {chunkSize}\n" +
            $"Starting Biome: {startingBiomeName}"
        );
    }

    private bool ValidateSettings()
    {
        if (defaultTilePrefab == null)
        {
            Debug.LogError(
                "Tile World Generator: " +
                "No default tile prefab assigned."
            );

            return false;
        }

        if (tilesWide <= 0 || tilesDeep <= 0)
        {
            Debug.LogError(
                "Tile World Generator: " +
                "World dimensions must be greater than zero."
            );

            return false;
        }

        if (tileSize <= 0f)
        {
            Debug.LogError(
                "Tile World Generator: " +
                "Tile size must be greater than zero."
            );

            return false;
        }

        if (chunkSize <= 0)
        {
            Debug.LogError(
                "Tile World Generator: " +
                "Chunk size must be greater than zero."
            );

            return false;
        }

        if (string.IsNullOrWhiteSpace(startingBiomeName))
        {
            Debug.LogError(
                "Tile World Generator: " +
                "Starting biome name cannot be empty."
            );

            return false;
        }

        return true;
    }

    private void DeleteWorld(bool showMessage)
    {
        GameObject world =
            GameObject.Find(WorldParentName);

        if (world == null)
        {
            if (showMessage)
            {
                Debug.Log(
                    "No generated tile world was found."
                );
            }

            return;
        }

        Undo.DestroyObjectImmediate(world);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        if (showMessage)
        {
            Debug.Log(
                "Generated tile world deleted."
            );
        }
    }
}