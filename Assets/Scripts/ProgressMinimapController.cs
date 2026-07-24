using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Shows the live vertical progress of active players in Map1 and Map2.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[RequireComponent(typeof(ProgressMinimapBounds))]
public sealed class ProgressMinimapController : MonoBehaviour
{
    private const float OverlapThreshold = 0.055f;
    private static readonly float[] SharedLaneOffsets = { 0f, -24f, 24f };

    private readonly Dictionary<int, PlayerMarker> markers = new Dictionary<int, PlayerMarker>();
    private readonly List<PlayerProgress> playerProgress = new List<PlayerProgress>();
    private readonly List<int> staleMarkerKeys = new List<int>();

    private ProgressMinimapBounds bounds;
    private VisualElement minimapRoot;
    private VisualElement markerContainer;
    private bool isSupportedScene;

    private void Awake()
    {
        isSupportedScene = IsGameplayScene(SceneManager.GetActiveScene().name);
        bounds = GetComponent<ProgressMinimapBounds>();

        UIDocument document = GetComponent<UIDocument>();
        minimapRoot = document.rootVisualElement.Q<VisualElement>("ProgressMinimapRoot");
        markerContainer = document.rootVisualElement.Q<VisualElement>("PlayerMarkers");

        if (minimapRoot != null)
            minimapRoot.style.display = isSupportedScene && bounds != null && bounds.IsConfigured
                ? DisplayStyle.Flex
                : DisplayStyle.None;
    }

    private void LateUpdate()
    {
        if (!isSupportedScene || bounds == null || !bounds.IsConfigured || markerContainer == null)
            return;

        playerProgress.Clear();
        PlayerControllerWithPhysics[] players =
            FindObjectsByType<PlayerControllerWithPhysics>(FindObjectsSortMode.None);

        foreach (PlayerControllerWithPhysics player in players)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;

            SpriteRenderer spriteRenderer = player.GetComponent<SpriteRenderer>();
            playerProgress.Add(new PlayerProgress(
                GetPlayerKey(player),
                bounds.GetProgress01(player.transform.position.y),
                spriteRenderer != null ? spriteRenderer.sprite : null));
        }

        playerProgress.Sort((left, right) => right.progress01.CompareTo(left.progress01));
        UpdateMarkers();
    }

    private void UpdateMarkers()
    {
        for (int i = 0; i < playerProgress.Count;)
        {
            int groupEnd = i + 1;
            while (groupEnd < playerProgress.Count &&
                   Mathf.Abs(playerProgress[groupEnd].progress01 - playerProgress[i].progress01) <
                   OverlapThreshold)
            {
                groupEnd++;
            }

            for (int groupIndex = i; groupIndex < groupEnd; groupIndex++)
            {
                PlayerProgress progress = playerProgress[groupIndex];
                int sharedLaneIndex = groupIndex - i;
                float laneOffset = sharedLaneIndex < SharedLaneOffsets.Length
                    ? SharedLaneOffsets[sharedLaneIndex]
                    : 0f;

                PlayerMarker marker = GetOrCreateMarker(progress.key);
                marker.Update(progress, laneOffset);
            }

            i = groupEnd;
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

    private static int GetPlayerKey(PlayerControllerWithPhysics player)
    {
        return player.photonView != null && player.photonView.ViewID != 0
            ? player.photonView.ViewID
            : player.GetInstanceID();
    }

    private static bool IsGameplayScene(string sceneName)
    {
        return sceneName == "Map1" || sceneName == "Map2";
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

        public void Update(PlayerProgress progress, float laneOffset)
        {
            root.style.bottom = new Length(progress.progress01 * 100f, LengthUnit.Percent);
            root.style.left = 30f + laneOffset;
            icon.sprite = progress.sprite;
            percentage.text = $"{Mathf.RoundToInt(progress.progress01 * 100f)}%";
        }
    }
}
