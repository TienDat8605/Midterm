using System;
using UnityEngine;

public enum DisplayStartupSource
{
    CommandLine,
    SavedPreference,
    NativeFullscreenDefault,
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

    public static DisplaySettingsManager Instance { get; private set; }
    public bool IsFullscreen => Screen.fullScreen;
    public event Action<bool> FullscreenChanged;

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

        if (fullscreen != lastFullscreen)
        {
            lastFullscreen = fullscreen;
            SaveFullscreenPreference();
            FullscreenChanged?.Invoke(fullscreen);
        }

        lastScreenWidth = width;
        lastScreenHeight = height;
    }

    public void ToggleFullscreen()
    {
        if (Screen.fullScreen)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Screen.fullScreen = false;
#else
            Vector2Int size = ClampWindowSize(rememberedWindowSize,
                Display.main.systemWidth, Display.main.systemHeight);
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
#endif
        }
        else
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // This method is called directly by the UI click/key event, which supplies
            // the user gesture browsers require for a fullscreen request.
            Screen.fullScreen = true;
#else
            rememberedWindowSize = ClampWindowSize(new Vector2Int(Screen.width, Screen.height),
                Display.main.systemWidth, Display.main.systemHeight);
            SaveWindowSize();
            Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight,
                FullScreenMode.FullScreenWindow);
#endif
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
}
