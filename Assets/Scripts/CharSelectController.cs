using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the 3-slot character selection screen.
/// 
/// Layout (per slot):
///   LeftArrow[N]  — cycles character left
///   RightArrow[N] — cycles character right
///   CharImage[N]  — displays the current character sprite
///   CharName[N]   — character name label
///   CharTag[N]    — character sub-tag (e.g. Vietnamese name)
///   CharDesc[N]   — character description label
///   ReadyBtn[N]   — toggle ready/unready for this slot
/// 
/// Bottom panel:
///   BackButton  — go back to main menu
///   StatusLabel — shows readiness message
///   StartButton — enabled only when all 3 slots are ready
/// </summary>
public class CharSelectController : MonoBehaviour
{
    // ----------------------------------------------------------------
    // Character data
    // ----------------------------------------------------------------
    [System.Serializable]
    public struct CharacterData
    {
        public string charName;
        public string charTag;
        public string description;
        public Sprite portrait;
    }

    [Header("Characters (assign 3 sprites in order: Anchor, Bounce, Sticky)")]
    public List<CharacterData> characters = new List<CharacterData>
    {
        new CharacterData { charName = "ANCHOR", charTag = "Slime Đá",  description = "Heavy and stable.\nBrace to anchor\nto surfaces." },
        new CharacterData { charName = "BOUNCE", charTag = "Slime Lò Xo", description = "High bounce.\nTrampoline to\nlaunch allies." },
        new CharacterData { charName = "STICKY", charTag = "Slime Dính", description = "Stick to walls.\nTether to pull\nor rescue allies." },
    };

    // ----------------------------------------------------------------
    // CSS class names (keep in sync with CharSelect.uss)
    // ----------------------------------------------------------------
    private const string CSS_SLOT_READY      = "slot-ready";
    private const string CSS_BTN_READY       = "ready-slot-button--active";
    private const string CSS_START_ENABLED   = "start-button--enabled";
    private const string CSS_IMAGE_HIGHLIGHT = "char-image-highlight";

    // ----------------------------------------------------------------
    // Per-slot runtime state
    // ----------------------------------------------------------------
    private const int NUM_SLOTS = 3;

    private int[]  _selectedCharIndex = new int[NUM_SLOTS]; // which character each slot shows
    private bool[] _isReady           = new bool[NUM_SLOTS];

    // Cached UI element references per slot
    private VisualElement[] _slotRoots;
    private VisualElement[]  _charImages;
    private Label[]          _charNames;
    private Label[]          _charTags;
    private Label[]          _charDescs;
    private Button[]         _readyBtns;

    // Bottom panel
    private Label  _statusLabel;
    private Button _startButton;
    private Button _backButton;

    // ----------------------------------------------------------------
    // Unity lifecycle
    // ----------------------------------------------------------------
    private UIDocument _uiDoc;
    private bool _initialized = false;

    private void Awake()
    {
        _uiDoc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (_uiDoc == null) return;

        var root = _uiDoc.rootVisualElement;

        // Initialise per-slot arrays
        _slotRoots  = new VisualElement[NUM_SLOTS];
        _charImages = new VisualElement[NUM_SLOTS];
        _charNames  = new Label[NUM_SLOTS];
        _charTags   = new Label[NUM_SLOTS];
        _charDescs  = new Label[NUM_SLOTS];
        _readyBtns  = new Button[NUM_SLOTS];

        for (int i = 0; i < NUM_SLOTS; i++)
        {
            int slotNumber = i + 1; // 1-based name suffix

            // Slot root
            _slotRoots[i] = root.Q<VisualElement>($"Slot{slotNumber}");

            // Left / Right arrows — capture i for the lambda
            int captured = i;
            var leftArrow  = root.Q<Button>($"LeftArrow{slotNumber}");
            var rightArrow = root.Q<Button>($"RightArrow{slotNumber}");
            if (leftArrow  != null) leftArrow.clicked  += () => CycleCharacter(captured, -1);
            if (rightArrow != null) rightArrow.clicked += () => CycleCharacter(captured, +1);

            // Portrait / labels
            _charImages[i] = root.Q<VisualElement>($"CharImage{slotNumber}");
            _charNames[i]  = root.Q<Label>($"CharName{slotNumber}");
            _charTags[i]   = root.Q<Label>($"CharTag{slotNumber}");
            _charDescs[i]  = root.Q<Label>($"CharDesc{slotNumber}");

            // Ready button
            _readyBtns[i] = root.Q<Button>($"ReadyBtn{slotNumber}");
            if (_readyBtns[i] != null)
                _readyBtns[i].clicked += () => ToggleReady(captured);

            // Start each slot on a different character so all 3 are visible by default
            _selectedCharIndex[i] = i % Mathf.Max(1, characters.Count);
            _isReady[i] = false;

            // Apply initial display
            RefreshSlotDisplay(i);
        }

        // Bottom panel
        _statusLabel = root.Q<Label>("StatusLabel");
        _startButton = root.Q<Button>("StartButton");
        _backButton  = root.Q<Button>("BackButton");

        if (_startButton != null) _startButton.clicked += OnStartClicked;
        if (_backButton  != null) _backButton.clicked  += OnBackClicked;

        RefreshBottomPanel();

        _initialized = true;
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        _initialized = false;

        if (_startButton != null) _startButton.clicked -= OnStartClicked;
        if (_backButton  != null) _backButton.clicked  -= OnBackClicked;
        // Arrow/ready button lambdas are captured closures; they disappear with the UI doc.
    }

