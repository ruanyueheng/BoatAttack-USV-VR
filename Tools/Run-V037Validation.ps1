param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV037Hands','FishingV037Portal','FishingV036Portal','FishingV037Weather','FishingV037UI','FishingV036Storage','FishingV036Orchard','FishingV036Performance','FishingPickaxe','FishingLantern','FishingAngling','FishingMouseFishing','FishingHunting','FishingSmoke')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
