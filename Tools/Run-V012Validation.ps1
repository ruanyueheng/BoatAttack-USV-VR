param(
    [ValidateSet('Quick', 'Full')][string]$Stage = 'Quick',
    [string]$EditorRoot,
    [ValidateRange(60, 7200)][int]$TimeoutSeconds = 3600
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runner = Join-Path $PSScriptRoot 'Run-UnityValidation.ps1'
$acceptance = Join-Path $PSScriptRoot 'Run-FishingAcceptance.ps1'
$evidence = Join-Path $repo ('Logs/v012-validation-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($evidence) | Out-Null
$results = [System.Collections.Generic.List[object]]::new()
function Step([string]$Name, [scriptblock]$Run) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    Write-Output "===== $Name ====="
    try {
        & $Run 2>&1 | Tee-Object -FilePath (Join-Path $evidence ($Name + '.log'))
        $results.Add([pscustomobject]@{ name = $Name; passed = $true; seconds = $watch.Elapsed.TotalSeconds })
    }
    catch {
        $results.Add([pscustomobject]@{ name = $Name; passed = $false; seconds = $watch.Elapsed.TotalSeconds; error = $_.Exception.Message })
        Write-Warning "$Name failed: $_"
    }
    finally {
        $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'results.json') -Encoding UTF8
    }
}
if (Get-Process Unity,FishingPrototype -ErrorAction SilentlyContinue) { throw 'Close the existing Unity editor/player first.' }
$library = Join-Path $repo 'Library'
if ((Test-Path -LiteralPath $library) -and ((Get-Item -LiteralPath $library).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Validation requires an independent Library, not a shared link.'
}
Write-Output "Fresh evidence: $evidence"
Push-Location -LiteralPath $repo
try {
    foreach ($project in @('FishingTests', 'InputTests')) {
        Step $project {
            & dotnet run --project (Join-Path $repo "Tools/$project/$project.csproj") --configuration Release -p:BuildInParallel=false
            if ($LASTEXITCODE -ne 0) { throw "dotnet exited $LASTEXITCODE" }
        }
    }
    $suites = @('FishingPlayer', 'FishingPlayerBoundary', 'FishingCharacter', 'FishingInteraction', 'FishingShore', 'FishingShop')
    if ($Stage -eq 'Full') {
        $suites += @('FishingTerrain', 'FishingMap', 'FishingField', 'FishingWorkshop', 'FishingExpedition', 'FishingCatchCapture', 'FishingSmoke', 'Structure', 'Regression', 'PlayMode')
    }
    foreach ($suite in $suites) {
        Step $suite {
            $options = @{ Suite = $suite; TimeoutSeconds = $TimeoutSeconds }
            if ($EditorRoot) { $options.EditorRoot = $EditorRoot }
            & $runner @options
        }
    }
    if ($Stage -eq 'Full' -and @($results | Where-Object { -not $_.passed }).Count -eq 0) {
        Step 'FishingBuild' {
            $options = @{ Suite = 'FishingBuild'; TimeoutSeconds = $TimeoutSeconds }
            if ($EditorRoot) { $options.EditorRoot = $EditorRoot }
            & $runner @options
        }
        if ($results[$results.Count - 1].passed) {
            foreach ($size in @(@(1280, 720), @(1280, 1024))) {
                Step "PlayerCapture-$($size[0])x$($size[1])" {
                    & $acceptance -Mode Capture -Session ('v012-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)) -Width $size[0] -Height $size[1] -TimeoutSeconds 600
                }
            }
        }
    }
    $failed = @($results | Where-Object { -not $_.passed })
    Write-Output "Completed: $($results.Count); failed: $($failed.Count); evidence: $evidence"
    if ($failed.Count) { throw ('Failed steps: ' + (($failed | ForEach-Object { $_.name }) -join ', ')) }
}
finally { Pop-Location }
