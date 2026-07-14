using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IMGUI-only development harness. Production UI should bind to the same NetworkManager API.
/// </summary>
public sealed class DevMultiplayerLobbyUI : MonoBehaviour
{
    private string nickname = string.Empty;
    private string roomCode = string.Empty;
    private Vector2 scrollPosition;
    private GUIStyle titleStyle;
    private GUIStyle errorStyle;
    private GUIStyle panelStyle;

    private void Start()
    {
        nickname = PhotonNetwork.NickName;
    }

    private void OnGUI()
    {
        NetworkManager manager = NetworkManager.Instance;
        if (ShouldHideLobby(manager))
            return;

        EnsureStyles();

        float width = Mathf.Min(620f, Screen.width - 32f);
        Rect area = new Rect((Screen.width - width) * 0.5f, 16f, width, Screen.height - 32f);
        GUILayout.BeginArea(area, panelStyle);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("DINO PARK — Multiplayer Test Lobby", titleStyle);
        if (manager == null)
        {
            GUILayout.Label("NetworkManager is missing from this scene.", errorStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        DrawConnection(manager);
        GUILayout.Space(10f);

        if (!PhotonNetwork.InRoom)
            DrawCreateJoin(manager);
        else
            DrawLobby(manager);

        if (!string.IsNullOrEmpty(manager.LastErrorMessage))
        {
            GUILayout.Space(10f);
            GUILayout.Label($"{manager.LastErrorCode}: {manager.LastErrorMessage}", errorStyle);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static bool ShouldHideLobby(NetworkManager manager)
    {
        if (manager == null || !PhotonNetwork.InRoom)
            return false;

        return manager.ConnectionState == NetworkConnectionState.LoadingGame ||
               manager.CurrentLobby.Phase != RoomPhase.Lobby;
    }

    private void DrawConnection(NetworkManager manager)
    {
        GUILayout.Label($"Connection: {manager.ConnectionState}");
        GUILayout.Label($"Version: {manager.GameVersion}    Region: {manager.Region}    Ping: {PhotonNetwork.GetPing()} ms");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Nickname", GUILayout.Width(80f));
        nickname = GUILayout.TextField(nickname, 20);
        if (GUILayout.Button("Apply", GUILayout.Width(70f)))
            manager.SetNickname(nickname);
        GUILayout.EndHorizontal();
    }

    private void DrawCreateJoin(NetworkManager manager)
    {
        bool connected = manager.ConnectionState == NetworkConnectionState.ConnectedToMaster;
        GUI.enabled = connected && !manager.IsOperationInProgress;
        if (GUILayout.Button("Create Private Room", GUILayout.Height(36f)))
            manager.CreateRoom();

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        roomCode = GUILayout.TextField(roomCode.ToUpperInvariant(), RoomCodeService.CodeLength);
        if (GUILayout.Button("Join Room", GUILayout.Width(130f), GUILayout.Height(30f)))
            manager.JoinRoom(roomCode);
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        if (!connected)
            GUILayout.Label("Waiting for Photon connection...");
    }

    private void DrawLobby(NetworkManager manager)
    {
        LobbySnapshot lobby = manager.CurrentLobby;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Room: {lobby.RoomCode}", titleStyle);
        if (GUILayout.Button("Copy Code", GUILayout.Width(100f)))
            GUIUtility.systemCopyBuffer = lobby.RoomCode;
        GUILayout.EndHorizontal();

        GUILayout.Label($"Phase: {lobby.Phase}    Local Actor: {PhotonNetwork.LocalPlayer.ActorNumber}    Master: {lobby.IsMasterClient}");
        GUILayout.Label($"Selected map: {lobby.SelectedMapDisplayName} ({lobby.SelectedMapId})");
        GUILayout.BeginHorizontal();
        IReadOnlyList<MultiplayerMapEntry> maps = manager.AvailableMaps;
        for (int i = 0; i < maps.Count; i++)
        {
            MultiplayerMapEntry map = maps[i];
            GUI.enabled = manager.CanSelectMap && map.Id != lobby.SelectedMapId;
            if (GUILayout.Button(map.DisplayName, GUILayout.Height(30f)))
                manager.SelectMap(map.Id);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        if (!manager.CanSelectMap)
            GUILayout.Label("Only the Master Client can select a map while in the Lobby.");
        GUILayout.Space(8f);
        GUILayout.Label("Players");

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            LobbyPlayerState player = lobby.Players[i];
            string master = player.IsMasterClient ? " [MASTER]" : string.Empty;
            string inactive = player.IsInactive ? " DISCONNECTED" : string.Empty;
            GUILayout.Label(
                $"Slot {i + 1}: {player.Nickname}{master} | {player.Role} | " +
                $"{(player.IsReady ? "READY" : "Not Ready")} | map ack {player.MapAcknowledgement}/{lobby.MapRevision} | {player.Ping} ms{inactive}");
        }

        for (int i = lobby.Players.Count; i < 3; i++)
            GUILayout.Label($"Slot {i + 1}: Waiting...");

        GUILayout.Space(10f);
        GUILayout.Label($"Local role: {manager.LocalRole}");
        GUILayout.BeginHorizontal();
        DrawRoleButton(manager, SlimeRole.Anchor);
        DrawRoleButton(manager, SlimeRole.Bounce);
        DrawRoleButton(manager, SlimeRole.Sticky);
        GUILayout.EndHorizontal();

        LobbyPlayerState localPlayer = FindLocalPlayer(lobby);
        bool localReady = localPlayer != null && localPlayer.IsReady;
        GUI.enabled = manager.LocalRole != SlimeRole.None;
        if (GUILayout.Button(localReady ? "Cancel Ready" : "Ready", GUILayout.Height(34f)))
            manager.SetReady(!localReady);
        GUI.enabled = true;

        GUI.enabled = manager.CanStartGame;
        if (GUILayout.Button("Start Game (Master Only)", GUILayout.Height(38f)))
            manager.StartGame();
        GUI.enabled = true;

        if (!manager.CanStartGame)
            GUILayout.Label("Start needs exactly 3 active players, all roles, current map acknowledged, and all Ready.");

        GUILayout.Space(8f);
        GUI.enabled = !manager.IsOperationInProgress;
        if (GUILayout.Button("Leave Room"))
            manager.LeaveRoom();
        GUI.enabled = true;
    }

    private static void DrawRoleButton(NetworkManager manager, SlimeRole role)
    {
        GUI.enabled = manager.LocalRole != role;
        if (GUILayout.Button(role.ToString(), GUILayout.Height(32f)))
            manager.SelectRole(role);
        GUI.enabled = true;
    }

    private static LobbyPlayerState FindLocalPlayer(LobbySnapshot lobby)
    {
        if (PhotonNetwork.LocalPlayer == null)
            return null;

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            if (lobby.Players[i].ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                return lobby.Players[i];
        }
        return null;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
        errorStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            normal = { textColor = new Color(1f, 0.45f, 0.35f) }
        };
        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(16, 16, 16, 16)
        };
    }
}
