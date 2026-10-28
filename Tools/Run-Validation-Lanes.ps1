param(
    [Parameter(Mandatory = $true)][string]$Suite,
    [ValidateRange(60, 7200)][int]$TimeoutSeconds = 1200,
    [switch]$DisableBurst,
    [ValidateRange(0, 600)][int]$WaitMinutes = 120
)
# Runs Tools/Run-UnityValidation.ps1 on the first idle "lane": the main project or a mirror under ../.lanes/laneN.
# Mirrors get Assets/Packages/ProjectSettings/Tools/ArtSource/Docs synced from main before each run; their Logs are
# copied back into main Logs afterwards, so callers read results exactly as before.
# Suites that write project assets (install/generate/build) always run on the main project.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$main = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lanesRoot = [IO.Path]::GetFullPath((Join-Path $main '../.lanes'))
$writesAssets = $Suite -match 'Install|Generate|Build|SceneArt|Setup'
$lanes = @($main)
if (-not $writesAssets -and (Test-Path -LiteralPath $lanesRoot)) {
    $lanes += @(Get-ChildItem -LiteralPath $lanesRoot -Directory -Filter 'lane*' | Sort-Object Name | ForEach-Object FullName)
}

function Test-UnityLock([string]$root) {
    $lock = Join-Path $root 'Temp/UnityLockfile'
    if (-not (Test-Path -LiteralPath $lock)) { return $false }
    try { $s = [IO.File]::Open($lock, 'Open', 'Read', 'None'); $s.Dispose(); return $false } catch { return $true }
}

$deadline = (Get-Date).AddMinutes($WaitMinutes)
$claim = $null; $lane = $null
while (-not $claim) {
    foreach ($candidate in $lanes) {
        if (Test-UnityLock $candidate) { continue }
        # The claim file stays exclusively open for the whole run, so two wrappers never pick the same lane.
        try { $claim = [IO.File]::Open((Join-Path $candidate '.lane-claim'), 'OpenOrCreate', 'ReadWrite', 'None'); $lane = $candidate; break } catch { }
    }
    if (-not $claim) {
        if ((Get-Date) -gt $deadline) { throw "No idle validation lane within $WaitMinutes minutes." }
        Start-Sleep -Seconds 20
    }
}
try {
    Write-Output "[LANES] $Suite -> $lane"
    if ($lane -ne $main) {
        foreach ($dir in 'Assets', 'Packages', 'ProjectSettings', 'Tools', 'ArtSource', 'Docs') {
            $src = Join-Path $main $dir
            if (Test-Path -LiteralPath $src) {
                robocopy $src (Join-Path $lane $dir) /MIR /MT:16 /R:1 /W:1 /NFL /NDL /NJH /NJS /NP /XF '.lane-claim' | Out-Null
                if ($LASTEXITCODE -ge 8) { throw "Lane sync failed for $dir (robocopy $LASTEXITCODE)." }
            }
        }
        # Tests write into fixed Logs subfolders (e.g. Logs/V038); mirror the folder tree, not the files.
        robocopy (Join-Path $main 'Logs') (Join-Path $lane 'Logs') /E /XF * /MT:16 /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
        Remove-Item -LiteralPath (Join-Path $lane 'Library/ArtifactDB-lock') -Force -ErrorAction SilentlyContinue
    }
    $script = Join-Path $lane 'Tools/Run-UnityValidation.ps1'
    $splat = @{ Suite = $Suite; TimeoutSeconds = $TimeoutSeconds }
    if ($DisableBurst) { $splat.DisableBurst = $true }
    $failed = $false
    try { & $script @splat } catch { $failed = $true; $err = $_ }
    if ($lane -ne $main) {
        robocopy (Join-Path $lane 'Logs') (Join-Path $main 'Logs') /E /XO /MT:16 /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    }
    if ($failed) { throw $err }
}
finally { $claim.Dispose() }
