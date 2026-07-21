using System;
using System.Collections.Generic;
using UnityEngine;

public enum DisplayStartupSource
{
    CommandLine,
    SavedPreference,
    NativeFullscreenDefault,
    WebEmbedded
}

public enum GameDisplayMode
{
    Window1280x720,
    Window1600x900,
    Window1920x1080,
    CustomWindow,
    Fullscreen,
    WebEmbedded
}

/// <summary>
/// Owns window/fullscreen state for the lifetime of the player.
/// GPU dynamic resolution is intentionally unrelated to this component.
/// </summary>
public sealed class DisplaySettingsManager : MonoBehaviour
{
    private const string FullscreenPreference = "Display.Fullscreen";
    private const string WindowWidthPreference = "Display.WindowWidth";
    private const string WindowHeightPreference = "Display.WindowHeight";
    private static readonly Vector2Int DefaultWindowSize = new Vector2Int(1280, 720);
    private static readonly GameDisplayMode[] DesktopModes =
    {
        GameDisplayMode.Window1280x720,
        GameDisplayMode.Window1600x900,
        GameDisplayMode.Window1920x1080,
        GameDisplayMode.Fullscreen
    };
    private static readonly GameDisplayMode[] WebModes =
    {
        GameDisplayMode.WebEmbedded,
        GameDisplayMode.Fullscreen
    };

    public static DisplaySettingsManager Instance { get; private set; }
    public bool IsFullscreen => Screen.fullScreen;
    public bool UsesWebDisplayModes => IsWebGLPlayer();
    public GameDisplayMode CurrentMode => ResolveDisplayMode(
        UsesWebDisplayModes, Screen.fullScreen, Screen.width, Screen.height);
    public IReadOnlyList<GameDisplayMode> AvailableModes =>
        GetAvailableModes(UsesWebDisplayModes);
    public event Action<bool> FullscreenChanged;
    public event Action<GameDisplayMode> DisplayModeChanged;

    private bool sessionOverride;
    private bool lastFullscreen;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private Vector2Int rememberedWindowSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null)
            return;

        GameObject managerObject = new GameObject(nameof(DisplaySettingsManager));
        DontDestroyOnLoad(managerObject);
        managerObject.AddComponent<DisplaySettingsManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        sessionOverride = HasScreenCommandLineOverride(Environment.GetCommandLineArgs());
        rememberedWindowSize = LoadWindowSize(Display.main.systemWidth, Display.main.systemHeight);

#if UNITY_WEBGL && !UNITY_EDITOR
        Screen.fullScreen = false;
#else
        if (!Application.isEditor && !sessionOverride)
            ApplyDesktopStartup();
#endif

        lastFullscreen = Screen.fullScreen;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void Update()
    {
        bool fullscreen = Screen.fullScreen;
        int width = Screen.width;
        int height = Screen.height;

        if (!fullscreen && width > 0 && height > 0 &&
            (width != lastScreenWidth || height != lastScreenHeight))
        {
            rememberedWindowSize = ClampWindowSize(new Vector2Int(width, height),
                Display.main.systemWidth, Display.main.systemHeight);
            SaveWindowSize();
        }

        bool fullscreenChanged = fullscreen != lastFullscreen;
        bool sizeChanged = width != lastScreenWidth || height != lastScreenHeight;
        if (fullscreenChanged)
        {
            lastFullscreen = fullscreen;
            SaveFullscreenPreference();
            FullscreenChanged?.Invoke(fullscreen);
        }

        if (fullscreenChanged || sizeChanged)
            DisplayModeChanged?.Invoke(ResolveDisplayMode(UsesWebDisplayModes, fullscreen, width, height));

        lastScreenWidth = width;
        lastScreenHeight = height;
    }

    public void ApplyDisplayMode(GameDisplayMode mode)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Called directly by the dropdown change event, which supplies the user
        // gesture browsers require for a fullscreen request.
        Screen.fullScreen = mode == GameDisplayMode.Fullscreen;
#else
        if (mode == GameDisplayMode.Fullscreen)
        {
            if (!Screen.fullScreen && Screen.width > 0 && Screen.height > 0)
            {
                rememberedWindowSize = ClampWindowSize(new Vector2Int(Screen.width, Screen.height),
                    Display.main.systemWidth, Display.main.systemHeight);
                SaveWindowSize();
            }

            Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight,
                FullScreenMode.FullScreenWindow);
            return;
        }

        Vector2Int requestedSize;
        if (!TryGetWindowSize(mode, out requestedSize))
            requestedSize = rememberedWindowSize;

        rememberedWindowSize = ClampWindowSize(requestedSize,
            Display.main.systemWidth, Display.main.systemHeight);
        SaveWindowSize();
        Screen.SetResolution(rememberedWindowSize.x, rememberedWindowSize.y, FullScreenMode.Windowed);
