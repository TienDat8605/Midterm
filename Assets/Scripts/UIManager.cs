using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.UIElements;

/// <summary>
/// Single MonoBehaviour that owns the one UIDocument in the scene.
/// Manages screen transitions between MainMenu and Lobby (which includes character selection).
/// </summary>
public class UIManager : MonoBehaviour
{
    // ----------------------------------------------------------------
    // Inspector
    // ----------------------------------------------------------------
    [Header("Multiplayer (placeholder)")]
    public string placeholderCode = "X4KD7P";

    // ================================================================
    // Character data
    // ================================================================
    [System.Serializable]
    public struct CharacterData
    {
        public string charName;
        public string charTag;
        public string description;
        public Sprite portrait;
    }

    [Header("Characters (Anchor / Bounce / Sticky)")]
    public List<CharacterData> characters = new List<CharacterData>
    {
        new CharacterData { charName = "ANCHOR", charTag = "Slime Da",    description = "Heavy and stable. Brace to anchor to surfaces." },
        new CharacterData { charName = "BOUNCE", charTag = "Slime Lo Xo", description = "High bounce. Trampoline to launch allies." },
        new CharacterData { charName = "STICKY", charTag = "Slime Dinh",  description = "Stick to walls. Tether to pull or rescue allies." },
    };

    // ================================================================
    // CSS class constants
    // ================================================================
    private const string CSS_SLOT_READY    = "slot-ready";
    private const string CSS_BTN_READY     = "ready-slot-button--active";
    private const string CSS_START_ENABLED = "start-button--enabled";

    // ================================================================
    // Private UI references
    // ================================================================
    private UIDocument _uiDoc;

    // Screen roots
    private VisualElement _mainMenuScreen;
    private VisualElement _lobbyScreen; // renamed from charSelectScreen

    // ---- Main Menu ----
    private Button    _hostButton;
    private Button    _singlePlayerButton;
    private Button    _joinButton;
    private TextField _codeInput;
    private DropdownField _displayModeDropdown;
    private readonly Dictionary<string, GameDisplayMode> _displayModesByLabel =
        new Dictionary<string, GameDisplayMode>();

    // ---- Lobby ----
    private const int NUM_SLOTS = 3;
    private int[]           _selectedIndex = new int[NUM_SLOTS];
    private bool[]          _isReady       = new bool[NUM_SLOTS];
    private VisualElement[] _slotRoots     = new VisualElement[NUM_SLOTS];
    private VisualElement[] _charImages    = new VisualElement[NUM_SLOTS];
    private Label[]         _charNames     = new Label[NUM_SLOTS];
    private Label[]         _charTags      = new Label[NUM_SLOTS];
    private Button[]        _readyBtns     = new Button[NUM_SLOTS];
    
    // New Lobby elements
    private Label           _roomCodeLabel;
    private Button          _copyBtn;
    private Button          _previousMapButton;
    private Button          _nextMapButton;
    private Label           _mapLabel;
    private MultiplayerMapCatalog _mapCatalog;
    private int             _selectedMapIndex;

    private Label           _statusLabel;
    private Button          _startButton;
    private Button          _backButton;

