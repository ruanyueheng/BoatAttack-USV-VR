param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV038','FishingV036Portal','FishingInteraction','FishingAngling','FishingMouseFishing','FishingHunting','FishingLantern','FishingV036Performance','FishingV038Swimming','FishingSmoke','Structure')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
