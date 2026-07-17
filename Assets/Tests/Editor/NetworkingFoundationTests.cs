#if UNITY_INCLUDE_TESTS
// Pure networking contract tests run in Unity Edit Mode.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public sealed class NetworkingFoundationTests
{
    [Test]
    public void ResolveUserId_UsesCommandLineOverride()
    {
        string userId = NetworkManagerCore.ResolveUserId(
            new[] { "Midterm", "-photonUserId", " player-2 " },
            "stable-user");

        Assert.That(userId, Is.EqualTo("player-2"));
    }

    [Test]
    public void ResolveUserId_UsesStableIdWhenOverrideIsMissingOrInvalid()
    {
        Assert.That(
            NetworkManagerCore.ResolveUserId(new[] { "Midterm" }, "stable-user"),
            Is.EqualTo("stable-user"));
        Assert.That(
            NetworkManagerCore.ResolveUserId(
                new[] { "Midterm", "-photonUserId", "-batchmode" },
                "stable-user"),
            Is.EqualTo("stable-user"));
    }

    [Test]
    public void GenerateRoomCode_UsesSixUnambiguousCharacters()
    {
        for (int i = 0; i < 100; i++)
        {
            string code = RoomCodeService.Generate();
            Assert.That(code, Has.Length.EqualTo(RoomCodeService.CodeLength));
            Assert.That(RoomCodeService.IsValid(code), Is.True);
            Assert.That(code, Does.Not.Contain("0").And.Not.Contain("O").And.Not.Contain("1").And.Not.Contain("I"));
        }
    }

    [Test]
    public void NormalizeRoomCode_TrimsAndUppercases()
    {
        Assert.That(RoomCodeService.Normalize("  ab2cd3  "), Is.EqualTo("AB2CD3"));
    }

    [Test]
    public void CanStart_RequiresThreeUniqueReadyRoles()
    {
        List<LobbyPlayerState> players = new List<LobbyPlayerState>
        {
            Player(1, SlimeRole.Anchor, true),
            Player(2, SlimeRole.Bounce, true),
            Player(3, SlimeRole.Sticky, true)
        };

        Assert.That(LobbyRules.CanStart(players), Is.True);
    }

    [Test]
    public void CanStart_RejectsMissingReadyOrDuplicateRole()
    {
        List<LobbyPlayerState> notReady = new List<LobbyPlayerState>
        {
            Player(1, SlimeRole.Anchor, true),
            Player(2, SlimeRole.Bounce, false),
            Player(3, SlimeRole.Sticky, true)
        };
        List<LobbyPlayerState> duplicate = new List<LobbyPlayerState>
        {
            Player(1, SlimeRole.Anchor, true),
            Player(2, SlimeRole.Anchor, true),
            Player(3, SlimeRole.Sticky, true)
        };

        Assert.That(LobbyRules.CanStart(notReady), Is.False);
        Assert.That(LobbyRules.CanStart(duplicate), Is.False);
    }

    [Test]
    public void ReservationKeys_AreDistinctForEveryPlayableRole()
    {
        Assert.That(NetworkPropertyKeys.ReservationKey(SlimeRole.Anchor), Is.Not.EqualTo(NetworkPropertyKeys.ReservationKey(SlimeRole.Bounce)));
        Assert.That(NetworkPropertyKeys.ReservationKey(SlimeRole.Bounce), Is.Not.EqualTo(NetworkPropertyKeys.ReservationKey(SlimeRole.Sticky)));
        Assert.That(NetworkPropertyKeys.ReservationKey(SlimeRole.Sticky), Is.Not.Empty);
    }

    [Test]
    public void MapCatalog_HasUniqueIdsValidDefaultAndEnabledScenes()
    {
        MultiplayerMapCatalog catalog =
            AssetDatabase.LoadAssetAtPath<MultiplayerMapCatalog>(
                "Assets/Resources/MultiplayerMapCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.IsValid(out string error), Is.True, error);
        Assert.That(catalog.Maps.Select(map => map.Id).Distinct().Count(),
            Is.EqualTo(catalog.Maps.Count));
        Assert.That(catalog.TryGetMap(catalog.DefaultMapId, out _), Is.True);

        HashSet<string> enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path))
            .ToHashSet();
        foreach (MultiplayerMapEntry map in catalog.Maps)
            Assert.That(enabledScenes, Does.Contain(map.SceneName));
    }

    [Test]
    public void MapCatalog_RejectsUnknownMapId()
    {
        MultiplayerMapCatalog catalog =
            AssetDatabase.LoadAssetAtPath<MultiplayerMapCatalog>(
                "Assets/Resources/MultiplayerMapCatalog.asset");

        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.TryGetMap("missing-map", out _), Is.False);
    }

    [Test]
    public void CanStart_RequiresCurrentMapAcknowledgement()
    {
        List<LobbyPlayerState> players = new List<LobbyPlayerState>
        {
            Player(1, SlimeRole.Anchor, true, 4),
            Player(2, SlimeRole.Bounce, true, 4),
            Player(3, SlimeRole.Sticky, true, 3)
        };

        Assert.That(LobbyRules.CanStart(players, 4), Is.False);
        players[2] = Player(3, SlimeRole.Sticky, true, 4);
        Assert.That(LobbyRules.CanStart(players, 4), Is.True);
    }

    [Test]
    public void NewMapRevision_BlocksStartUntilPlayersReadyAndAcknowledgeAgain()
    {
        List<LobbyPlayerState> previousRevision = new List<LobbyPlayerState>
        {
            Player(1, SlimeRole.Anchor, true, 1),
            Player(2, SlimeRole.Bounce, true, 1),
            Player(3, SlimeRole.Sticky, true, 1)
        };
        List<LobbyPlayerState> acknowledgedButReset = new List<LobbyPlayerState>
        {
            Player(1, SlimeRole.Anchor, false, 2),
            Player(2, SlimeRole.Bounce, false, 2),
            Player(3, SlimeRole.Sticky, false, 2)
        };

        Assert.That(LobbyRules.CanStart(previousRevision, 2), Is.False);
        Assert.That(LobbyRules.CanStart(acknowledgedButReset, 2), Is.False);
        acknowledgedButReset = acknowledgedButReset
            .Select(player => Player(player.ActorNumber, player.Role, true, 2))
            .ToList();
        Assert.That(LobbyRules.CanStart(acknowledgedButReset, 2), Is.True);
    }

    [Test]
    public void Map1_HasMultiplayerSpawnerCameraAndGroundPhysics()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/NewMap/Map1.unity", OpenSceneMode.Additive);
        try
        {
            NetworkPlayerSpawner spawner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NetworkPlayerSpawner>(true))
                .Single();
            SerializedObject serializedSpawner = new SerializedObject(spawner);
            Transform anchor = (Transform)serializedSpawner
                .FindProperty("anchorSpawnPoint").objectReferenceValue;
            Transform bounce = (Transform)serializedSpawner
                .FindProperty("bounceSpawnPoint").objectReferenceValue;
            Transform sticky = (Transform)serializedSpawner
                .FindProperty("stickySpawnPoint").objectReferenceValue;
            Assert.That(anchor.position, Is.EqualTo(new Vector3(-2f, -3f, 0f)));
            Assert.That(bounce.position, Is.EqualTo(new Vector3(0f, -3f, 0f)));
            Assert.That(sticky.position, Is.EqualTo(new Vector3(2f, -3f, 0f)));

            CameraFollow cameraFollow = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CameraFollow>(true))
                .Single();
            Assert.That(cameraFollow, Is.Not.Null);

            Tilemap ground = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Tilemap>(true))
                .Single(tilemap => tilemap.name == "Ground");
            Assert.That(ground.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ground")));
            TilemapCollider2D tilemapCollider = ground.GetComponent<TilemapCollider2D>();
            Assert.That(tilemapCollider, Is.Not.Null);
            Assert.That(tilemapCollider.compositeOperation,
                Is.EqualTo(Collider2D.CompositeOperation.Merge));
            Assert.That(ground.GetComponent<CompositeCollider2D>(), Is.Not.Null);
            Rigidbody2D rigidbody = ground.GetComponent<Rigidbody2D>();
            Assert.That(rigidbody, Is.Not.Null);
            Assert.That(rigidbody.bodyType, Is.EqualTo(RigidbodyType2D.Static));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void MapScene_HasDirectTestSelectorWithAllCharacters()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/MapScene.unity", OpenSceneMode.Additive);
        try
        {
            DirectMapTestCharacterSelector selector = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DirectMapTestCharacterSelector>(true))
                .Single();
            SerializedObject serializedSelector = new SerializedObject(selector);

            AssertCharacterPrefab(serializedSelector, "anchorPrefab", typeof(AnchorSlime));
            AssertCharacterPrefab(serializedSelector, "bouncyPrefab", typeof(BouncySlime));
            AssertCharacterPrefab(serializedSelector, "stickyPrefab", typeof(StickySlime));
            Assert.That(serializedSelector.FindProperty("testSpawnPoint").objectReferenceValue,
                Is.Not.Null);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AssertCharacterPrefab(
        SerializedObject selector,
        string propertyName,
        System.Type controllerType)
    {
        GameObject prefab = (GameObject)selector.FindProperty(propertyName).objectReferenceValue;
        Assert.That(prefab, Is.Not.Null, $"{propertyName} is not assigned.");
        Assert.That(prefab.GetComponent(controllerType), Is.Not.Null);
    }

    private static LobbyPlayerState Player(int actor, SlimeRole role, bool ready, int mapAcknowledgement = 0)
    {
        return new LobbyPlayerState(
            actor,
            $"user-{actor}",
            $"Player {actor}",
            role,
            ready,
            false,
            false,
            actor == 1,
            50,
            mapAcknowledgement);
    }
}
#endif
