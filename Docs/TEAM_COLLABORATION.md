# BoatAttack USV VR team collaboration

Updated: 2026-09-24

## Fixed baseline

- Unity Editor: 2022.3.62f1
- Current development branch: `vr-usv-dev`
- Git LFS is required for BoatAttack binary assets.
- The original BoatAttack license and attribution files stay in the repository.
- `main` should contain the last known good, runnable state.
- Work happens on `feature/*` or `fix/*` branches and enters `main` through a Pull Request.

## First-time setup

1. Install Git, Git LFS, Unity 2022.3.62f1, Android Build Support, SDK/NDK and OpenJDK.
2. Clone the team's GitHub repository; do not download it as a ZIP.
3. Run `git lfs install` and `git lfs pull`.
4. Open the project in Unity 2022.3.62f1 and wait for package/asset import to finish.
5. Do not commit `Library`, `Temp`, `Logs`, `UserSettings`, builds, or per-user IDE files.

## Daily workflow

```powershell
git switch main
git pull --ff-only
git switch -c feature/<short-name>
# work in Unity; close Unity before pulling or switching branches
git status
git add <reviewed-files>
git commit -m "Describe one focused change"
git push -u origin feature/<short-name>
```

Open a Pull Request from the feature branch to `main`. Do not merge if the project cannot open, compile, or enter the demo scene on the agreed Unity version.

## Every completed development iteration

- Run the applicable checks in TESTING.md; record what was not tested.
- Commit reviewed files and push the feature/fix branch to origin so friends can fetch the work. Do not leave a completed iteration only on this computer.
- Open/update a PR to the team's main; keep it draft if manual acceptance is outstanding. Do not automatically merge unvalidated changes.
- Always use `--repo ruanyueheng/BoatAttack-USV-VR` with gh commands: implicit selection can resolve to the official upstream.
- Report branch, commit, push status, PR URL and verification limits. If authentication/network prevents pushing, state that synchronization is incomplete; never force-push as a workaround.
- Never push to upstream or commit generated/unrelated files. Friends need an invitation to the private team repository and their own authentication.

## Unity conflict rules

- Only one person edits a given `.unity` scene at a time.
- Only one person edits a given prefab at a time, especially boat and level prefabs.
- Always commit `.meta` files together with their asset.
- Close Unity before `git pull`, branch switching, or conflict resolution.
- Do not change Unity, URP, Addressables, or package versions in a gameplay PR unless the PR is explicitly a migration PR.
- Keep PICO SDK import/version decisions documented; do not commit an unverified SDK dump.
- Commit `Packages/packages-lock.json` after the package graph is stable so both machines resolve the same versions.

## Suggested ownership

- Platform/XR/input: `feature/pico-xr`, `feature/desktop-input`
- Missions/gameplay: `feature/mission-*`
- Weather/water: `feature/weather-*`, `feature/water-*`
- Art/levels: `feature/level-*`
- Bug fixes: `fix/*`

## Before merging

- Unity opens without compile errors.
- `demo_Island` can enter Play Mode.
- The desktop keyboard fallback still works without a headset.
- The changed scene/prefab was tested by its author.
- No `Library`, `UserSettings`, APK, password, SDK credential, or local absolute path was added.
- Git LFS objects are present (`git lfs status`).