#endif
    }

    public void ToggleFullscreen()
    {
        if (Screen.fullScreen)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ApplyDisplayMode(GameDisplayMode.WebEmbedded);
#else
            ApplyDisplayMode(GameDisplayMode.CustomWindow);
#endif
        }
        else
        {
            ApplyDisplayMode(GameDisplayMode.Fullscreen);
        }
    }

    private void ApplyDesktopStartup()
    {
        bool fullscreen = !PlayerPrefs.HasKey(FullscreenPreference) ||
            PlayerPrefs.GetInt(FullscreenPreference, 1) != 0;
        if (fullscreen)
        {
            Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight,
                FullScreenMode.FullScreenWindow);
        }
        else
        {
            Screen.SetResolution(rememberedWindowSize.x, rememberedWindowSize.y, FullScreenMode.Windowed);
        }
    }

    private Vector2Int LoadWindowSize(int displayWidth, int displayHeight)
    {
        Vector2Int saved = new Vector2Int(
            PlayerPrefs.GetInt(WindowWidthPreference, DefaultWindowSize.x),
            PlayerPrefs.GetInt(WindowHeightPreference, DefaultWindowSize.y));
        return ClampWindowSize(saved, displayWidth, displayHeight);
    }

    private void SaveFullscreenPreference()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (!sessionOverride && !Application.isEditor)
        {
            PlayerPrefs.SetInt(FullscreenPreference, Screen.fullScreen ? 1 : 0);
            PlayerPrefs.Save();
        }
#endif
    }

    private void SaveWindowSize()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (!sessionOverride && !Application.isEditor)
        {
            PlayerPrefs.SetInt(WindowWidthPreference, rememberedWindowSize.x);
            PlayerPrefs.SetInt(WindowHeightPreference, rememberedWindowSize.y);
            PlayerPrefs.Save();
        }
#endif
    }

    public static bool HasScreenCommandLineOverride(string[] arguments)
    {
        if (arguments == null)
            return false;

        foreach (string argument in arguments)
        {
            if (!string.IsNullOrEmpty(argument) &&
                argument.StartsWith("-screen-", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static DisplayStartupSource ResolveStartupSource(
        bool isWebGL, bool hasCommandLineOverride, bool hasSavedDesktopPreference)
    {
        if (isWebGL)
            return DisplayStartupSource.WebEmbedded;
        if (hasCommandLineOverride)
            return DisplayStartupSource.CommandLine;
        return hasSavedDesktopPreference
            ? DisplayStartupSource.SavedPreference
            : DisplayStartupSource.NativeFullscreenDefault;
    }

    public static Vector2Int ClampWindowSize(Vector2Int requested, int displayWidth, int displayHeight)
    {
        int safeDisplayWidth = Mathf.Max(1, displayWidth);
        int safeDisplayHeight = Mathf.Max(1, displayHeight);
        Vector2Int candidate = requested.x > 0 && requested.y > 0 ? requested : DefaultWindowSize;
        return new Vector2Int(
            Mathf.Clamp(candidate.x, 1, safeDisplayWidth),
            Mathf.Clamp(candidate.y, 1, safeDisplayHeight));
    }

    public static GameDisplayMode[] GetAvailableModes(bool isWebGL)
    {
        GameDisplayMode[] source = isWebGL ? WebModes : DesktopModes;
        return (GameDisplayMode[])source.Clone();
    }

    public static bool TryGetWindowSize(GameDisplayMode mode, out Vector2Int size)
    {
        switch (mode)
        {
            case GameDisplayMode.Window1280x720:
                size = new Vector2Int(1280, 720);
                return true;
            case GameDisplayMode.Window1600x900:
                size = new Vector2Int(1600, 900);
                return true;
            case GameDisplayMode.Window1920x1080:
                size = new Vector2Int(1920, 1080);
                return true;
            default:
                size = default;
                return false;
        }
    }

    public static GameDisplayMode ResolveDisplayMode(
        bool isWebGL, bool isFullscreen, int width, int height)
    {
        if (isFullscreen)
            return GameDisplayMode.Fullscreen;
        if (isWebGL)
            return GameDisplayMode.WebEmbedded;

        Vector2Int currentSize = new Vector2Int(width, height);
        foreach (GameDisplayMode mode in DesktopModes)
        {
            if (TryGetWindowSize(mode, out Vector2Int presetSize) && presetSize == currentSize)
                return mode;
        }

        return GameDisplayMode.CustomWindow;
    }

    public static string GetDisplayModeLabel(GameDisplayMode mode, int width, int height)
    {
        if (TryGetWindowSize(mode, out Vector2Int size))
            return $"{size.x}×{size.y}";

        switch (mode)
        {
            case GameDisplayMode.Fullscreen:
                return "Fullscreen";
            case GameDisplayMode.WebEmbedded:
                return "Embedded";
            default:
                return $"Custom {Mathf.Max(1, width)}×{Mathf.Max(1, height)}";
        }
    }

    private static bool IsWebGLPlayer()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }
}
