param(
    [string]$EditorRoot,
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
$arguments = '-batchmode -nographics -buildTarget StandaloneWindows64 -projectPath "' + $repo + '" -executeMethod BoatAttack.EditorTools.USVProjectValidation.Run -logFile "' + $log + '"'
Write-Output "Running Unity desktop import/scene validation. Log: $log"
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
    if (-not (Select-String -LiteralPath $log -SimpleMatch '[USV-VALIDATION] PASS:' -Quiet)) {
        throw "Unity exited without executing all validation checks. See $log"
    }
    Select-String -LiteralPath $log -SimpleMatch '[USV-VALIDATION]' | ForEach-Object { $_.Line }
}
finally {
    $process.Dispose()
}
