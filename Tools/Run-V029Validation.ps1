param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingFirstPerson','FishingV029','FishingLantern','FishingV028Coast','FishingWorkshop','FishingCatch','FishingV027','FishingSmoke')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
