# PICO 0.51 validation — 2026-10-30

This release continues the interrupted PICO work. The Android build uses the existing independent `lane1` mirror because the main editor is being used for coursework screenshots.

## Changes

- Restore XR sprint through left grip or left stick click, jump through right stick click, and boat boost through left grip or left stick click. Keep right grip for grabbing, stamina, heat limits, braking priority, and the neutral-input gate after closing menus.
- Correct tracked mounts for binoculars, spear, lantern, work tools and watering can. Keep the separate desktop grip poses and the already corrected rod mount.
- Move the wreck collision onto the visible ship surface, add the 19-step boarding stair, and route the player around hanging rigging. Correct the FBX X-axis conversion and preserve the `Point N` waypoint naming required by prop placement.
- Include the pending Windows-style crafting page, batch choices and explicit material/fuel confirmation from the previous chat.

## Evidence

- `Tools/InputTests`: 74 checks passed, including trigger noise, deliberate braking and boost release/focus behavior.
- `Logs/WreckInspection/walk-v051-aisle.log`: actual CharacterController walked ashore → stairs → bridge → deck → portal without jumping. Each stop passed horizontal and visible-surface-height checks; deck feet were approximately 3.25 m high.
- `Logs/usv-validation-e39d1afcaad14bd0a7b626d704a27f30.log`: V025 passed in the native XR branch under the batch Mock Runtime. Actual capsule speed was 1.299992 m/s walking and 2.210007 m/s sprinting; stamina fell to 0.9958333, and jumping lifted the capsule. The test explicitly rearms the existing neutral-input gate before moving.
- `Logs/PicoLive/resume-baseline-tour.txt`: the installed 0.50 package completed eight notebook pages, three fishing grounds, settlement, duplicate prevention and isolated-save roundtrip on the connected PICO. Player manual and automatic save hashes were unchanged by this tour.

## Limits

The visible OpenXR controller simulation did not complete this turn. Its initial scene startup exposed the waypoint-name error, which is fixed and covered by the later full scene regression. Batch XR motor tests and scripted PICO tours do not certify physical grip comfort, controller button feel, or motion sickness. Prior measured PICO performance was about 30/90 FPS with a GPU bottleneck; this release does not claim that performance issue is fixed.

Installation and post-update device results are recorded in `Builds/v0.51.0-pico.json` when completed.

## Final installation

- Clean ARM64 APK: 761,904,330 bytes; 14 packed Data entries with no stale split files. Prior lane-cache leftovers were retained in the independent lane's `Logs/V051-stale-android-data-20261030` backup.
- `adb install -r`: Success. Device reports versionCode 51 / versionName 0.51.0.
- Local and installed APK SHA256: `9db35d76190b205994197ed0d0a579185789f945c8e91205f0155da0291b0109`.
- Player manual and automatic save hashes are unchanged across installation.
- Device remains Awake with stay-on enabled. Post-update app process is running, but `sys.pxr.boundary.ready=0` and the focused window belongs to `com.pvr.seethrough.setting`. The new package's tour is not marked passed; normal headset boundary/floor confirmation is required. See `Logs/PicoLive/v051-installed.png`.
