param(
    [ValidateSet('Quick', 'Full')][string]$Stage = 'Quick',
    [string]$EditorRoot,
    [string]$StartAt = '',
    [ValidateRange(60, 7200)][int]$TimeoutSeconds = 3600
)
$ErrorActionPreference = 'Stop'
# 脚本保留 UTF-8 BOM，并统一控制台编码，避免中文验证消息经 CLI 转发时乱码。
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$expectedVersion = '2022.3.62f1'
$versionText = Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') -Raw
if ($versionText -notmatch '(?m)^m_EditorVersion: (\S+)' -or $Matches[1] -ne $expectedVersion) {
    throw "本验收仅允许 Unity $expectedVersion，拒绝升级或换编辑器版本。"
}
$library = Join-Path $repo 'Library'
if (Test-Path -LiteralPath $library) {
    if ((Get-Item -LiteralPath $library -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Library 必须由当前副本独立生成，拒绝使用共享链接或目录联接。'
    }
}
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw '请先关闭现有 Unity 编辑器；本脚本不会终止别人的进程。' }
$runner = Join-Path $PSScriptRoot 'Run-UnityValidation.ps1'
$acceptance = Join-Path $PSScriptRoot 'Run-FishingAcceptance.ps1'
$player = Join-Path $repo 'Builds/FishingPrototype-v0.11.0/FishingPrototype.exe'
# 顺序固定：先编译与 Terrain，再非破坏性装店，再烘焙初始化后的真实地图。
$suites = @('FishingTerrain', 'FishingShopInstall', 'FishingMap', 'FishingCharacter', 'FishingInteraction', 'FishingShop')
if ($Stage -eq 'Full') {
    $suites += @('FishingShore', 'FishingField', 'FishingWorkshop', 'FishingExpedition', 'FishingCatchCapture', 'FishingBuild')
}
if ($StartAt) {
    if ($StartAt -notin $suites) { throw "StartAt '$StartAt' 不属于 $Stage 阶段；可选：$($suites -join ', ')" }
    $index = 0
    while ($suites[$index] -ne $StartAt) { $index++ }
    $suites = @($suites[$index..($suites.Count - 1)])
    Write-Output "从 $StartAt 继续；先前步骤不重跑，也不在本轮重新宣称通过。"
}
Write-Output "v0.11 源码/资源验收；当前副本：$repo"
Write-Output "Unity 固定版本：$expectedVersion；阶段：$Stage；每个 Unity 步骤上限：$TimeoutSeconds 秒。"
Write-Output '每步使用既有 Runner 的独立新日志、退出码和 PASS 标记；失败立即停止，不清理或覆盖玩家存档。'
$done = [System.Collections.Generic.List[string]]::new()
Push-Location -LiteralPath $repo
try {
    foreach ($suite in $suites) {
        Write-Output "===== 开始 $suite ====="
        $options = @{ Suite = $suite; TimeoutSeconds = $TimeoutSeconds }
        if ($EditorRoot) { $options.EditorRoot = $EditorRoot }
        # Runner 已固定子进程 WorkingDirectory 为此副本，并打印本次唯一日志路径。
        & $runner @options
        $done.Add($suite)
    }
    if ($Stage -eq 'Full') {
        if (-not (Test-Path -LiteralPath $player -PathType Leaf)) { throw "未生成本版 Windows Player：$player" }
        # 编辑器相机 RT 截图只作场景检查，不能冒充完整 HUD。
        # 现有 Player 验收在 EndOfFrame 捕获并验证 fullHudProbeVerified，共两种分辨率。
        foreach ($size in @(@(1280, 720), @(1280, 1024))) {
            $session = 'v011-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
            Write-Output "===== 新版 Player 全 HUD Capture $($size[0])x$($size[1]) ====="
            & $acceptance -Mode Capture -Player $player -Session $session -Width $size[0] -Height $size[1] -TimeoutSeconds 600
            $done.Add("PlayerCapture-$($size[0])x$($size[1])")
        }
    }
    Write-Output ('完成步骤：' + ($done -join ', '))
    if ($Stage -eq 'Quick') { Write-Output 'Quick 未执行全部旧回归、构建或 Player 截图；完整集中验收请选 Full。' }
    Write-Output '请保留各步骤打印的新日志与 PNG/manifest，人工复核地图对齐、颈口/胸部裁切、握点及动作接触。自动断言不代表 PICO 真机或实体输入已通过。'
}
catch {
    Write-Output ('已完成步骤：' + ($done -join ', '))
    Write-Output '当前步骤失败，后续步骤未执行。请提供上方 Runner 打印的本次新日志路径及错误；本脚本不会重置、清理或自动修改权限。'
    throw
}
finally { Pop-Location }
