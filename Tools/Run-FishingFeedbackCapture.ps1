param([Parameter(Mandatory)][string]$Player,[ValidateRange(60,600)][int]$TimeoutSeconds=240)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not [IO.Path]::IsPathRooted($Player)){$Player=Join-Path $repo $Player}
if(-not (Test-Path -LiteralPath $Player -PathType Leaf)){throw "Player missing: $Player"}
if(Get-Process FishingPrototype -ErrorAction SilentlyContinue){throw 'Close the existing game before native capture.'}
$session='feedback-'+[Guid]::NewGuid().ToString('N')
$output=Join-Path $repo ('Logs/'+$session)
[IO.Directory]::CreateDirectory($output)|Out-Null
$log=Join-Path $output 'player.log'
$arguments='-fishing-acceptance -fishing-acceptance-session '+$session+' -fishing-acceptance-output "'+$output+'" -fishing-feedback-capture -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "'+$log+'"'
# Keep the automated player out of the user's workspace; the capture verifies actual backbuffer rendering.
$process=Start-Process -FilePath $Player -ArgumentList $arguments -WorkingDirectory $repo -WindowStyle Hidden -PassThru
Write-Output "Native feedback capture: $output"
try {
    if(-not $process.WaitForExit($TimeoutSeconds*1000)){$process.Kill();$process.WaitForExit();throw "Capture timed out: $log"}
    $process.Refresh()
    if($process.ExitCode -ne 0){throw "Capture failed exit $($process.ExitCode): $log"}
    if(-not (Select-String -LiteralPath $log -SimpleMatch '[FEEDBACK-CAPTURE] PASS:' -Quiet)){throw "No completion marker: $log"}
    $feedback=Get-Content -LiteralPath (Join-Path $output 'feedback-manifest.json') -Raw | ConvertFrom-Json
    if($feedback.captures.Count -ne 7){throw 'All seven new feature views must be captured.'}
    $manifests=@(Get-ChildItem -LiteralPath $output -Filter manifest.json -Recurse)
    if($manifests.Count -ne 1){throw 'Expected one full-HUD capture manifest.'}
    $manifest=Get-Content -LiteralPath $manifests[0].FullName -Raw | ConvertFrom-Json
    if($manifest.captures.Count -ne 7){throw 'HUD manifest count differs from feature manifest.'}
    foreach($capture in $manifest.captures){
        if(-not $capture.fullHudProbeVerified -or $capture.width -ne 1600 -or $capture.height -ne 900){throw 'Native full-HUD proof failed.'}
        if(-not (Test-Path -LiteralPath (Join-Path $manifests[0].DirectoryName $capture.file))){throw 'Native screenshot is missing.'}
    }
    Write-Output '[FEEDBACK-CAPTURE] PASS: seven native images and full-HUD proofs verified; visual review remains separate.'
} finally {$process.Dispose()}
