using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class MigrateGridPrefabPhysics
{
    private static readonly string[] PrefabPaths =
    {
        "Assets/Prefabs/Map/Grid 1.prefab",
        "Assets/Prefabs/Map/Grid 2.prefab"
    };

    public static void Run()
    {
        foreach (string prefabPath in PrefabPaths)
            ConfigurePrefab(prefabPath);

        RemoveMap1PhysicsOverrides();
        AssetDatabase.SaveAssets();
    }

    private static void ConfigurePrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Tilemap ground = root.GetComponentsInChildren<Tilemap>(true)
                .Single(tilemap => tilemap.name == "Ground");
            ground.gameObject.layer = LayerMask.NameToLayer("Ground");

            Rigidbody2D rigidbody = GetOrAdd<Rigidbody2D>(ground.gameObject);
            rigidbody.bodyType = RigidbodyType2D.Static;
            rigidbody.simulated = true;

            CompositeCollider2D composite = GetOrAdd<CompositeCollider2D>(ground.gameObject);
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.isTrigger = false;

            TilemapCollider2D tilemapCollider = GetOrAdd<TilemapCollider2D>(ground.gameObject);
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            tilemapCollider.isTrigger = false;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveMap1PhysicsOverrides()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/NewMap/Map1.unity", OpenSceneMode.Single);
        Tilemap ground = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Tilemap>(true))
            .Single(tilemap => tilemap.name == "Ground");

        RemoveAddedOverrides(ground.GetComponents<TilemapCollider2D>());
        RemoveAddedOverrides(ground.GetComponents<CompositeCollider2D>());
        RemoveAddedOverrides(ground.GetComponents<Rigidbody2D>());
        EditorSceneManager.SaveScene(scene);
    }

    private static void RemoveAddedOverrides<T>(T[] components) where T : Component
    {
        foreach (T component in components)
        {
            if (PrefabUtility.IsAddedComponentOverride(component))
                Object.DestroyImmediate(component);
        }
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
