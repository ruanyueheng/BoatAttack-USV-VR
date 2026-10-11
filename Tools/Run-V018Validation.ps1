param([string]$EditorRoot, [switch]$Capture)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $repo
try {
    foreach ($project in @('FishingTests', 'InputTests')) {
        & dotnet run --project "Tools/$project" --configuration Release
        if ($LASTEXITCODE -ne 0) { throw "$project failed" }
    }
    foreach ($suite in @('FishingV018', 'FishingV017', 'FishingV016', 'FishingShop', 'FishingShore', 'Regression', 'FishingSmoke', 'Structure', 'FishingBuild')) {
        $options = @{ Suite = $suite; TimeoutSeconds = 3600 }
        if ($EditorRoot) { $options.EditorRoot = $EditorRoot }
        & "$PSScriptRoot/Run-UnityValidation.ps1" @options
    }
    & "$PSScriptRoot/Run-FishingAcceptance.ps1" -Mode Smoke -Session ('v018-' + [Guid]::NewGuid().ToString('N').Substring(0,12)) -TimeoutSeconds 600
    if ($Capture) {
        foreach ($size in @(@(1600,900), @(1280,1024))) {
            & "$PSScriptRoot/Run-FishingAcceptance.ps1" -Mode Capture -Session ('v018-' + [Guid]::NewGuid().ToString('N').Substring(0,12)) -Width $size[0] -Height $size[1] -TimeoutSeconds 600
        }
    }
} finally { Pop-Location }
