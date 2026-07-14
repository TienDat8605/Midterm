using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class MultiplayerSceneSetup
{
    private const string LobbyScenePath = "Assets/Scenes/DevMultiplayerLobby.unity";
    private const string Map1ScenePath = "Assets/Scenes/NewMap/Map1.unity";
    private const string TestMapScenePath = "Assets/Scenes/MapScene.unity";
    private const string CatalogPath = "Assets/Resources/MultiplayerMapCatalog.asset";

    [MenuItem("Tools/DINO PARK/Configure Multiplayer Test Scenes")]
    public static void Configure()
    {
        MultiplayerMapCatalog catalog = EnsureMapCatalog();
        ConfigureLobbyScene(catalog);
        ConfigureGameplayScene(Map1ScenePath, new Vector3(-2f, -3f, 0f),
            new Vector3(0f, -3f, 0f), new Vector3(2f, -3f, 0f), true);
        ConfigureGameplayScene(TestMapScenePath, new Vector3(-2f, 1f, 0f),
            new Vector3(0f, 1f, 0f), new Vector3(2f, 1f, 0f), false);
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        Debug.Log("[MultiplayerSetup] Catalog, lobby, Map1, MapScene, and Build Settings configured.");
    }

    private static MultiplayerMapCatalog EnsureMapCatalog()
    {
        MultiplayerMapCatalog catalog = AssetDatabase.LoadAssetAtPath<MultiplayerMapCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MultiplayerMapCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        SerializedObject serializedCatalog = new SerializedObject(catalog);
        serializedCatalog.FindProperty("defaultMapId").stringValue = "map1";
        SerializedProperty maps = serializedCatalog.FindProperty("maps");
        maps.arraySize = 2;
        ConfigureMapEntry(maps.GetArrayElementAtIndex(0), "map1", "Map 1", "Map1");
        ConfigureMapEntry(maps.GetArrayElementAtIndex(1), "test-map", "Test Map", "MapScene");
        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void ConfigureMapEntry(
        SerializedProperty entry,
        string id,
        string displayName,
        string sceneName)
    {
        entry.FindPropertyRelative("id").stringValue = id;
        entry.FindPropertyRelative("displayName").stringValue = displayName;
        entry.FindPropertyRelative("sceneName").stringValue = sceneName;
    }

    private static void ConfigureLobbyScene(MultiplayerMapCatalog catalog)
    {
        Scene scene;
        if (File.Exists(LobbyScenePath))
            scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        else
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        NetworkManager manager = Object.FindFirstObjectByType<NetworkManager>();
        if (manager == null)
            manager = new GameObject("NetworkManager").AddComponent<NetworkManager>();

        DevMultiplayerLobbyUI lobbyUi = Object.FindFirstObjectByType<DevMultiplayerLobbyUI>();
        if (lobbyUi == null)
            new GameObject("DevMultiplayerLobbyUI").AddComponent<DevMultiplayerLobbyUI>();

        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("gameVersion").stringValue = "0.6.0";
        serializedManager.FindProperty("mapCatalog").objectReferenceValue = catalog;
        SerializedProperty prefabs = serializedManager.FindProperty("networkPlayerPrefabs");
        string[] paths =
        {
            "Assets/Prefabs/AnchorSlime.prefab",
            "Assets/Prefabs/BouncySlime.prefab",
            "Assets/Prefabs/StickySlime.prefab"
        };
        prefabs.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++)
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, LobbyScenePath);
    }

    private static void ConfigureGameplayScene(
        string scenePath,
        Vector3 anchorPosition,
        Vector3 bouncePosition,
        Vector3 stickyPosition,
        bool configureMap1Physics)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        NetworkManager oldManager = Object.FindFirstObjectByType<NetworkManager>();
        GameObject spawnerObject;
        if (oldManager != null)
        {
            spawnerObject = oldManager.gameObject;
            Object.DestroyImmediate(oldManager);
            spawnerObject.name = "NetworkPlayerSpawner";
        }
        else
        {
            NetworkPlayerSpawner existing = Object.FindFirstObjectByType<NetworkPlayerSpawner>();
            spawnerObject = existing != null ? existing.gameObject : new GameObject("NetworkPlayerSpawner");
        }

        NetworkPlayerSpawner spawner = spawnerObject.GetComponent<NetworkPlayerSpawner>();
        if (spawner == null)
            spawner = spawnerObject.AddComponent<NetworkPlayerSpawner>();

        Transform anchor = GetOrCreateSpawn(spawnerObject.transform, "Spawn_Anchor", anchorPosition);
        Transform bounce = GetOrCreateSpawn(spawnerObject.transform, "Spawn_Bounce", bouncePosition);
        Transform sticky = GetOrCreateSpawn(spawnerObject.transform, "Spawn_Sticky", stickyPosition);

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("anchorSpawnPoint").objectReferenceValue = anchor;
        serializedSpawner.FindProperty("bounceSpawnPoint").objectReferenceValue = bounce;
        serializedSpawner.FindProperty("stickySpawnPoint").objectReferenceValue = sticky;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<CameraFollow>() == null)
            mainCamera.gameObject.AddComponent<CameraFollow>();

        if (configureMap1Physics)
            ConfigureMap1Ground();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureMap1Ground()
    {
        Tilemap ground = Object.FindObjectsByType<Tilemap>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(tilemap => tilemap.name == "Ground");
        if (ground == null)
            throw new MissingReferenceException("Map1 must contain a Ground Tilemap.");

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
            throw new MissingReferenceException("Project must define a Ground layer.");
        ground.gameObject.layer = groundLayer;

        Rigidbody2D rigidbody = GetOrAdd<Rigidbody2D>(ground.gameObject);
        rigidbody.bodyType = RigidbodyType2D.Static;
        rigidbody.simulated = true;

        CompositeCollider2D composite = GetOrAdd<CompositeCollider2D>(ground.gameObject);
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

        TilemapCollider2D tilemapCollider = GetOrAdd<TilemapCollider2D>(ground.gameObject);
        tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static Transform GetOrCreateSpawn(Transform parent, string name, Vector3 position)
    {
        Transform spawn = parent.Find(name);
        if (spawn == null)
        {
            spawn = new GameObject(name).transform;
            spawn.SetParent(parent);
        }
        spawn.position = position;
        return spawn;
    }

    private static void ConfigureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(scene => scene.path == LobbyScenePath ||
                                  scene.path == Map1ScenePath ||
                                  scene.path == TestMapScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(LobbyScenePath, true));
        scenes.Insert(1, new EditorBuildSettingsScene(Map1ScenePath, true));
        scenes.Insert(2, new EditorBuildSettingsScene(TestMapScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
