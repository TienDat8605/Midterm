#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public sealed class DisplaySettingsTests
{
    [Test]
    public void Viewport_SixteenByNineUsesFullScreen()
    {
        AssertRect(AspectRatioPresentation.CalculateViewport(1920, 1080), 0f, 0f, 1f, 1f);
    }

    [Test]
    public void Viewport_UltrawideProducesCenteredPillarbox()
    {
        Rect viewport = AspectRatioPresentation.CalculateViewport(2560, 1080);
        Assert.That(viewport.width, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(viewport.x, Is.EqualTo(0.125f).Within(0.0001f));
        Assert.That(viewport.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(viewport.height, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void Viewport_FourByThreeProducesCenteredLetterbox()
    {
        Rect viewport = AspectRatioPresentation.CalculateViewport(1600, 1200);
        Assert.That(viewport.width, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(viewport.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(viewport.y, Is.EqualTo(0.125f).Within(0.0001f));
        Assert.That(viewport.height, Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void StartupPrecedence_IsOverrideThenSavedThenNativeDefault()
    {
        Assert.That(DisplaySettingsManager.ResolveStartupSource(false, true, true),
            Is.EqualTo(DisplayStartupSource.CommandLine));
        Assert.That(DisplaySettingsManager.ResolveStartupSource(false, false, true),
            Is.EqualTo(DisplayStartupSource.SavedPreference));
        Assert.That(DisplaySettingsManager.ResolveStartupSource(false, false, false),
            Is.EqualTo(DisplayStartupSource.NativeFullscreenDefault));
        Assert.That(DisplaySettingsManager.ResolveStartupSource(true, true, true),
            Is.EqualTo(DisplayStartupSource.WebEmbedded));
    }

    [Test]
    public void ScreenArguments_AreDetectedAsSessionOverrides()
    {
        Assert.That(DisplaySettingsManager.HasScreenCommandLineOverride(
            new[] { "DinoPark.exe", "-screen-width", "640", "-screen-height", "360",
                "-screen-fullscreen", "0" }), Is.True);
        Assert.That(DisplaySettingsManager.HasScreenCommandLineOverride(
            new[] { "DinoPark.exe", "-photonUserId", "local-1" }), Is.False);
    }

    [Test]
    public void WindowSize_InvalidFallsBackAndSavedSizeClampsToDisplay()
    {
        Assert.That(DisplaySettingsManager.ClampWindowSize(Vector2Int.zero, 1920, 1080),
            Is.EqualTo(new Vector2Int(1280, 720)));
        Assert.That(DisplaySettingsManager.ClampWindowSize(new Vector2Int(2560, 1440), 1920, 1080),
            Is.EqualTo(new Vector2Int(1920, 1080)));
        Assert.That(DisplaySettingsManager.ClampWindowSize(new Vector2Int(1024, 576), 1920, 1080),
            Is.EqualTo(new Vector2Int(1024, 576)));
    }

    [Test]
    public void DisplayModes_ExposeDesktopPresetsAndWebBrowserModes()
    {
        Assert.That(DisplaySettingsManager.GetAvailableModes(false), Is.EqualTo(new[]
        {
            GameDisplayMode.Window1280x720,
            GameDisplayMode.Window1600x900,
            GameDisplayMode.Window1920x1080,
            GameDisplayMode.Fullscreen
        }));
        Assert.That(DisplaySettingsManager.GetAvailableModes(true), Is.EqualTo(new[]
        {
            GameDisplayMode.WebEmbedded,
            GameDisplayMode.Fullscreen
        }));
    }

    [TestCase(GameDisplayMode.Window1280x720, 1280, 720)]
    [TestCase(GameDisplayMode.Window1600x900, 1600, 900)]
    [TestCase(GameDisplayMode.Window1920x1080, 1920, 1080)]
    public void DisplayModes_MapWindowPresetsToExpectedSizes(
        GameDisplayMode mode, int expectedWidth, int expectedHeight)
    {
        Assert.That(DisplaySettingsManager.TryGetWindowSize(mode, out Vector2Int size), Is.True);
        Assert.That(size, Is.EqualTo(new Vector2Int(expectedWidth, expectedHeight)));
    }

    [Test]
    public void DisplayModeResolution_DetectsPresetsFullscreenWebAndCustom()
    {
        Assert.That(DisplaySettingsManager.ResolveDisplayMode(false, false, 1600, 900),
            Is.EqualTo(GameDisplayMode.Window1600x900));
        Assert.That(DisplaySettingsManager.ResolveDisplayMode(false, true, 1920, 1080),
            Is.EqualTo(GameDisplayMode.Fullscreen));
        Assert.That(DisplaySettingsManager.ResolveDisplayMode(true, false, 1280, 720),
            Is.EqualTo(GameDisplayMode.WebEmbedded));
        Assert.That(DisplaySettingsManager.ResolveDisplayMode(false, false, 1400, 800),
            Is.EqualTo(GameDisplayMode.CustomWindow));
        Assert.That(DisplaySettingsManager.GetDisplayModeLabel(
            GameDisplayMode.CustomWindow, 1400, 800), Is.EqualTo("Custom 1400×800"));
    }

    private static void AssertRect(Rect actual, float x, float y, float width, float height)
    {
        Assert.That(actual.x, Is.EqualTo(x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(y).Within(0.0001f));
        Assert.That(actual.width, Is.EqualTo(width).Within(0.0001f));
        Assert.That(actual.height, Is.EqualTo(height).Within(0.0001f));
    }
}
#endif
