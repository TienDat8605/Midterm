using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Implementation behind the project-owned NetworkManager component.
/// Keeping this separate leaves NetworkManager.cs as the stable Unity entry point.
/// </summary>
public abstract class NetworkManagerCore : MonoBehaviourPunCallbacks
{
    private const string UserIdPreferenceKey = "DinoPark.PhotonUserId";
    private const int MaximumCreateAttempts = 3;

    public static NetworkManager Instance { get; private set; }

    [Header("Photon")]
    [SerializeField] private string gameVersion = "0.6.0";
    [SerializeField] private string developmentRegion = "asia";
    [SerializeField] private bool connectOnStart = true;
    [SerializeField] private MultiplayerMapCatalog mapCatalog;

    [Header("Network player prefabs")]
    [Tooltip("Prefabs registered with PUN's DefaultPool. They may live outside Resources.")]
    [SerializeField] private GameObject[] networkPlayerPrefabs;

    public event Action<NetworkConnectionState> ConnectionStateChanged;
    public event Action<LobbySnapshot> LobbyStateChanged;
    public event Action<LobbyPlayerState> PlayerStateChanged;
    public event Action<RoomStateSnapshot> RoomStateChanged;
    public event Action<NetworkErrorCode, string> NetworkError;

    public NetworkConnectionState ConnectionState { get; private set; } = NetworkConnectionState.Disconnected;
    public NetworkErrorCode LastErrorCode { get; private set; } = NetworkErrorCode.None;
    public string LastErrorMessage { get; private set; } = string.Empty;
    public LobbySnapshot CurrentLobby { get; private set; } = LobbySnapshot.Empty;
    public RoomStateSnapshot CurrentRoomState { get; private set; } = RoomStateSnapshot.Empty;
    public string GameVersion => gameVersion;
    public string Region => PhotonNetwork.CloudRegion ?? developmentRegion;
    public string CurrentRoomCode => PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : string.Empty;
    public bool IsMasterClient => PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;
    public bool CanStartGame =>
        CurrentLobby.CanStartGame && IsMasterClient && !isMapSelectionPending;
    public bool CanSelectMap =>
        IsMasterClient && ReadPhase() == RoomPhase.Lobby && !isMapSelectionPending;
    public IReadOnlyList<MultiplayerMapEntry> AvailableMaps =>
        ResolvedMapCatalog != null ? ResolvedMapCatalog.Maps : Array.Empty<MultiplayerMapEntry>();
    public bool IsOperationInProgress { get; private set; }
    public SlimeRole LocalRole => PhotonNetwork.InRoom
        ? ReadRole(PhotonNetwork.LocalPlayer.CustomProperties)
        : SlimeRole.None;

    private int createAttemptCount;
    private string pendingRoomCode;

