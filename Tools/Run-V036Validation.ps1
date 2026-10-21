param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV036Art','FishingV036','FishingV036Portal','FishingV036Farm','FishingV036UI','FishingV036Storage','FishingV036Orchard','FishingV036Performance','FishingV035Farm','FishingV035','FishingPickaxe','FishingMouseFishing','FishingAngling','FishingV033','FishingSmoke')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
