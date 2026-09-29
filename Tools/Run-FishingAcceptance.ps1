param(
    [ValidateSet('Smoke', 'Capture', 'Manual')][string]$Mode = 'Smoke',
    [string]$Player = 'Builds/FishingPrototype-v0.4.3/FishingPrototype.exe',
    [ValidatePattern('^[A-Za-z0-9_-]{1,48}$')][string]$Session = 'manual-v043',
    [ValidateRange(640, 7680)][int]$Width = 1280,
    [ValidateRange(480, 4320)][int]$Height = 720,
    [ValidateRange(60, 600)][int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [IO.Path]::IsPathRooted($Player)) { $Player = Join-Path $repo $Player }
if (-not (Test-Path -LiteralPath $Player -PathType Leaf)) { throw "Build the player first: $Player" }
if (Get-Process FishingPrototype -ErrorAction SilentlyContinue) { throw 'Close the existing player before acceptance.' }
$output = Join-Path $repo ('Logs/desktop-acceptance-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($output) | Out-Null
$log = Join-Path $output 'player.log'
$arguments = '-fishing-acceptance -fishing-acceptance-session ' + $Session + ' -logFile "' + $log + '" -screen-fullscreen 0 -screen-width ' + $Width + ' -screen-height ' + $Height
if ($Mode -eq 'Smoke') { $arguments += ' -batchmode -nographics -fishing-acceptance-smoke' }
if ($Mode -eq 'Capture') { $arguments += ' -fishing-acceptance-capture -fishing-acceptance-output "' + $output + '"' }
# Capture/Manual explicitly request a visible interactive player. Smoke stays hidden.
$style = if ($Mode -eq 'Smoke') { 'Hidden' } else { 'Normal' }
$p = Start-Process -FilePath $Player -ArgumentList $arguments -WindowStyle $style -PassThru
Write-Output "Acceptance $Mode ($Width x $Height), PID $($p.Id), evidence: $output"
if ($Mode -eq 'Manual') { $p.Dispose(); return }
try {
    if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
        $p.Kill(); $p.WaitForExit()
        throw "Acceptance timed out. See $log"
    }
    $p.Refresh()
    if ($p.ExitCode -ne 0) { throw "Acceptance exit $($p.ExitCode). See $log" }
    if (-not (Select-String -LiteralPath $log -SimpleMatch '[FISHING] PASS: desktop acceptance cases' -Quiet)) { throw "No acceptance PASS marker. See $log" }
    if ($Mode -eq 'Capture') {
        $manifests = @(Get-ChildItem -LiteralPath $output -Filter manifest.json -Recurse)
        if ($manifests.Count -ne 1) { throw 'Expected exactly one fresh capture manifest.' }
        $manifest = Get-Content -LiteralPath $manifests[0].FullName -Raw | ConvertFrom-Json
        if ($manifest.captures.Count -ne 12) { throw 'Expected 12 full-HUD captures.' }
        foreach ($capture in $manifest.captures) {
            if (-not $capture.fullHudProbeVerified -or $capture.width -ne $Width -or $capture.height -ne $Height) { throw 'HUD proof or requested resolution failed.' }
            if (-not (Test-Path -LiteralPath (Join-Path $manifests[0].DirectoryName $capture.file))) { throw 'Capture image is missing.' }
        }
    }
    Select-String -LiteralPath $log -SimpleMatch '[FISHING] PASS:' | ForEach-Object { $_.Line }
} finally { $p.Dispose() }
