using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI Toolkit fade-to-black transition for level changes.
/// The overlay is authored in Assets/Resources/SceneTransition.uxml.
/// </summary>
public sealed class SceneTransition : MonoBehaviour, IOnEventCallback
{
    private const string OverlayName = "SceneTransitionOverlay";
    private const string OverlayResourcePath = "SceneTransition";
    private const float FadeDuration = 0.45f;
    private const float TransitionSortingOrder = 10000f;
    private const byte PhotonFadeEventCode = 201;

    private static SceneTransition instance;

    private VisualTreeAsset overlayAsset;
    private UIDocument transitionDocument;
    private VisualElement overlay;
    private bool isTransitioning;
    private bool loadInitiatedLocally;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInstance()
    {
        if (instance != null)
            return;

        GameObject transitionObject = new GameObject(nameof(SceneTransition));
        instance = transitionObject.AddComponent<SceneTransition>();
        DontDestroyOnLoad(transitionObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        overlayAsset = Resources.Load<VisualTreeAsset>(OverlayResourcePath);
        SceneManager.sceneLoaded += OnSceneLoaded;
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PhotonNetwork.RemoveCallbackTarget(this);

        if (instance == this)
            instance = null;
    }

    public static void LoadScene(string sceneName)
    {
        if (instance == null)
            CreateInstance();

        instance.BeginTransition(() => SceneManager.LoadSceneAsync(sceneName));
    }

    public static void LoadPhotonLevel(string sceneName)
    {
        if (instance == null)
            CreateInstance();

        instance.BeginPhotonTransition(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isTransitioning)
        {
            EnsureOverlay();
            return;
        }

        if (!loadInitiatedLocally)
            StartCoroutine(FadeInAfterExternalSceneLoad());
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != PhotonFadeEventCode ||
            !PhotonNetwork.InRoom ||
            PhotonNetwork.IsMasterClient)
        {
            return;
        }

        BeginRemotePhotonFade();
    }

    private void BeginTransition(System.Func<AsyncOperation> loadScene)
    {
        if (isTransitioning)
            return;

        StartCoroutine(TransitionRoutine(loadScene));
    }

    private void BeginPhotonTransition(string sceneName)
    {
        if (isTransitioning)
            return;

        if (!PhotonNetwork.InRoom)
        {
            BeginTransition(() => SceneManager.LoadSceneAsync(sceneName));
            return;
        }

        PhotonNetwork.RaiseEvent(
            PhotonFadeEventCode,
            null,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable);

        BeginTransition(() =>
        {
            PhotonNetwork.LoadLevel(sceneName);
            return null;
        });
    }

    private void BeginRemotePhotonFade()
    {
        if (isTransitioning)
            return;

        StartCoroutine(FadeToBlackForPhotonSceneLoad());
    }

    private IEnumerator TransitionRoutine(System.Func<AsyncOperation> loadScene)
    {
        isTransitioning = true;

        EnsureOverlay();
        if (overlay == null)
        {
            isTransitioning = false;
            yield break;
        }

        yield return FadeTo(1f);

        loadInitiatedLocally = true;
        AsyncOperation operation = loadScene();
        if (operation != null)
            yield return operation;

        yield return null;
        EnsureOverlay();
        yield return FadeTo(0f);

        isTransitioning = false;
        loadInitiatedLocally = false;
    }

    private IEnumerator FadeToBlackForPhotonSceneLoad()
    {
        isTransitioning = true;
        EnsureOverlay();

        if (overlay == null)
        {
            isTransitioning = false;
            yield break;
        }

        yield return FadeTo(1f);
    }

    private IEnumerator FadeInAfterExternalSceneLoad()
    {
        yield return null;
        EnsureOverlay();

        if (overlay != null)
        {
            overlay.style.opacity = 1f;
            yield return FadeTo(0f);
        }

        isTransitioning = false;
    }

    private void EnsureOverlay()
    {
        if (overlayAsset == null)
        {
            Debug.LogWarning("[SceneTransition] Missing Resources/SceneTransition.uxml.");
            return;
        }

        if (transitionDocument == null)
        {
            UIDocument sourceDocument = null;
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (!document.isActiveAndEnabled || document.panelSettings == null)
                    continue;

                if (sourceDocument == null || document.sortingOrder > sourceDocument.sortingOrder)
                    sourceDocument = document;
            }

            if (sourceDocument == null)
            {
                overlay = null;
                Debug.LogWarning("[SceneTransition] No active UIDocument with PanelSettings was found.");
                return;
            }

            transitionDocument = gameObject.AddComponent<UIDocument>();
            transitionDocument.enabled = false;
            transitionDocument.panelSettings = sourceDocument.panelSettings;
            transitionDocument.visualTreeAsset = overlayAsset;
            transitionDocument.sortingOrder =
                Mathf.Max(TransitionSortingOrder, sourceDocument.sortingOrder + 1f);
            transitionDocument.enabled = true;
        }

        VisualElement root = transitionDocument.rootVisualElement;
        root.style.position = Position.Absolute;
        root.style.left = 0f;
        root.style.right = 0f;
        root.style.top = 0f;
        root.style.bottom = 0f;

        VisualElement resolvedOverlay = root.Q<VisualElement>(OverlayName);
        if (overlay == resolvedOverlay)
            return;

        overlay = resolvedOverlay;
        if (overlay != null)
            overlay.style.opacity = 0f;
    }

    private IEnumerator FadeTo(float targetOpacity)
    {
        if (overlay == null)
            yield break;

        float startOpacity = overlay.resolvedStyle.opacity;
        float elapsed = 0f;

        while (elapsed < FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlay.style.opacity = Mathf.Lerp(startOpacity, targetOpacity, elapsed / FadeDuration);
            yield return null;
        }

        overlay.style.opacity = targetOpacity;
    }
}
