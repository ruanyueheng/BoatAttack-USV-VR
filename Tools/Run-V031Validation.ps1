param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV031','FishingFirstPerson','FishingLantern','FishingPlayerBoundary','FishingInteraction','FishingV028ToolsSleep','FishingV030','FishingSmoke')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
