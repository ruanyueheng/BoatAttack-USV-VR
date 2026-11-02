# v0.58.0 PICO interaction repairs

This Unity 2022.3.62f1 / URP project combines boat travel, VR fishing, island exploration, home crafting, farming, animal habitats and saved inventory/shop transactions. The production scene remains `Assets/scenes/Fishing/fishing_Island.unity`.

## Changes and headset controls

1. **Binoculars:** the tracked body is approximately 19 × 17 cm, with 52 mm lens openings. Rear eyepieces follow the runtime's measured eye separation (64 mm fallback). Hold both grips, raise the binoculars near the face and hold either trigger for eye alignment assistance. Releasing the grips or lowering the binoculars ends observation. Right stick changes magnification; A records animals. Only the optical camera magnifies the image; the HMD's stereo projection remains unchanged.
2. **Workshop hand tools:** move the right palm near the visible handle and squeeze the grip. Lift and strike the hammer, push/pull the saw with its teeth in the kerf, or hold the winding handle and turn it in the wheel's plane. Contact controls progress independently of attachment; moving the tool away from its workpiece no longer drops it. Opening the hand releases it. Tracking loss requires releasing the grip before another grab. Materials are charged only when a completed output is committed.
3. **Fallen orchard fruit:** after shaking the tree and waiting for the fruit to land, bring either hand within reach and squeeze the grip. The actual harvest transaction saves one fruit to the backpack. A displays help in VR. A full backpack leaves the fruit on the ground.
4. **Habitats:** home ecology opens directly to all six construction choices (aquarium plus five animal habitats); its directory also provides fish display, feeding, animals and traps. Empty animal plots have visible markers. Permanent habitats sample the authored home land collider instead of overlapping legacy terrain or their own fences, keeping their foundation height stable across refreshes.
5. **Quick purchase:** the notebook supply page performs the original wallet/backpack transaction from safe locations, including while the backpack itself is open. Normal mode charges the displayed price; creative mode preserves the existing free-purchase rule. Successful orders save immediately. Invalid items, insufficient money and full backpacks show explicit reasons and produce no partial transaction. Physical counter purchases retain their original range checks.
6. **Cashier 汐禾:** the existing character and textures remain in place. Both LODs receive a lightweight five-joint procedural skin for breathing, looking toward customers, greeting nods and conversational head/arm movements. The root and feet stay at the original counter anchor. No missing animation clips or empty Animator controllers are required.

## Reproducible checks

```powershell
python Tools/Check-FastCompile.py
dotnet run --project Tools/FishingTests/FishingTests.csproj --configuration Release -p:BuildInParallel=false
dotnet run --project Tools/InputTests/InputTests.csproj --configuration Release -p:BuildInParallel=false
./Tools/Run-UnityValidation.ps1 -Suite FishingV058 -TimeoutSeconds 1200
./Tools/Run-UnityValidation.ps1 -Suite FishingV056 -TimeoutSeconds 1200
./Tools/Run-UnityValidation.ps1 -Suite FishingFeedbackTools -TimeoutSeconds 1200
./Tools/Run-FishingAndroidBuild.ps1 -TimeoutSeconds 3600
```

The V058 suite uses an isolated career in the production scene. It exercises workpiece contact and grab lifetime, physical wheel rotation, binocular dimensions/IPD/alignment/projection, real quick-purchase/save/failure paths, all six deployments and their foundation footprints, orchard pickup, and actual baked cashier vertex motion with planted feet. Native OpenXR simulation and connected-device installation are recorded below once completed. Simulated inputs and screenshots cannot establish wearer comfort or the user's physical controller fit.

## Run results and delivery

- Unity Roslyn fast compilation: runtime and editor assemblies passed.
- Pure rules: **4852 passed**; input/render policy rules: **74 passed**.
- Unity V058 targeted production-scene checks: passed (`Logs/usv-validation-305082820859444a8a64d6fd7aba09ee.log`).
- Unity V056 integrated regression: passed (`Logs/usv-validation-c8654ad0633944f1978a406e1039da7e.log`).
- Full FeedbackTools regression: passed, including strict visible rake/palm contact (`Logs/usv-validation-c77d485dd9334892846e4802afb19201.log`). The contact probe now samples each Player LateUpdate because the slower editor validation iterator could miss the actual contact frame.
- Native OpenXR simulation: passed all ten observations in `Logs/V058/NativeXR/native-v058.json`, using `Tools/Test-XRV058.py`. Two separate trigger presses bought two real plank bundles; holding the trigger did not repeat the purchase. All six construction cards were visible. The 63.995 mm mock-runtime eye separation produced matching eyepieces, stable trigger-assisted alignment and active 2× optics; releasing one grip closed the optical view. Screenshots were inspected, including actual magnified shore geometry.
- Android build: passed using Unity 2022.3.62f1, package `com.ruanyueheng.tidefin.vr`, version **0.58.0 / 58**, ARM64 IL2CPP. APK CRC, current repair method metadata and UnityFS LZ4HC archive checks passed. APK Signature Scheme v2 verified against the existing installed version's certificate.
- Local APK: `Builds/FishingPrototype-v0.58.0-Android/TideFin-VR-v0.58.0-PICO.apk` (**761,950,277 bytes**).
- SHA-256: `324b5b4ede785ac9d1086e5322ca3bc131c8cdf040fae7700530823e90a6a8dd`.
- PICO A9210 delivery: copied to `/sdcard/Download/TideFin-VR-v0.58.0-PICO.apk`; the on-device SHA-256 matched. `adb install -r` returned **Success**, and package inspection confirmed **0.58.0 / 58**, updated on 2026-11-02. Existing application data was preserved; no uninstall or data clear was used.
- The installed Activity accepted the launch command and its process started. The PICO OpenXR runtime reported client version `0.58.0`; the captured startup log contained no fatal exception. The headset paused the Activity while not worn, so this check does not establish an in-headset gameplay session. Manual headset wear, hand fit and comfort acceptance remain outstanding; this PR should stay draft until those checks are complete.
