using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MultiplayerSceneSetup
{
    private const string LobbyScenePath = "Assets/Scenes/DevMultiplayerLobby.unity";
    private const string MapScenePath = "Assets/Scenes/MapScene.unity";

    [MenuItem("Tools/DINO PARK/Configure Multiplayer Test Scenes")]
    public static void Configure()
    {
        CreateLobbyScene();
        ConfigureMapScene();
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        Debug.Log("[MultiplayerSetup] Dev lobby, map spawner, and Build Settings configured.");
    }

    private static void CreateLobbyScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "DevMultiplayerLobby";

        GameObject root = new GameObject("NetworkManager");
        NetworkManager manager = root.AddComponent<NetworkManager>();

        GameObject lobbyUi = new GameObject("DevMultiplayerLobbyUI");
        lobbyUi.AddComponent<DevMultiplayerLobbyUI>();

        SerializedObject serializedManager = new SerializedObject(manager);
        SerializedProperty prefabs = serializedManager.FindProperty("networkPlayerPrefabs");
        string[] paths =
        {
            "Assets/Prefabs/AnchorSlime.prefab",
            "Assets/Prefabs/BouncySlime.prefab",
            "Assets/Prefabs/StickySlime.prefab"
        };
        prefabs.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++)
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
        serializedManager.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, LobbyScenePath);
    }

    private static void ConfigureMapScene()
    {
        Scene scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
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

        Transform anchor = GetOrCreateSpawn(spawnerObject.transform, "Spawn_Anchor", new Vector3(-2f, 1f, 0f));
        Transform bounce = GetOrCreateSpawn(spawnerObject.transform, "Spawn_Bounce", new Vector3(0f, 1f, 0f));
        Transform sticky = GetOrCreateSpawn(spawnerObject.transform, "Spawn_Sticky", new Vector3(2f, 1f, 0f));

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("anchorSpawnPoint").objectReferenceValue = anchor;
        serializedSpawner.FindProperty("bounceSpawnPoint").objectReferenceValue = bounce;
        serializedSpawner.FindProperty("stickySpawnPoint").objectReferenceValue = sticky;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
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
        scenes.RemoveAll(scene => scene.path == LobbyScenePath || scene.path == MapScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(LobbyScenePath, true));
        scenes.Insert(1, new EditorBuildSettingsScene(MapScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
