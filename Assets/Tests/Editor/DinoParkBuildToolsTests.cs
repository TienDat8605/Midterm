#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Build;

public sealed class DinoParkBuildToolsTests
{
    [Test]
    public void SceneList_IsUiThenUniqueCatalogScenesOnly()
    {
        string[] scenes = DinoParkBuildTools.ResolveBuildScenes();
        Assert.That(scenes[0], Is.EqualTo(DinoParkBuildTools.UiScenePath));
        Assert.That(scenes, Does.Contain("Assets/Scenes/NewMap/Map1.unity"));
        Assert.That(scenes, Does.Contain("Assets/Scenes/MapScene.unity"));
        Assert.That(scenes.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(scenes.Length));
        Assert.That(scenes, Has.None.EqualTo("Assets/Scenes/SampleScene.unity"));
        Assert.That(scenes, Has.None.EqualTo("Assets/Scenes/NewMap/Map2.unity"));
    }

    [Test]
    public void CatalogValidation_RejectsMissingScene()
    {
        Assert.Throws<BuildFailedException>(() =>
            DinoParkBuildTools.ResolveUniqueScenePath("DefinitelyMissingDinoParkScene"));
    }

    [Test]
    public void IdentityValidation_AllowsTestBuildPlaceholders()
    {
        Assert.DoesNotThrow(() => DinoParkBuildTools.ValidateIdentity(true));
    }

    [Test]
    public void SafeDeletion_RejectsBuildRootAndOutsidePaths()
    {
        string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        Assert.That(DinoParkBuildTools.IsSafeBuildOutputDirectory(Path.Combine(root, "Builds")), Is.False);
        Assert.That(DinoParkBuildTools.IsSafeBuildOutputDirectory(Path.Combine(root, "Assets")), Is.False);
        Assert.That(DinoParkBuildTools.IsSafeBuildOutputDirectory(
            Path.Combine(root, "Builds", "Windows", "Test")), Is.True);
    }

    [Test]
    public void LauncherArguments_HaveUniqueIdsWindowSizeAndLogs()
    {
        string[] first = DinoParkBuildTools.CreateClientArguments("session", 1, "one.log");
        string[] second = DinoParkBuildTools.CreateClientArguments("session", 2, "two.log");
        Assert.That(first[1], Is.EqualTo("dinopark-local-session-1"));
        Assert.That(second[1], Is.EqualTo("dinopark-local-session-2"));
        Assert.That(first[1], Is.Not.EqualTo(second[1]));
        string joined = string.Join(" ", first);
        Assert.That(joined, Does.Contain("-screen-fullscreen 0"));
        Assert.That(joined, Does.Contain("-screen-width 640"));
        Assert.That(joined, Does.Contain("-screen-height 360"));
        Assert.That(first, Does.Contain("-logFile"));
    }

    [Test]
    public void WebZip_HasIndexAtArchiveRoot()
    {
        string project = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        string testRoot = Path.Combine(project, "Builds", "Tests", Guid.NewGuid().ToString("N"));
        string web = Path.Combine(testRoot, "Web");
        string zip = Path.Combine(testRoot, "DinoPark-Web.zip");
        try
        {
            Directory.CreateDirectory(Path.Combine(web, "Build"));
            File.WriteAllText(Path.Combine(web, "index.html"), "<html></html>");
            File.WriteAllText(Path.Combine(web, "Build", "game.data"), "data");
            DinoParkBuildTools.CreateWebZip(web, zip);
            using (ZipArchive archive = ZipFile.OpenRead(zip))
            {
                Assert.That(archive.GetEntry("index.html"), Is.Not.Null);
                Assert.That(archive.GetEntry("Web/index.html"), Is.Null);
                Assert.That(archive.GetEntry("Build/game.data"), Is.Not.Null);
            }
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }
    }
}
#endif
