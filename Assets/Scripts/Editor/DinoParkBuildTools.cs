using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class DinoParkBuildTools
{
    public const string UiScenePath = "Assets/Scenes/UI.unity";
    public const string CatalogPath = "Assets/Resources/MultiplayerMapCatalog.asset";
    private const string PendingKey = "DinoParkBuildTools.Pending";
    private const string TestExe = "Builds/Windows/Test/DinoPark.exe";
    private const string WinExe = "Builds/Windows/Release/DinoPark.exe";
    private const string MacApp = "Builds/macOS/Release/DinoPark.app";
    private const string WebDir = "Builds/Web/Release";
    private const string WebZip = "Builds/Web/DinoPark-Web.zip";
    private enum Request { WindowsRelease, MacOSRelease, WebRelease, WindowsTest }

    [MenuItem("Tools/DINO PARK/Build/Windows Release")]
    public static void BuildWindowsRelease() => Start(Request.WindowsRelease);
    [MenuItem("Tools/DINO PARK/Build/macOS Release")]
    public static void BuildMacOSRelease() => Start(Request.MacOSRelease);
    [MenuItem("Tools/DINO PARK/Build/Web Release (itch.io ZIP)")]
    public static void BuildWebRelease() => Start(Request.WebRelease);
    [MenuItem("Tools/DINO PARK/Test/Build Windows + Launch 3 Clients")]
    public static void BuildWindowsTestAndLaunch() => Start(Request.WindowsTest);

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        string pending = SessionState.GetString(PendingKey, string.Empty);
        if (string.IsNullOrEmpty(pending)) return;
        SessionState.EraseString(PendingKey);
        if (Enum.TryParse(pending, out Request request))
            EditorApplication.delayCall += () => Start(request);
    }

    private static void Start(Request request)
    {
        try
        {
            BuildTarget target = Target(request);
            BuildTargetGroup group = Group(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new BuildFailedException($"Build Support for {target} is not installed.");
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                if (Application.isBatchMode)
                    throw new BuildFailedException($"Use -buildTarget {CommandTarget(target)} for {request}.");
                SessionState.SetString(PendingKey, request.ToString());
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                {
                    SessionState.EraseString(PendingKey);
                    throw new BuildFailedException($"Unity could not switch to {target}.");
                }
                Debug.Log($"Switching to {target}; the build will resume after scripts reload.");
                return;
            }
            Execute(request, target);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) throw;
            EditorUtility.DisplayDialog("DINO PARK Build Failed", exception.Message, "OK");
        }
    }

    private static void Execute(Request request, BuildTarget target)
    {
        bool test = request == Request.WindowsTest;
        string[] scenes = ResolveBuildScenes();
        ValidatePhoton();
        ValidateIdentity(test);
        if (test) { ValidateTestSettings(); HandleRunningClients(); }
        EditorBuildSettings.scenes = scenes.Select(x => new EditorBuildSettingsScene(x, true)).ToArray();

        string output = Absolute(Output(request));
        string directory = request == Request.WebRelease ? output : Path.GetDirectoryName(output);
        if (request == Request.WebRelease) DeleteFile(Absolute(WebZip));
        DeleteBuildOutputDirectory(directory);
        Directory.CreateDirectory(directory);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = output, target = target, targetGroup = Group(target),
            options = test ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None
        };
        BuildReport report = request == Request.WebRelease ? BuildWeb(options) :
            request == Request.MacOSRelease ? BuildMac(options) : BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"DINO PARK build {summary.result}: {Bytes(summary.totalSize)}, duration {summary.totalTime}, " +
            $"warnings {summary.totalWarnings}, errors {summary.totalErrors}, output {output}");
        if (summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"DINO PARK {request} failed with result {summary.result}.");
        string artifact = output;
        if (request == Request.WebRelease) { artifact = Absolute(WebZip); CreateWebZip(output, artifact); }
        Debug.Log($"DINO PARK {request} ready: {artifact}");
        if (request == Request.MacOSRelease && Application.platform != RuntimePlatform.OSXEditor)
            Debug.LogWarning("Correct executable permissions on a Mac before testing. Signing and notarization are still required.");
        if (test) LaunchClients(output);
    }

    public static string[] ResolveBuildScenes()
    {
        if (!File.Exists(Absolute(UiScenePath))) throw new BuildFailedException($"Missing scene: {UiScenePath}");
        MultiplayerMapCatalog catalog = AssetDatabase.LoadAssetAtPath<MultiplayerMapCatalog>(CatalogPath);
        if (catalog == null) throw new BuildFailedException($"Missing catalog: {CatalogPath}");
        if (!catalog.IsValid(out string error)) throw new BuildFailedException(error);
        List<string> scenes = new List<string> { UiScenePath };
        HashSet<string> unique = new HashSet<string>(scenes, StringComparer.OrdinalIgnoreCase);
        foreach (MultiplayerMapEntry map in catalog.Maps)
        {
            string path = ResolveUniqueScenePath(map.SceneName);
            if (unique.Add(path)) scenes.Add(path);
        }
        return scenes.ToArray();
    }

    public static string ResolveUniqueScenePath(string name)
    {
        string[] paths = AssetDatabase.FindAssets($"{name} t:Scene").Select(AssetDatabase.GUIDToAssetPath)
            .Where(x => string.Equals(Path.GetFileNameWithoutExtension(x), name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length != 1)
            throw new BuildFailedException(paths.Length == 0 ? $"Catalog scene '{name}' is missing." :
                $"Catalog scene '{name}' is ambiguous: {string.Join(", ", paths)}");
        return paths[0];
    }

    public static void ValidateIdentity(bool allowPlaceholders)
    {
        if (allowPlaceholders) return;
        List<string> errors = new List<string>();
        if (PlayerSettings.productName != "DINO PARK") errors.Add("Product Name must be 'DINO PARK'.");
        if (string.IsNullOrWhiteSpace(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
            errors.Add("Company Name must be changed from DefaultCompany.");
        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
        if (string.IsNullOrWhiteSpace(id) || id == "com.DefaultCompany.2D-URP")
            errors.Add("Standalone identifier must be changed from com.DefaultCompany.2D-URP.");
        if (errors.Count > 0)
            throw new BuildFailedException("Release identity failed:\n- " + string.Join("\n- ", errors));
    }

    public static bool IsSafeBuildOutputDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string root = Trailing(Absolute("Builds"));
        string candidate = Trailing(Path.GetFullPath(path));
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase);
    }

    public static void DeleteBuildOutputDirectory(string path)
    {
        if (!IsSafeBuildOutputDirectory(path)) throw new BuildFailedException($"Unsafe deletion refused: {path}");
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    public static string[] CreateClientArguments(string session, int number, string log)
    {
        if (string.IsNullOrWhiteSpace(session)) throw new ArgumentException(nameof(session));
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        return new[] { "-photonUserId", $"dinopark-local-{session}-{number}", "-screen-fullscreen", "0",
            "-screen-width", "640", "-screen-height", "360", "-logFile", Path.GetFullPath(log) };
    }

    public static void CreateWebZip(string webDirectory, string zipPath)
    {
        if (!File.Exists(Path.Combine(webDirectory, "index.html")))
            throw new BuildFailedException("Web build has no root index.html.");
        DeleteFile(zipPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath)));
        using (FileStream stream = new FileStream(zipPath, FileMode.CreateNew))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            foreach (string file in Directory.GetFiles(webDirectory, "*", SearchOption.AllDirectories))
                archive.CreateEntryFromFile(file, Relative(webDirectory, file).Replace('\\', '/'),
                    System.IO.Compression.CompressionLevel.Optimal);
    }

    private static BuildReport BuildWeb(BuildPlayerOptions options)
    {
        WebGLCompressionFormat compression = PlayerSettings.WebGL.compressionFormat;
        bool caching = PlayerSettings.WebGL.dataCaching;
        bool fallback = PlayerSettings.WebGL.decompressionFallback;
        try
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.decompressionFallback = false;
            return BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.dataCaching = caching;
            PlayerSettings.WebGL.decompressionFallback = fallback;
        }
    }

    private static BuildReport BuildMac(BuildPlayerOptions options)
    {
        int architecture = PlayerSettings.GetArchitecture(NamedBuildTarget.Standalone);
        try { PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 2); return BuildPipeline.BuildPlayer(options); }
        finally { PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, architecture); }
    }

    private static void ValidatePhoton()
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(
            "Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset");
        SerializedProperty id = asset == null ? null : new SerializedObject(asset).FindProperty("AppSettings")?
            .FindPropertyRelative("AppIdRealtime");
        if (id == null || string.IsNullOrWhiteSpace(id.stringValue))
            throw new BuildFailedException("Photon Realtime App ID is missing.");
    }

    private static void ValidateTestSettings()
    {
        if (!PlayerSettings.runInBackground || PlayerSettings.forceSingleInstance)
            throw new BuildFailedException("Enable Run In Background and disable Force Single Instance.");
    }

    private static void HandleRunningClients()
    {
        List<Process> clients = ProcessesFor(Absolute(TestExe));
        if (clients.Count == 0) return;
        if (Application.isBatchMode) throw new BuildFailedException("Windows test clients are running.");
        if (!EditorUtility.DisplayDialog("DINO PARK Clients Running", $"Close {clients.Count} clients?",
            "Close and Build", "Cancel"))
            throw new OperationCanceledException("Build cancelled; output unchanged.");
        foreach (Process client in clients) { client.Kill(); client.WaitForExit(5000); client.Dispose(); }
    }

    private static List<Process> ProcessesFor(string executable)
    {
        List<Process> result = new List<Process>();
        foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
        {
            try
            {
                if (string.Equals(Path.GetFullPath(process.MainModule.FileName), executable,
                    StringComparison.OrdinalIgnoreCase)) result.Add(process);
                else process.Dispose();
            }
            catch { process.Dispose(); }
        }
        return result;
    }

    private static void LaunchClients(string executable)
    {
        string session = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" +
            Guid.NewGuid().ToString("N").Substring(0, 8);
        string logs = Path.Combine(Path.GetDirectoryName(executable), "ClientLogs");
        Directory.CreateDirectory(logs);
        for (int number = 1; number <= 3; number++)
        {
            string[] args = CreateClientArguments(session, number, Path.Combine(logs, $"Client-{number}.log"));
            Process.Start(new ProcessStartInfo { FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable),
                Arguments = string.Join(" ", args.Select(Quote)), UseShellExecute = false });
        }
    }

    private static BuildTarget Target(Request r) => r == Request.MacOSRelease ? BuildTarget.StandaloneOSX :
        r == Request.WebRelease ? BuildTarget.WebGL : BuildTarget.StandaloneWindows64;
    private static BuildTargetGroup Group(BuildTarget t) =>
        t == BuildTarget.WebGL ? BuildTargetGroup.WebGL : BuildTargetGroup.Standalone;
    private static string Output(Request r) => r == Request.WindowsRelease ? WinExe :
        r == Request.MacOSRelease ? MacApp : r == Request.WebRelease ? WebDir : TestExe;
    private static string CommandTarget(BuildTarget t) => t == BuildTarget.WebGL ? "WebGL" :
        t == BuildTarget.StandaloneOSX ? "StandaloneOSX" : "Win64";
    private static string Absolute(string path) => Path.GetFullPath(Path.Combine(
        Directory.GetParent(Application.dataPath).FullName, path));
    private static string Trailing(string path) => path.TrimEnd(
        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    private static string Relative(string root, string file) => Uri.UnescapeDataString(
        new Uri(Trailing(Path.GetFullPath(root))).MakeRelativeUri(new Uri(Path.GetFullPath(file))).ToString());
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    private static void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
    private static string Bytes(ulong bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes; int unit = 0;
        while (value >= 1024 && unit < 3) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
