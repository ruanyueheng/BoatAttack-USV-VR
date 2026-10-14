param([string]$EditorRoot)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repo
try {
    & python 'Tools/Art/validate_v023_art.py'
    if($LASTEXITCODE -ne 0){throw 'V023 art checks failed'}
    foreach($project in @('FishingTests','InputTests')) {
        & dotnet run --project "Tools/$project" --configuration Release
        if($LASTEXITCODE -ne 0){throw "$project failed"}
    }
    foreach($suite in @('FishingV023Art','FishingV023','FishingV022','FishingV021','FishingV018','FishingFirstPerson','FishingPlayer','FishingLivingCoast','FishingSmoke','FishingBuild')) {
        $options=@{Suite=$suite;TimeoutSeconds=1800}
        if($EditorRoot){$options.EditorRoot=$EditorRoot}
        & "$PSScriptRoot/Run-UnityValidation.ps1" @options
    }
    & "$PSScriptRoot/Run-FishingAcceptance.ps1" -Mode Smoke -Session ('v023-'+[Guid]::NewGuid().ToString('N').Substring(0,12)) -TimeoutSeconds 600
} finally {Pop-Location}
