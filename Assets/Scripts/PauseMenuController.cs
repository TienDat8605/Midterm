using System.Collections.Generic;
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
    private VisualElement pauseOverlay;
    private VisualElement tutorialScreen;
    private VisualElement tutorialContainer;
    private Button resumeButton;
    private Button tutorialsButton;
    private Button quitButton;
    private Button tutorialOverlayButton;
    private VisualElement tutorialControlsOverlay;
    private Button previousTutorialButton;
    private Button nextTutorialButton;
    private Button tutorialBackButton;
    private readonly VisualElement[] tutorialPanels = new VisualElement[3];
    private readonly bool[] viewedTutorialPages = new bool[3];
    private readonly Dictionary<PlayerControllerWithPhysics, bool> localInputStates =
        new Dictionary<PlayerControllerWithPhysics, bool>();
    private int currentTutorialPage;
    private bool isPaused;
    private bool isTutorialMap;
    private bool tutorialOpenedFromOverlay;
    private bool tutorialIntroLocked;

    private void Start()
    {
        pauseDocument = GetComponent<UIDocument>();

        VisualElement root = pauseDocument.rootVisualElement;
        pauseOverlay = root.Q<VisualElement>("PauseOverlay");
        tutorialScreen = root.Q<VisualElement>("TutorialScreen");
        resumeButton = root.Q<Button>("ResumeButton");
        tutorialsButton = root.Q<Button>("TutorialsButton");
        quitButton = root.Q<Button>("QuitButton");
        tutorialOverlayButton = root.Q<Button>("TutorialOverlayButton");
        tutorialControlsOverlay = root.Q<VisualElement>("TutorialControlsOverlay");
        isTutorialMap = SceneManager.GetActiveScene().name == SinglePlayerSession.TutorialSceneName;

        if (tutorialScreen != null)
        {
            tutorialContainer = tutorialScreen.Q<VisualElement>("InstructionsContainer");
            previousTutorialButton = tutorialScreen.Q<Button>("PrevPageBut");
            nextTutorialButton = tutorialScreen.Q<Button>("NextPageBut");
            tutorialBackButton = tutorialScreen.Q<Button>("GoodLuckBut");
            tutorialPanels[0] = tutorialScreen.Q<VisualElement>("InstructionPanel1");
            tutorialPanels[1] = tutorialScreen.Q<VisualElement>("InstructionPanel2");
            tutorialPanels[2] = tutorialScreen.Q<VisualElement>("InstructionPanel3");
        }

        if (resumeButton != null) resumeButton.clicked += ResumeGame;
        if (tutorialsButton != null) tutorialsButton.clicked += ShowTutorials;
        if (quitButton != null) quitButton.clicked += QuitGame;
        if (tutorialOverlayButton != null)
            tutorialOverlayButton.clicked += OpenTutorialFromOverlay;
        if (previousTutorialButton != null) previousTutorialButton.clicked += ShowPreviousTutorialPage;
        if (nextTutorialButton != null) nextTutorialButton.clicked += ShowNextTutorialPage;
        if (tutorialBackButton != null)
        {
            tutorialBackButton.text = "BACK";
            tutorialBackButton.clicked += ReturnToPauseMenu;
        }

        SetPaused(false);

        if (isTutorialMap)
            OpenTutorialFromOverlay(true);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            if (tutorialOpenedFromOverlay)
                CloseTutorialOverlay();
            else
                SetPaused(!isPaused);
        }

        if (isTutorialMap && Keyboard.current != null &&
            Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (tutorialOpenedFromOverlay)
                CloseTutorialOverlay();
            else if (!isPaused)
                OpenTutorialFromOverlay();
        }

        if (isPaused && PhotonNetwork.InRoom)
            DisableLocalMultiplayerInput();
    }

    private void OnDestroy()
    {
        RestoreLocalMultiplayerInput();
        Time.timeScale = 1f;

        if (resumeButton != null) resumeButton.clicked -= ResumeGame;
        if (tutorialsButton != null) tutorialsButton.clicked -= ShowTutorials;
        if (quitButton != null) quitButton.clicked -= QuitGame;
        if (tutorialOverlayButton != null)
            tutorialOverlayButton.clicked -= OpenTutorialFromOverlay;
        if (previousTutorialButton != null) previousTutorialButton.clicked -= ShowPreviousTutorialPage;
        if (nextTutorialButton != null) nextTutorialButton.clicked -= ShowNextTutorialPage;
        if (tutorialBackButton != null) tutorialBackButton.clicked -= ReturnToPauseMenu;
    }

    private void SetPaused(bool shouldPause)
    {
        isPaused = shouldPause;

        if (PhotonNetwork.InRoom)
        {
            Time.timeScale = 1f;
            if (shouldPause)
                DisableLocalMultiplayerInput();
            else
                RestoreLocalMultiplayerInput();
        }
        else
        {
            Time.timeScale = shouldPause ? 0f : 1f;
        }

        if (pauseOverlay != null)
            pauseOverlay.style.display = shouldPause ? DisplayStyle.Flex : DisplayStyle.None;
        if (tutorialScreen != null)
            tutorialScreen.style.display = DisplayStyle.None;

        UpdateTutorialOverlayButton();
    }

    private void DisableLocalMultiplayerInput()
    {
        PlayerControllerWithPhysics[] controllers =
            FindObjectsByType<PlayerControllerWithPhysics>(FindObjectsSortMode.None);

        foreach (PlayerControllerWithPhysics controller in controllers)
        {
            PhotonView view = controller.photonView;
            bool isLocallyOwned = view == null || view.ViewID == 0 || view.IsMine;
            if (!isLocallyOwned)
                continue;

            if (!localInputStates.ContainsKey(controller))
                localInputStates.Add(controller, controller.inputEnabled);

            controller.SetInputEnabled(false);
        }
    }

    private void RestoreLocalMultiplayerInput()
    {
        foreach (KeyValuePair<PlayerControllerWithPhysics, bool> entry in localInputStates)
        {
            if (entry.Key != null)
                entry.Key.SetInputEnabled(entry.Value);
        }

        localInputStates.Clear();
    }

    private void ResumeGame()
    {
        SetPaused(false);
    }

    private void ShowTutorials()
    {
        if (tutorialContainer != null)
            tutorialContainer.RemoveFromClassList("tutorial-gameplay-overlay");

        tutorialOpenedFromOverlay = false;
        tutorialIntroLocked = false;
        currentTutorialPage = 0;
        UpdateTutorialPage();

        if (tutorialBackButton != null)
        {
            tutorialBackButton.text = "BACK";
            tutorialBackButton.style.display = DisplayStyle.Flex;
        }

        if (pauseOverlay != null)
            pauseOverlay.style.display = DisplayStyle.None;
        if (tutorialScreen != null)
            tutorialScreen.style.display = DisplayStyle.Flex;
    }

    private void ShowPreviousTutorialPage()
    {
        currentTutorialPage =
            (currentTutorialPage - 1 + tutorialPanels.Length) % tutorialPanels.Length;
        MarkCurrentTutorialPageViewed();
        UpdateTutorialPage();
        UpdateTutorialCloseButton();
    }

    private void ShowNextTutorialPage()
    {
        currentTutorialPage = (currentTutorialPage + 1) % tutorialPanels.Length;
        MarkCurrentTutorialPageViewed();
        UpdateTutorialPage();
        UpdateTutorialCloseButton();
    }

    private void UpdateTutorialPage()
    {
        for (int i = 0; i < tutorialPanels.Length; i++)
        {
            if (tutorialPanels[i] != null)
            {
                tutorialPanels[i].style.display =
                    i == currentTutorialPage ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }

    private void ReturnToPauseMenu()
    {
        if (tutorialOpenedFromOverlay)
        {
            CloseTutorialOverlay();
            return;
        }

        if (tutorialScreen != null)
            tutorialScreen.style.display = DisplayStyle.None;
        if (pauseOverlay != null)
            pauseOverlay.style.display = DisplayStyle.Flex;
    }

    private void OpenTutorialFromOverlay()
    {
        OpenTutorialFromOverlay(false);
    }

    private void OpenTutorialFromOverlay(bool requireAllPages)
    {
        if (!isTutorialMap || isPaused)
            return;

        if (tutorialContainer != null)
            tutorialContainer.AddToClassList("tutorial-gameplay-overlay");

        tutorialOpenedFromOverlay = true;
        tutorialIntroLocked = requireAllPages;
        currentTutorialPage = 0;
        ClearViewedTutorialPages();
        MarkCurrentTutorialPageViewed();
        UpdateTutorialPage();
        SetPaused(true);

        if (pauseOverlay != null)
            pauseOverlay.style.display = DisplayStyle.None;
        if (tutorialScreen != null)
            tutorialScreen.style.display = DisplayStyle.Flex;
        UpdateTutorialCloseButton();

        UpdateTutorialOverlayButton();
    }

    private void CloseTutorialOverlay()
    {
        if (tutorialIntroLocked && !HasViewedAllTutorialPages())
            return;

        tutorialOpenedFromOverlay = false;
        tutorialIntroLocked = false;

        if (tutorialContainer != null)
            tutorialContainer.RemoveFromClassList("tutorial-gameplay-overlay");

        UpdateTutorialCloseButton();

        SetPaused(false);
    }

    private void ClearViewedTutorialPages()
    {
        for (int i = 0; i < viewedTutorialPages.Length; i++)
            viewedTutorialPages[i] = false;
    }

    private void MarkCurrentTutorialPageViewed()
    {
        if (currentTutorialPage >= 0 && currentTutorialPage < viewedTutorialPages.Length)
            viewedTutorialPages[currentTutorialPage] = true;
    }

    private bool HasViewedAllTutorialPages()
    {
        for (int i = 0; i < viewedTutorialPages.Length; i++)
        {
            if (!viewedTutorialPages[i])
                return false;
        }

        return true;
    }

    private void UpdateTutorialCloseButton()
    {
        if (tutorialBackButton == null)
            return;

        if (!tutorialOpenedFromOverlay)
        {
            tutorialBackButton.text = "BACK";
            tutorialBackButton.style.display = DisplayStyle.Flex;
            return;
        }

        bool canClose = !tutorialIntroLocked || HasViewedAllTutorialPages();
        tutorialBackButton.text = "CLOSE";
        tutorialBackButton.style.display = canClose ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void UpdateTutorialOverlayButton()
    {
        bool shouldShowTutorialOverlays =
            isTutorialMap && !isPaused && !tutorialOpenedFromOverlay;

        if (tutorialOverlayButton != null)
        {
            tutorialOverlayButton.style.display =
                shouldShowTutorialOverlays
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        if (tutorialControlsOverlay != null)
        {
            tutorialControlsOverlay.style.display =
                shouldShowTutorialOverlays ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void QuitGame()
    {
        SetPaused(false);
        SinglePlayerSession.Stop();

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();

        SceneManager.LoadScene("Main");
    }
}
