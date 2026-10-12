param(
    [string]$EditorRoot,
    [ValidateSet("FishingV019Install", "FishingV019", "FishingV018Install", "FishingV018", "FishingSkiffBuild", "FishingV017", "FishingV016", "FishingLivingCoast", "Structure", "Regression", "PlayMode", "FishingGenerate", "FishingSmoke", "FishingBuild", "FishingShore", "FishingExpedition", "FishingExpeditionCapture", "FishingField", "FishingFieldCapture", "FishingXRSetup", "FishingCatch", "FishingCatchCapture", "FishingAndroidBuild", "FishingWorkshop", "FishingTerrain", "FishingShopInstall", "FishingShop", "FishingMap", "FishingCharacter", "FishingInteraction", "FishingPlayer", "FishingPlayerBoundary", "FishingFirstPerson", "FishingCashierBuild", "FishingCashier", "FishingHunting")]
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
    'FishingHunting' { 'BoatAttack.EditorTools.FishingHuntingValidation.Run' }
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
    'FishingHunting' { '[HUNTING]' }
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
if ($Suite -in @('FishingHunting', 'FishingV019Install', 'FishingV019', 'FishingV018', 'FishingV017', 'FishingV016', 'FishingCatch', 'FishingLivingCoast', 'FishingFirstPerson', 'FishingPlayerBoundary', 'FishingPlayer', 'FishingMap', 'FishingCharacter', 'FishingInteraction', 'FishingShop', 'FishingShore', 'FishingField', 'FishingExpedition', 'FishingSmoke', 'FishingWorkshop', 'FishingExpeditionCapture', 'FishingFieldCapture', 'FishingCatchCapture')) { $arguments = $arguments.Replace(' -nographics', '') }
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
