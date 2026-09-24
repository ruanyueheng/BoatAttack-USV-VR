# Project health check — 2026-09-24

Scope: existing desktop baseline only. No new gameplay, tasks, maps, boat abilities or XR integration. This is a focused audit, not a claim that the entire project is bug-free.

## Reproduced and fixed

### Menu selection bounds and initialization

- Non-looping EnumSelector and ColorSelector clamped to array length, then indexed that invalid element.
- Initial selections skipped validation; an uninitialized color palette could be read before loading.
- EnumSelector did not handle an empty/null option list.
- Fix: clamp to the final valid index, validate initial selections, initialize the palette before reading its length, and leave empty enum controls neutral without emitting a bogus selection.

### Repeated menu event subscriptions

MainMenuHelper subscribed six handlers on every OnEnable without unsubscribing. Reopening the panel accumulated duplicate callbacks. Matching OnDisable cleanup now releases all six handlers.

### Engine lifecycle, optional audio and steering timestep

- A persistent NativeArray was allocated in Awake and disposed in OnDisable, but never recreated on re-enable.
- Audio setup assumed every assigned AudioSource had a clip, and FixedUpdate assumed engineSound always existed.
- SmoothDampAngle received Time.fixedTime (total elapsed time) instead of Time.fixedDeltaTime (one physics step).
- Fix: paired OnEnable allocation/OnDisable guarded disposal, optional-audio guards, safe behavior until a rigidbody/water sample is available, and the correct timestep. No new propulsion/reverse mechanics were added.

### Dynamic-resolution time units

Time.deltaTime is in seconds, but the old threshold used 1000/FPS (milliseconds). A normal slow frame therefore requested a higher, not lower, scale. The policy now compares seconds with 1/FPS and uses unscaled frame duration so gameplay pause does not appear to be infinitely fast rendering. This fixes the decision logic, not measured device performance.

### Returning from a race opened the wrong scene

The real Play Mode test loaded a single-player race successfully, but UnloadRace requested build index 0, which was demo_Island. Reproduced with an explicit failing log before the fix.

Return navigation now uses the main menu's explicit path. The normal desktop build list starts at main_menu and includes level_Island; demo and benchmark scenes remain in the list but disabled. No scene contents or prefabs were edited.

## Verification evidence

- Before fixes: 2/14 Unity component checks passed; 12 reproduced failures.
- After fixes: 14/14 component checks passed. Tests invoke real component logic in edit mode, including lifecycle methods and repeated menu subscriptions.
- Pure input/render-policy suite: 62 passed, including 2,000 deterministic input-axis samples and 17 frame-budget assertions.
- Headless real Play Mode: main menu -> single-player island -> main menu -> second single-player island; four Addressable boats, countdown and live engine disable/re-enable checks passed. The test exits Play Mode and treats unexpected logged errors/exceptions as failures.
- Structure validation: shared input asset, enabled desktop build entries and all three primary scene structures checked separately.
- Logs remain under ignored Logs/ and are not committed. Run commands in TESTING.md to reproduce.

## Not covered / follow-up work

1. Graphics and real input: headless execution cannot validate water/reflections/shaders, readable UI, real keyboard/gamepad control or VR comfort.
2. Builds: editor Addressable instantiation is not an Addressables content build or a standalone Windows/Android build. Those remain required before release.
3. Water-sampling lifecycle: GerstnerWavesJobs retains per-object registry entries and has no unregister API; cleanup also does not reset all static state. Repeated spawning and Enter Play Mode without domain reload need a dedicated stress test and lifecycle fix, not an untested package rewrite here.
4. Scene-load failure recovery: AppSettings unloads the old scene before the destination is known to have loaded. Missing/failed assets and rapid repeated load requests need dedicated recovery/re-entry tests. This audit verified successful flows only.
5. PICO 4 Ultra loader, SDK/firmware compatibility, stereo rendering, APK, performance and thermal behavior remain unverified.

Gameplay implementation remains on hold until the owner chooses a design. Keep the PR draft pending manual desktop/visual and build acceptance; do not equate a green CI check with a release-ready VR game.
