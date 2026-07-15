using System.Collections.Generic;
using UnityEngine;
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
    private Button    _joinButton;
    private TextField _codeInput;

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
    

    private Label           _statusLabel;
    private Button          _startButton;
    private Button          _backButton;

    // ================================================================
    // Unity lifecycle
    // ================================================================
    private void Awake()
    {
        _uiDoc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (_uiDoc == null) return;
        var root = _uiDoc.rootVisualElement;

        // ---- Locate screen roots ----
        _mainMenuScreen = root.Q<VisualElement>("MainMenuScreen");
        _lobbyScreen    = root.Q<VisualElement>("LobbyScreen"); // from RootUI.uxml

        SetupMainMenu();
        SetupLobby();

        ShowScreen(Screen.MainMenu);
    }

    private void OnDisable()
    {
        if (_hostButton != null) _hostButton.clicked -= OnHostClicked;
        if (_joinButton != null) _joinButton.clicked -= OnJoinClicked;
        if (_startButton != null) _startButton.clicked -= OnStartClicked;
        if (_backButton  != null) _backButton.clicked  -= OnLeaveRoom;
        if (_copyBtn != null) _copyBtn.clicked -= OnCopyCodeClicked;

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
        _joinButton = _mainMenuScreen.Q<Button>("JoinBut");
        _codeInput  = _mainMenuScreen.Q<TextField>();

        if (_hostButton != null) _hostButton.clicked += OnHostClicked;
        if (_joinButton != null) _joinButton.clicked += OnJoinClicked;
    }

    private void OnHostClicked()
    {
        Debug.Log("[UIManager] Host Game clicked.");
        if (_roomCodeLabel != null) _roomCodeLabel.text = placeholderCode; // host creates room
        ShowScreen(Screen.Lobby);
    }

    private void OnJoinClicked()
    {
        string code = _codeInput != null ? _codeInput.value : string.Empty;
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[UIManager] Room code is empty.");
            return;
        }
        if (_roomCodeLabel != null) _roomCodeLabel.text = code.ToUpper(); // join room
        ShowScreen(Screen.Lobby);
    }

    // ================================================================
    // LOBBY setup
    // ================================================================
    private void SetupLobby()
    {
        if (_lobbyScreen == null) return;

        _roomCodeLabel = _lobbyScreen.Q<Label>("RoomCodeLabel");
        _copyBtn = _lobbyScreen.Q<Button>("CopyBtn");
        if (_copyBtn != null) _copyBtn.clicked += OnCopyCodeClicked;

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

            // Stagger default selections so all three are different
            _selectedIndex[i] = i % Mathf.Max(1, characters.Count);
            _isReady[i]       = false;

            RefreshSlotDisplay(i);
        }

        _statusLabel = _lobbyScreen.Q<Label>("StatusLabel");
        _startButton = _lobbyScreen.Q<Button>("StartButton");
        _backButton  = _lobbyScreen.Q<Button>("BackButton");

        if (_startButton != null) _startButton.clicked += OnStartClicked;
        if (_backButton  != null) _backButton.clicked  += OnLeaveRoom;



        RefreshBottomPanel();
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
        if (_isReady[slot]) return;
        if (characters == null || characters.Count == 0) return;
        _selectedIndex[slot] = (_selectedIndex[slot] + dir + characters.Count) % characters.Count;
        RefreshSlotDisplay(slot);
        RefreshBottomPanel();
    }

    private void ToggleReady(int slot)
    {
        _isReady[slot] = !_isReady[slot];
        RefreshSlotDisplay(slot);
        RefreshBottomPanel();
    }

    private void RefreshSlotDisplay(int slot)
    {
        if (characters == null || characters.Count == 0) return;

        var data  = characters[_selectedIndex[slot]];
        bool ready = _isReady[slot];

        // Portrait
        if (_charImages[slot] != null && data.portrait != null)
            _charImages[slot].style.backgroundImage = new StyleBackground(data.portrait);

        // Labels
        if (_charNames[slot] != null) _charNames[slot].text = data.charName;
        if (_charTags[slot]  != null) _charTags[slot].text  = data.charTag;

        // Slot styling
        if (_slotRoots[slot] != null)
        {
            if (ready) _slotRoots[slot].AddToClassList(CSS_SLOT_READY);
            else       _slotRoots[slot].RemoveFromClassList(CSS_SLOT_READY);
        }

        // Ready button
        if (_readyBtns[slot] != null)
        {
            _readyBtns[slot].text = ready ? "UNREADY" : "READY";
            if (ready) _readyBtns[slot].AddToClassList(CSS_BTN_READY);
            else       _readyBtns[slot].RemoveFromClassList(CSS_BTN_READY);
        }
    }

    private void RefreshBottomPanel()
    {
        int readyCount = 0;
        foreach (var r in _isReady) if (r) readyCount++;

        bool allReady = (readyCount == NUM_SLOTS);
        bool allDifferent = false;

        if (allReady)
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < NUM_SLOTS; i++) seen.Add(_selectedIndex[i]);
            allDifferent = (seen.Count == NUM_SLOTS);
        }

        bool canStart = allReady && allDifferent;

        if (_statusLabel != null)
        {
            if (canStart)
                _statusLabel.text = "Waiting for Host to Start... (3/3 ready)"; // Example text
            else if (allReady && !allDifferent)
                _statusLabel.text = "All 3 players must choose DIFFERENT slimes!";
            else
                _statusLabel.text = $"Waiting for all players to ready... ({readyCount}/3 ready)";
        }

        if (_startButton != null)
        {
            if (canStart)
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
        foreach (var r in _isReady) if (!r) return;
        Debug.Log("[UIManager] All players ready — starting game!");
        // TODO: SceneManager.LoadScene("GameScene");
    }

    private void OnLeaveRoom()
    {
        for (int i = 0; i < NUM_SLOTS; i++)
        {
            _isReady[i] = false;
            RefreshSlotDisplay(i);
        }
        RefreshBottomPanel();
        ShowScreen(Screen.MainMenu);
    }
}
