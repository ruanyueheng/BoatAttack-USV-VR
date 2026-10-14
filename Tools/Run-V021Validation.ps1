param([string]$EditorRoot)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repo
try {
    foreach($project in @('FishingTests','InputTests')) {
        & dotnet run --project "Tools/$project" --configuration Release
        if($LASTEXITCODE -ne 0){throw "$project failed"}
    }
    foreach($suite in @('FishingV021','FishingCatch','FishingSmoke','FishingBuild')) {
        $options=@{Suite=$suite;TimeoutSeconds=1800}
        if($EditorRoot){$options.EditorRoot=$EditorRoot}
        & "$PSScriptRoot/Run-UnityValidation.ps1" @options
    }
    & "$PSScriptRoot/Run-FishingAcceptance.ps1" -Mode Smoke -Session ('v021-'+[Guid]::NewGuid().ToString('N').Substring(0,12)) -TimeoutSeconds 600
} finally {Pop-Location}
