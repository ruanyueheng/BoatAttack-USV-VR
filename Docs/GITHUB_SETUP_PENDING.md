# GitHub setup — completed

Updated: 2026-09-24. This historical filename remains for existing references; setup is no longer pending.

- Team repository: https://github.com/ruanyueheng/BoatAttack-USV-VR (private).
- origin: https://github.com/ruanyueheng/BoatAttack-USV-VR.git
- upstream: https://github.com/Unity-Technologies/BoatAttack.git (reference only; never push there).
- Default branch: main; earlier development baseline: vr-usv-dev.

Each collaborator needs their own GitHub access and authentication. The owner must invite friends to the private repository. Never share credentials or commit tokens.

Always select the repository explicitly with GitHub CLI: automatic detection in this checkout can select the upstream Unity project instead of the team repository.

Example: `gh pr list --repo ruanyueheng/BoatAttack-USV-VR`.

Follow TEAM_COLLABORATION.md and TESTING.md. A pushed feature branch is available to friends but is not merged into main. Do not mark untested gameplay or device work as validated.
