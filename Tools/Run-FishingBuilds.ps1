param([ValidateRange(60,7200)][int]$TimeoutSeconds=3600)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -LiteralPath (Join-Path $repo 'Assets/Scripts/Fishing/FishingPrototype.cs') -Raw -Encoding UTF8
$match=[regex]::Match($source,'const string Version\s*=\s*"([0-9.]+)"')
if(-not $match.Success){throw 'Cannot read the game version.'}
$version=$match.Groups[1].Value
# A release always includes both platforms; low-level Unity entries remain available for diagnostics.
& "$PSScriptRoot/Run-UnityValidation.ps1" -Suite FishingBuild -TimeoutSeconds $TimeoutSeconds
& "$PSScriptRoot/Run-FishingAndroidBuild.ps1" -TimeoutSeconds $TimeoutSeconds
& "$PSScriptRoot/Run-UnityValidation.ps1" -Suite Structure -TimeoutSeconds $TimeoutSeconds
$windows=Join-Path $repo "Builds/FishingPrototype-v$version"
$apk=Join-Path $repo "Builds/FishingPrototype-v$version-Android/FishingPrototype.apk"
$portable=Join-Path $repo "Builds/FishingPrototype-v$version-Windows.zip"
foreach($file in @((Join-Path $windows 'FishingPrototype.exe'),$apk)){
    if(-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -eq 0){throw "Missing build output: $file"}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$android=[IO.Compression.ZipFile]::OpenRead($apk)
try {
    if(-not $android.GetEntry('AndroidManifest.xml') -or -not $android.GetEntry('lib/arm64-v8a/libil2cpp.so')){throw 'APK is missing its manifest or ARM64 runtime.'}
    foreach($entry in $android.Entries){
        if($entry.Length -eq 0){continue}
        $stream=$entry.Open()
        try{$stream.CopyTo([IO.Stream]::Null)}finally{$stream.Dispose()}
    }
} finally {$android.Dispose()}
$temporary=$portable+'.partial-'+[Guid]::NewGuid().ToString('N')
$archive=[IO.Compression.ZipFile]::Open($temporary,[IO.Compression.ZipArchiveMode]::Create)
try {
    $files=@(Get-ChildItem -LiteralPath $windows -File -Recurse | Where-Object {$_.FullName -notmatch 'DoNotShip'})
    foreach($file in $files){
        $relative=$file.FullName.Substring($windows.Length+1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($temporary)
try {
    if($archive.Entries.Count -ne $files.Count){throw 'Portable package file count differs from the Windows build.'}
    foreach($entry in $archive.Entries){
        $original=Join-Path $windows $entry.FullName
        if(-not (Test-Path -LiteralPath $original -PathType Leaf)){throw "Unexpected portable entry: $($entry.FullName)"}
        $stream=$entry.Open();$hash=[Security.Cryptography.SHA256]::Create()
        try{$digest=[BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','')}
        finally{$hash.Dispose();$stream.Dispose()}
        if($digest -ne (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash){throw "Portable file differs: $($entry.FullName)"}
    }
} finally {$archive.Dispose()}
Move-Item -LiteralPath $temporary -Destination $portable -Force
$manifest=[ordered]@{version=$version;windowsFiles=$files.Count;windowsZip=$portable;windowsSha256=(Get-FileHash -LiteralPath $portable).Hash;androidApk=$apk;androidSha256=(Get-FileHash -LiteralPath $apk).Hash}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repo "Builds/v$version-builds.json") -Encoding UTF8
Write-Output "Both builds verified: $portable ; $apk"
