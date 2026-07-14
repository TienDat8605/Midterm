# Repository Guidelines

## Project Structure & Module Organization

This is a Unity 6000.3.16f1 project. Keep gameplay scripts in `Assets/Scripts`, scenes in `Assets/Scenes`, prefabs in `Assets/Prefabs`, sprites and tiles in `Assets/Sprites` and `Assets/Tiles`, physics materials in `Assets/Physics`, and runtime-loaded assets in `Assets/Resources`. Photon/PUN package content lives under `Assets/Photon`; avoid editing vendor demo or package files unless the change is intentional. Unity configuration is stored in `ProjectSettings`, and package dependencies are tracked in `Packages/manifest.json` and `Packages/packages-lock.json`.

## Current Role Focus

My primary ownership in this repository is multiplayer integration and game deployment. Keep multiplayer work centered on the project-owned PUN setup: `Assets/Scripts/NetworkManager.cs`, `Assets/Scripts/PlayerControllerMulti.cs`, `Assets/Resources/PlayerPrefab.prefab`, `Assets/Scenes/MapScene.unity`, and `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`. Avoid taking ownership of map-making tools or level-generation content unless the change is required to make multiplayer or builds work.

For multiplayer changes, preserve the Photon/PUN flow currently in place: `NetworkManager` connects with `PhotonNetwork.ConnectUsingSettings()`, joins or creates `TestRoom`, and spawns `PlayerPrefab` from `Assets/Resources` with `PhotonNetwork.Instantiate`. `PlayerControllerMulti` owns local input and physics only when `photonView.IsMine` is true, and syncs remote player position/velocity through `IPunObservable`. Keep networked prefabs in `Assets/Resources` when they need to be instantiated by prefab name, and keep PhotonView observed components aligned with the scripts that serialize network state.

For deployment work, verify `ProjectSettings/EditorBuildSettings.asset` before building; it currently lists only `Assets/Scenes/SampleScene.unity`, so multiplayer/gameplay deployment may need the intended scenes added, such as `Assets/Scenes/Init.unity` and `Assets/Scenes/MapScene.unity`. Player settings live in `ProjectSettings/ProjectSettings.asset`, with Photon scripting defines already present for Standalone and WebGL. No `BuildScript.PerformBuild` implementation is currently present, so add one under an editor-safe location before relying on the documented CI build command.

## Build, Test, and Development Commands

- Open locally: launch Unity Hub and open this repository with Unity `6000.3.16f1`.
- Local Unity editor executable: `D:\Unity\Editor\6000.3.16f1\Editor\Unity.exe`. Use this full path when `Unity.exe` is not on `PATH`.
- Run in editor: open `Assets/Scenes/Init.unity` or `Assets/Scenes/MapScene.unity`, then press Play.
- Run tests from the command line:
  `& "D:\Unity\Editor\6000.3.16f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -quit`
- Build from CI or scripts:
  `& "D:\Unity\Editor\6000.3.16f1\Editor\Unity.exe" -batchmode -projectPath . -quit -executeMethod BuildScript.PerformBuild`
  Add `BuildScript` before relying on this command; no project build script is currently present.

## Coding Style & Naming Conventions

Use C# with 4-space indentation and Unity naming conventions. Name MonoBehaviour classes and files in PascalCase, for example `CameraFollow.cs` contains `CameraFollow`. Use PascalCase for public methods such as `SetTarget`, camelCase for local variables and private fields, and `[SerializeField] private` for Inspector-exposed references. Keep frame-sensitive logic in `Update`, physics movement in `FixedUpdate`, and camera follow work in `LateUpdate`. Prefer small component scripts over large managers.

## Testing Guidelines

The Unity Test Framework package is installed, but no test folders are currently present. Add Edit Mode tests under `Assets/Tests/EditMode` and Play Mode tests under `Assets/Tests/PlayMode`. Name test files after the behavior under test, for example `PlayerControllerTests.cs`, and use focused test names such as `Jump_AppliesUpwardVelocity`.

## Commit & Pull Request Guidelines

Recent history uses short imperative messages, sometimes with a conventional prefix, for example `feat: set up PUN 2 multiplayer...` and `create slime prefab`. Keep commits focused and use clear messages like `fix: adjust camera smoothing` or `add map collision tiles`.

Pull requests should include a brief summary, affected scenes or prefabs, test results, and screenshots or short clips for visual/gameplay changes. Link related issues when available. Do not commit generated cache folders such as `Library`, `Temp`, `Logs`, or `UserSettings`; keep Unity `.meta` files with their assets.

