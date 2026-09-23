param(
    [string]$EditorRoot = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1',
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$android = Join-Path $EditorRoot 'Editor\Data\PlaybackEngines\AndroidPlayer'
$manifest = Get-Content -LiteralPath (Join-Path $repo 'Packages\manifest.json') -Raw | ConvertFrom-Json
$version = Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings\ProjectVersion.txt') -Raw
$urpPath = Join-Path $EditorRoot 'Editor\Data\Resources\PackageManager\BuiltInPackages\com.unity.render-pipelines.universal\package.json'
$installedUrp = if(Test-Path -LiteralPath $urpPath) { (Get-Content -LiteralPath $urpPath -Raw | ConvertFrom-Json).version } else { $null }
$warnings = [Collections.Generic.List[string]]::new()
if ($manifest.dependencies.'com.unity.render-pipelines.universal' -ne $installedUrp) {
    $warnings.Add('Original URP differs from candidate editor. Migration is NOT yet validated; change packages through Unity Package Manager/MCP, not by blind manifest editing.')
}
$warnings.Add('File existence checks do not verify licenses, Gradle builds, Unity compilation, rendering, frame rate, or headset compatibility.')
$warnings.Add('PICO SDK 6.1 local requirements document lists PICO Space Pro / OS 6; PICO 4 Ultra compatibility is unverified.')
$warnings.Add('XR input adapter is implemented. No XR loader, XR rig, stereo camera, or device APK has been configured/validated.')
$report = [ordered]@{
    checkedAt = (Get-Date -Format 'o')
    project = $repo
    projectVersion = $version.Trim()
    candidateEditor = $EditorRoot
    editorExecutablePresent = (Test-Path -LiteralPath (Join-Path $EditorRoot 'Editor\Unity.exe'))
    candidateUrp = $installedUrp
    projectUrp = $manifest.dependencies.'com.unity.render-pipelines.universal'
    adbPresent = (Test-Path -LiteralPath (Join-Path $android 'SDK\platform-tools\adb.exe'))
    ndkPresent = (Test-Path -LiteralPath (Join-Path $android 'NDK\source.properties'))
    javaPresent = (Test-Path -LiteralPath (Join-Path $android 'OpenJDK\bin\java.exe'))
    packageLockPresent = (Test-Path -LiteralPath (Join-Path $repo 'Packages\packages-lock.json'))
    unityLibraryPresent = (Test-Path -LiteralPath (Join-Path $repo 'Library'))
    windowsBuildValidated = $false
    picoBuildValidated = $false
    headsetTestValidated = $false
    warnings = @($warnings)
}
$json = $report | ConvertTo-Json -Depth 6
if ($OutputPath) {
    # Optional caller-selected diagnostic output only; never starts Unity or changes project settings.
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$json,[Text.UTF8Encoding]::new($false))
}
$json
