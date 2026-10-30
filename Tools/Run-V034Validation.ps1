param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV034','FishingV033','FishingFirstPerson','FishingPickaxe','FishingHunting','FishingWorkshop','FishingV028ToolsSleep','FishingSmoke')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
