using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuUIController : MonoBehaviour
{
    [Header("Managers")]
    [Tooltip("The Multiplayer UI Manager to delegate hosting/joining actions to.")]
    public MultiplayerUIManager multiplayerUIManager;

    private UIDocument uiDocument;
    private Button hostButton;
    private Button joinButton;
    private TextField codeInput;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (uiDocument == null) return;

        var root = uiDocument.rootVisualElement;

        // Query the elements from the UXML template
        hostButton = root.Q<Button>("HostButton");
        joinButton = root.Q<Button>("JoinButton");
        codeInput = root.Q<TextField>("CodeInput");

        // Bind callback events
        if (hostButton != null)
        {
            hostButton.clicked += OnHostClicked;
        }

        if (joinButton != null)
        {
            joinButton.clicked += OnJoinClicked;
        }
    }

    private void OnDisable()
    {
        // Unbind callback events to prevent memory leaks
        if (hostButton != null)
        {
            hostButton.clicked -= OnHostClicked;
        }

        if (joinButton != null)
        {
            joinButton.clicked -= OnJoinClicked;
        }
    }

    private void OnHostClicked()
    {
        Debug.Log("[MainMenuUIController] Host Button Clicked.");
        if (multiplayerUIManager != null)
        {
            multiplayerUIManager.HostGame();
            multiplayerUIManager.ShowMultiplayerPanel(); // Transition to character selection panel
            gameObject.SetActive(false);                 // Hide main menu
        }
        else
        {
            Debug.LogWarning("[MainMenuUIController] MultiplayerUIManager is not assigned!");
        }
    }

    private void OnJoinClicked()
    {
        string code = codeInput != null ? codeInput.value : string.Empty;
        Debug.Log($"[MainMenuUIController] Join Button Clicked with code: {code}");
        
        if (multiplayerUIManager != null)
        {
            if (!string.IsNullOrEmpty(code))
            {
                multiplayerUIManager.JoinGameWithCode(code);
                multiplayerUIManager.ShowMultiplayerPanel(); // Transition to character selection panel
                gameObject.SetActive(false);                 // Hide main menu
            }
            else
            {
                Debug.LogWarning("[MainMenuUIController] Room code is empty.");
            }
        }
        else
        {
            Debug.LogWarning("[MainMenuUIController] MultiplayerUIManager is not assigned!");
        }
    }
}
