# Validation guide

Run commands from the repository root. Green input tests are not playable-game or VR certification.

## Hardware-free input regression

Install the .NET 10 SDK (separate from Unity's runtime), then run:

```powershell
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release
```

The hand-authored project directly compiles runtime USVInputState and USVInputProcessor sources. No Unity editor, headset, NuGet test framework or LFS binaries are needed. A failed assertion returns a nonzero exit code; compiler warnings are errors.

Current suite: 45 assertions including 2,000 deterministic axis samples. Covers routing, focus recovery, source switches, independent command edges, deadzone boundaries, invalid floats, brake limits and axis invariants.

The Input regression GitHub Actions workflow runs this command on Windows and Linux for pushes/PRs. It does not compile or run Unity. Private-repository Actions settings and allowance can affect execution.

## Unity import and scene structures (Windows)

Install the exact editor from ProjectVersion.txt, activate its license in Unity Hub, obtain LFS assets and close Unity:

```powershell
./Tools/Run-UnityValidation.ps1
# For another installation location:
./Tools/Run-UnityValidation.ps1 -EditorRoot 'D:/Unity/2022.3.62f1'
```

The helper starts a hidden batch editor targeting Windows, validates shared input defaults and opens three primary scenes without saving. It checks missing scripts and Unity errors during the validation method. Both exit code zero and a success marker in a unique ignored Logs/ file are required.

Default timeout: 20 minutes; use -TimeoutSeconds 3600 for a long first import. On timeout only the helper it started is stopped. It refuses to run while any Unity editor is open. It does not change the package manifest or install SDKs; Unity can still import/resolve existing project dependencies normally.

Not covered: Play Mode, dynamic Addressables instantiation, complete serialized-reference integrity, rendering (uses -nographics), player builds, actual controller input, frame rate or headset compatibility. Editor scene opening is not the in-game menu transition.

## Manual desktop acceptance — still required

1. Open Assets/scenes/main_menu.unity in Unity 2022.3.62f1.
2. Enter Play Mode and click Start Race; verify the island loads without exceptions.
3. Verify player keyboard/gamepad controls, braking, pause/resume, reset and focus recovery. The direct demo may use spectator/AI defaults; it does not prove human input works.
4. Inspect water, reflections, buoyancy, UI, sound and race completion.
5. Build Addressables, make a Windows player and repeat the flow outside the editor.
6. Record commit, settings, environment, result and limitations in the PR.

PICO loader/rig integration and APK/device acceptance remain separate. Do not tick desktop/device acceptance merely because the automated checks passed.

## Local evidence (2026-09-24)

- Input suite: 45 passed.
- Unity 2022.3.62f1 batch import/compiler: exit code 0.
- Shared input asset and three scene structures: explicit PASS, exit code 0.
- Play Mode, Windows build, APK, visuals and headset: not tested this iteration.

Raw logs stay under ignored Logs/; do not commit editor caches or per-machine diagnostics.
