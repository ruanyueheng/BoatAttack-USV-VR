param([string]$EditorRoot)
$ErrorActionPreference='Stop'
# Existing suites have independent scene and inventory fixtures.
foreach($suite in @('FishingV025','FishingHunting','FishingForge','FishingFirstPerson','FishingV024')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
