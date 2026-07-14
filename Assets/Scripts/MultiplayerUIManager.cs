using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerUIManager : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("The main multiplayer panel container.")]
    public GameObject multiplayerPanel;

    [Header("Invite Code Section")]
    [Tooltip("The text component displaying the invite code.")]
    public TMP_Text codeText;
    [Tooltip("The input field for entering a join code.")]
    public TMP_InputField joinInputField;

    [Header("Config")]
    [Tooltip("Placeholder code for the initial implementation.")]
    public string placeholderCode = "3XLY62";

    void Start()
    {
        if (codeText != null)
        {
            codeText.text = placeholderCode;
        }
    }

    /// <summary>
    /// Enables the multiplayer UI.
    /// </summary>
    public void ShowMultiplayerPanel()
    {
        if (multiplayerPanel != null)
        {
            multiplayerPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Returns to the main menu.
    /// </summary>
    public void HideMultiplayerPanel()
    {
        if (multiplayerPanel != null)
        {
            multiplayerPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Copies the current invite code to the system clipboard.
    /// </summary>
    public void CopyCodeToClipboard()
    {
        if (codeText != null)
        {
            GUIUtility.systemCopyBuffer = codeText.text;
        }
    }

    /// <summary>
    /// Placeholder for Photon Fusion join logic.
    /// </summary>
    /// <param name="code">The room code to join.</param>
    public void JoinGameWithCode(string code)
    {
        // TODO: Integrate with Photon Fusion session joining
        Debug.Log("Joining game with code: " + code);
    }

    /// <summary>
    /// Triggered by the Join button in the UI.
    /// </summary>
    public void OnJoinClicked()
    {
        if (joinInputField != null && !string.IsNullOrEmpty(joinInputField.text))
        {
            JoinGameWithCode(joinInputField.text);
        }
    }

    /// <summary>
    /// Placeholder for Photon Fusion host logic.
    /// </summary>
    public void HostGame()
    {
        // TODO: Integrate with Photon Fusion session hosting
        Debug.Log("Hosting a new game session.");
    }
}
