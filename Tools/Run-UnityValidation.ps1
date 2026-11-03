param(
    [string]$EditorRoot,
    [ValidateSet("FishingV061", "FishingV060", "FishingV059", "FishingV058", "FishingV057", "FishingV056", "FishingV055", "FishingV054", "FishingPerfDevBuild", "FishingV044Perf", "FishingV044Tide", "FishingV044Life", "FishingV044Overlap", "FishingV044Voyage", "FishingV044Sea", "FishingV044Sky", "FishingV044Walk", "FishingV044Scene", "FishingV044SceneArt", "FishingV044Story", "FishingV044Npc", "FishingV044Realms", "FishingV043", "FishingV042", "FishingCoastArt", "FishingFeedbackMotion", "FishingMarineCapture", "FishingFeedbackTools", "FishingVoyageQuest", "FishingHomeEcology", "FishingFeedbackP3", "FishingCombat", "FishingWater", "FishingP0Art", "FishingPortableChart", "FishingWorldAudio", "FishingV039Realm", "FishingV039Home", "FishingV039", "FishingV038Swimming", "FishingV038", "FishingAudioMix", "FishingRegionalMusic", "FishingActionAudio", "FishingMouseFishing", "FishingAngling", "FishingV035Farm", "FishingV035", "FishingV035Levels", "FishingV035Art", "FishingV034Art", "FishingV034", "FishingV033", "FishingV032", "FishingV031", "FishingV030", "FishingV029", "FishingV028", "FishingV028UI", "FishingV028ToolsSleep", "FishingV028Coast", "FishingCashierDialogue", "FishingLantern", "FishingPickaxe", "FishingV027", "FishingV026", "FishingV025", "FishingV024", "FishingV023Art", "FishingV023", "FishingV022Art", "FishingV022", "FishingV021", "FishingV020Install", "FishingV020", "FishingV019Install", "FishingV019", "FishingV018Install", "FishingV018", "FishingSkiffBuild", "FishingV017", "FishingV016", "FishingLivingCoast", "Structure", "Regression", "PlayMode", "FishingGenerate", "FishingSmoke", "FishingBuild", "FishingShore", "FishingExpedition", "FishingExpeditionCapture", "FishingField", "FishingFieldCapture", "FishingXRSetup", "FishingCatch", "FishingCatchCapture", "FishingAndroidBuild", "FishingWorkshop", "FishingTerrain", "FishingShopInstall", "FishingShop", "FishingMap", "FishingCharacter", "FishingInteraction", "FishingPlayer", "FishingPlayerBoundary", "FishingFirstPerson", "FishingCashierBuild", "FishingCashier", "FishingHunting", "FishingForge", "FishingV037Hands", "FishingV037Portal", "FishingV037Weather", "FishingV037UI", "FishingV036Storage", "FishingV036Orchard", "FishingV036Performance", "FishingV036", "FishingV036Art", "FishingV036Portal", "FishingV036Farm", "FishingV036UI")]



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
    'FishingV061' { 'BoatAttack.EditorTools.FishingV061Validation.Run' }
    'FishingV060' { 'BoatAttack.EditorTools.FishingV060Validation.Run' }
    'FishingV059' { 'BoatAttack.EditorTools.FishingV059Validation.Run' }
    'FishingV058' { 'BoatAttack.EditorTools.FishingV058Validation.Run' }
    'FishingV057' { 'BoatAttack.EditorTools.FishingV057Validation.Run' }
    'FishingV056' { 'BoatAttack.EditorTools.FishingV056Validation.Run' }
    'FishingV055' { 'BoatAttack.EditorTools.FishingV055Validation.Run' }
    'FishingV054' { 'BoatAttack.EditorTools.FishingV054Validation.Run' }
    'FishingV044Voyage' { 'BoatAttack.EditorTools.FishingV044VoyageFloatAudit.Run' }
    'FishingV044Life' { 'BoatAttack.EditorTools.FishingV044LifeValidation.Run' }
    'FishingV044Tide' { 'BoatAttack.EditorTools.FishingV044TideValidation.Run' }
    'FishingV044Sea' { 'BoatAttack.EditorTools.FishingV044SeaValidation.Run' }
    'FishingPerfDevBuild' { 'BoatAttack.EditorTools.FishingPerfDevBuild.Build' }
    'FishingV044Perf' { 'BoatAttack.EditorTools.FishingV044PerfValidation.Run' }
    'FishingV044Sky' { 'BoatAttack.EditorTools.FishingV044SkyValidation.Run' }
    'FishingV044Overlap' { 'BoatAttack.EditorTools.FishingV044OverlapAudit.Run' }
    'FishingV044Scene' { 'BoatAttack.EditorTools.FishingV044SceneCapture.Run' }
    'FishingV044SceneArt' { 'BoatAttack.EditorTools.FishingV044SceneArt.Install' }
    'FishingV044Walk' { 'BoatAttack.EditorTools.FishingV044WalkAudit.Run' }
    'FishingV044Story' { 'BoatAttack.EditorTools.FishingV044StoryValidation.Run' }
    'FishingV044Npc' { 'BoatAttack.EditorTools.FishingV044NpcValidation.Run' }
    'FishingV044Realms' { 'BoatAttack.EditorTools.FishingV044RealmValidation.Run' }
    'FishingV043' { 'BoatAttack.EditorTools.FishingV043Validation.Run' }
    'FishingV042' { 'BoatAttack.EditorTools.FishingV042Validation.Run' }
    'FishingCoastArt' { 'BoatAttack.EditorTools.FishingCoastArtValidation.Run' }
    'FishingFeedbackMotion' { 'BoatAttack.EditorTools.FishingFeedbackP3Validation.RunMotion' }
    'FishingMarineCapture' { 'BoatAttack.EditorTools.FishingMarineCaptureValidation.Run' }
    'FishingFeedbackTools' { 'BoatAttack.EditorTools.FishingFeedbackToolsValidation.Run' }
    'FishingVoyageQuest' { 'BoatAttack.EditorTools.FishingVoyageValidation.Run' }
    'FishingHomeEcology' { 'BoatAttack.EditorTools.FishingHomeEcologyValidation.Run' }
    'FishingFeedbackP3' { 'BoatAttack.EditorTools.FishingFeedbackP3Validation.Run' }
    'FishingCombat' { 'BoatAttack.EditorTools.FishingCombatValidation.Run' }
    'FishingWater' { 'BoatAttack.EditorTools.FishingWaterValidation.Run' }
    'FishingP0Art' { 'BoatAttack.EditorTools.FishingP0ArtValidation.Run' }
    'FishingPortableChart' { 'BoatAttack.EditorTools.FishingPortableChartValidation.Run' }
    'FishingWorldAudio' { 'BoatAttack.EditorTools.FishingWorldAudioValidation.Run' }
    'FishingAudioMix' { 'BoatAttack.EditorTools.FishingAudioMixValidation.Run' }
    'FishingRegionalMusic' { 'BoatAttack.EditorTools.FishingRegionalMusicValidation.Run' }
    'FishingActionAudio' { 'BoatAttack.EditorTools.FishingActionAudioValidation.Run' }
    'FishingV038Swimming' { 'BoatAttack.EditorTools.FishingV038Validation.RunSwimming' }
    'FishingV039Realm' { 'BoatAttack.EditorTools.FishingV039Validation.RunRealm' }
    'FishingV039Home' { 'BoatAttack.EditorTools.FishingV039Validation.RunHome' }
    'FishingV039' { 'BoatAttack.EditorTools.FishingV039Validation.Run' }
    'FishingV038' { 'BoatAttack.EditorTools.FishingV038Validation.Run' }
    'FishingV037Hands' { 'BoatAttack.EditorTools.FishingV037HandsValidation.Run' }
    'FishingV037Portal' { 'BoatAttack.EditorTools.FishingV037PortalValidation.Run' }
    'FishingV037Weather' { 'BoatAttack.EditorTools.FishingV037WeatherValidation.Run' }
    'FishingV037UI' { 'BoatAttack.EditorTools.FishingV037UIValidation.Run' }

    'FishingV035Farm' { 'BoatAttack.EditorTools.FishingV035FarmValidation.Run' }
    'FishingV036Storage' { 'BoatAttack.EditorTools.FishingV036StorageValidation.Run' }
    'FishingV036Orchard' { 'BoatAttack.EditorTools.FishingV036OrchardValidation.Run' }
    'FishingV036Performance' { 'BoatAttack.EditorTools.FishingV036PerformanceValidation.Run' }
    'FishingV036' { 'BoatAttack.EditorTools.FishingV036Validation.Run' }
    'FishingV036Art' { 'BoatAttack.EditorTools.FishingV036Validation.Install' }
    'FishingV036Portal' { 'BoatAttack.EditorTools.FishingV036PortalValidation.Run' }
    'FishingV036Farm' { 'BoatAttack.EditorTools.FishingV036FarmValidation.Run' }
    'FishingV036UI' { 'BoatAttack.EditorTools.FishingV036UIValidation.Run' }
    'FishingV035' { 'BoatAttack.EditorTools.FishingV035Validation.Run' }
    'FishingV035Levels' { 'BoatAttack.EditorTools.FishingV035Levels.Run' }
    'FishingV035Art' { 'BoatAttack.EditorTools.FishingV035Art.Install' }
    'FishingV034Art' { 'BoatAttack.EditorTools.FishingV034Art.Prepare' }
    'FishingV034' { 'BoatAttack.EditorTools.FishingV034Validation.Run' }
    'FishingMouseFishing' { 'BoatAttack.EditorTools.FishingMouseFishingValidation.Run' }
    'FishingAngling' { 'BoatAttack.EditorTools.FishingAnglingValidation.Run' }
    'FishingV033' { 'BoatAttack.EditorTools.FishingV033Validation.Run' }
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
    'FishingV061' { '[V061]' }
    'FishingV060' { '[V060]' }
    'FishingV059' { '[V059]' }
    'FishingV058' { '[V058]' }
    'FishingV057' { '[V057]' }
    'FishingV056' { '[V056]' }
    'FishingV055' { '[V055]' }
    'FishingV054' { '[V054]' }
    'FishingV044Voyage' { '[V044-VOYAGE]' }
    'FishingV044Life' { '[V044-LIFE]' }
    'FishingV044Tide' { '[V044-TIDE]' }
    'FishingV044Sea' { '[V044-SEA]' }
    'FishingPerfDevBuild' { '[PERF-DEV]' }
    'FishingV044Perf' { '[V044-PERF]' }
    'FishingV044Sky' { '[V044-SKY]' }
    'FishingV044Overlap' { '[V044-OVERLAP]' }
    'FishingV044Scene' { '[V044-SCENE]' }
    'FishingV044SceneArt' { '[V044-SCENE-ART]' }
    'FishingV044Walk' { '[V044-WALK]' }
    'FishingV044Story' { '[V044-STORY]' }
    'FishingV044Npc' { '[V044-NPC]' }
    'FishingV044Realms' { '[V044-REALMS]' }
    'FishingV043' { '[V043]' }
    'FishingV042' { '[V042]' }
    'FishingCoastArt' { '[COAST-ART]' }
    'FishingFeedbackMotion' { '[FEEDBACK-MOTION]' }
    'FishingMarineCapture' { '[MARINE-CAPTURE]' }
    'FishingFeedbackTools' { '[FEEDBACK-TOOLS]' }
    'FishingVoyageQuest' { '[VOYAGE-QUEST]' }
    'FishingHomeEcology' { '[HOME-ECOLOGY]' }
    'FishingFeedbackP3' { '[FEEDBACK-P3]' }
    'FishingCombat' { '[COMBAT]' }
    'FishingWater' { '[WATER]' }
    'FishingP0Art' { '[P0-ART]' }
    'FishingPortableChart' { '[PORTABLE-CHART]' }
    'FishingWorldAudio' { '[WORLD-AUDIO]' }
    'FishingAudioMix' { '[AUDIO-MIX]' }
    'FishingRegionalMusic' { '[REGIONAL-MUSIC]' }
    'FishingActionAudio' { '[ACTION-AUDIO]' }
    'FishingV038Swimming' { '[V038-SWIMMING]' }
    'FishingV039Realm' { '[V039-REALM]' }
    'FishingV039Home' { '[V039-HOME]' }
    'FishingV039' { '[V039]' }
    'FishingV038' { '[V038]' }
    'FishingV037Hands' { '[V037-HANDS]' }
    'FishingV037Portal' { '[V037-PORTAL]' }
    'FishingV037Weather' { '[V037-WEATHER]' }
    'FishingV037UI' { '[V037-UI]' }

    'FishingV035Farm' { '[V035-FARM]' }
    'FishingV036Storage' { '[V036-STORAGE]' }
    'FishingV036Orchard' { '[V036-ORCHARD]' }
    'FishingV036Performance' { '[V036-PERFORMANCE]' }
    'FishingV036' { '[V036]' }
    'FishingV036Art' { '[V036-ART]' }
    'FishingV036Portal' { '[V036-PORTAL]' }
    'FishingV036Farm' { '[V036-FARM]' }
    'FishingV036UI' { '[V036-UI]' }
    'FishingV035' { '[V035]' }
    'FishingV035Levels' { '[V035-LEVELS]' }
    'FishingV035Art' { '[V035-ART]' }
    'FishingV034Art' { '[V034-ART]' }
    'FishingV034' { '[V034]' }
    'FishingMouseFishing' { '[MOUSE-FISHING]' }
    'FishingAngling' { '[ANGLING]' }
    'FishingV033' { '[V033]' }
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
if ($Suite -in @('FishingV061','FishingV060','FishingV059','FishingV058','FishingV057','FishingV056','FishingV055','FishingV054','FishingV044Perf','FishingV044Tide','FishingV044Life','FishingV044Overlap','FishingV044Scene','FishingV044Voyage','FishingV044Sea','FishingV044Sky','FishingV044Walk','FishingV044Story','FishingV044Npc','FishingV044Realms','FishingV043','FishingV042','FishingCoastArt','FishingFeedbackMotion','FishingMarineCapture','FishingFeedbackTools','FishingVoyageQuest','FishingHomeEcology','FishingFeedbackP3','FishingCombat','FishingWater','FishingP0Art','FishingPortableChart','FishingWorldAudio','FishingAudioMix','FishingActionAudio','FishingRegionalMusic')) { $arguments = $arguments.Replace(' -nographics', '') + ' -screen-width 1600 -screen-height 900' }
if ($Suite -in @('FishingV022Art', 'FishingV022', 'FishingV021', 'FishingHunting', 'FishingForge', 'FishingV020Install', 'FishingV020', 'FishingV019Install', 'FishingV019', 'FishingV018', 'FishingV017', 'FishingV016', 'FishingCatch', 'FishingLivingCoast', 'FishingFirstPerson', 'FishingPlayerBoundary', 'FishingPlayer', 'FishingMap', 'FishingCharacter', 'FishingInteraction', 'FishingShop', 'FishingShore', 'FishingField', 'FishingExpedition', 'FishingSmoke', 'FishingWorkshop', 'FishingExpeditionCapture', 'FishingFieldCapture', 'FishingCatchCapture')) { $arguments = $arguments.Replace(' -nographics', '') }
if ($Suite -in @('FishingV039Realm','FishingV039Home','FishingV039','FishingV038Swimming','FishingV038','FishingV037Hands','FishingV037Portal','FishingV037Weather','FishingV037UI','FishingV036Storage','FishingV036Orchard','FishingV036Performance','FishingV036','FishingV036Portal','FishingV036Farm','FishingV036UI','FishingMouseFishing','FishingAngling','FishingV035Farm','FishingV035Levels','FishingV035','FishingV034','FishingV033','FishingV032','FishingV031','FishingV030','FishingV029','FishingV028','FishingV028UI','FishingV028ToolsSleep','FishingV028Coast','FishingCashierDialogue','FishingLantern','FishingPickaxe','FishingV023','FishingV024','FishingV025','FishingV026','FishingV027')) { $arguments = $arguments.Replace(' -nographics', '') + ' -screen-width 1600 -screen-height 900' }
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