    // ----------------------------------------------------------------
    // Character cycling
    // ----------------------------------------------------------------
    private void CycleCharacter(int slotIndex, int direction)
    {
        if (characters == null || characters.Count == 0) return;

        // Don't allow cycling while ready
        if (_isReady[slotIndex]) return;

        _selectedCharIndex[slotIndex] =
            (_selectedCharIndex[slotIndex] + direction + characters.Count) % characters.Count;

        RefreshSlotDisplay(slotIndex);
    }

    // ----------------------------------------------------------------
    // Ready toggle
    // ----------------------------------------------------------------
    private void ToggleReady(int slotIndex)
    {
        _isReady[slotIndex] = !_isReady[slotIndex];
        RefreshSlotDisplay(slotIndex);
        RefreshBottomPanel();
    }

    // ----------------------------------------------------------------
    // Display refresh helpers
    // ----------------------------------------------------------------

    /// <summary>Updates all visual elements in one slot to match current state.</summary>
    private void RefreshSlotDisplay(int slotIndex)
    {
        if (characters == null || characters.Count == 0) return;

        int charIdx   = _selectedCharIndex[slotIndex];
        bool ready    = _isReady[slotIndex];
        var  charData = characters[charIdx];

        // Portrait sprite
        if (_charImages[slotIndex] != null)
        {
            if (charData.portrait != null)
            {
                _charImages[slotIndex].style.backgroundImage =
                    new StyleBackground(charData.portrait);
            }

            if (ready)
                _charImages[slotIndex].AddToClassList(CSS_IMAGE_HIGHLIGHT);
            else
                _charImages[slotIndex].RemoveFromClassList(CSS_IMAGE_HIGHLIGHT);
        }

        // Labels
        if (_charNames[slotIndex] != null)
            _charNames[slotIndex].text = charData.charName;

        if (_charTags[slotIndex] != null)
            _charTags[slotIndex].text = charData.charTag;

        if (_charDescs[slotIndex] != null)
            _charDescs[slotIndex].text = charData.description;

        // Slot root class
        if (_slotRoots[slotIndex] != null)
        {
            if (ready)
                _slotRoots[slotIndex].AddToClassList(CSS_SLOT_READY);
            else
                _slotRoots[slotIndex].RemoveFromClassList(CSS_SLOT_READY);
        }

        // Ready button text + class
        if (_readyBtns[slotIndex] != null)
        {
            _readyBtns[slotIndex].text = ready ? "UNREADY" : "READY";
            if (ready)
                _readyBtns[slotIndex].AddToClassList(CSS_BTN_READY);
            else
                _readyBtns[slotIndex].RemoveFromClassList(CSS_BTN_READY);
        }
    }

    /// <summary>Updates the status label and START button based on overall ready state.</summary>
    private void RefreshBottomPanel()
    {
        int readyCount = 0;
        foreach (var r in _isReady) if (r) readyCount++;

        bool allReady = (readyCount == NUM_SLOTS);
        bool allDifferent = false;

        if (allReady)
        {
            var uniqueSelections = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < NUM_SLOTS; i++)
            {
                uniqueSelections.Add(_selectedCharIndex[i]);
            }
            allDifferent = (uniqueSelections.Count == NUM_SLOTS);
        }

        bool canStart = allReady && allDifferent;

        if (_statusLabel != null)
        {
            if (canStart)
            {
                _statusLabel.text = "All players ready! Press START.";
            }
            else if (allReady && !allDifferent)
            {
                _statusLabel.text = "All 3 players must choose DIFFERENT slimes!";
            }
            else
            {
                _statusLabel.text = $"All 3 different roles are required to Start.  ({readyCount}/3 ready)";
            }
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

    // ----------------------------------------------------------------
    // Public accessors (for other systems)
    // ----------------------------------------------------------------

    /// <summary>Returns the selected CharacterData for a given slot (0-based).</summary>
    public CharacterData GetSelectionForSlot(int slotIndex)
    {
        if (characters == null || slotIndex < 0 || slotIndex >= NUM_SLOTS)
            return default;
        return characters[_selectedCharIndex[slotIndex]];
    }

    // ----------------------------------------------------------------
    // Start / Back handlers
    // ----------------------------------------------------------------
    private void OnStartClicked()
    {
        // Verify all slots ready (belt-and-suspenders)
        foreach (var r in _isReady)
            if (!r) return;

        Debug.Log("[CharSelectController] All players ready — starting game!");
        for (int i = 0; i < NUM_SLOTS; i++)
            Debug.Log($"  Slot {i + 1}: {GetSelectionForSlot(i).charName}");

        // TODO: persist selections then load the game scene, e.g.:
        // UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
    }

    private void OnBackClicked()
    {
        Debug.Log("[CharSelectController] Back — returning to main menu.");
        // TODO: UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
