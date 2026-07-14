using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

/// <summary>
/// Controls keyboard navigation on the Character Select screen.
/// Left / Right arrow keys (or A / D) cycle through available characters.
/// Z or Enter confirms the selection; X or Escape goes back.
/// Clicking a portrait also selects that character directly.
/// </summary>
public class CharSelectController : MonoBehaviour
{
    // ----------------------------------------------------------------
    // Inspector
    // ----------------------------------------------------------------
    [Header("Settings")]
    [Tooltip("Name of the USS class applied to the currently highlighted card.")]
    public string selectedClass = "card-selected";

    // ----------------------------------------------------------------
    // Private state
    // ----------------------------------------------------------------
    private UIDocument _uiDocument;

    /// <summary>All character slot VisualElements, ordered left to right.</summary>
    private VisualElement[] _charSlots;

    private int _currentIndex = 0;

    /// <summary>True only when OnEnable finished without early-returning.</summary>
    private bool _isInitialized = false;

    // Cached UI references
    private Button _selectButton;
    private Button _backButton;

    // ----------------------------------------------------------------
    // Unity lifecycle
    // ----------------------------------------------------------------
    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (_uiDocument == null) return;

        var root = _uiDocument.rootVisualElement;

        // ---- Gather character slots ----------------------------------------
        // First try the styled layout (CharSelectTest.uxml):
        //   CharacterSelectRow > VisualElements with class "character-card"
        var row = root.Q<VisualElement>("CharacterSelectRow");
        if (row != null)
        {
            var slots = row.Query<VisualElement>(className: "character-card").ToList();
            if (slots != null && slots.Count > 0)
                _charSlots = slots.ToArray();
        }

        // Fallback: simple layout (CharSelect.uxml) with named buttons
        if (_charSlots == null || _charSlots.Length == 0)
        {
            var list = new System.Collections.Generic.List<VisualElement>();
            foreach (var name in new[] { "Char1", "Char2", "Char3", "Char4" })
            {
                var btn = root.Q<Button>(name);
                if (btn != null) list.Add(btn);
            }
            _charSlots = list.Count > 0 ? list.ToArray() : null;
        }

        if (_charSlots == null || _charSlots.Length == 0)
        {
            Debug.LogWarning("[CharSelectController] No character slots found in the UI document!");
            return;
        }

        // ---- Bottom-panel buttons ------------------------------------------
        _selectButton = root.Q<Button>("SelectButton");
        _backButton   = root.Q<Button>("BackButton");

        if (_selectButton != null) _selectButton.clicked += OnSelectConfirmed;
        if (_backButton   != null) _backButton.clicked   += OnBackClicked;

        // ---- Click-to-select on each card / portrait button ----------------
        for (int i = 0; i < _charSlots.Length; i++)
        {
            int captured = i; // capture loop variable for the lambda

            // If the slot has a child Button (portrait), hook into that;
            // otherwise register a ClickEvent on the card element itself.
            var portraitBtn = _charSlots[i].Q<Button>();
            if (portraitBtn != null)
                portraitBtn.clicked += () => SelectCharacter(captured);
            else
                _charSlots[i].RegisterCallback<ClickEvent>(_ => SelectCharacter(captured));
        }

        // ---- Initial highlight ---------------------------------------------
        _currentIndex = 0;
        RefreshHighlight();

        // Give the root focus so UI Toolkit can receive KeyDownEvents
        root.focusable = true;
        root.Focus();
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);

        _isInitialized = true;
    }

    private void OnDisable()
    {
        // Only clean up what was actually set up
        if (!_isInitialized) return;
        _isInitialized = false;

        if (_uiDocument == null) return;

        var root = _uiDocument.rootVisualElement;
        if (root != null)
            root.UnregisterCallback<KeyDownEvent>(OnKeyDown);

        if (_selectButton != null) _selectButton.clicked -= OnSelectConfirmed;
        if (_backButton   != null) _backButton.clicked   -= OnBackClicked;
    }

    // ----------------------------------------------------------------
    // Keyboard input via UI Toolkit (fires when root has focus)
    // ----------------------------------------------------------------
    private void OnKeyDown(KeyDownEvent evt)
    {
        switch (evt.keyCode)
        {
            case KeyCode.LeftArrow:
            case KeyCode.A:
                MoveSelection(-1);
                evt.StopPropagation();
                break;

            case KeyCode.RightArrow:
            case KeyCode.D:
                MoveSelection(1);
                evt.StopPropagation();
                break;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            case KeyCode.Z:
                OnSelectConfirmed();
                evt.StopPropagation();
                break;

            case KeyCode.Escape:
            case KeyCode.X:
                OnBackClicked();
                evt.StopPropagation();
                break;
        }
    }

    // ----------------------------------------------------------------
    // Fallback input via the new Input System (works even when
    // the UI root loses keyboard focus).
    // ----------------------------------------------------------------
    private void Update()
    {
        if (_charSlots == null || _charSlots.Length == 0) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            MoveSelection(-1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            MoveSelection(1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)
            OnSelectConfirmed();
        else if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)
            OnBackClicked();
    }

    // ----------------------------------------------------------------
    // Navigation helpers
    // ----------------------------------------------------------------
    private void MoveSelection(int direction)
    {
        if (_charSlots == null || _charSlots.Length == 0) return;

        // Wrap around both ends
        _currentIndex = (_currentIndex + direction + _charSlots.Length) % _charSlots.Length;
        RefreshHighlight();
    }

    private void SelectCharacter(int index)
    {
        if (_charSlots == null || index < 0 || index >= _charSlots.Length) return;
        _currentIndex = index;
        RefreshHighlight();
    }

    /// <summary>
    /// Adds the selected CSS class to the active card and removes it from all others.
    /// </summary>
    private void RefreshHighlight()
    {
        for (int i = 0; i < _charSlots.Length; i++)
        {
            if (i == _currentIndex)
                _charSlots[i].AddToClassList(selectedClass);
            else
                _charSlots[i].RemoveFromClassList(selectedClass);
        }

        Debug.Log($"[CharSelectController] Highlighted slot index: {_currentIndex}");
    }

    // ----------------------------------------------------------------
    // Confirm / Back actions — hook your game logic here
    // ----------------------------------------------------------------
    private void OnSelectConfirmed()
    {
        if (_charSlots == null) return;
        Debug.Log($"[CharSelectController] Character confirmed — index {_currentIndex}");
        // TODO: persist selected character index (e.g. PlayerPrefs or a static data class)
        //       then load the next scene:
        // UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
    }

    private void OnBackClicked()
    {
        Debug.Log("[CharSelectController] Back — returning to main menu.");
        // TODO: load / activate the main menu scene or panel
        // UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
