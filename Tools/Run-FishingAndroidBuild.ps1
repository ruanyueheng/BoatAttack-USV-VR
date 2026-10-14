param(
    [ValidateRange(60,7200)][int]$TimeoutSeconds=3600,
    [ValidatePattern('^[R-Z]$')][string]$Drive='R'
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if($repo -notmatch '[^\x00-\x7F]') { & (Join-Path $PSScriptRoot 'Run-UnityValidation.ps1') -Suite FishingAndroidBuild -TimeoutSeconds $TimeoutSeconds; return }
$driveRoot=$Drive + ':\'
if(Test-Path -LiteralPath $driveRoot){throw "Drive $Drive already exists; choose another unused R-Z letter."}
# A temporary DOS-device alias avoids Android's non-ASCII project-path prohibition.
# No files are copied, moved or removed; only the alias owned by this invocation is removed in finally.
& subst.exe ($Drive + ':') $repo
if($LASTEXITCODE -ne 0){throw 'Could not establish ASCII project alias.'}
$token=[Guid]::NewGuid().ToString('N')
$marker='Logs/android-alias-' + $token + '.txt'
[IO.File]::WriteAllText((Join-Path $repo $marker),$token)
try {
    $head=Join-Path $repo '.git/HEAD';$aliasHead=Join-Path $driveRoot '.git/HEAD'
    if((Get-FileHash -LiteralPath $head).Hash -ne (Get-FileHash -LiteralPath $aliasHead).Hash){throw 'Alias does not point at the intended repository.'}
    Write-Output "Android build through temporary $Drive`: alias; source remains in its original workspace."
    & (Join-Path $driveRoot 'Tools/Run-UnityValidation.ps1') -Suite FishingAndroidBuild -TimeoutSeconds $TimeoutSeconds
} finally {
    $aliasMarker=Join-Path $driveRoot $marker
    if((Test-Path -LiteralPath $aliasMarker) -and [IO.File]::ReadAllText($aliasMarker) -eq $token) { & subst.exe ($Drive + ':') /D }
    else { Write-Warning 'Alias mapping changed externally; it was not removed.' }
}
