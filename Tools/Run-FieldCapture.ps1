param(
    [switch]$Visible,
    [string]$Player = 'Builds/FishingPrototype-v0.8.0/FishingPrototype.exe',
    [ValidateRange(640,3840)][int]$Width = 1280,
    [ValidateRange(480,2160)][int]$Height = 720,
    [ValidateRange(60,600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [IO.Path]::IsPathRooted($Player)) { $Player = Join-Path $repo $Player }
if (-not (Test-Path -LiteralPath $Player -PathType Leaf)) { throw "Build v0.8.0 first: $Player" }
if (Get-Process FishingPrototype -ErrorAction SilentlyContinue) { throw 'Close the existing player before capture.' }
$output = Join-Path $repo ('Logs/field-capture-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($output) | Out-Null
$log = Join-Path $output 'player.log'
$arguments = '-fishing-field-capture -field-capture-output "' + $output + '" -logFile "' + $log + '" -screen-fullscreen 0 -screen-width ' + $Width + ' -screen-height ' + $Height
# Hidden by default. A human can explicitly opt into a visible player to supply a graphics backbuffer.
$style = if($Visible) { 'Normal' } else { 'Hidden' }
$p = Start-Process -FilePath $Player -ArgumentList $arguments -WindowStyle $style -PassThru
Write-Output "Field capture: PID $($p.Id), evidence: $output"
try {
    if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
        $p.Kill(); $p.WaitForExit(); throw "Capture timed out. See $log"
    }
    $p.Refresh()
    if ($p.ExitCode -ne 0) { throw "Capture failed (exit $($p.ExitCode)). See $log" }
    if (-not (Select-String -LiteralPath $log -SimpleMatch '[FIELD-CAPTURE] PASS:' -Quiet)) { throw "No capture PASS marker: $log" }
    $images = @(Get-ChildItem -LiteralPath $output -Filter '*.png' -File)
    if ($images.Count -ne 5 -or @($images | Where-Object Name -Like 'INVALID-*').Count -gt 0) { throw 'Five valid full-HUD screenshots are required.' }
    Select-String -LiteralPath $log -SimpleMatch '[FIELD-CAPTURE]' | ForEach-Object { $_.Line }
} finally { $p.Dispose() }
