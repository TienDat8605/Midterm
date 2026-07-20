using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Shows the gameplay pause menu and pauses this client's simulation.
/// In multiplayer, pausing is intentionally local so it does not stop other players.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PauseMenuController : MonoBehaviour
{
    private UIDocument pauseDocument;
    private Button resumeButton;
    private Button mainMenuButton;
    private Button quitButton;
    private bool isPaused;

    private void Start()
    {
        pauseDocument = GetComponent<UIDocument>();

        VisualElement root = pauseDocument.rootVisualElement;
        resumeButton = root.Q<Button>("ResumeButton");
        mainMenuButton = root.Q<Button>("MainMenuButton");
        quitButton = root.Q<Button>("QuitButton");

        if (resumeButton != null) resumeButton.clicked += ResumeGame;
        if (mainMenuButton != null) mainMenuButton.clicked += ReturnToMainMenu;
        if (quitButton != null) quitButton.clicked += QuitGame;

        SetPaused(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            SetPaused(!isPaused);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;

        if (resumeButton != null) resumeButton.clicked -= ResumeGame;
        if (mainMenuButton != null) mainMenuButton.clicked -= ReturnToMainMenu;
        if (quitButton != null) quitButton.clicked -= QuitGame;
    }

    private void SetPaused(bool shouldPause)
    {
        isPaused = shouldPause;
        Time.timeScale = shouldPause ? 0f : 1f;

        if (pauseDocument != null)
            pauseDocument.enabled = shouldPause;
    }

    private void ResumeGame()
    {
        SetPaused(false);
    }

    private void ReturnToMainMenu()
    {
        SetPaused(false);
        SinglePlayerSession.Stop();

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();

        SceneManager.LoadScene("Main");
    }

    private void QuitGame()
    {
        SetPaused(false);
        Application.Quit();
    }
}