    private float nextPingUpdateAt;
    private bool isMapSelectionPending;
    private MultiplayerMapCatalog ResolvedMapCatalog
    {
        get
        {
            if (mapCatalog == null)
                mapCatalog = Resources.Load<MultiplayerMapCatalog>("MultiplayerMapCatalog");
            return mapCatalog;
        }
    }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = (NetworkManager)this;
        DontDestroyOnLoad(gameObject);
        ConfigurePhoton();
        RegisterNetworkPrefabs();
    }

    protected virtual void Start()
    {
        if (connectOnStart)
            Connect();
    }

    protected virtual void Update()
    {
        if (!PhotonNetwork.InRoom || Time.unscaledTime < nextPingUpdateAt)
            return;

        nextPingUpdateAt = Time.unscaledTime + 2f;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { NetworkPropertyKeys.PlayerPing, PhotonNetwork.GetPing() }
        });
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Connect()
    {
        if (PhotonNetwork.IsConnected)
            return;

        ClearError();
        SetConnectionState(NetworkConnectionState.Connecting);
        Debug.Log($"[Network] Connecting with version {gameVersion} in region {developmentRegion}...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public void SetNickname(string nickname)
    {
        string normalized = string.IsNullOrWhiteSpace(nickname) ? string.Empty : nickname.Trim();
        if (normalized.Length > 20)
            normalized = normalized.Substring(0, 20);

        PhotonNetwork.NickName = string.IsNullOrEmpty(normalized)
            ? BuildDefaultNickname(PhotonNetwork.AuthValues?.UserId)
            : normalized;
        RaiseSnapshotChanged();
    }

    public void CreateRoom()
    {
        if (!CanBeginRoomOperation())
            return;
        if (!TryResolveMap(ResolvedMapCatalog != null ? ResolvedMapCatalog.DefaultMapId : string.Empty,
                true, out _))
            return;

        ClearError();
        IsOperationInProgress = true;
        createAttemptCount = 0;
        SetConnectionState(NetworkConnectionState.JoiningRoom);
        TryCreateGeneratedRoom();
    }

    public void JoinRoom(string roomCode)
    {
        if (!CanBeginRoomOperation())
            return;

        string normalized = RoomCodeService.Normalize(roomCode);
        if (!RoomCodeService.IsValid(normalized))
        {
            ReportError(NetworkErrorCode.InvalidRoomCode, "Room code must contain six valid letters or numbers.");
            return;
        }

        ClearError();
        IsOperationInProgress = true;
        pendingRoomCode = normalized;
        SetConnectionState(NetworkConnectionState.JoiningRoom);
        PhotonNetwork.JoinRoom(normalized);
    }

    public void LeaveRoom()
    {
        if (!PhotonNetwork.InRoom || IsOperationInProgress)
            return;

        IsOperationInProgress = true;
        PhotonNetwork.LeaveRoom(false);
    }

    public void SelectRole(SlimeRole role)
    {
        if (!PhotonNetwork.InRoom || ReadPhase() != RoomPhase.Lobby)
        {
            ReportError(NetworkErrorCode.PhotonError, "Roles can only be selected while in the lobby.");
            return;
        }

        if (role == LocalRole)
            return;

        ClearError();
        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { NetworkPropertyKeys.PlayerRole, RoleToString(role) },
            { NetworkPropertyKeys.PlayerReady, false }
        });
    }

    public void SetReady(bool ready)
    {
        if (!PhotonNetwork.InRoom || ReadPhase() != RoomPhase.Lobby || LocalRole == SlimeRole.None)
            return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { NetworkPropertyKeys.PlayerReady, ready },
            { NetworkPropertyKeys.PlayerMapAcknowledgement, ReadMapRevision() }
        });
    }

    public bool SelectMap(string mapId)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            ReportError(NetworkErrorCode.NotMasterClient, "Only the Master Client can select a map.");
            return false;
        }

        if (ReadPhase() != RoomPhase.Lobby)
        {
            ReportError(NetworkErrorCode.InvalidMap, "Maps can only be selected while in the lobby.");
            return false;
        }

        if (isMapSelectionPending)
        {
            ReportError(NetworkErrorCode.OperationInProgress, "A map selection is already pending.");
            return false;
        }

        if (!TryResolveMap(mapId, true, out MultiplayerMapEntry map))
            return false;

        PhotonHashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
        string currentMapId = ReadString(room, NetworkPropertyKeys.Map, string.Empty);
        if (currentMapId == map.Id)
            return true;

        int currentRevision = ReadInt(room, NetworkPropertyKeys.MapRevision, 0);
        isMapSelectionPending = true;
        RaiseSnapshotChanged();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            { NetworkPropertyKeys.Map, map.Id },
            { NetworkPropertyKeys.MapRevision, currentRevision + 1 }
        });
        return true;
    }

    public void StartGame()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            ReportError(NetworkErrorCode.NotMasterClient, "Only the Master Client can start the game.");
            return;
        }

        RaiseSnapshotChanged();
        if (!CanStartGame)
        {
            ReportError(NetworkErrorCode.StartRequirementsNotMet,
                "Start requires three active players, all roles, and everyone Ready.");
            return;
        }

        if (!TryResolveMap(CurrentLobby.SelectedMapId, true, out MultiplayerMapEntry selectedMap))
            return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            { NetworkPropertyKeys.Phase, PhaseToString(RoomPhase.Loading) }
        });
        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { NetworkPropertyKeys.PlayerLoaded, false }
        });

        SetConnectionState(NetworkConnectionState.LoadingGame);
        PhotonNetwork.LoadLevel(selectedMap.SceneName);
    }

    public void MarkLocalPlayerLoaded()
    {
        if (!PhotonNetwork.InRoom)
            return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { NetworkPropertyKeys.PlayerLoaded, true }
        });
        TryEnterPlayingPhase();
    }

    public bool TrySetCheckpoint(int checkpoint) =>
        TryMutateSharedState(NetworkPropertyKeys.Checkpoint, checkpoint);

    public bool TrySetSharedEggs(int eggs) =>
        TryMutateSharedState(NetworkPropertyKeys.Eggs, Mathf.Max(0, eggs));

    public bool TrySetShopState(string state) =>
        TryMutateSharedState(NetworkPropertyKeys.Shop, state ?? string.Empty);

    public override void OnConnectedToMaster()
    {
        IsOperationInProgress = false;
        SetConnectionState(NetworkConnectionState.ConnectedToMaster);
        Debug.Log($"[Network] Connected to Photon region {PhotonNetwork.CloudRegion}.");
    }

    public override void OnCreatedRoom()
    {
        Debug.Log($"[Network] Created private room {PhotonNetwork.CurrentRoom.Name}.");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (returnCode == ErrorCode.GameIdAlreadyExists && createAttemptCount < MaximumCreateAttempts)
        {
            TryCreateGeneratedRoom();
            return;
        }

        IsOperationInProgress = false;
        SetConnectionState(NetworkConnectionState.ConnectedToMaster);
        ReportError(NetworkErrorCode.PhotonError, $"Could not create a room: {message}");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        IsOperationInProgress = false;
        SetConnectionState(NetworkConnectionState.ConnectedToMaster);

        NetworkErrorCode code = NetworkErrorCode.PhotonError;
        if (returnCode == ErrorCode.GameDoesNotExist) code = NetworkErrorCode.RoomNotFound;
        else if (returnCode == ErrorCode.GameFull) code = NetworkErrorCode.RoomFull;
        else if (returnCode == ErrorCode.GameClosed) code = NetworkErrorCode.RoomClosed;

        ReportError(code, $"Could not join room {pendingRoomCode}: {message}");
    }

    public override void OnJoinedRoom()
    {
        IsOperationInProgress = false;
        pendingRoomCode = PhotonNetwork.CurrentRoom.Name;
        EnsureLocalPlayerProperties();
        AcknowledgeMapRevision();
        SetConnectionState(ReadPhase() == RoomPhase.Loading
            ? NetworkConnectionState.LoadingGame
            : NetworkConnectionState.InRoom);
        RaiseSnapshotChanged();
        Debug.Log($"[Network] Joined room {pendingRoomCode}. Players: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnLeftRoom()
    {
        IsOperationInProgress = false;
        isMapSelectionPending = false;
        pendingRoomCode = string.Empty;
        CurrentLobby = LobbySnapshot.Empty;
        CurrentRoomState = RoomStateSnapshot.Empty;
        SetConnectionState(NetworkConnectionState.ConnectedToMaster);
        LobbyStateChanged?.Invoke(CurrentLobby);
        RoomStateChanged?.Invoke(CurrentRoomState);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[Network] {newPlayer.NickName} joined room {CurrentRoomCode}.");
        RaiseSnapshotChanged(newPlayer);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (PhotonNetwork.IsMasterClient && !otherPlayer.IsInactive)
            ReleaseReservationsFor(otherPlayer.UserId);

        Debug.Log($"[Network] {otherPlayer.NickName} left. Inactive={otherPlayer.IsInactive}");
        RaiseSnapshotChanged(otherPlayer);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps)
    {
        RaiseSnapshotChanged(targetPlayer);
        TryEnterPlayingPhase();
    }

    public override void OnRoomPropertiesUpdate(PhotonHashtable changedProps)
    {
        ConfirmPendingRoleIfPossible();
        if (changedProps.ContainsKey(NetworkPropertyKeys.Map) ||
            changedProps.ContainsKey(NetworkPropertyKeys.MapRevision))
        {
            isMapSelectionPending = false;
            AcknowledgeMapRevision();
        }
        RaiseSnapshotChanged();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"[Network] Master Client changed to actor {newMasterClient.ActorNumber}.");
        RaiseSnapshotChanged(newMasterClient);
        TryEnterPlayingPhase();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        IsOperationInProgress = false;
        isMapSelectionPending = false;
        CurrentLobby = LobbySnapshot.Empty;
        CurrentRoomState = RoomStateSnapshot.Empty;
        SetConnectionState(NetworkConnectionState.Failed);
        ReportError(NetworkErrorCode.PhotonError, $"Disconnected from Photon: {cause}");
    }

    private void ConfigurePhoton()
    {
        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.SendRate = 20;
        PhotonNetwork.SerializationRate = 10;
        PhotonNetwork.KeepAliveInBackground = 60;
        if (!string.IsNullOrWhiteSpace(developmentRegion))
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = developmentRegion.Trim().ToLowerInvariant();

        string userId = GetOrCreateStableUserId();
        PhotonNetwork.AuthValues = new AuthenticationValues(userId);
        if (string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
            PhotonNetwork.NickName = BuildDefaultNickname(userId);
    }

    private void RegisterNetworkPrefabs()
    {
        if (!(PhotonNetwork.PrefabPool is DefaultPool defaultPool))
        {
            Debug.LogWarning("[Network] Custom prefab pool active; skipping DefaultPool registration.");
            return;
        }

        if (networkPlayerPrefabs == null)
            return;

        foreach (GameObject prefab in networkPlayerPrefabs)
        {
            if (prefab == null)
                continue;
            if (prefab.GetComponent<PhotonView>() == null)
            {
                Debug.LogError($"[Network] Cannot register {prefab.name}: PhotonView is missing.");
                continue;
            }
            defaultPool.ResourceCache[prefab.name] = prefab;
        }
    }

    private bool CanBeginRoomOperation()
    {
        if (!PhotonNetwork.IsConnectedAndReady || ConnectionState != NetworkConnectionState.ConnectedToMaster)
        {
            ReportError(NetworkErrorCode.NotConnected, "Wait until Photon is connected first.");
            return false;
        }
        if (IsOperationInProgress)
        {
            ReportError(NetworkErrorCode.OperationInProgress, "Another network operation is in progress.");
            return false;
        }
        return true;
    }

    private void TryCreateGeneratedRoom()
    {
        createAttemptCount++;
        pendingRoomCode = RoomCodeService.Generate();
        MultiplayerMapEntry defaultMap;
        if (!TryResolveMap(ResolvedMapCatalog != null ? ResolvedMapCatalog.DefaultMapId : string.Empty,
                true, out defaultMap))
        {
            IsOperationInProgress = false;
            SetConnectionState(NetworkConnectionState.ConnectedToMaster);
            return;
        }

        PhotonHashtable properties = new PhotonHashtable
        {
            { NetworkPropertyKeys.SchemaVersion, 1 },
            { NetworkPropertyKeys.SaveOwner, PhotonNetwork.AuthValues.UserId },
            { NetworkPropertyKeys.Phase, PhaseToString(RoomPhase.Lobby) },
            { NetworkPropertyKeys.Map, defaultMap.Id },
            { NetworkPropertyKeys.MapRevision, 0 },
            { NetworkPropertyKeys.Mode, "normal" },
            { NetworkPropertyKeys.Checkpoint, -1 },
            { NetworkPropertyKeys.Eggs, 0 },
            { NetworkPropertyKeys.Shop, string.Empty },
            { NetworkPropertyKeys.Revision, 0 },
            { NetworkPropertyKeys.AnchorReservation, string.Empty },
            { NetworkPropertyKeys.BounceReservation, string.Empty },
            { NetworkPropertyKeys.StickyReservation, string.Empty }
        };
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 3,
            IsVisible = false,
            IsOpen = true,
            PublishUserId = true,
            PlayerTtl = 60000,
            EmptyRoomTtl = 60000,
            CleanupCacheOnLeave = false,
            CustomRoomProperties = properties
        };
        PhotonNetwork.CreateRoom(pendingRoomCode, options, TypedLobby.Default);
    }

    private void EnsureLocalPlayerProperties()
    {
        PhotonHashtable current = PhotonNetwork.LocalPlayer.CustomProperties;
        PhotonHashtable defaults = new PhotonHashtable();
        if (ReadPhase() == RoomPhase.Lobby)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
            {
                { NetworkPropertyKeys.PlayerRole, "none" },
                { NetworkPropertyKeys.PlayerReady, false },
                { NetworkPropertyKeys.PlayerItem, "none" },
                { NetworkPropertyKeys.PlayerConnection, "connected" },
                { NetworkPropertyKeys.PlayerLoaded, false },
                { NetworkPropertyKeys.PlayerPing, PhotonNetwork.GetPing() },
                { NetworkPropertyKeys.PlayerMapAcknowledgement, ReadMapRevision() }
            });
            return;
        }

        AddDefault(defaults, current, NetworkPropertyKeys.PlayerRole, "none");
        AddDefault(defaults, current, NetworkPropertyKeys.PlayerReady, false);
        AddDefault(defaults, current, NetworkPropertyKeys.PlayerItem, "none");
        AddDefault(defaults, current, NetworkPropertyKeys.PlayerConnection, "connected");
        AddDefault(defaults, current, NetworkPropertyKeys.PlayerLoaded, false);
        AddDefault(defaults, current, NetworkPropertyKeys.PlayerPing, PhotonNetwork.GetPing());
        AddDefault(defaults, current, NetworkPropertyKeys.PlayerMapAcknowledgement, ReadMapRevision());
        if (defaults.Count > 0)
            PhotonNetwork.LocalPlayer.SetCustomProperties(defaults);
    }

    private static void AddDefault(PhotonHashtable destination, PhotonHashtable current, string key, object value)
    {
        if (!current.ContainsKey(key)) destination[key] = value;
    }

    private void AcknowledgeMapRevision()
    {
        if (!PhotonNetwork.InRoom || ReadPhase() != RoomPhase.Lobby)
            return;

        int revision = ReadMapRevision();
        PhotonHashtable player = PhotonNetwork.LocalPlayer.CustomProperties;
        if (ReadInt(player, NetworkPropertyKeys.PlayerMapAcknowledgement, -1) == revision &&
            !ReadBool(player, NetworkPropertyKeys.PlayerReady, false))
        {
            return;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { NetworkPropertyKeys.PlayerReady, false },
            { NetworkPropertyKeys.PlayerMapAcknowledgement, revision }
        });
    }

    private void ConfirmPendingRoleIfPossible()
    {
    }

    private void ReleaseReservationsFor(string userId)
    {
    }

    private void TryEnterPlayingPhase()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || ReadPhase() != RoomPhase.Loading)
            return;

        int loaded = 0;
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (player.IsInactive || !ReadBool(player.CustomProperties, NetworkPropertyKeys.PlayerLoaded, false))
                return;
            loaded++;
        }
        if (loaded == 3)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                { NetworkPropertyKeys.Phase, PhaseToString(RoomPhase.Playing) }
            });
        }
    }

    private bool TryMutateSharedState(string key, object value)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            ReportError(NetworkErrorCode.NotMasterClient, "Only the Master Client can mutate shared state.");
            return false;
        }
        int revision = ReadInt(PhotonNetwork.CurrentRoom.CustomProperties, NetworkPropertyKeys.Revision, 0);
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            { key, value },
            { NetworkPropertyKeys.Revision, revision + 1 }
        });
        return true;
    }

    private void RaiseSnapshotChanged(Player changedPlayer = null)
    {
        if (!PhotonNetwork.InRoom)
        {
            CurrentLobby = LobbySnapshot.Empty;
            CurrentRoomState = RoomStateSnapshot.Empty;
            return;
        }

        List<LobbyPlayerState> players = new List<LobbyPlayerState>();
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
            players.Add(BuildPlayerState(player));
        players.Sort((a, b) => a.ActorNumber.CompareTo(b.ActorNumber));

        PhotonHashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
        string selectedMapId = ReadString(room, NetworkPropertyKeys.Map,
            ResolvedMapCatalog != null ? ResolvedMapCatalog.DefaultMapId : string.Empty);
        int mapRevision = ReadInt(room, NetworkPropertyKeys.MapRevision, 0);
        string selectedMapDisplayName = selectedMapId;
        if (ResolvedMapCatalog != null &&
            ResolvedMapCatalog.TryGetMap(selectedMapId, out MultiplayerMapEntry selectedMap))
        {
            selectedMapDisplayName = selectedMap.DisplayName;
        }

        CurrentLobby = new LobbySnapshot(
            PhotonNetwork.CurrentRoom.Name,
            ReadPhase(),
            selectedMapId,
            selectedMapDisplayName,
            mapRevision,
            PhotonNetwork.IsMasterClient,
            LobbyRules.CanStart(players, mapRevision) && !isMapSelectionPending,
            players);
        CurrentRoomState = new RoomStateSnapshot(
            ReadInt(room, NetworkPropertyKeys.Checkpoint, -1),
            ReadInt(room, NetworkPropertyKeys.Eggs, 0),
            ReadString(room, NetworkPropertyKeys.Shop, string.Empty),
            ReadInt(room, NetworkPropertyKeys.Revision, 0));

        LobbyStateChanged?.Invoke(CurrentLobby);
        RoomStateChanged?.Invoke(CurrentRoomState);
        if (changedPlayer != null) PlayerStateChanged?.Invoke(BuildPlayerState(changedPlayer));
    }

    private static LobbyPlayerState BuildPlayerState(Player player)
    {
        return new LobbyPlayerState(
            player.ActorNumber,
            player.UserId ?? string.Empty,
            player.NickName ?? $"Player {player.ActorNumber}",
            ReadRole(player.CustomProperties),
            ReadBool(player.CustomProperties, NetworkPropertyKeys.PlayerReady, false),
            ReadBool(player.CustomProperties, NetworkPropertyKeys.PlayerLoaded, false),
            player.IsInactive,
            PhotonNetwork.MasterClient != null && player.ActorNumber == PhotonNetwork.MasterClient.ActorNumber,
            ReadInt(player.CustomProperties, NetworkPropertyKeys.PlayerPing, 0),
            ReadInt(player.CustomProperties, NetworkPropertyKeys.PlayerMapAcknowledgement, -1));
    }

    private int ReadMapRevision()
    {
        return PhotonNetwork.InRoom
            ? ReadInt(PhotonNetwork.CurrentRoom.CustomProperties, NetworkPropertyKeys.MapRevision, 0)
            : 0;
    }

    private bool TryResolveMap(string mapId, bool requireBuildScene, out MultiplayerMapEntry map)
    {
        map = null;
        MultiplayerMapCatalog catalog = ResolvedMapCatalog;
        if (catalog == null)
        {
            ReportError(NetworkErrorCode.InvalidMap, "Multiplayer map catalog is missing.");
            return false;
        }

        if (!catalog.IsValid(out string catalogError))
        {
            ReportError(NetworkErrorCode.InvalidMap, catalogError);
            return false;
        }

        if (!catalog.TryGetMap(mapId, out map))
        {
            ReportError(NetworkErrorCode.InvalidMap, $"Unknown multiplayer map id: '{mapId}'.");
            return false;
        }

        if (requireBuildScene && !Application.CanStreamedLevelBeLoaded(map.SceneName))
        {
            ReportError(NetworkErrorCode.InvalidMap,
                $"Scene '{map.SceneName}' for map '{map.Id}' is not enabled in Build Settings.");
            map = null;
            return false;
        }

        return true;
    }

    private RoomPhase ReadPhase()
    {
        string value = PhotonNetwork.InRoom
            ? ReadString(PhotonNetwork.CurrentRoom.CustomProperties, NetworkPropertyKeys.Phase, string.Empty)
            : string.Empty;
        if (value == "lobby") return RoomPhase.Lobby;
        if (value == "loading") return RoomPhase.Loading;
        if (value == "playing") return RoomPhase.Playing;
        if (value == "paused") return RoomPhase.Paused;
        if (value == "finished") return RoomPhase.Finished;
        return RoomPhase.None;
    }

    private static SlimeRole ReadRole(PhotonHashtable properties)
    {
        string value = ReadString(properties, NetworkPropertyKeys.PlayerRole, "none");
        if (value == "anchor") return SlimeRole.Anchor;
        if (value == "bounce") return SlimeRole.Bounce;
        if (value == "sticky") return SlimeRole.Sticky;
        return SlimeRole.None;
    }

    private static string RoleToString(SlimeRole role)
    {
        if (role == SlimeRole.Anchor) return "anchor";
        if (role == SlimeRole.Bounce) return "bounce";
        if (role == SlimeRole.Sticky) return "sticky";
        return "none";
    }

    private static string PhaseToString(RoomPhase phase)
    {
        if (phase == RoomPhase.Lobby) return "lobby";
        if (phase == RoomPhase.Loading) return "loading";
        if (phase == RoomPhase.Playing) return "playing";
        if (phase == RoomPhase.Paused) return "paused";
        if (phase == RoomPhase.Finished) return "finished";
        return "none";
    }

    private static string ReadString(PhotonHashtable table, string key, string fallback) =>
        table != null && table.TryGetValue(key, out object value) && value is string text ? text : fallback;

    private static bool ReadBool(PhotonHashtable table, string key, bool fallback) =>
        table != null && table.TryGetValue(key, out object value) && value is bool result ? result : fallback;

    private static int ReadInt(PhotonHashtable table, string key, int fallback)
    {
        if (table == null || !table.TryGetValue(key, out object value)) return fallback;
        if (value is int intValue) return intValue;
        if (value is byte byteValue) return byteValue;
        if (value is short shortValue) return shortValue;
        return fallback;
    }

    private static string GetOrCreateStableUserId()
    {
        string baseId = PlayerPrefs.GetString(UserIdPreferenceKey, string.Empty);
        if (string.IsNullOrEmpty(baseId))
        {
            baseId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(UserIdPreferenceKey, baseId);
            PlayerPrefs.Save();
        }
#if UNITY_EDITOR
        return $"{baseId}-{ShortHash(Application.dataPath)}";
#else
        return baseId;
#endif
    }

    private static string ShortHash(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder text = new StringBuilder(8);
            for (int i = 0; i < 4; i++) text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }
    }

    private static string BuildDefaultNickname(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return "Player";
        int start = Mathf.Max(0, userId.Length - 4);
        return $"Player-{userId.Substring(start).ToUpperInvariant()}";
    }

    private void SetConnectionState(NetworkConnectionState state)
    {
        if (ConnectionState == state) return;
        ConnectionState = state;
        ConnectionStateChanged?.Invoke(state);
    }

    private void ClearError()
    {
        LastErrorCode = NetworkErrorCode.None;
        LastErrorMessage = string.Empty;
    }

    private void ReportError(NetworkErrorCode code, string message)
    {
        LastErrorCode = code;
        LastErrorMessage = message;
        Debug.LogWarning($"[Network] {code}: {message}");
        NetworkError?.Invoke(code, message);
    }
}
