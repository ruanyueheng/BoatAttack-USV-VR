param([ValidateRange(60,7200)][int]$TimeoutSeconds=3600)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# Keep both builds on the same ASCII path: Bee can retain the Android alias in its Windows player cache.
if($repo -match '[^\x00-\x7F]'){
    $letter=@('R','S','T','U','V','W','X','Y','Z') | Where-Object {-not (Test-Path -LiteralPath ($_+':\'))} | Select-Object -First 1
    if(-not $letter){throw 'No free R-Z drive is available for the ASCII build path.'}
    $aliasRoot=$letter+':\';$aliasDrive=$letter+':'
    & subst.exe $aliasDrive $repo
    if($LASTEXITCODE -ne 0){throw 'Could not establish the ASCII build alias.'}
    $token=[Guid]::NewGuid().ToString('N');$marker='Logs/build-alias-'+$token+'.txt'
    [IO.File]::WriteAllText((Join-Path $repo $marker),$token)
    try{
        if((Get-FileHash -LiteralPath (Join-Path $repo '.git/HEAD')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $aliasRoot '.git/HEAD')).Hash){throw 'Build alias points at another repository.'}
        & (Join-Path $aliasRoot 'Tools/Run-FishingBuilds.ps1') -TimeoutSeconds $TimeoutSeconds
        $version=([regex]::Match([IO.File]::ReadAllText((Join-Path $repo 'Assets/Scripts/Fishing/FishingPrototype.cs')),'const string Version\s*=\s*"([0-9.]+)"')).Groups[1].Value
        $manifestPath=Join-Path $repo "Builds/v$version-builds.json"
        $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $manifest.windowsZip=Join-Path $repo "Builds/FishingPrototype-v$version-Windows.zip"
        $manifest.androidApk=Join-Path $repo "Builds/FishingPrototype-v$version-Android/FishingPrototype.apk"
        $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    } finally {
        $owned=Join-Path $aliasRoot $marker
        if((Test-Path -LiteralPath $owned) -and [IO.File]::ReadAllText($owned) -eq $token){& subst.exe $aliasDrive /D}
        else{Write-Warning 'Build alias changed externally; it was not removed.'}
    }
    return
}
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
    $playerData=$android.GetEntry('assets/bin/Data/data.unity3d')
    if(-not $playerData -or $playerData.CompressedLength -ne $playerData.Length){throw 'Android player data must use an LZ4 container stored without outer ZIP compression (PICO startup regression).'}
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
