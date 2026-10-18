param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV030','FishingFirstPerson','FishingLantern','FishingV029','FishingV028ToolsSleep','FishingCatch','FishingCharacter','FishingSmoke')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
