param(
    [Parameter(Mandatory)][string]$Player,
    [ValidatePattern('^[A-Za-z0-9_-]{1,48}$')][string]$Label='baseline',
    [ValidateRange(300,1200)][int]$TimeoutSeconds=720
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not [IO.Path]::IsPathRooted($Player)){$Player=Join-Path $repo $Player}
if(-not (Test-Path -LiteralPath $Player -PathType Leaf)){throw "Player not found: $Player"}
if(Get-Process FishingPrototype -ErrorAction SilentlyContinue){throw 'Close the existing game before the isolated benchmark.'}
$output=Join-Path $repo ('Logs/Performance/'+$Label+'-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($output)|Out-Null
$log=Join-Path $output 'player.log'
$arguments='-fishing-performance-voyage -force-d3d11 -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile "'+$log+'"'
$watch=[Diagnostics.Stopwatch]::StartNew()
$taskLaunchUtc=[DateTime]::UtcNow
$process=Start-Process -FilePath $Player -ArgumentList $arguments -WorkingDirectory $repo -WindowStyle Hidden -PassThru
Write-Output "Fixed 1080p performance tour: $output"
try {
    if(-not $process.WaitForExit($TimeoutSeconds*1000)){$process.Kill();$process.WaitForExit();throw "Benchmark timed out: $log"}
    $process.Refresh()
    if($process.ExitCode -ne 0){throw "Benchmark exit $($process.ExitCode): $log"}
    $pass=Select-String -LiteralPath $log -SimpleMatch '[PERFORMANCE-VOYAGE] PASS: CSV=' | Select-Object -Last 1
    if($null -eq $pass){throw "Benchmark did not finish all measured phases: $log"}
    $csv=$pass.Line.Substring($pass.Line.IndexOf('CSV=')+4).Trim()
    foreach($file in @($csv,[IO.Path]::ChangeExtension($csv,'txt'),[IO.Path]::ChangeExtension($csv,'tour.json'))){
        if(-not (Test-Path -LiteralPath $file -PathType Leaf)){throw "Benchmark output is missing: $file"}
        Copy-Item -LiteralPath $file -Destination $output
    }
    $tour=Get-Content -LiteralPath ([IO.Path]::ChangeExtension($csv,'tour.json')) -Raw | ConvertFrom-Json
    $ready=[DateTimeOffset]$tour.readyUtc
    $startup=($ready.UtcDateTime-$taskLaunchUtc).TotalSeconds
    if($startup -lt 0){throw 'The clock changed during startup; startup timing cannot be accepted.'}
    $watch.Stop()
    [ordered]@{player=[IO.Path]::GetFullPath($Player);label=$Label;processWallSeconds=$watch.Elapsed.TotalSeconds;processLaunchToReadySeconds=$startup;launchUtc=$taskLaunchUtc.ToString('O');log=$log} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'launch.json') -Encoding utf8
    & python (Join-Path $PSScriptRoot 'Analyze-FishingPerformance.py') $output
    if($LASTEXITCODE -ne 0){throw 'Performance output verification failed.'}
} finally {$process.Dispose()}
