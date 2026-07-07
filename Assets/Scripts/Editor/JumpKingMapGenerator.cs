using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor window for generating handcrafted Jump King-style maps onto a Tilemap.
/// Maps are loaded from .md files — add new files to add new maps.
/// </summary>
public class JumpKingMapGenerator : EditorWindow
{
    [SerializeField] private Tilemap targetTilemap;
    [SerializeField] private TileBase groundTile;     // dirt / body of platforms
    [SerializeField] private TileBase grassTile;      // top surface of platforms
    [SerializeField] private TileBase leftWallTile;   // left side wall
    [SerializeField] private TileBase rightWallTile;  // right side wall
    [SerializeField] private GameObject spawnPrefab;
    [SerializeField] private GameObject goalPrefab;
    // .md mode
    [SerializeField] private int selectedMapIndex;
    // Procedural mode
    [SerializeField] private bool useProcedural;
    [SerializeField] private int proceduralHeight = 60;
    [SerializeField] private int selectedDifficulty; // 0=Easy, 1=Normal, 2=Hard

    private Vector2 scrollPosition;
    private List<MapData> cachedMaps;

    [MenuItem("Tools/Jump King/Map Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<JumpKingMapGenerator>("Jump King Map Generator");
        window.minSize = new Vector2(380, 360);
        window.Show();
    }

    private void OnEnable()
    {
        cachedMaps = null;

        // Assign default tiles if not yet set
        if (grassTile == null)
            grassTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Sprites/Tiles/Forest_Tree_249.asset");
        if (groundTile == null)
            groundTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Sprites/Tiles/Forest_Tree_200.asset");
        if (leftWallTile == null)
            leftWallTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Sprites/Tiles/Forest_Tree_156.asset");
        if (rightWallTile == null)
            rightWallTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Sprites/Tiles/Forest_Tree_156.asset");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader();
        DrawReferences();
        DrawSeparator();
        DrawModeToggle();
        DrawSeparator();

        if (useProcedural)
            DrawProceduralControls();
        else
            DrawMapControls();

        DrawSeparator();
        DrawLegend();

        EditorGUILayout.EndScrollView();
    }

    private void DrawModeToggle()
    {
        EditorGUILayout.LabelField("Source Mode", EditorStyles.boldLabel);
        useProcedural = GUILayout.Toolbar(useProcedural ? 1 : 0, new[] { "ASCII .md Files", "Procedural Generation" }) == 1;
        EditorGUILayout.Space();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Jump King Map Generator", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Maps loaded from: Assets/MapAscii/", EditorStyles.miniLabel);
        EditorGUILayout.Space();
    }

    private void DrawReferences()
    {
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        targetTilemap = (Tilemap)EditorGUILayout.ObjectField("Target Tilemap", targetTilemap, typeof(Tilemap), true);
        groundTile = (TileBase)EditorGUILayout.ObjectField("Ground Tile (body)", groundTile, typeof(TileBase), false);
        grassTile = (TileBase)EditorGUILayout.ObjectField("Grass Tile (surface)", grassTile, typeof(TileBase), false);
        leftWallTile = (TileBase)EditorGUILayout.ObjectField("Left Wall Tile", leftWallTile, typeof(TileBase), false);
        rightWallTile = (TileBase)EditorGUILayout.ObjectField("Right Wall Tile", rightWallTile, typeof(TileBase), false);
        spawnPrefab = (GameObject)EditorGUILayout.ObjectField("Spawn Prefab (optional)", spawnPrefab, typeof(GameObject), false);
        goalPrefab = (GameObject)EditorGUILayout.ObjectField("Goal Prefab (optional)", goalPrefab, typeof(GameObject), false);
        EditorGUILayout.Space();
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Separator();
        EditorGUILayout.Space();
    }

