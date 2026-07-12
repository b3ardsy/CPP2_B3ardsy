using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class BiomeTilePainter : EditorWindow
{
    private GameObject paintTilePrefab;
    private string biomeName = "Dirt";

    private bool paintMode;
    private int brushRadius = 0;

    [MenuItem("Tools/Biome Tile Painter")]
    public static void ShowWindow()
    {
        GetWindow<BiomeTilePainter>("Biome Tile Painter");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        GUILayout.Label(
            "Biome Tile Painter",
            EditorStyles.boldLabel
        );

        GUILayout.Space(8);

        paintTilePrefab =
            (GameObject)EditorGUILayout.ObjectField(
                "Paint Tile Prefab",
                paintTilePrefab,
                typeof(GameObject),
                false
            );

        biomeName =
            EditorGUILayout.TextField(
                "Biome Name",
                biomeName
            );

        brushRadius =
            EditorGUILayout.IntSlider(
                "Brush Radius",
                brushRadius,
                0,
                5
            );

        GUILayout.Space(8);

        paintMode =
            GUILayout.Toggle(
                paintMode,
                paintMode
                    ? "Paint Mode: ON"
                    : "Paint Mode: OFF",
                "Button",
                GUILayout.Height(30)
            );

        GUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "Turn Paint Mode on, then click or drag across " +
            "existing WorldTile objects in the Scene view.\n\n" +
            "Brush Radius 0 replaces one tile.",
            MessageType.Info
        );

        if (paintMode && paintTilePrefab == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a tile prefab before painting.",
                MessageType.Warning
            );
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!paintMode)
        {
            return;
        }

        if (paintTilePrefab == null)
        {
            return;
        }

        Event currentEvent = Event.current;

        HandleUtility.AddDefaultControl(
            GUIUtility.GetControlID(
                FocusType.Passive
            )
        );

        Ray mouseRay =
            HandleUtility.GUIPointToWorldRay(
                currentEvent.mousePosition
            );

        if (!Physics.Raycast(
                mouseRay,
                out RaycastHit hit,
                Mathf.Infinity
            ))
        {
            return;
        }

        WorldTile hoveredTile =
            hit.collider.GetComponentInParent<WorldTile>();

        if (hoveredTile == null)
        {
            return;
        }

        DrawBrushPreview(
            hoveredTile
        );

        bool leftClick =
            currentEvent.button == 0;

        bool clicked =
            currentEvent.type == EventType.MouseDown;

        bool dragged =
            currentEvent.type == EventType.MouseDrag;

        if (leftClick && (clicked || dragged))
        {
            PaintTilesAround(
                hoveredTile
            );

            currentEvent.Use();
        }
    }

    private void DrawBrushPreview(
        WorldTile centerTile
    )
    {
        float tileSize = 3f;

        Vector3 center =
            centerTile.transform.position;

        float width =
            (brushRadius * 2 + 1) * tileSize;

        Handles.DrawWireCube(
            center + Vector3.up * 0.05f,
            new Vector3(
                width,
                0.1f,
                width
            )
        );

        SceneView.RepaintAll();
    }

    private void PaintTilesAround(
        WorldTile centerTile
    )
    {
        GameObject world =
            GameObject.Find(
                "Generated Tile World"
            );

        if (world == null)
        {
            Debug.LogError(
                "Biome Tile Painter: Generated Tile World not found."
            );

            return;
        }

        int centerX =
            centerTile.GridX;

        int centerZ =
            centerTile.GridZ;

        Undo.SetCurrentGroupName(
            $"Paint {biomeName} Tiles"
        );

        int undoGroup =
            Undo.GetCurrentGroup();

        WorldTile[] allTiles =
            world.GetComponentsInChildren<WorldTile>();

        for (
            int offsetZ = -brushRadius;
            offsetZ <= brushRadius;
            offsetZ++
        )
        {
            for (
                int offsetX = -brushRadius;
                offsetX <= brushRadius;
                offsetX++
            )
            {
                int targetX =
                    centerX + offsetX;

                int targetZ =
                    centerZ + offsetZ;

                WorldTile targetTile =
                    FindTile(
                        allTiles,
                        targetX,
                        targetZ
                    );

                if (targetTile != null)
                {
                    ReplaceTile(
                        targetTile
                    );
                }
            }
        }

        Undo.CollapseUndoOperations(
            undoGroup
        );

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );
    }

    private WorldTile FindTile(
        WorldTile[] tiles,
        int gridX,
        int gridZ
    )
    {
        foreach (WorldTile tile in tiles)
        {
            if (
                tile.GridX == gridX &&
                tile.GridZ == gridZ
            )
            {
                return tile;
            }
        }

        return null;
    }

    private void ReplaceTile(
        WorldTile oldWorldTile
    )
    {
        GameObject oldTile =
            oldWorldTile.gameObject;

        if (
            oldWorldTile.BiomeName ==
            biomeName
        )
        {
            return;
        }

        Transform chunkParent =
            oldTile.transform.parent;

        Vector3 position =
            oldTile.transform.position;

        Quaternion rotation =
            oldTile.transform.rotation;

        Vector3 scale =
            oldTile.transform.localScale;

        int gridX =
            oldWorldTile.GridX;

        int gridZ =
            oldWorldTile.GridZ;

        GameObject newTile =
            PrefabUtility.InstantiatePrefab(
                paintTilePrefab
            ) as GameObject;

        if (newTile == null)
        {
            Debug.LogError(
                "Biome Tile Painter: Could not instantiate paint tile."
            );

            return;
        }

        Undo.RegisterCreatedObjectUndo(
            newTile,
            "Paint Biome Tile"
        );

        newTile.transform.SetParent(
            chunkParent,
            true
        );

        newTile.transform.position =
            position;

        newTile.transform.rotation =
            rotation;

        newTile.transform.localScale =
            scale;

        newTile.name =
            $"Tile_{gridX}_{gridZ}_{biomeName}";

        WorldTile newWorldTile =
            newTile.GetComponent<WorldTile>();

        if (newWorldTile == null)
        {
            newWorldTile =
                Undo.AddComponent<WorldTile>(
                    newTile
                );
        }

        newWorldTile.Initialize(
            gridX,
            gridZ,
            biomeName
        );

        EditorUtility.SetDirty(
            newWorldTile
        );

        Undo.DestroyObjectImmediate(
            oldTile
        );
    }
}