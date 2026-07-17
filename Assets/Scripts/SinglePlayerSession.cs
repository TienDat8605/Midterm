using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Holds the local-only game choice while the gameplay scene is loading.
/// Single-player games do not create or join a Photon room.
/// </summary>
public static class SinglePlayerSession
{
    public static bool IsActive { get; private set; }
    public static SlimeRole SelectedRole { get; private set; } = SlimeRole.Anchor;
    public static string SelectedSceneName { get; private set; } = "Map1";

    public static void BeginSetup()
    {
        IsActive = true;
        SelectedRole = SlimeRole.Anchor;

        MultiplayerMapCatalog catalog =
            Resources.Load<MultiplayerMapCatalog>("MultiplayerMapCatalog");
        if (catalog != null && catalog.TryGetMap(catalog.DefaultMapId, out MultiplayerMapEntry map))
            SelectedSceneName = map.SceneName;
    }

    public static void SelectRole(SlimeRole role)
    {
        if (role != SlimeRole.None)
            SelectedRole = role;
    }

    public static void SelectMap(MultiplayerMapEntry map)
    {
        if (map != null)
            SelectedSceneName = map.SceneName;
    }

    public static void StartGame()
    {
        if (IsActive)
            SceneManager.LoadScene(SelectedSceneName);
    }

    public static void Stop()
    {
        IsActive = false;
        SelectedRole = SlimeRole.Anchor;
        SelectedSceneName = "Map1";
    }
}
