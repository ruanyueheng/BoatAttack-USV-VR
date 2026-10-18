param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV033','FishingFirstPerson','FishingCatch','FishingV028ToolsSleep','FishingV019','FishingV031','FishingSmoke')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
