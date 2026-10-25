param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV039','FishingV036Portal','FishingV037Hands','FishingV035Farm','FishingV038Swimming','FishingInteraction','FishingAngling','FishingMouseFishing','FishingHunting','FishingLantern','FishingV036Performance','FishingSmoke','Structure')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
