using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor window for generating handcrafted Jump King-style maps onto a Tilemap.
/// </summary>
public class JumpKingMapGenerator : EditorWindow
{
    [SerializeField] private Tilemap targetTilemap;
    [SerializeField] private TileBase groundTile;
    [SerializeField] private GameObject spawnPrefab;
    [SerializeField] private GameObject goalPrefab;
    [SerializeField] private int selectedMapIndex;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Jump King/Map Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<JumpKingMapGenerator>("Jump King Map Generator");
        window.minSize = new Vector2(360, 320);
        window.Show();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader();
        DrawReferences();
        DrawSeparator();
        DrawMapControls();
        DrawSeparator();
        DrawLegend();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Jump King Map Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
    }

    private void DrawReferences()
    {
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        targetTilemap = (Tilemap)EditorGUILayout.ObjectField("Target Tilemap", targetTilemap, typeof(Tilemap), true);
        groundTile = (TileBase)EditorGUILayout.ObjectField("Ground Tile", groundTile, typeof(TileBase), false);
        spawnPrefab = (GameObject)EditorGUILayout.ObjectField("Spawn Prefab (optional)", spawnPrefab, typeof(GameObject), false);
        goalPrefab = (GameObject)EditorGUILayout.ObjectField("Goal Prefab (optional)", goalPrefab, typeof(GameObject), false);
        EditorGUILayout.Space();
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Separator();
        EditorGUILayout.Space();
    }

    private void DrawMapControls()
    {
        EditorGUILayout.LabelField("Generate Map", EditorStyles.boldLabel);

        var maps = LevelDefinitions.AllMaps;

        // Build display names with dimensions
        string[] displayNames = new string[maps.Count];
        for (int i = 0; i < maps.Count; i++)
            displayNames[i] = $"{maps[i].MapName} ({maps[i].Width}×{maps[i].Height})";

        selectedMapIndex = GUILayout.Toolbar(selectedMapIndex, displayNames);
        EditorGUILayout.Space();

        // Generate button
        bool validSelection = selectedMapIndex >= 0 && selectedMapIndex < maps.Count;
        string buttonLabel = validSelection
            ? $"Generate {maps[selectedMapIndex].MapName}"
            : "Generate Map";

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = validSelection && targetTilemap != null && groundTile != null;

        if (GUILayout.Button(buttonLabel, GUILayout.Height(36)))
            GenerateMap(selectedMapIndex);

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Clear button
        GUI.enabled = targetTilemap != null;
        if (GUILayout.Button("Clear Map", GUILayout.Height(28)))
            ClearMap();
        GUI.enabled = true;

        EditorGUILayout.Space();

        // Miniature preview info
        if (validSelection)
        {
            var map = maps[selectedMapIndex];
            EditorGUILayout.LabelField($"Size: {map.Width}×{map.Height}  |  Sections to climb: ~{map.Height / 6}",
                EditorStyles.miniLabel);
        }
    }

    private void DrawLegend()
    {
        EditorGUILayout.LabelField("Map Legend", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "#  = Ground Tile\n" +
            ".  = Empty\n" +
            "S  = Spawn Point + Ground\n" +
            "G  = Goal Point\n" +
            "R  = Recovery Platform (Ground)\n" +
            "C  = Checkpoint (not yet implemented)\n" +
            "D  = Decoration (future)\n" +
            "L  = Ladder (future)",
            MessageType.Info
        );
    }

    private void GenerateMap(int mapIndex)
    {
        if (targetTilemap == null || groundTile == null)
        {
            EditorUtility.DisplayDialog("Missing References",
                "Assign both a Target Tilemap and a Ground Tile before generating.", "OK");
            return;
        }

        var maps = LevelDefinitions.AllMaps;
        if (mapIndex < 0 || mapIndex >= maps.Count)
            return;

        MapData mapData = maps[mapIndex];
        Transform gridParent = targetTilemap.transform.parent;

        // Register undo
        Undo.RegisterCompleteObjectUndo(targetTilemap, $"Generate {mapData.MapName}");
        if (gridParent != null)
            Undo.RegisterFullObjectHierarchyUndo(gridParent.gameObject, $"Generate {mapData.MapName}");

        // Clear
        targetTilemap.ClearAllTiles();
        DestroyMarkers(gridParent);

        // Track special positions
        Vector3Int? spawnCell = null;
        Vector3Int? goalCell = null;

        // Place tiles: data rows are stored top-to-bottom.
        // Row index 0 in data = top of map = highest Y in tilemap.
        for (int rowIndex = 0; rowIndex < mapData.Height; rowIndex++)
        {
            int tileY = mapData.Height - 1 - rowIndex; // flip: data[0] = top row
            string row = mapData.Rows[rowIndex];

            for (int x = 0; x < Mathf.Min(row.Length, mapData.Width); x++)
            {
                char c = row[x];
                var cellPos = new Vector3Int(x, tileY, 0);

                switch (c)
                {
                    case '#':
                    case 'R':
                        targetTilemap.SetTile(cellPos, groundTile);
                        break;
                    case 'S':
                        targetTilemap.SetTile(cellPos, groundTile);
                        spawnCell = cellPos;
                        break;
                    case 'G':
                        goalCell = cellPos;
                        break;
                    // '.' and everything else: leave empty
                }
            }
        }

        // Place marker objects
        if (spawnCell.HasValue)
            PlaceMarker(spawnCell.Value, spawnPrefab, typeof(SpawnPoint), "SpawnPoint", gridParent);
        if (goalCell.HasValue)
            PlaceMarker(goalCell.Value, goalPrefab, typeof(GoalPoint), "GoalPoint", gridParent);

        EditorSceneManager.MarkSceneDirty(targetTilemap.gameObject.scene);
        Debug.Log($"[Map Generator] Generated \"{mapData.MapName}\" ({mapData.Width}×{mapData.Height})");
    }

    private void ClearMap()
    {
        if (targetTilemap == null) return;

        Transform gridParent = targetTilemap.transform.parent;
        Undo.RegisterCompleteObjectUndo(targetTilemap, "Clear Map");
        targetTilemap.ClearAllTiles();

        if (gridParent != null)
            DestroyMarkers(gridParent);

        EditorSceneManager.MarkSceneDirty(targetTilemap.gameObject.scene);
        Debug.Log("[Map Generator] Map cleared.");
    }

    private void DestroyMarkers(Transform parent)
    {
        if (parent == null) return;

        var spawns = parent.GetComponentsInChildren<SpawnPoint>();
        foreach (var s in spawns)
            Undo.DestroyObjectImmediate(s.gameObject);

        var goals = parent.GetComponentsInChildren<GoalPoint>();
        foreach (var g in goals)
            Undo.DestroyObjectImmediate(g.gameObject);
    }

    private void PlaceMarker(Vector3Int cell, GameObject prefab, System.Type componentType,
        string objectName, Transform parent)
    {
        if (parent == null) return;

        Vector3 worldPos = targetTilemap.CellToWorld(cell) + new Vector3(0.5f, 0.5f, 0);
        GameObject go;

        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = worldPos;
        }
        else
        {
            go = new GameObject(objectName, componentType);
            go.transform.SetParent(parent);
            go.transform.position = worldPos;
        }

        go.name = objectName;
        Undo.RegisterCreatedObjectUndo(go, $"Place {objectName}");
    }
}
