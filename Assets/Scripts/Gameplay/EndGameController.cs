using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controls the end-of-level HUD and routes its actions to the next level or main menu.
/// Call <see cref="ShowEndGame"/> from a goal trigger when the level is completed.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public sealed class EndGameController : MonoBehaviour
{
    [Header("Scene routing")]
    [SerializeField] private string nextLevelSceneName;
    [SerializeField] private string mainMenuSceneName = "Main";

    private VisualElement endGameScreen;
    private VisualElement map1Actions;
    private VisualElement map2Actions;
    private Button nextLevelButton;
    private Button map1ExitButton;
    private Button map2ExitButton;
    private bool isShowing;

    private void Awake()
    {
        UIDocument document = GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;
        endGameScreen = root.Q<VisualElement>("EndGameScreen");
        map1Actions = root.Q<VisualElement>("Map1Actions");
        map2Actions = root.Q<VisualElement>("Map2Actions");
        nextLevelButton = root.Q<Button>("NextLevelButton");
        map1ExitButton = root.Q<Button>("Map1ExitButton");
        map2ExitButton = root.Q<Button>("Map2ExitButton");

        if (nextLevelButton != null) nextLevelButton.clicked += LoadNextLevel;
        if (map1ExitButton != null) map1ExitButton.clicked += ReturnToMainMenu;
        if (map2ExitButton != null) map2ExitButton.clicked += ReturnToMainMenu;

        if (endGameScreen != null)
            endGameScreen.style.display = DisplayStyle.None;
    }

    private void OnDestroy()
    {
        if (nextLevelButton != null) nextLevelButton.clicked -= LoadNextLevel;
        if (map1ExitButton != null) map1ExitButton.clicked -= ReturnToMainMenu;
        if (map2ExitButton != null) map2ExitButton.clicked -= ReturnToMainMenu;
    }

    public void ShowEndGame()
    {
        if (isShowing)
            return;

        isShowing = true;
        StopLocalPlayers();

        bool hasNextLevel = !string.IsNullOrWhiteSpace(nextLevelSceneName) &&
                            Application.CanStreamedLevelBeLoaded(nextLevelSceneName);
        if (map1Actions != null)
            map1Actions.style.display = hasNextLevel ? DisplayStyle.Flex : DisplayStyle.None;
        if (map2Actions != null)
            map2Actions.style.display = hasNextLevel ? DisplayStyle.None : DisplayStyle.Flex;

        // Photon scene changes must be issued by the master client.
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient && nextLevelButton != null)
            nextLevelButton.style.display = DisplayStyle.None;

        if (endGameScreen != null)
            endGameScreen.style.display = DisplayStyle.Flex;
    }

    public void LoadNextLevel()
    {
        if (string.IsNullOrWhiteSpace(nextLevelSceneName) ||
            !Application.CanStreamedLevelBeLoaded(nextLevelSceneName))
        {
            Debug.LogWarning("[EndGame] No enabled next-level scene is configured.");
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            PhotonNetwork.LoadLevel(nextLevelSceneName);
            return;
        }

        SceneManager.LoadScene(nextLevelSceneName);
    }

    public void ReturnToMainMenu()
    {
        SinglePlayerSession.Stop();

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogWarning($"[EndGame] Main menu scene '{mainMenuSceneName}' is not enabled in Build Settings.");
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private static void StopLocalPlayers()
    {
        PlayerControllerWithPhysics[] players =
            FindObjectsByType<PlayerControllerWithPhysics>(FindObjectsSortMode.None);
        foreach (PlayerControllerWithPhysics player in players)
        {
            PhotonView view = player.photonView;
            if (view == null || view.ViewID == 0 || view.IsMine)
            {
                player.SetInputEnabled(false);
                if (player.Rigidbody != null)
                    player.Rigidbody.linearVelocity = Vector2.zero;
            }
        }
    }
}
