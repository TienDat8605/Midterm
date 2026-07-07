using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor tool to automatically add collision components to a Tilemap.
/// Access via Tools → Jump King → Setup Tilemap Physics.
/// </summary>
public class TilemapPhysicsTool : EditorWindow
{
    [SerializeField] private Tilemap targetTilemap;
    [SerializeField] private bool useComposite = true;
    [SerializeField] private PhysicsMaterial2D physicsMaterial;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Jump King/Setup Tilemap Physics")]
    public static void ShowWindow()
    {
        var window = GetWindow<TilemapPhysicsTool>("Tilemap Physics Setup");
        window.minSize = new Vector2(320, 220);
        window.Show();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tilemap Physics Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetTilemap = (Tilemap)EditorGUILayout.ObjectField("Target Tilemap", targetTilemap, typeof(Tilemap), true);
        physicsMaterial = (PhysicsMaterial2D)EditorGUILayout.ObjectField("Physics Material 2D (optional)", physicsMaterial, typeof(PhysicsMaterial2D), false);
        useComposite = EditorGUILayout.Toggle("Use Composite Collider", useComposite);

        EditorGUILayout.Space();

        GUI.enabled = targetTilemap != null;
        if (GUILayout.Button("Setup Components", GUILayout.Height(36)))
        {
            SetupPhysics();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Adds collision components to the Tilemap.\n" +
            "Composite mode merges all tile colliders into one shape " +
            "(better performance, required for smooth physics).\n\n" +
            "Without composite: each tile gets its own collider edge.",
            MessageType.Info
        );

        EditorGUILayout.EndScrollView();
    }

    private void SetupPhysics()
    {
        if (targetTilemap == null) return;

        GameObject go = targetTilemap.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(go, "Setup Tilemap Physics");

        bool addedAny = false;

        // --- TilemapCollider2D ---
        TilemapCollider2D tilemapCollider = go.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
        {
            tilemapCollider = go.AddComponent<TilemapCollider2D>();
            Undo.RegisterCreatedObjectUndo(tilemapCollider, "Add TilemapCollider2D");
            addedAny = true;
        }

        // --- CompositeCollider2D + Rigidbody2D ---
        CompositeCollider2D composite = go.GetComponent<CompositeCollider2D>();
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();

        if (useComposite)
        {
            // Add Rigidbody2D if missing (required by CompositeCollider2D)
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody2D>();
                Undo.RegisterCreatedObjectUndo(rb, "Add Rigidbody2D");
                addedAny = true;
            }

            // Configure Rigidbody2D for a static tilemap
            rb.bodyType = RigidbodyType2D.Static;
            rb.simulated = true;

            // Add CompositeCollider2D if missing
            if (composite == null)
            {
                composite = go.AddComponent<CompositeCollider2D>();
                Undo.RegisterCreatedObjectUndo(composite, "Add CompositeCollider2D");
                addedAny = true;
            }

            // Wire TilemapCollider2D to the composite
            tilemapCollider.usedByComposite = true;

            // Set geometry type to polygons for smoother edges
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.isTrigger = false;

            // Assign physics material to composite if one was provided
            if (physicsMaterial != null)
                composite.sharedMaterial = physicsMaterial;
        }
        else
        {
            // Remove composite if it exists and we're not using it
            if (composite != null)
            {
                Undo.DestroyObjectImmediate(composite);
            }
            if (rb != null && rb.bodyType == RigidbodyType2D.Static)
            {
                // Keep the Rigidbody2D only if it was pre-existing and needed
                // Otherwise remove it since it's not needed without composite
            }

            tilemapCollider.usedByComposite = false;

            if (physicsMaterial != null)
                tilemapCollider.sharedMaterial = physicsMaterial;
        }

        if (addedAny)
        {
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log($"[TilemapPhysics] Components added to {go.name}" +
                (useComposite ? " (composite mode)" : " (individual tiles)"));
        }
        else
        {
            Debug.Log("[TilemapPhysics] All components already present — no changes needed.");
        }
    }
}
