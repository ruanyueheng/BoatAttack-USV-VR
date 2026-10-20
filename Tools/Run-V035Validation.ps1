param([string]$EditorRoot)
$ErrorActionPreference='Stop'
foreach($suite in @('FishingV035Farm','FishingV035','FishingMouseFishing','FishingAngling','FishingV019','FishingFirstPerson','FishingV033','FishingSmoke')) {
    $options=@{Suite=$suite;TimeoutSeconds=1800}
    if($EditorRoot){$options.EditorRoot=$EditorRoot}
    & "$PSScriptRoot/Run-UnityValidation.ps1" @options
}
