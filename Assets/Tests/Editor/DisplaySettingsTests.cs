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

    private static void AssertRect(Rect actual, float x, float y, float width, float height)
    {
        Assert.That(actual.x, Is.EqualTo(x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(y).Within(0.0001f));
        Assert.That(actual.width, Is.EqualTo(width).Within(0.0001f));
        Assert.That(actual.height, Is.EqualTo(height).Within(0.0001f));
    }
}
#endif
