using System;
using System.Collections.Generic;

public enum SlimeRole
{
    None,
    Anchor,
    Bounce,
    Sticky
}

public enum RoomPhase
{
    None,
    Lobby,
    Loading,
    Playing,
    Paused,
    Finished
}

public enum NetworkConnectionState
{
    Disconnected,
    Connecting,
    ConnectedToMaster,
    JoiningRoom,
    InRoom,
    LoadingGame,
    Failed
}

public enum NetworkErrorCode
{
    None,
    NotConnected,
    InvalidRoomCode,
    RoomNotFound,
    RoomFull,
    RoomClosed,
    RoleUnavailable,
    StartRequirementsNotMet,
    NotMasterClient,
    OperationInProgress,
    PhotonError,
    InvalidMap
}

[Serializable]
public sealed class LobbyPlayerState
{
    public int ActorNumber { get; }
    public string UserId { get; }
    public string Nickname { get; }
    public SlimeRole Role { get; }
    public bool IsReady { get; }
    public bool IsLoaded { get; }
    public bool IsInactive { get; }
    public bool IsMasterClient { get; }
    public int Ping { get; }
    public int MapAcknowledgement { get; }

    public LobbyPlayerState(
        int actorNumber,
        string userId,
        string nickname,
        SlimeRole role,
        bool isReady,
        bool isLoaded,
        bool isInactive,
        bool isMasterClient,
        int ping,
        int mapAcknowledgement = 0)
    {
        ActorNumber = actorNumber;
        UserId = userId;
        Nickname = nickname;
        Role = role;
        IsReady = isReady;
        IsLoaded = isLoaded;
        IsInactive = isInactive;
        IsMasterClient = isMasterClient;
        Ping = ping;
        MapAcknowledgement = mapAcknowledgement;
    }
}

public sealed class LobbySnapshot
{
    public static readonly LobbySnapshot Empty = new LobbySnapshot(
        string.Empty,
        RoomPhase.None,
        string.Empty,
        string.Empty,
        0,
        false,
        false,
        Array.Empty<LobbyPlayerState>());

    public string RoomCode { get; }
    public RoomPhase Phase { get; }
    public string SelectedMapId { get; }
    public string SelectedMapDisplayName { get; }
    public int MapRevision { get; }
    public bool IsMasterClient { get; }
    public bool CanStartGame { get; }
    public IReadOnlyList<LobbyPlayerState> Players { get; }

    public LobbySnapshot(
        string roomCode,
        RoomPhase phase,
        string selectedMapId,
        string selectedMapDisplayName,
        int mapRevision,
        bool isMasterClient,
        bool canStartGame,
        IReadOnlyList<LobbyPlayerState> players)
    {
        RoomCode = roomCode;
        Phase = phase;
        SelectedMapId = selectedMapId;
        SelectedMapDisplayName = selectedMapDisplayName;
        MapRevision = mapRevision;
        IsMasterClient = isMasterClient;
        CanStartGame = canStartGame;
        Players = players;
    }
}

public sealed class RoomStateSnapshot
{
    public static readonly RoomStateSnapshot Empty = new RoomStateSnapshot(-1, 0, string.Empty, 0);

    public int Checkpoint { get; }
    public int SharedEggs { get; }
    public string ShopState { get; }
    public int Revision { get; }

    public RoomStateSnapshot(int checkpoint, int sharedEggs, string shopState, int revision)
    {
        Checkpoint = checkpoint;
        SharedEggs = sharedEggs;
        ShopState = shopState;
        Revision = revision;
    }
}
