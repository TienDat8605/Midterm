using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor tool to add the background image behind a Tilemap.
/// Access via Tools → Jump King → Add Background.
/// </summary>
public class BackgroundTool : EditorWindow
{
    [SerializeField] private Tilemap targetTilemap;
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private float zOffset = 5f;
    [SerializeField] private Vector2 scaleMultiplier = Vector2.one;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Jump King/Add Background")]
    public static void ShowWindow()
    {
        var window = GetWindow<BackgroundTool>("Jump King Background");
        window.minSize = new Vector2(320, 200);
        window.Show();
    }

    private void OnEnable()
    {
        // Auto-load the background sprite if not set
        if (backgroundSprite == null)
        {
            backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Sprites/Bg/images_bg.jpeg");
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add Background Image", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetTilemap = (Tilemap)EditorGUILayout.ObjectField("Target Tilemap", targetTilemap, typeof(Tilemap), true);
        backgroundSprite = (Sprite)EditorGUILayout.ObjectField("Background Sprite", backgroundSprite, typeof(Sprite), false);
        zOffset = EditorGUILayout.FloatField("Z Offset (behind)", zOffset);
        scaleMultiplier = EditorGUILayout.Vector2Field("Scale Multiplier", scaleMultiplier);

        EditorGUILayout.Space();

        GUI.enabled = targetTilemap != null && backgroundSprite != null;
        if (GUILayout.Button("Create Background", GUILayout.Height(36)))
        {
            CreateBackground();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Creates a background GameObject behind the Tilemap.\n" +
            "The sprite is sized to cover the tilemap bounds.\n" +
            "Use Z Offset to push it further behind (positive = behind).",
            MessageType.Info
        );

        EditorGUILayout.EndScrollView();
    }

    private void CreateBackground()
    {
        if (targetTilemap == null || backgroundSprite == null)
        {
            EditorUtility.DisplayDialog("Missing References",
                "Assign both a Target Tilemap and a Background Sprite.", "OK");
            return;
        }

        // Find or create the background container
        Transform gridParent = targetTilemap.transform.parent;
        Transform bgParent = gridParent != null ? gridParent : targetTilemap.transform;

        // Remove existing background if present
        var existing = bgParent.GetComponentInChildren<BackgroundImage>();
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Replace Background?",
                    "A background already exists. Replace it?", "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // Calculate tilemap bounds
        BoundsInt bounds = targetTilemap.cellBounds;
        if (bounds.size.x == 0 || bounds.size.y == 0)
        {
            EditorUtility.DisplayDialog("Empty Tilemap",
                "The tilemap has no tiles. Generate a map first.", "OK");
            return;
        }

        Vector3 tilemapCenter = targetTilemap.CellToWorld(
            new Vector3Int(bounds.x + bounds.size.x / 2, bounds.y + bounds.size.y / 2, 0));
        Vector3 tilemapSize = new Vector3(bounds.size.x, bounds.size.y, 0);

        // Create background game object
        GameObject bgObj = new GameObject("Background", typeof(SpriteRenderer), typeof(BackgroundImage));
        bgObj.transform.SetParent(bgParent);
        bgObj.transform.SetAsFirstSibling(); // behind everything

        SpriteRenderer sr = bgObj.GetComponent<SpriteRenderer>();

        // Check if backgroundSprite is actually valid as a Sprite
        if (backgroundSprite == null)
        {
            Debug.LogError("[BgTool] Background sprite is null.");
            return;
        }

        sr.sprite = backgroundSprite;

        // Calculate sprite dimensions in world units
        float spriteWidth = backgroundSprite.bounds.size.x;
        float spriteHeight = backgroundSprite.bounds.size.y;

        if (spriteWidth <= 0 || spriteHeight <= 0)
        {
            Texture2D tex = backgroundSprite.texture;
            spriteWidth = tex.width / backgroundSprite.pixelsPerUnit;
            spriteHeight = tex.height / backgroundSprite.pixelsPerUnit;
        }

        // Scale the sprite so its width exactly matches the tilemap width
        // Height auto-adjusts to maintain aspect ratio
        float scale = (tilemapSize.x / spriteWidth) * scaleMultiplier.x;

        bgObj.transform.localScale = new Vector3(scale, scale, 1);
        bgObj.transform.position = new Vector3(
            tilemapCenter.x,
            tilemapCenter.y,
            -zOffset
        );

        // Set sorting to render behind tiles
        sr.sortingLayerName = "Default";

        // Ensure tiles render on top: tilemap uses 0 by default, so use negative order
        sr.sortingOrder = -10;

        Undo.RegisterCreatedObjectUndo(bgObj, "Create Background");
        EditorSceneManager.MarkSceneDirty(targetTilemap.gameObject.scene);

        Debug.Log($"[BgTool] Background created: scale={scale:F2}, pos=({bgObj.transform.position.x:F1}, {bgObj.transform.position.y:F1}), z={-zOffset:F1}");
    }
}

/// <summary>
/// Marker component so the tool can find existing backgrounds.
/// </summary>
public class BackgroundImage : MonoBehaviour { }
