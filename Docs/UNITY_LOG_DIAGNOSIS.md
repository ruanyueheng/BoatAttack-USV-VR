# Unity log diagnosis - 2026-09-23

## Source

`C:/Users/Lenovo/AppData/Local/Unity/Editor/Editor.log`

## Blocking error

The main menu called `RaceManager.SetLevel(0)`, which produced the legacy path:

`scenes/_levels/level_Island`

Unity 2022.3 then reported:

`LoadSceneAsyncInPlayMode expects a valid full path. The provided path was 'scenes/_levels/level_Island'.`

The returned async operation was null, and `AppSettings.LoadSceneInternal` subsequently dereferenced it in the progress loop. This generated the visible `NullReferenceException` at `AppSettings.cs:242`.

## Fix applied

`Assets/Scripts/GameSystem/AppSettings.cs` now:

- normalizes old `scenes/...` values to `Assets/scenes/...` before loading;
- stores the level path from `ConstantData.GetLevelName` as an `Assets/`-relative path;
- reports a clear error instead of dereferencing a null async operation;
- guards loading-screen progress updates.

The same file now has a fallback color palette when `Resources.Load("textures/colorSwatch")` is temporarily unavailable during import. The asset exists at `Assets/Resources/textures/colorSwatch.png`; the log also shows a successful `Found 44 colors` after import.

## Non-blocking messages

- `GameplayIngredients.Editor.SceneViewToolbar ... unsupported layout: HorizontalToolbar` is a Unity 2022 editor overlay-layout compatibility warning.
- `EditorUpdateCheck ... 404` is an editor update-check request failure.
- The licensing client initially reported a signature validation warning but later resolved entitlement details.
- `Native extension for WindowsStandalone/Android target not found` appeared during editor assembly setup and was not a C# compilation error.

## Verification status

The log captured before the source edit still contains the old error. Unity had not yet recompiled the edited source when this report was generated (`Assembly-CSharp.dll` timestamp remained earlier than the source edit). Stop Play Mode and let Unity finish script compilation, then click the menu's Start Race button again. If the old path still appears, close and reopen the project once so Unity reloads the edited assembly.


## Follow-up verification — 2026-09-24

The earlier verification paragraph above records the historical state, not the latest result. Headless real Play Mode now successfully loads the island through MainMenuHelper, creates four Addressable boats, completes the countdown, returns to the menu and starts a second race. A separate return-navigation bug (build index zero pointed to demo_Island) was reproduced and fixed with an explicit main-menu path and normal desktop build entries. Interactive graphics, actual keyboard/gamepad input and standalone builds remain unverified. See PROJECT_HEALTH_CHECK.md and TESTING.md.
