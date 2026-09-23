# GitHub setup pending

The local repository currently has the official Unity BoatAttack remote. Before the first push, rename that remote to `upstream` and add the team's own GitHub repository as `origin`.

Required input for the final remote step: the team's GitHub repository URL, or a GitHub CLI login.

```powershell
git remote rename origin upstream
git remote add origin <TEAM_REPOSITORY_URL>
git push -u origin main
```

Do not push to `upstream`; it is the public Unity Technologies reference repository.
