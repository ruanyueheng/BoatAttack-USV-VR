# Validation guide

Run commands from the repository root. These checks cover different layers; none certifies a complete VR game.

## 1. Hardware-free core logic

Install .NET 10 SDK (separate from Unity's runtime):

```powershell
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release
```

The hand-authored project directly compiles the runtime input state/processor and dynamic-resolution policy. No Unity license, headset, NuGet test framework or LFS binaries are needed. Failed assertions return a nonzero exit code; compiler warnings are errors.

Current suite: 62 assertions, including 2,000 deterministic input-axis samples and 17 frame-budget tests. The existing Input regression GitHub workflow runs this suite on Windows and Linux. It does not run Unity; private-repository Actions settings/allowance can affect execution.

## 2. Unity batch checks (Windows)

Install the exact editor from ProjectVersion.txt, activate its license in Unity Hub, obtain LFS assets and close all Unity editors:

```powershell
# Import, input asset, desktop build entries and primary scene structures:
./Tools/Run-UnityValidation.ps1 -Suite Structure
# Real component methods in edit mode, including lifecycle/boundary regressions:
./Tools/Run-UnityValidation.ps1 -Suite Regression
# Real Play Mode: menu -> race -> menu -> race, with engine disable/re-enable:
./Tools/Run-UnityValidation.ps1 -Suite PlayMode
```

Default suite is Structure. For another editor location, add `-EditorRoot 'D:/Unity/2022.3.62f1'`.

The helper checks the editor version before opening the project, starts a hidden Windows-target batch editor and requires both exit code zero and the suite's explicit success marker in a unique ignored Logs/ file. It does not save scenes. The timeout defaults to 20 minutes (`-TimeoutSeconds 3600` for a long first import); PlayMode also has an internal flow timeout. On timeout only its own helper is stopped. Unity can import/resolve the already-configured dependencies normally; no manifest or SDK installation is performed by the helper.

### What each suite proves

- Structure: scripts compile, input defaults load, desktop menu/island build entries exist, and three scenes open without missing script components. Not full serialized-reference integrity.
- Regression: 14 component checks for enum/color bounds, initialization, empty lists, menu callbacks, optional audio, native sample buffer re-enable and steering timestep. It invokes lifecycle methods in edit mode; it does not substitute for live runtime tests.
- PlayMode: MainMenuHelper starts single-player races, four Addressable boats initialize, countdown completes, live engine re-enable survives, and returning to the menu permits another race. Unexpected logged errors fail the run. It does not synthesize hardware input or validate race completion/scoring.

All suites use -nographics. They do not prove visual rendering, frame rate, actual keyboard/gamepad behavior, content/player builds or headset compatibility.

## 3. Manual desktop acceptance — still required

1. Open Assets/scenes/main_menu.unity, enter Play Mode and use the actual menu UI to start a race.
2. Drive with keyboard/gamepad; check braking, pause/resume, reset, focus recovery and race completion.
3. Inspect water, reflections, buoyancy, UI and sound. Dynamic-resolution device performance still needs measurement.
4. Build Addressables, make a Windows player and repeat outside the editor.
5. Record commit, environment, settings and limits in the PR.

The build list now boots the normal main menu and includes the island. Demo/benchmark entries are retained but disabled; opening them directly in the editor remains possible.

PICO loader/rig, ARM64 APK and device acceptance remain separate. Do not tick those checkboxes merely because the automated checks pass. Latest findings and remaining risks: PROJECT_HEALTH_CHECK.md.


## 4. Fishing prototype (2026-09-25)

The separate prototype adds 50 hardware-free catch/economy assertions:

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild -TimeoutSeconds 3600
```

The same Windows/Linux CI job runs the fishing rules as well as the original 62 core checks. FishingSmoke exercises three species, claiming each catch once, inventory, selling, upgrading, save round trips and backup recovery in a dedicated temporary save. It uses operation-level automation, not OS keyboard/mouse injection.

The normal generated scene is committed; only regenerate deliberately with FishingGenerate, because custom edits to that generated scene are replaced. Runtime art is original procedural geometry. No commercial How to Fish assets are included. See FISHING_PROTOTYPE.md for player controls, visual scope and current limitations.

The first full player/content build serialized existing URP 14 and Burst assets to their current formats and generated an empty XR settings container (no loader). The Unity, URP and package versions were not changed. Addressables content-state/linker output and player builds are ignored; generic settings/materials and the source scene are tracked.
