param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV032','FishingV019','FishingV022','FishingV031','FishingFirstPerson','FishingCatch','FishingSmoke')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
