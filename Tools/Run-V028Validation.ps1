param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV028','FishingCashierDialogue','FishingLantern','FishingPickaxe','FishingV027','FishingCatch','FishingForge','FishingWorkshop','FishingFirstPerson','FishingV024','FishingSmoke')){
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
