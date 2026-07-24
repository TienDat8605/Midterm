using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Shows the live vertical progress of active players in scenes with configured bounds.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[RequireComponent(typeof(ProgressMinimapBounds))]
public sealed class ProgressMinimapController : MonoBehaviour
{
    private readonly Dictionary<int, PlayerMarker> markers = new Dictionary<int, PlayerMarker>();
    private readonly List<PlayerProgress> playerProgress = new List<PlayerProgress>();
    private readonly List<int> staleMarkerKeys = new List<int>();

    private ProgressMinimapBounds bounds;
    private UIDocument document;
    private VisualElement minimapRoot;
    private VisualElement mapNameOverlay;
    private VisualElement markerContainer;
    private bool isConfigured;
    private bool isUiInitialized;
    private bool isVisible;

    private void Awake()
    {
        bounds = GetComponent<ProgressMinimapBounds>();
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        TryInitializeUi();
    }

    private bool TryInitializeUi()
    {
        if (isUiInitialized)
            return isConfigured;

        if (bounds == null)
            bounds = GetComponent<ProgressMinimapBounds>();
        if (document == null)
            document = GetComponent<UIDocument>();
        if (bounds == null || document == null)
            return false;

        minimapRoot = document.rootVisualElement.Q<VisualElement>("ProgressMinimapRoot");
        mapNameOverlay = document.rootVisualElement.Q<VisualElement>("MapNameOverlay");
        markerContainer = document.rootVisualElement.Q<VisualElement>("PlayerMarkers");
        if (minimapRoot == null || markerContainer == null)
            return false;

        isConfigured = bounds.IsConfigured;
        SetMinimapVisible(isConfigured);
        isUiInitialized = true;
        return isConfigured;
    }

    private void LateUpdate()
    {
        if (!TryInitializeUi())
            return;

        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            SetMinimapVisible(!isVisible);

        if (!isVisible)
            return;

        playerProgress.Clear();
        if (PhotonNetwork.InRoom)
            CollectNetworkPlayerProgress();
        else
            CollectLocalPlayerProgress();

        playerProgress.Sort((left, right) => right.progress01.CompareTo(left.progress01));
        UpdateMarkers();
    }

    private void CollectLocalPlayerProgress()
    {
        PlayerControllerWithPhysics[] players =
            FindObjectsByType<PlayerControllerWithPhysics>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (PlayerControllerWithPhysics player in players)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;

            SpriteRenderer spriteRenderer = player.GetComponent<SpriteRenderer>();
            AddPlayerProgress(
                player.GetInstanceID(),
                player.transform.position.y,
                spriteRenderer != null ? spriteRenderer.sprite : null);
        }
    }

    private void CollectNetworkPlayerProgress()
    {
        PhotonView[] networkViews = FindObjectsByType<PhotonView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (PhotonView view in networkViews)
        {
            if (view == null || !view.gameObject.activeInHierarchy || view.ViewID == 0 ||
                view.GetComponent<PlayerControllerWithPhysics>() == null)
                continue;

            SpriteRenderer spriteRenderer = view.GetComponent<SpriteRenderer>();
            AddPlayerProgress(
                view.ViewID,
                view.transform.position.y,
                spriteRenderer != null ? spriteRenderer.sprite : null);
        }
    }

    private void AddPlayerProgress(int key, float worldHeight, Sprite sprite)
    {
        playerProgress.Add(new PlayerProgress(
            key,
            bounds.GetProgress01(worldHeight),
            sprite));
    }

    private void SetMinimapVisible(bool visible)
    {
        isVisible = visible;
        minimapRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (mapNameOverlay != null)
            mapNameOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void UpdateMarkers()
    {
        foreach (PlayerProgress progress in playerProgress)
        {
            PlayerMarker marker = GetOrCreateMarker(progress.key);
            marker.Update(progress);
        }

        staleMarkerKeys.Clear();
        foreach (KeyValuePair<int, PlayerMarker> pair in markers)
        {
            bool isActive = playerProgress.Exists(progress => progress.key == pair.Key);
            if (!isActive)
                staleMarkerKeys.Add(pair.Key);
        }

        foreach (int key in staleMarkerKeys)
        {
            markers[key].root.RemoveFromHierarchy();
            markers.Remove(key);
        }
    }

    private PlayerMarker GetOrCreateMarker(int key)
    {
        if (markers.TryGetValue(key, out PlayerMarker existing))
            return existing;

        VisualElement root = new VisualElement { name = $"PlayerMarker_{key}" };
        root.AddToClassList("progress-minimap-player-marker");
        root.pickingMode = PickingMode.Ignore;

        Image icon = new Image { name = "PlayerIcon" };
        icon.AddToClassList("progress-minimap-player-icon");
        icon.pickingMode = PickingMode.Ignore;

        Label percentage = new Label { name = "PlayerPercentage" };
        percentage.AddToClassList("progress-minimap-player-percentage");
        percentage.pickingMode = PickingMode.Ignore;

        root.Add(icon);
        root.Add(percentage);
        markerContainer.Add(root);

        PlayerMarker marker = new PlayerMarker(root, icon, percentage);
        markers.Add(key, marker);
        return marker;
    }

    private readonly struct PlayerProgress
    {
        public readonly int key;
        public readonly float progress01;
        public readonly Sprite sprite;

        public PlayerProgress(int key, float progress01, Sprite sprite)
        {
            this.key = key;
            this.progress01 = progress01;
            this.sprite = sprite;
        }
    }

    private sealed class PlayerMarker
    {
        public readonly VisualElement root;
        private readonly Image icon;
        private readonly Label percentage;

        public PlayerMarker(VisualElement root, Image icon, Label percentage)
        {
            this.root = root;
            this.icon = icon;
            this.percentage = percentage;
        }

        public void Update(PlayerProgress progress)
        {
            root.style.bottom = new Length(progress.progress01 * 100f, LengthUnit.Percent);
            root.style.left = 65f;
            icon.sprite = progress.sprite;
            percentage.text = $"{Mathf.RoundToInt(progress.progress01 * 100f)}%";
        }
    }
}
