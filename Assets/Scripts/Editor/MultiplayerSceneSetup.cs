using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public static class MultiplayerSceneSetup
{
    private const string LobbyScenePath = "Assets/Scenes/DevMultiplayerLobby.unity";
    private const string Map1ScenePath = "Assets/Scenes/NewMap/Map1.unity";
    private const string Map2ScenePath = "Assets/Scenes/NewMap/Map2.unity";
    private const string TestMapScenePath = "Assets/Scenes/MapScene.unity";
    private const string CatalogPath = "Assets/Resources/MultiplayerMapCatalog.asset";
    private const string ProgressMinimapMap1UxmlPath = "Assets/UI Toolkit/HUD/ProgressMinimapMap1.uxml";
    private const string ProgressMinimapMap2UxmlPath = "Assets/UI Toolkit/HUD/ProgressMinimapMap2.uxml";
    private const string ProgressMinimapPanelSettingsGuid = "f6db9b6aeff441d4a8bb81caee21b178";

    [MenuItem("Tools/DINO PARK/Configure Multiplayer Test Scenes")]
    public static void Configure()
    {
        MultiplayerMapCatalog catalog = EnsureMapCatalog();
        ConfigureLobbyScene(catalog);
        ConfigureGameplayScene(Map1ScenePath, new Vector3(-2f, -3f, 0f),
            new Vector3(0f, -3f, 0f), new Vector3(2f, -3f, 0f), true);
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        Debug.Log("[MultiplayerSetup] Catalog, lobby, Map1, Map2, and Build Settings configured.");
    }

    public static void ConfigureMapSceneDirectTest()
    {
        ConfigureGameplayScene(TestMapScenePath, new Vector3(-2f, 1f, 0f),
            new Vector3(0f, 1f, 0f), new Vector3(2f, 1f, 0f), false);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MultiplayerSetup] MapScene direct character test configured.");
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
        ConfigureMapEntry(maps.GetArrayElementAtIndex(1), "map2", "Map 2", "Map2");
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

        if (scenePath == TestMapScenePath)
            ConfigureDirectMapTest(spawnerObject, bounce);

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<CameraFollow>() == null)
            mainCamera.gameObject.AddComponent<CameraFollow>();

        if (configureMap1Physics)
            ConfigureMap1Ground();

        ConfigureProgressMinimap(scene, anchor, scenePath == Map1ScenePath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureDirectMapTest(GameObject host, Transform spawnPoint)
    {
        DirectMapTestCharacterSelector selector =
            GetOrAdd<DirectMapTestCharacterSelector>(host);
        SerializedObject serializedSelector = new SerializedObject(selector);
        serializedSelector.FindProperty("anchorPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AnchorSlime.prefab");
        serializedSelector.FindProperty("bouncyPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BouncySlime.prefab");
        serializedSelector.FindProperty("stickyPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StickySlime.prefab");
        serializedSelector.FindProperty("testSpawnPoint").objectReferenceValue = spawnPoint;
        serializedSelector.ApplyModifiedPropertiesWithoutUndo();
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

    private static void ConfigureProgressMinimap(Scene scene, Transform startReference, bool useGoalDoor)
    {
        GameObject hudObject = Object.FindFirstObjectByType<ProgressMinimapController>()?.gameObject;
        if (hudObject == null)
            hudObject = new GameObject("ProgressMinimapHUD");

        UIDocument document = GetOrAdd<UIDocument>(hudObject);
        document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
            AssetDatabase.GUIDToAssetPath(ProgressMinimapPanelSettingsGuid));
        string minimapUxmlPath = scene.name == "Map2"
            ? ProgressMinimapMap2UxmlPath
            : ProgressMinimapMap1UxmlPath;
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(minimapUxmlPath);
        document.sortingOrder = 5;

        ProgressMinimapBounds bounds = GetOrAdd<ProgressMinimapBounds>(hudObject);
        GetOrAdd<ProgressMinimapController>(hudObject);

        Transform goalReference = useGoalDoor
            ? FindTransformInScene(scene, "GoalDoor")
            : GetOrCreateProgressEndMarker(startReference);
        if (goalReference == null)
            throw new MissingReferenceException("Map1 must contain a GoalDoor for the progress minimap.");

        SerializedObject serializedBounds = new SerializedObject(bounds);
        serializedBounds.FindProperty("startReference").objectReferenceValue = startReference;
        serializedBounds.FindProperty("goalReference").objectReferenceValue = goalReference;
        serializedBounds.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform GetOrCreateProgressEndMarker(Transform startReference)
    {
        GameObject marker = GameObject.Find("ProgressMinimapEndReference");
        if (marker == null)
        {
            marker = new GameObject("ProgressMinimapEndReference");
            marker.transform.position = startReference.position + Vector3.up * 10f;
        }

        return marker.transform;
    }

    private static Transform FindTransformInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                    return transform;
            }
        }

        return null;
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
                                  scene.path == Map2ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(LobbyScenePath, true));
        scenes.Insert(1, new EditorBuildSettingsScene(Map1ScenePath, true));
        scenes.Insert(2, new EditorBuildSettingsScene(Map2ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