    // ================================================================
    // Unity lifecycle
    // ================================================================
    private void Awake()
    {
        SinglePlayerSession.Stop();
        _uiDoc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (_uiDoc == null) return;
        var root = _uiDoc.rootVisualElement;

        // ---- Locate screen roots ----
        _mainMenuScreen = root.Q<VisualElement>("MainMenuScreen");
        _lobbyScreen    = root.Q<VisualElement>("LobbyScreen"); // from RootUI.uxml
        _displayModeDropdown = root.Q<DropdownField>("DisplayModeDropdown");

        SetupMainMenu();
        SetupLobby();
        SetupDisplayModeDropdown();

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.ConnectionStateChanged += OnConnectionStateChanged;
            NetworkManager.Instance.LobbyStateChanged += OnLobbyStateChanged;
            OnConnectionStateChanged(NetworkManager.Instance.ConnectionState);
            OnLobbyStateChanged(NetworkManager.Instance.CurrentLobby);
        }
        else
        {
            ShowScreen(Screen.MainMenu);
        }
    }

    private void OnDisable()
    {
        if (_hostButton != null) _hostButton.clicked -= OnHostClicked;
        if (_singlePlayerButton != null) _singlePlayerButton.clicked -= OnSinglePlayerClicked;
        if (_joinButton != null) _joinButton.clicked -= OnJoinClicked;
        if (_startButton != null) _startButton.clicked -= OnStartClicked;
        if (_backButton  != null) _backButton.clicked  -= OnLeaveRoom;
        if (_copyBtn != null) _copyBtn.clicked -= OnCopyCodeClicked;
        if (_displayModeDropdown != null)
            _displayModeDropdown.UnregisterValueChangedCallback(OnDisplayModeSelected);

        if (DisplaySettingsManager.Instance != null)
            DisplaySettingsManager.Instance.DisplayModeChanged -= OnDisplayModeChanged;

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.ConnectionStateChanged -= OnConnectionStateChanged;
            NetworkManager.Instance.LobbyStateChanged -= OnLobbyStateChanged;
        }
    }

    private void OnConnectionStateChanged(NetworkConnectionState state)
    {
        if (SinglePlayerSession.IsActive)
            return;

        if (state == NetworkConnectionState.InRoom)
            ShowScreen(Screen.Lobby);
        else
            ShowScreen(Screen.MainMenu);
    }

    private void OnLobbyStateChanged(LobbySnapshot lobby)
    {
        if (SinglePlayerSession.IsActive)
            return;

        if (lobby == null) return;
        RestoreMultiplayerLobbyPresentation();
        
        if (_roomCodeLabel != null) _roomCodeLabel.text = lobby.RoomCode;

        if (_mapLabel != null && !string.IsNullOrEmpty(lobby.SelectedMapDisplayName))
            _mapLabel.text = $"MAP: {lobby.SelectedMapDisplayName.ToUpper()}";

        for (int i = 0; i < NUM_SLOTS; i++)
        {
            if (i < lobby.Players.Count)
            {
                var player = lobby.Players[i];
                UpdateSlotWithPlayer(i, player);
            }
            else
            {
                ClearSlot(i);
            }
        }
        RefreshBottomPanel(lobby);
    }

    // ================================================================
    // Screen switching
    // ================================================================
    public enum Screen { MainMenu, Lobby }

    private void ShowScreen(Screen screen)
    {
        if (_mainMenuScreen != null)
            _mainMenuScreen.style.display = (screen == Screen.MainMenu) ? DisplayStyle.Flex : DisplayStyle.None;

        if (_lobbyScreen != null)
            _lobbyScreen.style.display = (screen == Screen.Lobby) ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ================================================================
    // MAIN MENU setup
    // ================================================================
    private void SetupMainMenu()
    {
        if (_mainMenuScreen == null) return;

        _hostButton = _mainMenuScreen.Q<Button>("HostBut");
        _singlePlayerButton = _mainMenuScreen.Q<Button>("SinglePlayerBut");
        _joinButton = _mainMenuScreen.Q<Button>("JoinBut");
        _codeInput  = _mainMenuScreen.Q<TextField>();

        if (_codeInput != null)
        {
            _codeInput.value = "Enter code...";
            _codeInput.RegisterCallback<FocusInEvent>(e =>
            {
                if (_codeInput.value == "Enter code...") 
                    _codeInput.value = "";
            });
            _codeInput.RegisterCallback<FocusOutEvent>(e =>
            {
                if (string.IsNullOrEmpty(_codeInput.value)) 
                    _codeInput.value = "Enter code...";
            });
        }

        if (_hostButton != null) _hostButton.clicked += OnHostClicked;
        if (_singlePlayerButton != null) _singlePlayerButton.clicked += OnSinglePlayerClicked;
        if (_joinButton != null) _joinButton.clicked += OnJoinClicked;
    }

    private void SetupDisplayModeDropdown()
    {
        if (_displayModeDropdown == null)
            return;

        _displayModeDropdown.UnregisterValueChangedCallback(OnDisplayModeSelected);
        _displayModeDropdown.RegisterValueChangedCallback(OnDisplayModeSelected);

        DisplaySettingsManager manager = DisplaySettingsManager.Instance;
        if (manager == null)
            return;

        manager.DisplayModeChanged -= OnDisplayModeChanged;
        manager.DisplayModeChanged += OnDisplayModeChanged;
        RefreshDisplayModeDropdown();
    }

    private void OnDisplayModeSelected(ChangeEvent<string> evt)
    {
        if (_displayModesByLabel.TryGetValue(evt.newValue, out GameDisplayMode mode))
            DisplaySettingsManager.Instance?.ApplyDisplayMode(mode);
    }

    private void OnDisplayModeChanged(GameDisplayMode mode)
    {
        RefreshDisplayModeDropdown();
    }

    private void RefreshDisplayModeDropdown()
    {
        if (_displayModeDropdown == null || DisplaySettingsManager.Instance == null)
            return;

        DisplaySettingsManager manager = DisplaySettingsManager.Instance;
        GameDisplayMode currentMode = manager.CurrentMode;
        var modes = new List<GameDisplayMode>(manager.AvailableModes);
        if (currentMode == GameDisplayMode.CustomWindow)
            modes.Insert(Mathf.Max(0, modes.Count - 1), currentMode);

        _displayModesByLabel.Clear();
        var labels = new List<string>(modes.Count);
        foreach (GameDisplayMode mode in modes)
        {
            string label = DisplaySettingsManager.GetDisplayModeLabel(
                mode, UnityEngine.Screen.width, UnityEngine.Screen.height);
            labels.Add(label);
            _displayModesByLabel[label] = mode;
        }

        string currentLabel = DisplaySettingsManager.GetDisplayModeLabel(
            currentMode, UnityEngine.Screen.width, UnityEngine.Screen.height);
        _displayModeDropdown.choices = labels;
        _displayModeDropdown.SetValueWithoutNotify(currentLabel);
        _displayModeDropdown.tooltip = manager.UsesWebDisplayModes
            ? "Choose embedded or browser fullscreen mode"
            : "Choose window resolution or fullscreen mode";
    }

    private void OnSinglePlayerClicked()
    {
        Debug.Log("[UIManager] Opening single-player setup.");
        SinglePlayerSession.BeginSetup();
        ShowSinglePlayerLobby();
    }

    private void OnHostClicked()
    {
        Debug.Log("[UIManager] Host Game clicked.");
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.CreateRoom();
    }

    private void OnJoinClicked()
    {
        string code = _codeInput != null ? _codeInput.value : string.Empty;
        if (code == "Enter code...") code = string.Empty;

        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[UIManager] Room code is empty.");
            return;
        }
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.JoinRoom(code);
    }

    // ================================================================
    // LOBBY setup
    // ================================================================
    private void SetupLobby()
    {
        if (_lobbyScreen == null) return;

        _roomCodeLabel = _lobbyScreen.Q<Label>("RoomCodeLabel");
        _copyBtn = _lobbyScreen.Q<Button>("CopyBtn");
        _mapLabel = _lobbyScreen.Q<Label>("MapLabel");
        _previousMapButton = _lobbyScreen.Q<Button>("PreviousMapButton");
        _nextMapButton = _lobbyScreen.Q<Button>("NextMapButton");
        if (_copyBtn != null) _copyBtn.clicked += OnCopyCodeClicked;
        if (_previousMapButton != null) _previousMapButton.clicked += () => CycleMap(-1);
        if (_nextMapButton != null) _nextMapButton.clicked += () => CycleMap(1);

        for (int i = 0; i < NUM_SLOTS; i++)
        {
            int slotNumber = i + 1;
            int captured   = i;

            _slotRoots[i]  = _lobbyScreen.Q<VisualElement>($"Slot{slotNumber}");
            _charImages[i] = _lobbyScreen.Q<VisualElement>($"CharImage{slotNumber}");
            _charNames[i]  = _lobbyScreen.Q<Label>($"CharName{slotNumber}");
            _charTags[i]   = _lobbyScreen.Q<Label>($"CharTag{slotNumber}");
            _readyBtns[i]  = _lobbyScreen.Q<Button>($"ReadyBtn{slotNumber}");

            var leftArrow  = _lobbyScreen.Q<Button>($"LeftArrow{slotNumber}");
            var rightArrow = _lobbyScreen.Q<Button>($"RightArrow{slotNumber}");

            if (leftArrow  != null) leftArrow.clicked  += () => CycleCharacter(captured, -1);
            if (rightArrow != null) rightArrow.clicked += () => CycleCharacter(captured, +1);
            if (_readyBtns[i] != null) _readyBtns[i].clicked += () => ToggleReady(captured);
        }

        _statusLabel = _lobbyScreen.Q<Label>("StatusLabel");
        _startButton = _lobbyScreen.Q<Button>("StartButton");
        _backButton  = _lobbyScreen.Q<Button>("BackButton");

        if (_startButton != null) _startButton.clicked += OnStartClicked;
        if (_backButton  != null) _backButton.clicked  += OnLeaveRoom;
    }

    private void OnCopyCodeClicked()
    {
        if (_roomCodeLabel != null && !string.IsNullOrEmpty(_roomCodeLabel.text))
        {
            GUIUtility.systemCopyBuffer = _roomCodeLabel.text;
            Debug.Log($"Copied code: {_roomCodeLabel.text}");
        }
    }

    private void CycleCharacter(int slot, int dir)
    {
        if (SinglePlayerSession.IsActive)
        {
            if (slot != 0 || characters.Count == 0)
                return;

            _selectedIndex[0] = (_selectedIndex[0] + dir + characters.Count) % characters.Count;
            SinglePlayerSession.SelectRole((SlimeRole)(_selectedIndex[0] + 1));
            RefreshSinglePlayerCharacter();
            return;
        }

        if (NetworkManager.Instance == null || NetworkManager.Instance.CurrentLobby == null) return;
        var lobby = NetworkManager.Instance.CurrentLobby;
        if (slot >= lobby.Players.Count) return;
        var player = lobby.Players[slot];
        if (player.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return; // Only control local
        if (player.IsReady) return;

        int roleIndex = (int)player.Role - 1;
        if (roleIndex < 0) roleIndex = 0; // Default to first if none
        roleIndex = (roleIndex + dir + 3) % 3;
        NetworkManager.Instance.SelectRole((SlimeRole)(roleIndex + 1));
    }

    private void ToggleReady(int slot)
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.CurrentLobby == null) return;
        var lobby = NetworkManager.Instance.CurrentLobby;
        if (slot >= lobby.Players.Count) return;
        var player = lobby.Players[slot];
        if (player.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return; // Only control local
        if (player.Role == SlimeRole.None) return;

        bool isSettingReady = !player.IsReady;
        if (isSettingReady)
        {
            foreach (var otherPlayer in lobby.Players)
            {
                if (otherPlayer.ActorNumber != player.ActorNumber && otherPlayer.Role == player.Role)
                {
                    Debug.LogWarning("[UIManager] Cannot set ready: Role is already selected by another player.");
                    return;
                }
            }
        }

        NetworkManager.Instance.SetReady(isSettingReady);
    }

    private void UpdateSlotWithPlayer(int slot, LobbyPlayerState player)
    {
        _slotRoots[slot].style.display = DisplayStyle.Flex; // ensure visible
        bool isLocal = player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;

        // Player name and host crown
        var hostCrown = _lobbyScreen.Q<Label>($"HostCrown{slot + 1}");
        if (hostCrown != null) hostCrown.style.display = player.IsMasterClient ? DisplayStyle.Flex : DisplayStyle.None;
        var playerLabel = _lobbyScreen.Q<Label>($"PlayerLabel{slot + 1}");
        if (playerLabel != null) playerLabel.text = string.IsNullOrEmpty(player.Nickname) ? $"Player {slot + 1}" : player.Nickname;

        // Ping
        var pingLabel = _lobbyScreen.Q<Label>($"PingLabel{slot + 1}");
        if (pingLabel != null) pingLabel.text = $"PING: {player.Ping}ms";

        // Character display
        int roleIndex = (int)player.Role - 1;
        if (roleIndex >= 0 && roleIndex < characters.Count)
        {
            var data = characters[roleIndex];
            if (_charImages[slot] != null && data.portrait != null)
                _charImages[slot].style.backgroundImage = new StyleBackground(data.portrait);
            if (_charNames[slot] != null) _charNames[slot].text = data.charName;
            if (_charTags[slot] != null) _charTags[slot].text = data.charTag;
        }
        else
        {
            if (_charImages[slot] != null) _charImages[slot].style.backgroundImage = null;
            if (_charNames[slot] != null) _charNames[slot].text = "SELECTING...";
            if (_charTags[slot] != null) _charTags[slot].text = "";
        }

        // Ready state styling
        if (player.IsReady) _slotRoots[slot].AddToClassList(CSS_SLOT_READY);
        else _slotRoots[slot].RemoveFromClassList(CSS_SLOT_READY);

        // Arrows visibility (only local player)
        var leftArrow = _lobbyScreen.Q<Button>($"LeftArrow{slot + 1}");
        var rightArrow = _lobbyScreen.Q<Button>($"RightArrow{slot + 1}");
        if (leftArrow != null) leftArrow.style.display = isLocal && !player.IsReady ? DisplayStyle.Flex : DisplayStyle.None;
        if (rightArrow != null) rightArrow.style.display = isLocal && !player.IsReady ? DisplayStyle.Flex : DisplayStyle.None;

        // Ready Button visibility
        if (_readyBtns[slot] != null)
        {
            if (isLocal)
            {
                _readyBtns[slot].SetEnabled(true);
                _readyBtns[slot].text = player.IsReady ? "UNREADY" : "READY";
            }
            else
            {
                _readyBtns[slot].SetEnabled(false);
                _readyBtns[slot].text = player.IsReady ? "READY" : "WAITING";
            }
            if (player.IsReady) _readyBtns[slot].AddToClassList(CSS_BTN_READY);
            else _readyBtns[slot].RemoveFromClassList(CSS_BTN_READY);
        }
    }

    private void ClearSlot(int slot)
    {
        _slotRoots[slot].style.display = DisplayStyle.Flex;
        
        var hostCrown = _lobbyScreen.Q<Label>($"HostCrown{slot + 1}");
        if (hostCrown != null) hostCrown.style.display = DisplayStyle.None;
        
        var playerLabel = _lobbyScreen.Q<Label>($"PlayerLabel{slot + 1}");
        if (playerLabel != null) playerLabel.text = "Waiting...";
        
        var pingLabel = _lobbyScreen.Q<Label>($"PingLabel{slot + 1}");
        if (pingLabel != null) pingLabel.text = "";
        
        if (_charImages[slot] != null) _charImages[slot].style.backgroundImage = null;
        if (_charNames[slot] != null) _charNames[slot].text = "";
        if (_charTags[slot] != null) _charTags[slot].text = "";

        _slotRoots[slot].RemoveFromClassList(CSS_SLOT_READY);

        var leftArrow = _lobbyScreen.Q<Button>($"LeftArrow{slot + 1}");
        var rightArrow = _lobbyScreen.Q<Button>($"RightArrow{slot + 1}");
        if (leftArrow != null) leftArrow.style.display = DisplayStyle.None;
        if (rightArrow != null) rightArrow.style.display = DisplayStyle.None;

        if (_readyBtns[slot] != null)
        {
            _readyBtns[slot].SetEnabled(false);
            _readyBtns[slot].text = "";
            _readyBtns[slot].RemoveFromClassList(CSS_BTN_READY);
        }
    }

    private void RefreshBottomPanel(LobbySnapshot lobby)
    {
        if (_statusLabel != null)
        {
            if (lobby.CanStartGame)
                _statusLabel.text = "Waiting for Host to Start... (3/3 ready)";
            else
            {
                int readyCount = 0;
                foreach (var p in lobby.Players) if (p.IsReady) readyCount++;
                _statusLabel.text = $"Waiting for all players to ready... ({readyCount}/3 ready)";
            }
        }

        if (_startButton != null)
        {
            bool isMaster = lobby.IsMasterClient;
            _startButton.style.display = isMaster ? DisplayStyle.Flex : DisplayStyle.None;

            if (lobby.CanStartGame)
            {
                _startButton.AddToClassList(CSS_START_ENABLED);
                _startButton.SetEnabled(true);
            }
            else
            {
                _startButton.RemoveFromClassList(CSS_START_ENABLED);
                _startButton.SetEnabled(false);
            }
        }
    }

    private void OnStartClicked()
    {
        if (SinglePlayerSession.IsActive)
        {
            Debug.Log("[UIManager] Starting single-player game.");
            SinglePlayerSession.StartGame();
            return;
        }

        if (NetworkManager.Instance != null && NetworkManager.Instance.CanStartGame)
        {
            Debug.Log("[UIManager] Starting game via NetworkManager!");
            NetworkManager.Instance.StartGame();
        }
    }

    private void OnLeaveRoom()
    {
        if (SinglePlayerSession.IsActive)
        {
            SinglePlayerSession.Stop();
            ShowScreen(Screen.MainMenu);
            return;
        }

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.LeaveRoom();
        }
    }

    private void ShowSinglePlayerLobby()
    {
        _mapCatalog = Resources.Load<MultiplayerMapCatalog>("MultiplayerMapCatalog");
        _selectedMapIndex = 0;
        if (_mapCatalog != null)
        {
            for (int i = 0; i < _mapCatalog.Maps.Count; i++)
            {
                if (_mapCatalog.Maps[i].SceneName == SinglePlayerSession.SelectedSceneName)
                {
                    _selectedMapIndex = i;
                    break;
                }
            }
        }

        _selectedIndex[0] = (int)SinglePlayerSession.SelectedRole - 1;
        ShowScreen(Screen.Lobby);

        if (_roomCodeLabel != null) _roomCodeLabel.text = "LOCAL";
        if (_copyBtn != null) _copyBtn.style.display = DisplayStyle.None;
        Label roomTitle = _lobbyScreen.Q<Label>("RoomCodeTitle");
        if (roomTitle != null) roomTitle.text = "SINGLE PLAYER";
        Label modeLabel = _lobbyScreen.Q<Label>("ModeLabel");
        if (modeLabel != null) modeLabel.text = "MODE: SOLO";

        for (int i = 0; i < NUM_SLOTS; i++)
        {
            _slotRoots[i].style.display = i == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_readyBtns[i] != null)
                _readyBtns[i].style.display = DisplayStyle.None;

            Button leftArrow = _lobbyScreen.Q<Button>($"LeftArrow{i + 1}");
            Button rightArrow = _lobbyScreen.Q<Button>($"RightArrow{i + 1}");
            if (leftArrow != null)
                leftArrow.style.display = i == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (rightArrow != null)
                rightArrow.style.display = i == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        Label playerLabel = _lobbyScreen.Q<Label>("PlayerLabel1");
        if (playerLabel != null) playerLabel.text = "PLAYER 1";
        Label hostCrown = _lobbyScreen.Q<Label>("HostCrown1");
        if (hostCrown != null) hostCrown.style.display = DisplayStyle.None;
        Label pingLabel = _lobbyScreen.Q<Label>("PingLabel1");
        if (pingLabel != null) pingLabel.text = "LOCAL";

        if (_statusLabel != null) _statusLabel.text = "Choose a character and map";
        if (_startButton != null)
        {
            _startButton.style.display = DisplayStyle.Flex;
            _startButton.SetEnabled(true);
            _startButton.AddToClassList(CSS_START_ENABLED);
        }
        if (_backButton != null) _backButton.text = "MAIN MENU";

        RefreshSinglePlayerCharacter();
        RefreshSinglePlayerMap();
    }

    private void RestoreMultiplayerLobbyPresentation()
    {
        if (_copyBtn != null) _copyBtn.style.display = DisplayStyle.Flex;
        if (_previousMapButton != null) _previousMapButton.style.display = DisplayStyle.None;
        if (_nextMapButton != null) _nextMapButton.style.display = DisplayStyle.None;

        Label roomTitle = _lobbyScreen.Q<Label>("RoomCodeTitle");
        if (roomTitle != null) roomTitle.text = "ROOM CODE";
        Label modeLabel = _lobbyScreen.Q<Label>("ModeLabel");
        if (modeLabel != null) modeLabel.text = "MODE: NORMAL";

        for (int i = 0; i < NUM_SLOTS; i++)
        {
            _slotRoots[i].style.display = DisplayStyle.Flex;
            if (_readyBtns[i] != null)
                _readyBtns[i].style.display = DisplayStyle.Flex;
        }

        if (_backButton != null) _backButton.text = "LEAVE ROOM";
    }

    private void RefreshSinglePlayerCharacter()
    {
        if (characters.Count == 0)
            return;

        CharacterData data = characters[_selectedIndex[0]];
        if (_charImages[0] != null && data.portrait != null)
            _charImages[0].style.backgroundImage = new StyleBackground(data.portrait);
        if (_charNames[0] != null) _charNames[0].text = data.charName;
        if (_charTags[0] != null) _charTags[0].text = data.charTag;
    }

    private void CycleMap(int direction)
    {
        if (!SinglePlayerSession.IsActive || _mapCatalog == null || _mapCatalog.Maps.Count == 0)
            return;

        _selectedMapIndex =
            (_selectedMapIndex + direction + _mapCatalog.Maps.Count) % _mapCatalog.Maps.Count;
        SinglePlayerSession.SelectMap(_mapCatalog.Maps[_selectedMapIndex]);
        RefreshSinglePlayerMap();
    }

    private void RefreshSinglePlayerMap()
    {
        bool hasMaps = _mapCatalog != null && _mapCatalog.Maps.Count > 0;
        if (_previousMapButton != null) _previousMapButton.style.display = hasMaps ? DisplayStyle.Flex : DisplayStyle.None;
        if (_nextMapButton != null) _nextMapButton.style.display = hasMaps ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasMaps && _mapLabel != null)
            _mapLabel.text = $"MAP: {_mapCatalog.Maps[_selectedMapIndex].DisplayName.ToUpper()}";
    }
}
