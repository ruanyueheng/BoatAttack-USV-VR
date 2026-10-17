param(
    [string]$EditorRoot,
    [ValidateSet("FishingV032", "FishingV031", "FishingV030", "FishingV029", "FishingV028", "FishingV028UI", "FishingV028ToolsSleep", "FishingV028Coast", "FishingCashierDialogue", "FishingLantern", "FishingPickaxe", "FishingV027", "FishingV026", "FishingV025", "FishingV024", "FishingV023Art", "FishingV023", "FishingV022Art", "FishingV022", "FishingV021", "FishingV020Install", "FishingV020", "FishingV019Install", "FishingV019", "FishingV018Install", "FishingV018", "FishingSkiffBuild", "FishingV017", "FishingV016", "FishingLivingCoast", "Structure", "Regression", "PlayMode", "FishingGenerate", "FishingSmoke", "FishingBuild", "FishingShore", "FishingExpedition", "FishingExpeditionCapture", "FishingField", "FishingFieldCapture", "FishingXRSetup", "FishingCatch", "FishingCatchCapture", "FishingAndroidBuild", "FishingWorkshop", "FishingTerrain", "FishingShopInstall", "FishingShop", "FishingMap", "FishingCharacter", "FishingInteraction", "FishingPlayer", "FishingPlayerBoundary", "FishingFirstPerson", "FishingCashierBuild", "FishingCashier", "FishingHunting", "FishingForge")]



    [string]$Suite = "Structure",
    [ValidateRange(60, 7200)]
    [int]$TimeoutSeconds = 1200,
    [switch]$DisableBurst
)
$ErrorActionPreference = 'Stop'
# 与 CLI 和 Unity UTF-8 日志保持一致，不依赖 Windows 当前代码页。
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionText = Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') -Raw
if ($versionText -notmatch '(?m)^m_EditorVersion: (\S+)') { throw 'Cannot read the project Unity version.' }
$version = $Matches[1]
if (-not $EditorRoot) { $EditorRoot = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version" }
$editor = Join-Path $EditorRoot 'Editor/Unity.exe'
if (-not (Test-Path -LiteralPath $editor -PathType Leaf)) { throw "Unity $version not found: $editor" }
$installedVersion = (Get-Item -LiteralPath $editor).VersionInfo.ProductVersion
if (-not $installedVersion -or $installedVersion.Split('_')[0] -ne $version) {
    throw "Refusing to open project with Unity '$installedVersion'; expected $version."
}
# Do not contend with an interactive editor or interrupt another person's work.
# Unity locks each project independently; leave unrelated editors running.
$projectLock = Join-Path $repo 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $projectLock) {
    try { $lockProbe = [IO.File]::Open($projectLock, 'Open', 'Read', 'None'); $lockProbe.Dispose() }
    catch { throw 'Close the Unity editor using this project before running batch validation.' }
}
$logDirectory = Join-Path $repo 'Logs'
[IO.Directory]::CreateDirectory($logDirectory) | Out-Null
# Unique logs cannot accidentally satisfy this run with a previous PASS marker.
$log = Join-Path $logDirectory ("usv-validation-" + [Guid]::NewGuid().ToString('N') + '.log')
$method = switch ($Suite) {
    'FishingV032' { 'BoatAttack.EditorTools.FishingV032Validation.Run' }
    'FishingV031' { 'BoatAttack.EditorTools.FishingV031Validation.Run' }
    'FishingV030' { 'BoatAttack.EditorTools.FishingV030Validation.Run' }
    'FishingV029' { 'BoatAttack.EditorTools.FishingV029Validation.Run' }
    'FishingV028' { 'BoatAttack.EditorTools.FishingV028Validation.Run' }
    'FishingV028UI' { 'BoatAttack.EditorTools.FishingV028UIValidation.Run' }
    'FishingV028ToolsSleep' { 'BoatAttack.EditorTools.FishingV028ToolsSleepValidation.Run' }
    'FishingV028Coast' { 'BoatAttack.EditorTools.FishingV028CoastValidation.Run' }
    'FishingCashierDialogue' { 'BoatAttack.EditorTools.FishingCashierDialogueValidation.Run' }
    'FishingLantern' { 'BoatAttack.EditorTools.FishingLanternValidation.Run' }
    'FishingPickaxe' { 'BoatAttack.EditorTools.FishingPickaxeValidation.Run' }
    'FishingV027' { 'BoatAttack.EditorTools.FishingV027Assets.Run' }
    'FishingV026' { 'BoatAttack.EditorTools.FishingV026Validation.Run' }
    'FishingV025' { 'BoatAttack.EditorTools.FishingV025Validation.Run' }
    'FishingV024' { 'BoatAttack.EditorTools.FishingV024Validation.Run' }
    'FishingV023Art' { 'BoatAttack.EditorTools.FishingV023Assets.Build' }
    'FishingV023' { 'BoatAttack.EditorTools.FishingV023Validation.Run' }
    'FishingV022Art' { 'BoatAttack.EditorTools.FishingV022Assets.Build' }
    'FishingV022' { 'BoatAttack.EditorTools.FishingV022Validation.Run' }
    'FishingV021' { 'BoatAttack.EditorTools.FishingV021Validation.Run' }
    'FishingHunting' { 'BoatAttack.EditorTools.FishingHuntingValidation.Run' }
    'FishingForge' { 'BoatAttack.EditorTools.FishingForgeValidation.Run' }

    'FishingV020Install' { 'BoatAttack.EditorTools.FishingV020WorldBuilder.Install' }
    'FishingV020' { 'BoatAttack.EditorTools.FishingV020Validation.Run' }
    'FishingV019Install' { 'BoatAttack.EditorTools.FishingV019WorldBuilder.Install' }
    'FishingV019' { 'BoatAttack.EditorTools.FishingV019Validation.Run' }
    'FishingV018Install' { 'BoatAttack.EditorTools.FishingV018WorldBuilder.Install' }
    'FishingV018' { 'BoatAttack.EditorTools.FishingV018Validation.Run' }
    'FishingSkiffBuild' { 'BoatAttack.EditorTools.FishingSkiffBuilder.Build' }
    'FishingV017' { 'BoatAttack.EditorTools.FishingV017Validation.Run' }
    'FishingV016' { 'BoatAttack.EditorTools.FishingV016Validation.Run' }
    'FishingLivingCoast' { 'BoatAttack.EditorTools.FishingLivingCoastValidation.Run' }
    'FishingCashierBuild' { 'BoatAttack.EditorTools.FishingCashierValidation.BuildAndValidate' }
    'FishingCashier' { 'BoatAttack.EditorTools.FishingCashierValidation.Run' }
    'FishingFirstPerson' { 'BoatAttack.EditorTools.FishingFirstPersonValidation.Run' }
    'FishingPlayerBoundary' { 'BoatAttack.EditorTools.FishingPlayerBoundaryValidation.Run' }
    'FishingPlayer' { 'BoatAttack.EditorTools.FishingPlayerRepairValidation.Run' }
    'FishingMap' { 'BoatAttack.EditorTools.FishingMapValidation.Run' }
    'FishingCharacter' { 'BoatAttack.EditorTools.FishingCharacterValidation.Run' }
    'FishingInteraction' { 'BoatAttack.EditorTools.FishingInteractionValidation.Run' }
    'FishingTerrain' { 'BoatAttack.EditorTools.FishingTerrainValidation.Run' }
    'FishingShopInstall' { 'BoatAttack.EditorTools.BeachTackleShopBuilder.Install' }
    'FishingShop' { 'BoatAttack.EditorTools.FishingShopValidation.Run' }
    'FishingWorkshop' { 'BoatAttack.EditorTools.FishingWorkshopValidation.Run' }
    'Structure' { 'BoatAttack.EditorTools.USVProjectValidation.Run' }
    'Regression' { 'BoatAttack.EditorTools.USVRuntimeRegression.Run' }
    'PlayMode' { 'BoatAttack.EditorTools.USVPlayModeSmoke.Run' }
    'FishingGenerate' { 'BoatAttack.EditorTools.FishingPrototypeEditor.Generate' }
    'FishingSmoke' { 'BoatAttack.EditorTools.FishingPrototypeSmoke.Run' }
    'FishingBuild' { 'BoatAttack.EditorTools.FishingPrototypeEditor.Build' }
    'FishingShore' { 'BoatAttack.EditorTools.FishingShoreValidation.Run' }
    'FishingXRSetup' { 'BoatAttack.EditorTools.FishingXRConfiguration.Configure' }
    'FishingAndroidBuild' { 'BoatAttack.EditorTools.FishingXRConfiguration.BuildAndroid' }
    'FishingCatch' { 'BoatAttack.EditorTools.FishingCatchValidation.Run' }
    'FishingCatchCapture' { 'BoatAttack.EditorTools.FishingCatchValidation.Capture' }
    'FishingField' { 'BoatAttack.EditorTools.FishingFieldValidation.Run' }
    'FishingFieldCapture' { 'BoatAttack.EditorTools.FishingFieldValidation.Capture' }
    'FishingExpedition' { 'BoatAttack.EditorTools.FishingExpeditionValidation.Run' }
    'FishingExpeditionCapture' { 'BoatAttack.EditorTools.FishingExpeditionValidation.Capture' }
}
$marker = switch ($Suite) {
    'FishingV032' { '[V032]' }
    'FishingV031' { '[V031]' }
    'FishingV030' { '[V030]' }
    'FishingV029' { '[V029]' }
    'FishingV028' { '[V028]' }
    'FishingV028UI' { '[V028-UI]' }
    'FishingV028ToolsSleep' { '[V028-TOOLS-SLEEP]' }
    'FishingV028Coast' { '[V028-COAST]' }
    'FishingCashierDialogue' { '[CASHIER-DIALOGUE]' }
    'FishingLantern' { '[LANTERN]' }
    'FishingPickaxe' { '[PICKAXE]' }
    'FishingV027' { '[V027]' }
    'FishingV026' { '[V026]' }
    'FishingV025' { '[V025]' }
    'FishingV024' { '[V024]' }
    'FishingV023Art' { '[V023-ART]' }
    'FishingV023' { '[V023]' }
    'FishingV022Art' { '[V022-ART]' }
    'FishingV022' { '[V022]' }
    'FishingV021' { '[V021]' }
    'FishingHunting' { '[HUNTING]' }
    'FishingForge' { '[FORGE]' }

    'FishingV020Install' { '[V020-WORLD]' }
    'FishingV020' { '[V020]' }
    'FishingV019Install' { '[V019-WORLD]' }
    'FishingV019' { '[V019]' }
    'FishingV018Install' { '[V018-WORLD]' }
    'FishingV018' { '[V018]' }
    'FishingSkiffBuild' { '[SKIFF]' }
    'FishingV017' { '[V017]' }
    'FishingV016' { '[V016]' }
    'FishingLivingCoast' { '[LIVING-COAST]' }
    'FishingCashierBuild' { '[CASHIER]' }
    'FishingCashier' { '[CASHIER]' }
    'FishingFirstPerson' { '[FIRST-PERSON]' }
    'FishingPlayerBoundary' { '[PLAYER-BOUNDARY]' }
    'FishingPlayer' { '[PLAYER]' }
    'FishingMap' { '[MAP]' }
    'FishingCharacter' { '[CHARACTER]' }
    'FishingInteraction' { '[INTERACTION]' }
    'FishingTerrain' { '[FISHING-TERRAIN]' }
    'FishingShopInstall' { '[SHOP-INSTALL]' }
    'FishingShop' { '[BEACH-SHOP]' }
    'FishingWorkshop' { '[WORKSHOP]' }
    'Structure' { '[USV-VALIDATION]' }
    'Regression' { '[USV-REGRESSION]' }
    'PlayMode' { '[USV-PLAYMODE]' }
    'FishingGenerate' { '[FISHING]' }
    'FishingSmoke' { '[FISHING]' }
    'FishingBuild' { '[FISHING]' }
    'FishingShore' { '[SHORE]' }
    'FishingXRSetup' { '[XR-SETUP]' }
    'FishingAndroidBuild' { '[XR-BUILD]' }
    'FishingCatch' { '[CATCH]' }
    'FishingCatchCapture' { '[CATCH]' }
    'FishingField' { '[FIELD]' }
    'FishingFieldCapture' { '[FIELD]' }
    'FishingExpedition' { '[EXPEDITION]' }
    'FishingExpeditionCapture' { '[EXPEDITION]' }
}
$arguments = '-batchmode -nographics -buildTarget StandaloneWindows64 -projectPath "' + $repo.Replace('\', '/') + '" -executeMethod ' + $method + ' -logFile "' + $log + '"'
if ($Suite -in @('FishingV022Art', 'FishingV022', 'FishingV021', 'FishingHunting', 'FishingForge', 'FishingV020Install', 'FishingV020', 'FishingV019Install', 'FishingV019', 'FishingV018', 'FishingV017', 'FishingV016', 'FishingCatch', 'FishingLivingCoast', 'FishingFirstPerson', 'FishingPlayerBoundary', 'FishingPlayer', 'FishingMap', 'FishingCharacter', 'FishingInteraction', 'FishingShop', 'FishingShore', 'FishingField', 'FishingExpedition', 'FishingSmoke', 'FishingWorkshop', 'FishingExpeditionCapture', 'FishingFieldCapture', 'FishingCatchCapture')) { $arguments = $arguments.Replace(' -nographics', '') }
if ($Suite -in @('FishingV032','FishingV031','FishingV030','FishingV029','FishingV028','FishingV028UI','FishingV028ToolsSleep','FishingV028Coast','FishingCashierDialogue','FishingLantern','FishingPickaxe','FishingV023','FishingV024','FishingV025','FishingV026','FishingV027')) { $arguments = $arguments.Replace(' -nographics', '') + ' -screen-width 1600 -screen-height 900' }
if ($DisableBurst) { $arguments += ' --burst-disable-compilation' }



if ($Suite -eq 'FishingAndroidBuild') { $arguments = $arguments.Replace('StandaloneWindows64', 'Android') }
if ($Suite -eq 'FishingSmoke') { $arguments += ' -fishing-autoplay' }
Write-Output "Running Unity $Suite validation. Log: $log"
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WorkingDirectory $repo -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        # Only stop the helper launched by this script, never an existing editor.
        $process.Kill()
        $process.WaitForExit()
        throw "Unity validation timed out after $TimeoutSeconds seconds. See $log"
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Unity validation failed (exit $($process.ExitCode)). See $log" }
    if (-not (Select-String -LiteralPath $log -Encoding UTF8 -SimpleMatch ($marker + ' PASS:') -Quiet)) {
        throw "Unity exited without executing all validation checks. See $log"
    }
    Select-String -LiteralPath $log -Encoding UTF8 -SimpleMatch $marker | ForEach-Object { $_.Line }
}
finally {
    $process.Dispose()
}
