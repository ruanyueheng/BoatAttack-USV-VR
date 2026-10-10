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
$evidence = Join-Path $repo ('Logs/v013-validation-' + [Guid]::NewGuid().ToString('N'))
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
if (Get-Process FishingPrototype -ErrorAction SilentlyContinue) { throw '请先关闭 FishingPrototype，避免打断其他工作。' }
# Each Unity step checks this project's lock; unrelated editors may stay open.
$library = Join-Path $repo 'Library'
if ((Test-Path -LiteralPath $library) -and ((Get-Item -LiteralPath $library).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw '验收要求独立 Library，不使用共享缓存链接。'
}
Write-Output "本轮唯一证据目录：$evidence"
Push-Location -LiteralPath $repo
try {
    foreach ($project in @('FishingTests', 'InputTests')) {
        Step $project {
            & dotnet run --project (Join-Path $repo "Tools/$project/$project.csproj") --configuration Release -p:BuildInParallel=false
            if ($LASTEXITCODE -ne 0) { throw "dotnet exited $LASTEXITCODE" }
        }
    }
    $suites = @('FishingCashier', 'FishingFirstPerson', 'FishingPlayer', 'FishingPlayerBoundary', 'FishingCharacter', 'FishingInteraction', 'FishingShore', 'FishingShop')
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
            Step 'PlayerSmoke' {
                & $acceptance -Mode Smoke -Session ('v013-smoke-' + [Guid]::NewGuid().ToString('N').Substring(0, 10)) -TimeoutSeconds 600
            }
            foreach ($size in @(@(1280, 720), @(1280, 1024), @(1680, 720))) {
                Step "PlayerCapture-$($size[0])x$($size[1])" {
                    & $acceptance -Mode Capture -Session ('v013-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)) -Width $size[0] -Height $size[1] -TimeoutSeconds 600
                }
            }
        }
    }
    $failed = @($results | Where-Object { -not $_.passed })
    Write-Output "完成：$($results.Count)；失败：$($failed.Count)；证据：$evidence"
    if ($failed.Count) { throw ('失败步骤：' + (($failed | ForEach-Object { $_.name }) -join ', ')) }
}
finally { Pop-Location }
