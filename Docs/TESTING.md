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

The separate prototype adds 73 hardware-free catch/economy/input-recovery assertions:

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild -TimeoutSeconds 3600
```

The same Windows/Linux CI job runs the fishing rules as well as the original 62 core checks. FishingSmoke exercises three species, claiming each catch once, inventory, selling, upgrading, save round trips and backup recovery in a dedicated temporary save. It uses operation-level automation, not OS keyboard/mouse injection.

The normal generated scene is committed; only regenerate deliberately with FishingGenerate, because custom edits to that generated scene are replaced. Runtime art is original procedural geometry. No commercial How to Fish assets are included. See FISHING_PROTOTYPE.md for player controls, visual scope and current limitations.

The first full player/content build serialized existing URP 14 and Burst assets to their current formats and generated an empty XR settings container (no loader). The Unity, URP and package versions were not changed. Addressables content-state/linker output and player builds are ignored; generic settings/materials and the source scene are tracked.


v0.2 adds mouse/gamepad/look focus/pause recovery assertions and live checks that first-/third-person switching preserves the catch phase/inventory and keeps a level horizon. Before teleporting to a fishing area, it checks the initial harbor spawn and 12m forward first-person clearance. It also verifies immediate third-person camera retraction at a newly inserted obstacle, restoration of the previous time scale, staged net deployment, nonzero fish mesh normals and four viewport safe areas. The same FishingSmoke/FishingBuild commands apply; the v0.2 build went to Builds/FishingPrototype-v0.2; v0.3 now uses its own directory. See FISHING_V02.md for XR boundaries and manual visual/comfort acceptance.

## 5. v0.3 validation (2026-09-27)

The current desktop player is `Builds/FishingPrototype-v0.3/FishingPrototype.exe`. Older build directories are retained. `FishingGenerate` is **not** required: v0.3 scenery and UI are generated at runtime in the existing fishing scene.

Executed locally:

- `dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release`: **125 passed**. Includes all nine bait/species combinations, readable windup/rest/surge cycles, invalid bait rejection, atomic mixed-species deliveries, bonus accounting, normal-before-rare consumption, repeat-delivery rejection, persistent discoveries, v1 migration, v2 validation and the old catch/input recovery cases.
- `dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release`: **62 passed**.
- Unity `Regression`: **14 passed**, zero failed; `Structure`: input settings and three original scenes passed.
- Unity `FishingSmoke`: three species with three baits, first-/third-person cameras, journal pause/restore, navigation cycling, bait-change lock during a cast, order delivery, remaining-cargo sale, upgrade, extended save round trip and backup recovery passed.
- Static procedural scenery budget check: at most 40 combined renderers and 60,000 combined vertices; observed 26 / 34,627, including the old market meshes. LineRenderer, text, island and water are outside this count.
- A Windows Development Player was built and its explicit autoplay completed the same extended gameplay checks. Test progress uses an isolated temporary save.

Visual QA boundaries:

- The real, foreground Windows player rendered the water/islands and v0.3 first-person main HUD successfully.
- The hidden-process `-fishing-capture` run produced black images. Those images are **not visual evidence**, even though its gameplay assertions passed. Do not redistribute them as screenshots of the game.
- OS-injected C/J shortcut checks did not confirm a state change in the observed preview; no claim of full physical keyboard/mouse acceptance is made. The operation-level camera/journal tests did pass. Confirm these keys physically during the next player test.
- Journal/pause layout, all harbor viewpoints, long routes, movement comfort, gamepad behavior and sustained performance still need interactive review. Aspect-ratio tests check layout bounds, not typography or rendering at every resolution.
- No PICO loader, tracked rig, PICO tool interaction, ARM64 APK or headset test was performed. Desktop shader compilation is not Vulkan/GLES/Multiview device validation. See FISHING_V03.md for the stereo rendering changes and device checklist.

A reproducible screenshot command remains available, but a genuinely rendering interactive window is required on this machine:

```powershell
# Run interactively when reviewing the actual Windows viewport:
./Builds/FishingPrototype-v0.3/FishingPrototype.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -force-d3d11 -fishing-capture ./Builds/FishingV03ManualPreview
```

The screenshot driver is opt-in; normal game startup does not autoplay. No cloud CI result is implied by the local test results.

Final v0.3 additional regression: original `PlayMode` menu → race → menu → second race, four Addressable boats, countdown and engine re-enable passed. The original racing entry flow remains intact.

## 6. Living bay / v0.3.1 checks (2026-09-27)

The release player now lives in `Builds/FishingPrototype-v0.3.1/`. It uses `BuildOptions.None`, not Development; the single-player feature requires no network access. An earlier Development preview triggered a Windows firewall dialog; the agent did not approve or change firewall settings and closed its own preview process.

- Pure fishing suite: **192 assertions passed** (125 prior + 67 climate/wildlife cases). Includes all five climates × three catchable species, transition lock, pause and nonfinite delta handling, fixed/automatic cycling, chance/wait modifiers, view-cone/distance/occlusion policy, exactly-once wildlife discoveries, no coin farming and old v2 default-field compatibility.
- Extended Unity `FishingSmoke`: passed five visual climate preset applications, safe fog bounds, cloned-sky lifecycle restoration, 96/32 rain budgets, fixed wildlife population, sight-based turtle discovery, persisted observation, and rejection of observing an unseen ray through an actual collider, plus the complete fishing flow.
- Original scene dressing: **22 combined renderers / 37,463 vertices**. Ecology: **9 wildlife renderers + 3 habitat meshes / 8,464 unique mesh vertices**. Rain: **96 maximum quads**, only **32 nondegenerate quads** with reduced effects or active XR. These counts exclude the original island/water/boat/tool/text renderers; no device FPS claim is inferred.
- Found and fixed a zero-delta camera bug during pause/resume in the first living-bay smoke run. That failed run is not counted as a pass. The subsequent smoke run completed cleanly, including exiting Play Mode.
- A D3D11 Windows player/content build compiled the custom palette shader successfully. This does not prove Vulkan/GLES/Multiview output on a PICO headset.

Manual review focus: weather brightness and rain visibility; marine animal shapes and path placement; the new journal tab and clickable toolbar; relative UI readability at smaller resolutions; animal observation from first-person; sound level and comfort. Test weather changes from pause, resume for the 12-second transition, then pause again before selecting another preset. Do not accept hidden-window black captures as screenshots.

### Off-screen render evidence

`-fishing-capture <directory>` now also writes `scene-*.png`, `wildlife-*.png` and `weather-*.png` using an explicit camera → RenderTexture render, with camera target/pose and temporary resources restored afterward. This works when the normal desktop backbuffer is black. It is opt-in automation only; the normal game does not capture files.

These are actual D3D11 scene/model renders, **not HUD screenshots**. The sky/lights/water/new meshes can be inspected without granting network access or operating a Windows security dialog. Complete UI/keyboard/gamepad acceptance still requires an interactive review. The Windows firewall prompt was not approved or modified by the agent.

Final image QA additionally caught and corrected the island sky property's `_SkyTint`/`_Tint` mismatch and excessive inherited exposure (4). Fishing's cloned sky caps exposure at 1.4; original assets are unchanged. Vertex palettes now explicitly convert sRGB to linear, and rain streaks use a light unlit palette instead of dark lit geometry. The sky cap is asserted in the live acceptance driver.

## 7. v0.4.1 review (2026-09-28)

See `FISHING_V04_REVIEW.md` for the GitHub sync audit, findings, evidence boundaries and remaining acceptance work. Keep validation serial; do not start another Unity editor while a suite is running.

```powershell
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release -p:BuildInParallel=false
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release -p:BuildInParallel=false
./Tools/Run-UnityValidation.ps1 -Suite FishingSmoke
./Tools/Run-UnityValidation.ps1 -Suite FishingBuild
```

The fishing suite now has 314 passing assertions, including warmed-up query allocation checks, cached-view/order invalidation, exact order-weight wording, and selection parity over 2,816 cargo/order combinations. Unity smoke checks 1/2/3-line harbor layouts using the same geometry as the HUD. Geometry checks do not replace full HUD screenshots or physical-input acceptance.

The 0.4.1 player is written to `Builds/FishingPrototype-v0.4.1/`, leaving the original 0.4 build intact. Opt-in `-fishing-autoplay` uses a temporary save and exits on completion; it does not certify physical input or headset behavior. CI's OS matrix is now serial (`max-parallel: 1`).
## 8. v0.4.2 focused HUD (2026-09-28)

See `FISHING_V042_REVIEW.md` for the scoped review, reproduced catch-feedback bug and manual acceptance checklist. Tests were run serially, without parallel agents or simultaneous Unity/player processes.

- Fishing rules: **323 passed**, including 9 chart projection assertions (north/east, uniform zoom, bearing-preserving clipping, non-finite inputs, extreme coordinates and sampled bounds).
- Input/render policy rules: **62 passed**.
- The added post-catch bait-feedback regression **failed before the fix** in real Unity Play Mode. The HUD now hides only the duplicate catch notice, not subsequent action feedback.
- Unity 2022.3.62f1 `FishingSmoke`: **passed** after the fix, including four viewport sizes, conditional pointer regions, pause guards, anchoring/harbor state, complete catches, trades, legend and save/backup flow.
- `FishingBuild`: **passed**; output is `Builds/FishingPrototype-v0.4.2/FishingPrototype.exe`, with old builds retained.
- That newly built Windows Player passed `-batchmode -nographics -fishing-autoplay`, including the new HUD/feedback markers and exit code 0.

The serial commands are in the v0.4.2 review. No full-HUD screenshot, physical keyboard/mouse/gamepad, GPU frame-time or VR-device acceptance is claimed. The original racing-specific `Structure`/`Regression`/`PlayMode` suites were not rerun in this HUD-only review. Keep the PR draft until the relevant manual desktop checks are complete.

## Desktop acceptance sandbox (v0.4.3)

See [FISHING_DESKTOP_ACCEPTANCE.md](FISHING_DESKTOP_ACCEPTANCE.md) for opt-in isolated profiles, the serial smoke/capture runner and the outstanding physical-input checklist. Full-HUD captures are not physical-input certification.


Pre-PR validation (2026-09-29): **382 fishing assertions** (including 59 acceptance assertions) and **62 input/render-policy assertions** passed. Unity FishingSmoke, the v0.4.3 Windows Release build and the new standalone player's 12-scenario acceptance smoke passed serially. Evidence and explicit untested areas are recorded in the acceptance handoff; complete HUD captures and real-device input remain pending.