    private void DrawProceduralControls()
    {
        EditorGUILayout.LabelField("Procedural Generation", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Height input
        proceduralHeight = EditorGUILayout.IntSlider("Map Height", proceduralHeight, 15, 200);
        EditorGUILayout.LabelField($"Approx. sections: ~{proceduralHeight / 6}", EditorStyles.miniLabel);
        EditorGUILayout.Space();

        // Difficulty selector
        selectedDifficulty = GUILayout.Toolbar(selectedDifficulty, new[] { "Easy", "Normal", "Hard" });
        EditorGUILayout.Space();

        // Generate button
        string[] diffNames = { "Easy", "Normal", "Hard" };
        bool canGenerate = targetTilemap != null && groundTile != null;
        GUI.enabled = canGenerate;
        if (GUILayout.Button($"Generate Procedural {diffNames[selectedDifficulty]} ({ProceduralMapGenerator.MAP_WIDTH}×{proceduralHeight})",
            GUILayout.Height(36)))
        {
            GenerateProceduralMap();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();

        // Clear button
        GUI.enabled = targetTilemap != null;
        if (GUILayout.Button("Clear Map", GUILayout.Height(28)))
            ClearMap();
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            $"Width is fixed at {ProceduralMapGenerator.MAP_WIDTH} tiles.\n" +
            "Easy: wide platforms, small gaps, recovery platforms.\n" +
            "Normal: tighter platforms, bigger gaps, shortcuts, bait platforms.\n" +
            "Hard: tiny ledges, chain jumps, long drops, hidden recovery, bait.",
            MessageType.Info
        );
    }

    private void DrawMapControls()
    {
        // Lazy-load .md maps on first draw
        if (cachedMaps == null)
            cachedMaps = LevelDefinitions.AllMaps;

        EditorGUILayout.LabelField("Generate Map", EditorStyles.boldLabel);

        // Build display names with dimensions
        string[] displayNames = new string[cachedMaps.Count];
        for (int i = 0; i < cachedMaps.Count; i++)
            displayNames[i] = $"{cachedMaps[i].MapName} ({cachedMaps[i].Width}×{cachedMaps[i].Height})";

        selectedMapIndex = GUILayout.Toolbar(selectedMapIndex, displayNames);
        EditorGUILayout.Space();

        // Generate button
        bool validSelection = selectedMapIndex >= 0 && selectedMapIndex < cachedMaps.Count;
        string buttonLabel = validSelection
            ? $"Generate {cachedMaps[selectedMapIndex].MapName}"
            : "Generate Map";

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = validSelection && targetTilemap != null && groundTile != null;

        if (GUILayout.Button(buttonLabel, GUILayout.Height(36)))
            GenerateMap(selectedMapIndex);

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Action row: Clear + Reload
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = targetTilemap != null;
        if (GUILayout.Button("Clear Map", GUILayout.Height(28)))
            ClearMap();
        GUI.enabled = true;

        if (GUILayout.Button("Reload Maps", GUILayout.Height(28)))
        {
            LevelDefinitions.Reload();
            cachedMaps = LevelDefinitions.AllMaps;
            Repaint();
            Debug.Log("[Map Generator] Maps reloaded from disk.");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Miniature preview info
        if (validSelection)
        {
            EditorGUILayout.LabelField(
                $"Size: {cachedMaps[selectedMapIndex].Width}×{cachedMaps[selectedMapIndex].Height}" +
                $"  |  Sections to climb: ~{cachedMaps[selectedMapIndex].Height / 6}",
                EditorStyles.miniLabel);
        }
    }

    private void DrawLegend()
    {
        EditorGUILayout.LabelField("Map Legend", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "g  = Grass surface tile\n" +
            "#  = Ground dirt / body\n" +
            "[  = Left wall\n" +
            "]  = Right wall\n" +
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
        if (mapIndex < 0 || mapIndex >= cachedMaps.Count) return;
        PlaceMapDataOnTilemap(cachedMaps[mapIndex]);
    }

    private void GenerateProceduralMap()
    {
        MapData mapData = null;

        switch (selectedDifficulty)
        {
            case 0: // Easy
                mapData = ProceduralMapGenerator.GenerateEasy(proceduralHeight);
                break;
            case 1: // Normal
                mapData = ProceduralMapGenerator.GenerateNormal(proceduralHeight);
                break;
            case 2: // Hard
                mapData = ProceduralMapGenerator.GenerateHard(proceduralHeight);
                break;
        }

        if (mapData != null)
            PlaceMapDataOnTilemap(mapData);
    }

    /// <summary>
    /// Takes a MapData and paints it onto the target Tilemap.
    /// Shared between .md file maps and procedural maps.
    /// </summary>
    private void PlaceMapDataOnTilemap(MapData mapData)
    {
        if (targetTilemap == null || groundTile == null)
        {
            EditorUtility.DisplayDialog("Missing References",
                "Assign at least a Target Tilemap and a Ground Tile before generating.", "OK");
            return;
        }

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
            int tileY = mapData.Height - 1 - rowIndex; // flip: data[0] = highest Y
            string row = mapData.Rows[rowIndex];

            for (int x = 0; x < Mathf.Min(row.Length, mapData.Width); x++)
            {
                char c = row[x];
                var cellPos = new Vector3Int(x, tileY, 0);

                switch (c)
                {
                    case 'g':
                        // Grass surface tile
                        if (grassTile != null)
                            targetTilemap.SetTile(cellPos, grassTile);
                        else
                            targetTilemap.SetTile(cellPos, groundTile);
                        break;

                    case '#':
                    case 'R':
                        // Ground body / recovery platform
                        targetTilemap.SetTile(cellPos, groundTile);
                        break;

                    case '[':
                        // Left wall
                        targetTilemap.SetTile(cellPos, leftWallTile != null ? leftWallTile : groundTile);
                        break;

                    case ']':
                    {
                        // Right wall (flipped horizontally relative to left wall)
                        TileBase wallTile = rightWallTile ?? leftWallTile ?? groundTile;
                        targetTilemap.SetTile(cellPos, wallTile);
                        // Flip horizontally so the right wall faces inward
                        targetTilemap.SetTileFlags(cellPos, TileFlags.None);
                        targetTilemap.SetTransformMatrix(cellPos,
                            Matrix4x4.Scale(new Vector3(-1, 1, 1)));
                        break;
                    }

                    case 'S':
                        // Ground under spawn
                        targetTilemap.SetTile(cellPos, groundTile);
                        spawnCell = cellPos;
                        break;

                    case 'G':
                        // Goal point (no tile, just marker position)
                        goalCell = cellPos;
                        break;
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
