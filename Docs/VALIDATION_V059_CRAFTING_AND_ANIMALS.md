# v0.59.0 handcraft feedback and animal habitats

This Unity 2022.3.62f1 / URP project combines boat travel, VR fishing, island exploration, home crafting, farming and animal habitats, with persistent inventory and shop transactions. The production scene is `Assets/scenes/Fishing/fishing_Island.unity`.

The work started after synchronizing GitHub `main` at `d12f48d` (PR #66). The repair branch is `feature/pico-v059-crafting-animals-20261102`. This document records the state of verification on 2026-11-02; pending work below must be updated from its actual run evidence.

## Problems, repairs and controls

1. **Quiet sawing and hammering:** accepted wood-cutting strokes and real billet strikes use distinct local sound cues (`SawCut` and `ForgeHammer`) with stronger workshop gain. Sound follows the workpiece and the existing Effects volume control. Lifting, missing the workpiece or holding a tool still must not play repeated crafting impacts. The five new recordings have been generated, downloaded, mastered and decoded in Unity; the full V059 suite confirms local playback, stronger gain and channel controls. Physical headset loudness remains a wearer check.
2. **Static workpieces:** the saw now exposes fresh wood faces as the kerf deepens with accepted strokes. Its lower connecting web shrinks, and the completed cut separates the two pieces. Each valid hammer blow spreads, flattens and dents the actual iron mesh, while sparks travel from both sides of the billet. The hammer's contact point and desktop rest pose follow the lowering metal surface. Wood chips and sparks use pooled geometry and a shared material per effect; the spark ribbons are double sided. Fresh cut faces replace their original surface faces to avoid coincident geometry flickering in VR. A recipe switch resets the billet and its particles. Cancelled work consumes no input; completion still uses the original inventory exchange, and the finished shape remains visible briefly after claiming the output.
3. **Tidal sieve grip:** either hand can directly squeeze either visible handle and start with that same grip press. The held handle follows the real palm while lifting and tilting the sieve. Alternating hand motion advances the task in a stable calibrated motion frame, so turning the head alone does not sift sand. Release returns the sieve to its tray. Tracking loss releases attachment, and recovery requires opening the hand before regrabbing.
4. **Animals cannot be placed in habitats or heard:** the matching rabbit, fox, tortoise, seabird or sea-turtle habitat gate offers direct placement of a carried animal. After catching with the capture net or collecting a trap, approach that species' gate and press **A** in VR (**E** on desktop) to place one animal. The animal page also provides placement. Missing habitats and full enclosures retain the carried animal and show the required next action. Placed animals use the existing models; the seabird stands on its actual perch. Species calls originate at their resident's position and obey Ambient volume, pause and quiet mode. Ownership, capacity checks and persistence continue through the production career/save transactions. The production-scene checks pass for all five species, active model renderers, perch placement, spatial calls, repeat-command protection and ownership/save persistence. Ten scene/model captures were generated; review of the five detail views confirms every resident is visible, including the seabird between the actual perch rails.

For the saw and hammer, place the right palm beside the visible handle and squeeze the grip. Push/pull the saw with its teeth at the kerf; lift the hammer and strike the billet. Opening the gripping hand releases the tool. Tool attachment and workpiece contact are independent, allowing a tool to remain held during a lift or miss. The sieve uses either hand and supports lifting clear of its tray before shaking.

The new mesh effects are generated directly in Unity from accepted task progress and reuse the existing animal models. They do not require replacement models from an external 3D service.

## Sound assets and provenance

Five completed outputs were generated in the [user's Meowa project](https://meowa.ai/canvas?projectId=proj-1793122448843-s5k5c1bo), with one two-second variant per sound at 10 credits each: **50 credits total**. The exact completed-card media URLs, prompts, settings, costs, file sizes and original SHA-256 hashes are recorded in [v059-sources.json](../ArtSource/Audio/MeowaWorld/v059-sources.json). All original stereo MP3 downloads are preserved in [Originals](../ArtSource/Audio/MeowaWorld/Originals); their hashes were verified before and after processing.

[prepare_v059_audio.py](../Tools/Art/prepare_v059_audio.py) uses Blender's `aud` decoder to create mono 44.1 kHz PCM16 WAVs with DC removal, silence trimming, level matching, an 8 ms fade-in and a 35 ms fade-out. The saw is edited to one 650 ms contact to keep repeated strokes short. Gain targets are RMS 0.16 for the saw and 0.12 for animal calls, with a peak ceiling of 0.82; peak limiting takes precedence over the RMS target. The [v059-manifest.json](../ArtSource/Audio/MeowaWorld/v059-manifest.json) records the actual trim, gain, peak, RMS and source/master hashes.

| Resource | Master duration | Peak | RMS |
| --- | --- | --- | --- |
| `SawCut` | 0.650 s | 0.8200 | 0.1553 |
| `RabbitCall` | 1.612 s | 0.8200 | 0.0850 |
| `FoxCall` | 1.185 s | 0.8200 | 0.0834 |
| `TortoiseCall` | 1.770 s | 0.8146 | 0.1200 |
| `SeaTurtleCall` | 1.760 s | 0.6582 | 0.1199 |

The five WAVs have unique Unity import GUIDs and three-dimensional mono import settings in `Assets/Resources/FishingAudio/World`. `ForgeHammer` reuses the existing `ForgeStrike` recording through a separate cue; this keeps the paused menu's original `ForgeStrike` receipt intact. The resident bird's `GullCall` similarly reuses the existing `SeagullCall` recording while preserving the wild gull's original ambience range. The V059 Unity run confirms decoding and local playback of all seven runtime cues and checks volume, pause and quiet guards.

## Reproducible checks

Run commands from the repository root. Run Unity validation, the native simulator and the Android build sequentially so they do not contend for the same project lock.

```powershell
python Tools/Check-FastCompile.py
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release -p:BuildInParallel=false
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release -p:BuildInParallel=false
./Tools/Run-UnityValidation.ps1 -Suite FishingFeedbackTools -TimeoutSeconds 1200
./Tools/Run-UnityValidation.ps1 -Suite FishingV059 -TimeoutSeconds 1200
```

If the sandbox cannot write its default system temporary directory, the same fast compiler can use the repository's ignored `Temp` directory:

```powershell
python -c "import pathlib,runpy,tempfile; p=pathlib.Path('Temp/V059FastCompile').resolve(); p.mkdir(parents=True,exist_ok=True); tempfile.tempdir=str(p); runpy.run_path('Tools/Check-FastCompile.py',run_name='__main__')"
```

`FishingFeedbackTools` checks the existing full workshop interaction path, actual tool/workpiece contact, hand presentation, held-tool lifetime, physical wheel rotation, cancellation and transactions. The V059 suite adds isolated production-scene checks for sound playback and volume guards, both sieve hands/handles, progressive cut and forged meshes, bilateral sparks, recipe reset, exact completion rewards, five animal gate placements, ownership/save persistence, full/missing habitats and species calls. Its handcraft-only entry point is `BoatAttack.EditorTools.FishingV059Validation.RunHandcraft` for focused geometry and interaction regressions.

For native OpenXR controller input, start an isolated simulator with a graphics device; omit `-quit` and `-nographics`:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Unity.exe' `
  -projectPath "$PWD" `
  -executeMethod BoatAttack.EditorTools.FishingXRSimulator.Run `
  -fishing-acceptance -fishing-acceptance-session native-v059 `
  -fishing-acceptance-case HomeInterior -fishing-xr-simulator -fishing-pico-quality `
  -logFile Logs/v059-native.log
```

In another terminal, while that simulator is ready:

```powershell
python Tools/Test-XRV059.py --only sieve --output Logs/V059/NativeXR/native-sieve.json
python Tools/Test-XRV059.py --only craft --output Logs/V059/NativeXR/native-craft.json
```

The native script supplies real Mock OpenXR poses and grip input to the production XR Tick path. It uses an isolated acceptance profile rather than the player's ordinary save slots. Its checks do not establish physical controller fit, stereo comfort or perceived loudness while wearing a PICO headset.

Generate the APK with:

```powershell
./Tools/Run-FishingAndroidBuild.ps1 -TimeoutSeconds 3600
```

The user's latest delivery instruction is **generate the APK only; do not install it on the headset**. The APK was generated and verified as recorded below; it was not installed or transferred to a headset.

## Confirmed results

| Check | Result | Evidence |
| --- | --- | --- |
| GitHub baseline | Synchronized `main` at `d12f48d`, including PR #66 | Branch `feature/pico-v059-crafting-animals-20261102` |
| Unity Roslyn fast compilation | PASS for runtime and Editor assemblies | `Tools/Check-FastCompile.py` |
| Pure game rules | 4,852 PASS | `Tools/FishingTests/FishingTests.csproj` |
| Input/render policy rules | 74 PASS | `Tools/InputTests/InputTests.csproj` |
| Full FeedbackTools regression | PASS | `Logs/usv-validation-36c5f39d11e34f3495579a96de18c142.log` |
| V058 integration regression | PASS | `Logs/usv-validation-946459e94f184beabd64c5ee7fbfdef7.log` |
| V059 handcraft specialization, final steel material | PASS | `Logs/v059-handcraft-final.log`; inspected four workpiece screenshots in `Logs/V059` |
| World audio regression | PASS | `Logs/usv-validation-03c88390e7dc44fc93230bea773665f3.log` |
| Native OpenXR sieve | PASS | `Logs/V059/NativeXR/native-sieve.json` |
| Native OpenXR saw and hammer | PASS: native grip/pose input drives contact strokes, changing meshes, local sound counts and bilateral debris; air motion changes none of them | `Logs/V059/NativeXR/native-craft.json`; simulator log `Logs/v059-native-final.log` |
| Five new Meowa audio assets | PASS: original/master hashes, PCM format, levels, durations and unique import GUIDs verified; 50 credits total | [Source records](../ArtSource/Audio/MeowaWorld/v059-sources.json), [master manifest](../ArtSource/Audio/MeowaWorld/v059-manifest.json) |
| Full V059 production-scene suite | PASS: audio, sieve, workpieces and animals, including final animal capture framing | `Logs/usv-validation-def75d565a064af88255ce0399e3116d.log:4383` |
| Five animal habitat and call checks | PASS: direct gate placement, active renderers, bird perch, repeated commands, spatial calls, mute guards, save round-trip and full/missing habitat rejection | Same final full-suite log; animal evidence summary at line 4336 |
| Animal scene/model visual review | PASS: five resident detail views and five habitat overviews inspected, every species visible in its detail view | `Logs/V059/habitat-{Rabbit,Fox,Tortoise,Gull,SeaTurtle}.png` and matching `-overview.png` files |
| Android build | PASS: v0.59.0 / version code 59, ARM64 IL2CPP, Vulkan, UnityFS LZ4 | `Logs/usv-validation-a2af31347580472a98796f75ec2159fb.log` |
| APK content and signing | PASS: archive CRC, seven repair code markers in IL2CPP metadata, ARM64-only libraries, version/package fields, APK v2 signature and matching previous signing certificate | `Logs/v059-apk-content-verification.json`; Android SDK `aapt` and `apksigner verify --verbose --print-certs` |

The native sieve evidence covers direct grip entry for either hand at either handle, lift/tilt attachment, stationary controller input while the head turns, release, native tracking loss and recovery, and a real completed left-hand sand-to-shell transaction. This is simulator evidence, not a claim of an in-headset gameplay session.

The ten animal images are 1600 × 900 RenderTexture scene/model views. Each species has a habitat overview and a close view of its actual resident. Capture temporarily changes the scene camera position and field of view and hides the player's body, arms and held props; it preserves the habitats, signs, perch rails and animal transforms. The close views place the camera inside the front fence to avoid the gate sign, and the bird view stays below the aviary roof. Camera and renderer state are restored immediately after each capture. These images establish the models' appearance and placement in the scene; they do not establish an ordinary first-person or stereoscopic PICO view, physical hand fit or in-headset comfort. The final run records each resident's capture-frustum check at lines 3051, 3291, 3531, 3785 and 4025; visual inspection supplements those framing assertions.

## APK delivery and remaining device checks

- APK: `Builds/FishingPrototype-v0.59.0-Android/FishingPrototype.apk` (generated output, excluded from Git).
- Size: **762,586,557 bytes** (about 763 MB).
- Package: `com.ruanyueheng.tidefin.vr`, version name `0.59.0`, version code `59`, minimum Android API 29, target API 33.
- SHA-256: `074942489a2cb7b11db507c6a11df1b5f9e3f9073fb602c8c5c0311a9bbce303`.
- Signing certificate SHA-256: `94099dee1d1da6e66711fdf0df759129d1654e42e974720235ea641476b0a1fa` (same development certificate as the previous APK).

- Physical PICO listening and wearer comfort/hand fit; no installation is authorized for this round.

Physical device acceptance remains outstanding. Automated scene tests, native Mock OpenXR input and successful Android packaging do not replace that check.
