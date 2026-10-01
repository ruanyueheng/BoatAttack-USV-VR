param(
    [string]$EditorRoot,
    [ValidateSet("Structure", "Regression", "PlayMode", "FishingGenerate", "FishingSmoke", "FishingBuild", "FishingShore", "FishingExpedition", "FishingExpeditionCapture", "FishingField", "FishingFieldCapture", "FishingXRSetup", "FishingCatch", "FishingCatchCapture", "FishingAndroidBuild", "FishingWorkshop")]
    [string]$Suite = "Structure",
    [ValidateRange(60, 7200)]
    [int]$TimeoutSeconds = 1200
)
$ErrorActionPreference = 'Stop'
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
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'Close Unity before running batch validation.' }
$logDirectory = Join-Path $repo 'Logs'
[IO.Directory]::CreateDirectory($logDirectory) | Out-Null
# Unique logs cannot accidentally satisfy this run with a previous PASS marker.
$log = Join-Path $logDirectory ("usv-validation-" + [Guid]::NewGuid().ToString('N') + '.log')
$method = switch ($Suite) {
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
if ($Suite -eq 'FishingWorkshop' -or $Suite -eq 'FishingExpeditionCapture' -or $Suite -eq 'FishingFieldCapture' -or $Suite -eq 'FishingCatchCapture') { $arguments = $arguments.Replace(' -nographics', '') }
if ($Suite -eq 'FishingAndroidBuild') { $arguments = $arguments.Replace('StandaloneWindows64', 'Android') }
if ($Suite -eq 'FishingSmoke') { $arguments += ' -fishing-autoplay' }
Write-Output "Running Unity $Suite validation. Log: $log"
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        # Only stop the helper launched by this script, never an existing editor.
        $process.Kill()
        $process.WaitForExit()
        throw "Unity validation timed out after $TimeoutSeconds seconds. See $log"
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Unity validation failed (exit $($process.ExitCode)). See $log" }
    if (-not (Select-String -LiteralPath $log -SimpleMatch ($marker + ' PASS:') -Quiet)) {
        throw "Unity exited without executing all validation checks. See $log"
    }
    Select-String -LiteralPath $log -SimpleMatch $marker | ForEach-Object { $_.Line }
}
finally {
    $process.Dispose()
}
