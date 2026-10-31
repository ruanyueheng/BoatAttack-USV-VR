using System;
using System.Collections.Generic;
using System.IO;
using BoatAttack.VR;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BoatAttack.Fishing
{
    /// <summary>The most recent landed fish or sea find, for the HUD's catch card.</summary>
    public struct CatchCard
    {
        public CatchRecord Fish;
        public FieldItem Find;
        public float ShownAt;
        public bool Discovery;
        public bool PersonalBest;
        public bool Released;
        public int Bounty;
    }

    [DefaultExecutionOrder(100)]
    public sealed partial class FishingPrototype : MonoBehaviour
    {
        public const string Version = "0.52.0";
        public FishingHandGames HandGames { get; private set; }
        public FishingFleetPresentation Fleet { get; private set; }
        public FishingIslandWorld Islands { get; private set; }
        public FishingCastMotion CastMotion { get; } = new FishingCastMotion();
        private Vector3 _castOrigin;
        private bool _castSplash;
        public Boat boatPrefab;
        public Camera viewCamera;
        public Vector3 spawnPosition;
        public Quaternion spawnRotation = Quaternion.identity;
        public Vector3 harbor;
        public Vector3[] fishingSpots = new Vector3[3];
        public Material[] fishMaterials;
        public Material wood, cream, dark, orange, teal, lineMaterial;
        public Boat Player { get; private set; }
        private FishingCareer _career;
        public FishingCareer Career { get=>_career; private set { _career=value;LanternOn=false;_career?.SetCreativeMode(CreativeMode); } }
        public bool CreativeMode { get; private set; }
        public void SetCreativeMode(bool enabled)
        {
            CreativeMode=enabled;Career?.SetCreativeMode(enabled);
            Notice(enabled?"创造模式：无限金币购买已开启，背包容量及解锁条件仍有效。":"创造模式已关闭，恢复正常金币计费。",6);
        }
        public CatchSession Session { get; } = new CatchSession();
        public bool Anchored { get; private set; }
        public USVBoost Boost { get; } = new USVBoost();
        public bool Boosting => _initialized && !Paused && !Ashore && !Anchored && !HasPendingCatch
            && !InventoryOpen && !JournalOpen && !DockConsole.BlocksGameplay && !XRNotebook.BlocksGameplay
            && !ShopBlocksGameplay && !StoryBlocksGameplay && _input.Current.boost && Boost.Active;
        public bool Paused { get; private set; }
        public bool Automated { get; private set; }
        public FishingViewRig View { get; private set; }
        public FishingShore Shore { get; private set; }
        public FishingExpeditionScene Expedition { get; private set; }
        public bool Ashore => Shore != null && Shore.Ashore;
        public Vector3 ExplorerPosition => Ashore ? Shore.Position : Player.transform.position;
        public int HomeNavigationIndex => ShopNavigationIndex + 1 + FishingAdventure.Count;
        public int NavigationCount => HomeNavigationIndex + 3;
        public int IslandNavigationIndex(int island) => ShopNavigationIndex + 1 + island;
        public FishingWeather Weather { get; private set; }
        public FishingNightscape Nightscape { get; private set; }
        public FishingEcology Ecology { get; private set; }
        public Shader PaletteShader { get; private set; }
        public bool FirstPerson => View != null && View.FirstPerson;
        public bool ShowDetails { get; private set; }
        public bool ChartExpanded { get; private set; } = true;
        public int ChartRangeMeters { get; private set; } = 160;
        private int _boatChartRange = 160;
        public bool HarborDeskOpen { get; private set; }
        public BaitKind Bait { get; private set; }
        public bool JournalOpen { get; private set; }
        public bool ComfortMode { get; private set; } = true;
        public bool WorldInstruments { get; private set; }
        public bool QuietMode { get; private set; }
        public int NavigationTarget { get; private set; }
        public Vector3 NavigationPosition => NavigationWorldPosition();
        public string NavigationName => NavigationTarget>HomeNavigationIndex?FishingCoastalPresentation.IsletName(NavigationTarget-HomeNavigationIndex-1):NavigationTarget == HomeNavigationIndex ? "回潮渔屋" : NavigationTarget > ShopNavigationIndex ? FishingAdventure.Name(NavigationTarget-ShopNavigationIndex-1) : NavigationTarget == ShopNavigationIndex ? ShopName : NavigationTarget == 0 ? "潮汐渔港" : NavigationTarget <= fishingSpots.Length ? FishingGrounds.Name(NavigationTarget - 1) : FishingExploration.SiteName(NavigationTarget - fishingSpots.Length - 1);
        public bool CanChangeBait => Session.Phase == CatchPhase.Idle || Session.Phase == CatchPhase.Caught || Session.Phase == CatchPhase.Escaped;
        public CatchCard LastCatch { get; private set; }
        public bool LegendTrophy => Career != null && Career.LegendCatches > 0;
        /// <summary>Automation-only switch that removes the legend's strike chance; ignored in normal play.</summary>
        public bool ForceLegendForAutomation { get; set; }
        private bool _xrActive;
        public bool AutomationReel { get; set; }
        public int Spot { get; private set; }
        public string Message { get; private set; }
        public bool MessageIsCatch { get; private set; }
        public string SavePath { get; private set; }
        public string SaveWarning { get; private set; } = string.Empty;
        public float Speed => Player == null ? 0f : Vector3.ProjectOnPlane(Player.engine.RB.velocity,Vector3.up).magnitude;
        public float HarborDistance => Player == null ? 999f : FlatDistance(Player.engine.RB.position,harbor);
        public bool AtHarbor => !Ashore && HarborDistance < 22f && Speed < 3f && !Anchored;
        public float SpotDistance { get; private set; }
        private USVInputRouter _input;
        private FishingHUD _hud;
        private Vector3 _anchor, _catchStart;
        private Quaternion _anchorRotation;
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<GameObject> _storedFish = new List<GameObject>();
        private readonly List<Transform> _buoys = new List<Transform>();
        private GameObject _fish, _bobber, _trophy;
        public FishingHands Hands { get; private set; }
        private Transform _rodTip, _net, _box, _reelCrank;
        private LineRenderer _line;
        private float _netMotion, _caughtAt, _messageUntil;
        private CatchPhase _previousPhase;
        private AudioSource _feedback;
        private AudioClip _biteTone, _successTone, _legendTone;
        private bool _initialized;
        private int _castGround = -1;
        private int _previousTargetFrameRate = -1;
        private float _resumeTimeScale = 1f;
        private bool _reeling;
        private readonly FishingPointerGate _pointerGate = new FishingPointerGate();
        private readonly FishingPointerGate _gamepadGate = new FishingPointerGate();
        private float _netDeployment;
        public bool NetDeployed => _netDeployment > 0.95f;
        private bool ForceLegend => Automated && ForceLegendForAutomation;

        private void Start()
        {
            Application.runInBackground=true;
            gameObject.AddComponent<FishingMobileQuality>();
            _previousTargetFrameRate=Application.targetFrameRate;
            _xrActive=UnityEngine.XR.XRSettings.isDeviceActive;Application.targetFrameRate=_xrActive?-1:60;
            ReadAcceptanceOptions();
            Automated = (Application.isBatchMode && !XRSimulatorRequested) || FishingPicoTour.Requested || HasArgument("-fishing-field-capture") || HasArgument("-fishing-capture") || HasArgument("-fishing-autoplay") || HasArgument(FishingPerformanceVoyage.EnableFlag) || HasArgument(FishingFeedbackCapture.EnableFlag) || HasArgument(FishingUICapture.EnableFlag) || HasArgument(FishingStoryCapture.EnableFlag) || HasArgument(FishingFrameProbe.EnableFlag) || (AcceptanceOptions != null && AcceptanceOptions.Automatic);
            SavePath = AcceptanceOptions != null ? AcceptanceOptions.ProfilePath(Application.temporaryCachePath, false)
                : Path.Combine(Automated ? Application.temporaryCachePath : Application.persistentDataPath,
                Automated ? "fishing-test-" + Guid.NewGuid().ToString("N") + ".json" : "boatattack-fishing-v13.json");
            // Older executables keep their own files; migration never overwrites their progress.
            string warning;
            string legacy=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v12.json");
            if(!File.Exists(legacy) && !File.Exists(legacy+".bak"))legacy=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v11.json");
            if(!File.Exists(legacy) && !File.Exists(legacy+".bak"))legacy=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v10.json");
            if(!File.Exists(legacy) && !File.Exists(legacy+".bak"))legacy=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v9.json");
            if(!File.Exists(legacy) && !File.Exists(legacy+".bak"))legacy=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v8.json");
            if(!File.Exists(legacy) && !File.Exists(legacy+".bak"))legacy=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v7.json");
            if(!File.Exists(legacy) && !File.Exists(legacy+".bak"))legacy=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v6.json");
            if(!File.Exists(legacy) && !File.Exists(legacy+".bak"))legacy=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v5.json");
            if(!File.Exists(legacy) && !File.Exists(legacy+".bak"))legacy=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v1.json");
            Career = HasIsolatedProfile ? FishingSaveStore.Load(SavePath, out warning)
                : FishingSaveStore.LoadWithLegacyFallback(SavePath, legacy, out warning);
            ComfortMode=HasIsolatedProfile || PlayerPrefs.GetInt("Fishing.V3.Comfort",1)==1;
            QuietMode=!HasIsolatedProfile && PlayerPrefs.GetInt("Fishing.V3.Quiet",0)==1;
            NavigationTarget=1;
            if (viewCamera == null) throw new InvalidOperationException("Fishing scene camera is missing; regenerate the prototype scene.");
            foreach (var brain in FindObjectsOfType<CinemachineBrain>()) brain.enabled=false;
            foreach (var replay in FindObjectsOfType<ReplayCamera>()) replay.enabled=false;
            var skiff=Resources.Load<Boat>("FishingBoat/SoloSkiffBoat");
            if(skiff==null)throw new InvalidOperationException("Missing SoloSkiffBoat prefab; run FishingSkiffBuilder.Build.");
            Player=Instantiate(skiff,spawnPosition,spawnRotation);
            Player.name="Fishing player boat";Player.ParticipateInRaces=false;
            Player.Setup(1,true,new BoatLivery {primaryColor=new Color(0.05f,0.65f,0.65f),trimColor=new Color(1f,0.58f,0.14f)});
            // Buoyancy initialization temporarily moves the prefab to the origin for voxel sampling.
            // Restore both the interpolated Rigidbody and Transform before the first physics step.
            Teleport(spawnPosition,spawnRotation);
            // Exactly one driver applies forces: fishing mode uses the shared input router.
            Player.GetComponent<HumanController>().enabled=false;
            if(Player.cam!=null)Player.cam.gameObject.SetActive(false);
            _input=USVInputRouter.EnsureInstance();
            AppSettings.MainCamera=viewCamera;
            if(!_xrActive)viewCamera.fieldOfView=58f;
            if(!_xrActive)viewCamera.transform.SetPositionAndRotation(spawnPosition-spawnRotation*Vector3.forward*12f+Vector3.up*7f,
                Quaternion.LookRotation(spawnRotation*Vector3.forward*16f-Vector3.up*5f));
            PaletteShader=Resources.Load<Shader>("FishingPalette");
            if(PaletteShader==null)throw new InvalidOperationException("FishingPalette shader missing.");
            // Preserve the serialized v0.5 scene and append only the new ground.
            if(fishingSpots.Length == FishingSpecies.AreaCount)
            {
                Array.Resize(ref fishingSpots, FishingGrounds.Count);
                fishingSpots[FishingGrounds.NorthIsland] = new Vector3(FishingGrounds.NorthX,0,FishingGrounds.NorthZ);
            }
            BuildWorld(); BuildEquipment();
            var fleet = new GameObject("Fleet upgrades"); fleet.transform.SetParent(Player.transform,false);
            Fleet = fleet.AddComponent<FishingFleetPresentation>(); Fleet.Build(this);
            Islands = gameObject.AddComponent<FishingIslandWorld>(); Islands.Build(this);
            Weather=gameObject.AddComponent<FishingWeather>();Weather.Controller=this;
            Ecology=gameObject.AddComponent<FishingEcology>();Ecology.Controller=this;
            View=gameObject.AddComponent<FishingViewRig>();View.Controller=this;
            Shore=gameObject.AddComponent<FishingShore>();Shore.Build(this);
            BindTackleShop();
            Expedition=gameObject.AddComponent<FishingExpeditionScene>();Expedition.Build(this);
            FieldWorld=gameObject.AddComponent<FishingFieldWorld>();FieldWorld.Build(this);
            RifleHunting=gameObject.AddComponent<FishingRifleHunting>();RifleHunting.Build(this);
            Effects=gameObject.AddComponent<FishingEffects>();Effects.Controller=this;
            WorkshopWorld=gameObject.AddComponent<FishingWorkshopWorld>();WorkshopWorld.Build(this);
            XRSeat=gameObject.AddComponent<FishingXRSeat>();XRSeat.Build(this);XRComfort=gameObject.AddComponent<FishingXRComfort>();XRComfort.Build(this);gameObject.AddComponent<FishingXRHints>().Build(this);gameObject.AddComponent<FishingXRGrab>().Build(this);
            CatchRig=gameObject.AddComponent<FishingCatchRig>();CatchRig.Build(this);
            DockConsole=gameObject.AddComponent<FishingDockConsole>();DockConsole.Build(this);
            XRNotebook=gameObject.AddComponent<FishingXRNotebook>();XRNotebook.Build(this);
            Realms=gameObject.AddComponent<FishingRealmWorld>();Realms.Build(this);
            Residents=gameObject.AddComponent<FishingResidents>();Residents.Build(this);
            Regions=gameObject.AddComponent<FishingRegionPresentation>();Regions.Build(this);
            Letter=gameObject.AddComponent<FishingLetter>();Letter.Build(this);
            WaterRoute=gameObject.AddComponent<FishingWaterRoute>();WaterRoute.Build(this);
            Home=gameObject.AddComponent<FishingHome>();Home.Build(this);
            Farm=gameObject.AddComponent<FishingFarmWorld>();Farm.Build(this);
            CoastWorld=gameObject.AddComponent<FishingV028CoastWorld>();CoastWorld.Build(this);
            HandGames=gameObject.AddComponent<FishingHandGames>();HandGames.Build(this);
            Nightscape=gameObject.AddComponent<FishingNightscape>();Nightscape.Build(this);
            Prologue=gameObject.AddComponent<FishingPrologue>();Prologue.Build(this);FishingStoryCapture.Attach(this);FishingFrameProbe.Attach(this);
            Shore.RefreshClimbRoutes();
            ConfigurePresentation();
            _feedback=gameObject.AddComponent<AudioSource>();_feedback.spatialBlend=0f;_feedback.volume=0.45f;
            _biteTone=Tone(780f,0.13f);_successTone=Tone(1150f,0.2f);
            _legendTone=Chime(new[]{523f,659f,784f,1047f},0.11f);
            _hud=gameObject.AddComponent<FishingHUD>();_hud.Controller=this;
            gameObject.AddComponent<FishingWorldInstruments>().Controller=this;
            _feedback.mute=QuietMode;
            if(LegendTrophy)BuildTrophy();
            Notice(string.IsNullOrEmpty(warning)?(_xrActive || Application.platform==RuntimePlatform.Android
                ?"欢迎来到回潮海。校准完成后将播放序章和手柄教学；B 可随时打开潮汐手册。"
                :"潮汐回响：N选择岛屿，低速G下船，E与居民交谈 / 进入潮纹秘境。J查看世界航记；店员可介绍商品，离岸可游泳。"):warning,10f);
            _initialized=true;
            InitializeSettings();
            BuildVoyageWorld();
            BuildKeeperWorld(); // V044 守灯值夜 + 退潮礁石小路
            BuildLife(); // V044 相机图鉴/掌心地图/节日
            HomeEcology=gameObject.AddComponent<FishingHomeEcologyWorld>();HomeEcology.Build(this);
            Combat=gameObject.AddComponent<FishingCombatWorld>();Combat.Build(this);
            ActionAudio=gameObject.AddComponent<FishingActionAudio>();ActionAudio.Build(this);
            WorldAudio=gameObject.AddComponent<FishingWorldAudio>();WorldAudio.Build(this);
            RegionalMusic=gameObject.AddComponent<FishingRegionalMusic>();RegionalMusic.Build(this);
            InitializeSaveSlots(legacy);
            InitializeStartMenu();
            if(AcceptanceOptions != null) { Acceptance=gameObject.AddComponent<FishingAcceptance>(); Acceptance.Controller=this; }
            else if(HasArgument("-fishing-autoplay") || HasArgument("-fishing-capture")) gameObject.AddComponent<FishingAutoplay>().Controller=this;
            if(HasArgument(FishingFeedbackCapture.EnableFlag))gameObject.AddComponent<FishingFeedbackCapture>().Begin(this);
            if(HasArgument(FishingUICapture.EnableFlag))gameObject.AddComponent<FishingUICapture>().Begin(this);
            if(FishingPicoTour.Requested)gameObject.AddComponent<FishingPicoTour>().Begin(this);
            Debug.Log("[FISHING] Prototype v"+Version+" initialized. Procedural and Hunyuan fish; desktop/XR input.");
        }

        private void Update()
        {
            if(!_initialized)return;
            bool xr=UnityEngine.XR.XRSettings.isDeviceActive;
            if(xr!=_xrActive)
            {
                if(xr && InventoryOpen)ToggleInventory();if(xr && JournalOpen)ToggleJournal();
                _xrActive=xr;Application.targetFrameRate=xr?-1:60;
                viewCamera.GetUniversalAdditionalCameraData().renderPostProcessing=!xr;
                ResetToolInput();
            }
            var k=Keyboard.current;var m=Mouse.current;var g=Gamepad.current;
            bool focused=Automated || Application.isFocused;
            TickAutosave();
            if(TickStartMenu(k)){Shore.Tick(false);AnimateEquipment();return;}
            if(HandGames.Tick(k,m,focused,xr)){Shore.Tick(false);AnimateEquipment();return;}
            if(TickHomeChart(k)){Shore.Tick(false);AnimateEquipment();return;}
            if(TickGuideOpening(k,focused)){Shore.Tick(false);AnimateEquipment();return;}
            if(TickStoryInput(k,focused,xr)){Shore.Tick(false);AnimateEquipment();return;}
            if(Life.Tick(k,m,focused,xr)){Shore.Tick(false);AnimateEquipment();return;}
            if(!Anchored || HasPendingCatch)CastMotion.Reset();
            else if(Paused || !focused)CastMotion.CancelPreparation();
            else if(Acceptance==null || !Acceptance.FreezeScenario)CastMotion.Tick(Time.deltaTime);
            // 先处理取消边界，再推进接触时钟；暂停/换物品的同帧不能先偷偷提交收益。
            if(WorldActionBusy && focused && !Automated)
            {
                if(_input.Current.reset || (k!=null && k.gKey.wasPressedThisFrame))CancelWorldAction();
                ReadFieldShortcuts(k,m);
            }
            if(TickShopInput(k,focused && !Automated,focused,xr)){Shore.Tick(false);AnimateEquipment();return;}
            bool alignmentFrame=Expedition.Aligning;
            // Opening OR closing a modal consumes the entire frame; no held input leaks into tools or movement.
            if(!Automated && focused && k!=null && k.iKey.wasPressedThisFrame)
            {ToggleInventory();Shore.ResetMoveInput();return;}
            if(InventoryOpen)
            {
                if(!xr && !Automated && _input.Current.pause)ToggleInventory();
                else if(!Automated && focused)ReadFieldShortcuts(k,null);
                Shore.Tick(false);return;
            }
            if(TickItemInspection(k,m,focused)){Shore.Tick(false);AnimateEquipment();return;}
            if(!xr && !Automated && _input.Current.pause) { if(SaveMenuOpen){if(SaveConfirmation.Length>0)CancelSaveConfirmation();else CloseSaveMenu();}else if(alignmentFrame)Expedition.Cancel();else if(JournalOpen)ToggleJournal();else if(MenuControls){MenuControls=false;SaveSettings();}else TogglePause(); }
            if(!Automated && focused && k!=null && k.jKey.wasPressedThisFrame && (!Paused || JournalOpen))ToggleJournal();
            TickWorldAction(Time.deltaTime,focused,xr);
            if(WorldActionBlocksGameplay)
            {
                if(!Paused && focused && !Automated)
                {
                    if(k!=null && k.cKey.wasPressedThisFrame)ToggleView();
                    if(!xr && _input.Current.reset)ReturnToHarbor();
                    else if(k!=null && k.gKey.wasPressedThisFrame){if(Ashore)Shore.TryBoard();else Shore.TryDisembark();}
                }
                Shore.Tick(false);AnimateEquipment();return;
            }
            bool notebookInput=!Automated && XRNotebook.Tick(focused);
            if(notebookInput || XRNotebook.IsOpen){if(XRNotebook.IsOpen)Hands.ResetInteraction();Shore.Tick(false);AnimateEquipment();return;}
            bool dockInput=DockConsole.Tick(focused && !Automated && (!Ashore || HasPendingCatch));
            if(dockInput || DockConsole.BlocksGameplay)Hands.ResetInteraction();
            if(dockInput){Shore.Tick(false);AnimateEquipment();return;}
            if(HasPendingCatch){if(!Automated && focused)ReadPendingDesktop(k);Shore.Tick(false);AnimateEquipment();return;}
            if(!DockConsole.BlocksGameplay)Hands.ReadXRTools();
            // A modal frame never falls through into boarding, surveying or boat/tool input,
            // including the frame in which E/Esc closes the dial.
            if(alignmentFrame)
            {
                if(!focused || Paused)Expedition.Cancel();
                else if(!Automated && Expedition.Aligning)
                {
                    if(!xr && _input.Current.reset)ReturnToHarbor();
                    else
                    {
                        float axis=k==null?0:((k.dKey.isPressed || k.rightArrowKey.isPressed)?1:0)-((k.aKey.isPressed || k.leftArrowKey.isPressed)?1:0);
                        Expedition.Dial.Rotate(axis*55f*Mathf.Min(Time.deltaTime,.05f));
                        if(_input.Current.interact)Expedition.Confirm();
                    }
                }
                Shore.Tick(false);AnimateEquipment();return;
            }
            bool overTools=!_xrActive && m!=null && _hud.BlocksPointer(m.position.ReadValue(),Screen.width,Screen.height);
            bool leftHeld=m!=null && m.leftButton.isPressed;
            bool rightHeld=m!=null && m.rightButton.isPressed;
            bool pointerReady=_pointerGate.Update(focused,Paused,leftHeld,rightHeld);
            bool gamepadReady=_gamepadGate.Update(focused,Paused,g!=null && g.rightShoulder.isPressed,g!=null && g.rightTrigger.ReadValue()>0.5f);
            if(!Paused && focused && !Automated)
            {
                if(k!=null && k.cKey.wasPressedThisFrame)ToggleView();
                if(k!=null && k.vKey.wasPressedThisFrame)View.ResetLook();
                if(k!=null && k.hKey.wasPressedThisFrame)ToggleDetails();
                if(k!=null && k.tabKey.wasPressedThisFrame)ToggleHarborDesk();
                if(k!=null && k.bKey.wasPressedThisFrame)CycleBait();
                if(k!=null && k.nKey.wasPressedThisFrame)CycleNavigation();
                if(k!=null && k.oKey.wasPressedThisFrame)DeliverOrder();
                if(k!=null && k.tKey.wasPressedThisFrame){if(Ashore)FieldWorld.Observe();else Ecology.Observe();}
                if(k!=null && k.fKey.wasPressedThisFrame && Ashore)Islands.ConfirmNearby();
                ReadFieldShortcuts(k,m);
                if(!xr && _input.Current.reset)ReturnToHarbor();
                if(k!=null && k.qKey.wasPressedThisFrame)LeaveStation();
                if(k!=null && k.gKey.wasPressedThisFrame){if(Ashore)Shore.TryBoard();else Shore.TryDisembark();}
                if(k!=null && k.uKey.wasPressedThisFrame)BuyUpgrade(UpgradeTrack.Line);
                if(k!=null && k.xKey.wasPressedThisFrame)SellAll();
                if(!xr && _input.Current.interact && !DockConsole.BlocksGameplay && !(_xrActive && Hands.HoldingRod))RequestWorldInteraction();
                if(ShopBlocksGameplay || StoryBlocksGameplay){Shore.Tick(false);AnimateEquipment();return;}
                bool press=(pointerReady && !overTools && !rightHeld && m!=null && m.leftButton.wasPressedThisFrame) || (gamepadReady && g!=null && g.rightShoulder.wasPressedThisFrame);
                TickDesktopCast(press,pointerReady,overTools,rightHeld,m);

                if(Ashore && (k==null || (!k.leftAltKey.isPressed && !k.rightAltKey.isPressed)))RifleHunting.ReadDesktopInput((pointerReady && !overTools && leftHeld) || (gamepadReady && g!=null && g.rightShoulder.isPressed),(pointerReady && !overTools && rightHeld) || (gamepadReady && g!=null && g.leftTrigger.ReadValue()>.5f));
            }
            Shore.Tick(focused && !Automated && !Expedition.Aligning);
            UpdateNearestSpot();
            if(!AtHarbor)HarborDeskOpen=false;
            _reeling=false;
            if(Anchored && !Paused && focused)
            {
                bool reel=Automated?AutomationReel:_xrActive?Hands.XRReeling:(pointerReady && DesktopReeling)||(gamepadReady && g!=null && g.rightTrigger.ReadValue()>0.5f);
                _reeling=reel && Session.Phase==CatchPhase.Fighting;
                RodControl=ReadRodControl(k,g,pointerReady,gamepadReady);
                if(!CastMotion.Casting && (Acceptance == null || !Acceptance.FreezeScenario)) Session.Tick(Time.deltaTime,reel,RodControl);
                if(Session.Phase!=_previousPhase)
                {
                    if(Session.Phase==CatchPhase.Bite)
                    {
                        WorldAudio?.Play("FishBite",_bobber.transform.position,.55f);WorldAudio?.Play("BitePing",_bobber.transform.position,.6f);
                        if(_xrActive)Hands?.BiteHaptic();
                        bool legend=Session.Species.Legendary;
                        bool special=legend || Session.Rare || FishingSpecies.SizePermille(Session.Kind,Session.Grams)>=850;
                        _feedback.PlayOneShot(special?_legendTone:_biteTone);
                        if(special){Effects?.Burst(_bobber.transform.position,new Color(.7f,.9f,1f),true);Hands?.HookGesture();}
                        if(legend)Notice("金帆旗鱼咬钩了！提竿后看到“跃出水面”立刻松开，别硬拉。",7f);
                    }
                    if(Session.Phase==CatchPhase.Escaped)Notice(FishingText.Failure(Session.Failure,_xrActive),8f);
                    _previousPhase=Session.Phase;
                }
                // Caught fish remain pending until an explicit store/release.
            }
            AnimateEquipment();
        }

        private void TickDesktopCast(bool press,bool pointerReady,bool overTools,bool rightHeld,Mouse mouse)
        {
            if(!_xrActive)TickDesktopAngling(mouse,pointerReady && !rightHeld,overTools);
            if(press && Anchored && (mouse==null || !mouse.leftButton.wasPressedThisFrame))
            {
                if(Session.Phase==CatchPhase.Bite)Hook();
                else if(Session.Phase==CatchPhase.Landing)CatchRig.RequestLiftForInspection();
                else if(CanChangeBait && !CastMotion.Casting)Cast();
            }
        }

        private void FixedUpdate()
        {
            if(!_initialized)return;
            bool blocked=Paused || (!Automated && !Application.isFocused) || Ashore || Anchored || HasPendingCatch || InventoryOpen || JournalOpen
                || DockConsole.BlocksGameplay || XRNotebook.BlocksGameplay || ShopBlocksGameplay || StoryBlocksGameplay;
            Boost.Tick(Time.fixedDeltaTime,_input.Current.boost && _input.Current.throttle>0 && _input.Current.brake<=0 && Player.engine.InWater,blocked);
            if(Paused)return;
            var rb=Player.engine.RB;
            if(Ashore){Shore.HoldBoat();return;}
            if(_xrActive && (XRNotebook.BlocksGameplay || StoryBlocksGameplay))
            {
                rb.velocity=new Vector3(0,rb.velocity.y,0);
                rb.angularVelocity=new Vector3(rb.angularVelocity.x,0,rb.angularVelocity.z);
                return;
            }
            if(Anchored)
            {
                var p=rb.position;
                rb.AddForce(new Vector3(_anchor.x-p.x,0,_anchor.z-p.z)*3f-new Vector3(rb.velocity.x,0,rb.velocity.z)*2f,ForceMode.Acceleration);
                // Hold yaw only; vertical water response remains simulated.
                var yaw=Quaternion.Euler(0,_anchorRotation.eulerAngles.y,0);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation,yaw,Time.fixedDeltaTime*2f));
                return;
            }
            if(HasPendingCatch || InventoryOpen || JournalOpen || DockConsole.BlocksGameplay || XRNotebook.BlocksGameplay || ShopBlocksGameplay || StoryBlocksGameplay)return;
            var state=_input.Current;
            float comfort=ComfortMode?0.65f:1f;
            Player.engine.Accelerate(state.throttle*comfort, Boost.Active);Player.engine.Turn(state.steering*comfort);
            float braking=state.brake; // S is reverse thrust; only Shift/explicit brake decelerates velocity.
            if(braking>0f)
            {
                var v=rb.velocity;var h=Vector3.MoveTowards(new Vector3(v.x,0,v.z),Vector3.zero,7f*braking*Time.fixedDeltaTime);
                rb.velocity=new Vector3(h.x,v.y,h.z);
            }
        }

        private void LateUpdate()
        {
            if(_initialized)
            {
                PresentCharacterFrame(Time.deltaTime);
                Angling.PresentRod(AnglingAvailable?Time.deltaTime:0);
                if(_line.enabled)
                {
                    var target=_line.GetPosition(24);
                    for(int i=0;i<25;i++){float u=i/24f;_line.SetPosition(i,Vector3.Lerp(_rodTip.position,target,u)-Vector3.up*Mathf.Sin(u*Mathf.PI)*(Session.Phase==CatchPhase.Fighting?.15f:.5f));}
                }
            }
        }
        public void PresentCharacterFrame(float deltaTime=0)
        {
            if(!_initialized)return;
            XRSeat.Apply();Shore.SyncRider();Home.PresentSleepPose();Shore.ApplyTraversalPose();View.ApplyView();Hands.Apply();FieldWorld.ApplyHeldTool();XRNotebook.SampleTool();ApplyWorldActionPose();CatchRig.Apply(deltaTime);
            RifleHunting.Present();HandGames.Present();Realms.PresentObservation();Farm.ApplyPresentation();Home.ApplyOrchardPresentation();
            // 全部工具和掌心目标落位后才解同源骨架；随后只读取竿尖画线，不再次采样。
            PortableChart?.Present();
            Shore.Character.ApplyHandTargets(Hands.Left,Hands.Right,UnityEngine.XR.XRSettings.isDeviceActive,PortableChart!=null && PortableChart.Active);
            if(Acceptance!=null && Acceptance.FreezeScenario && (Acceptance.CurrentCase==FishingAcceptanceCase.ArchIslet || Acceptance.CurrentCase==FishingAcceptanceCase.FireflyIslet))
            {
                var island=FishingCoastalPresentation.Islets[Acceptance.CurrentCase==FishingAcceptanceCase.ArchIslet?0:1];
                var p=island+new Vector3(39,22,42);viewCamera.transform.SetPositionAndRotation(p,Quaternion.LookRotation(island+Vector3.up*2-p));
            }
            if(Acceptance!=null && Acceptance.FreezeScenario && Acceptance.CurrentCase==FishingAcceptanceCase.HomeMoonlight)
            {
                var island=Home.Origin;var position=island+new Vector3(26,8,-61);var target=island+new Vector3(-9,17,10);
                viewCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            }
            Shore.Character.SetOpticalView(View.OpticalView || ((Letter.Open || Prologue.Open || Realms.ObservingStars) && !UnityEngine.XR.XRSettings.isDeviceActive));DockConsole.Present();
        }
        public void ToggleView()
        {
            if(Paused)return;
            View.Toggle();
            Notice(Ashore?"岸上视角 · 移动鼠标转头 / Alt 操作界面 / V 回正 / C 切换":FirstPerson?"第一人称 · 右键环顾 / V 视线回正 / C 切换":"第三人称 · 右键环绕 / V 回正 / C 切换",4f);
        }
        public void TogglePause()
        {
            if(!Paused){Save();_resumeTimeScale=Time.timeScale;Time.timeScale=0f;Paused=true;}
            else{Time.timeScale=_resumeTimeScale;Paused=false;}
            ResetToolInput();SaveSettings();ApplyAudioSettings();
        }
        private void ResetToolInput()
        {
            HandGames?.Cancel();
            Realms?.ResetInteraction();
            RodControl=0;_rodControlReady=false;_mouseAngling.Reset();Angling?.Tick(0,false,false);
            EndItemInspection();

            CancelWorldAction();FieldWorld?.ClearToolPresentation();
            TackleShop?.Close();
            Residents?.Close();
            if(Expedition!=null)Expedition.Cancel();
            _pointerGate.Reset();_gamepadGate.Reset();
            if(View!=null)View.ResetInput();
            if(Shore!=null)Shore.ResetMoveInput();
            if(CatchRig!=null)CatchRig.Recover();
            if(Hands!=null)Hands.ResetInteraction();
            BinocularsRaised=false;
        }
        private void OnApplicationFocus(bool focused){if(!focused){ResetToolInput();SaveOnSuspend();}}
        private void OnApplicationPause(bool paused){if(paused){ResetToolInput();SaveOnSuspend();}}
        private void OnApplicationQuit()=>SaveOnSuspend();
        private void SaveOnSuspend(){if(_initialized){SaveSettings();if(!Automated)Save();}}
        private void ConfigurePresentation()
        {
            // The grade is isolated to this desktop camera, not the original race or XR camera.
            if(UnityEngine.XR.XRSettings.isDeviceActive)return;
            var data=viewCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.volumeLayerMask=1<<30;
            data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;
            var grade=new GameObject("Fishing-only color grade");grade.layer=30;grade.transform.SetParent(transform);
            var volume=grade.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Fishing desktop grade";_owned.Add(profile);volume.sharedProfile=profile;
            var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);_owned.Add(tone);
            var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(0.15f);color.contrast.Override(5f);color.saturation.Override(4f);_owned.Add(color);
            var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.24f);bloom.threshold.Override(1.2f);bloom.scatter.Override(.55f);_owned.Add(bloom);
            // Deliberately omit depth of field, motion blur and chromatic distortion.
        }

        public void Interact()
        {
            if(Paused)return;
            if(ShopOpen){TackleShop.Close();return;}
            if(ShopBlocksGameplay)return;
            if(Ashore)
            {
                if(TackleShop != null && TackleShop.NearCounter && ToggleShop())return;
                if(Hands!=null && !Automated)Hands.ReachGesture(Shore.Position+viewCamera.transform.forward*.7f+Vector3.up*1.1f);
                bool hadComponent=Career.Expedition.Has(FishingExpedition.ComponentFound);
                if(!Expedition.Interact() && !WorkshopWorld.Interact() && !FieldWorld.TryPickup())Shore.Survey();
                if(!hadComponent && Career.Expedition.Has(FishingExpedition.ComponentFound))Hands.PresentPickup();
                return;
            }
            if(!Anchored && SeaMarket!=null && SeaMarket.TryInteract())return; // V044 海上集市船：注视货品按 E / A 交易
            UpdateNearestSpot();
            if(Anchored){if(Session.Phase==CatchPhase.Landing)Scoop();return;}
            if(AtHarbor){int voyage=Islands.CompleteAtHarbor();int survey=ClaimShoreRewards();if(Career.OrderReady)DeliverOrder();else if((survey==0 && voyage==0) || Career.SaleValue>0)Sell();return;}
            if(!FishingGrounds.AvailableForCareer(Spot,Career)){Notice("北岛渔场尚未开放：完成浅湾图鉴调查，或修复旧港信标。J 查看调查进度。");return;}
            if(SpotDistance>22f){Notice("先驶入任一黄色渔区，再按 E 停船。");return;}
            if(Speed>3f){Notice("速度过快，按住 Shift 刹车再停船。");return;}
            Anchored=true;ChartExpanded=false;ShowDetails=false;HarborDeskOpen=false;_anchor=Player.engine.RB.position;_anchorRotation=Player.transform.rotation;
            var v=Player.engine.RB.velocity;Player.engine.RB.velocity=new Vector3(0,v.y,0);
            Session.Cancel();_previousPhase=CatchPhase.Idle;
            Notice(_xrActive?"已停船 · A 快捷拿竿并抛出，或右握把握住竿柄。\n拿竿后按住右扳机蓄力、松开抛竿；按下右摇杆收竿起航。":"已停船。按住左键后摆，松开甩竿；轻点也可近抛。Q 起航。",8f);
        }
        public bool Cast(float power = .55f)
        {
            if(HasPendingCatch || Ashore || !Anchored || Paused || !FishingGrounds.AvailableForCareer(Spot,Career))return false;
            if(Career.Count>=Career.Capacity){Notice("鱼箱已满！按 Q 起航，返港按 E 出售。");return false;}
            var weather=Weather.Climate.Current;
            var kind=Automated?FishingGrounds.Species(Spot):FishingSpecies.Select(Spot,Career.Calendar,weather,UnityEngine.Random.value);
            bool legend=LegendRules.CanStrike(Spot,weather,Bait,Career.LineLevel)
                && (ForceLegend || FishingSurvey.LegendCoordinates(Career) || UnityEngine.Random.value<LegendRules.StrikeChance);
            bool rare=!legend && UnityEngine.Random.value<Mathf.Clamp01(FishingVoyage.RareChance(Bait)+BayClimate.RareBonus(weather,kind));
            if(legend)kind=FishKind.Sailfish;
            SeaEvents?.AdjustCatch(Spot,ref kind,ref rare,legend); // V044 海上事件：鱼群沸腾/夜光海
            // Two uniform rolls: average fish are common and record-sized fish stay rare.
            float size=(UnityEngine.Random.value+UnityEngine.Random.value)*0.5f;
            bool cast=Session.Cast(kind,rare,Career.LineLevel,Guid.NewGuid().ToString("N"),Bait,weather,size,Career.ReelLevel);
            if(cast)
            {
                CastMotion.Launch(power);_castOrigin=_rodTip.position;_castSplash=false;
                Angling.BeginCast(power);
                ActionAudio?.Detail("RodCast",_rodTip.position);
                _castGround=Spot;_previousPhase=CatchPhase.Waiting;
                _fish.SetActive(false);Destroy(_fish);
                _fish=MakeFish(transform,kind,rare);
                if(!legend && LegendRules.Conditions(Spot,weather,Bait) && Career.LineLevel==0)
                    Notice("水下掠过一道金色的影子……鱼线太细，它不屑一顾。回港升级鱼线吧。",7f);
            }
            return cast;
        }
        public bool Hook()
        {
            if(!Anchored || Paused || !Session.Hook())return false;
            WorldAudio?.Play("FishHook",_bobber.transform.position,.5f);
            Hands?.HookGesture();return true;
        }
        public bool Scoop()=>CatchRig!=null && CatchRig.RequestScoop();
        public void LeaveStation()
        {
            if(Paused || Ashore)return;if(HasPendingCatch){Notice("先将待处理鱼获入箱或放生。",4);return;}
            Anchored=false;ChartExpanded=true;HarborDeskOpen=false;ShowDetails=false;Session.Cancel();CastMotion.Reset();AutomationReel=_reeling=false;
            Hands?.ResetInteraction();CatchRig?.Recover();
            Notice(UnityEngine.XR.XRSettings.isDeviceActive?"已收竿起航 · 左摇杆前进 / 倒车，右摇杆掌舵，左扳机刹车。":"工具已收起，W 前进，A/D 转向，S 倒航 / Shift 刹车；青色港口按 E 交易。");
        }
        public int Sell()
        {
            if(!AtHarbor || Paused)return 0;
            int reserved=Career.ReservedCount;
            int sold=Career.Sell();
            if(sold>0)
            {
                Save();RefreshHold();_feedback.PlayOneShot(_successTone);WorldAudio?.Play("CoinGain",ExplorerPosition,.6f);
                Notice("出售成功 +"+sold+" 金币"+(reserved>0?"，已为委托保留 "+reserved+" 条鱼（X 可全部出售）。":"！金币可在沙滩钓具店或港口购买装备。"),7f);
            }
            else Notice(reserved>0?"鱼货都在为委托保留。凑齐后按 E 交付，或按 X 全部出售。":"鱼箱还是空的。前往黄色渔区捕捞，带回这里出售。");
            return sold;
        }
        public int SellAll()
        {
            if(!AtHarbor || Paused){Notice("请先回到青色港口再出售。");return 0;}
            int sold=Career.SellAll();
            if(sold>0){Save();RefreshHold();_feedback.PlayOneShot(_successTone);WorldAudio?.Play("CoinGain",ExplorerPosition,.6f);Notice("全部出售 +"+sold+" 金币。",6f);}
            else Notice("鱼箱还是空的。");
            return sold;
        }
        public bool BuyUpgrade(UpgradeTrack track)
        {
            if(Paused || InventoryOpen || JournalOpen || HasPendingCatch || (!AtHarbor && (TackleShop == null || !TackleShop.CanPurchase)))
            {Notice("请到港口交易台，或登岸进入钓具店柜台后购买。");return false;}
            if(Career.SpecializationBlocked(track)){Notice("二级专精只能选择载重、动力或操控中的一项；其他两项可升至一级。",7);return false;}
            int cost=Career.NextUpgradeCost(track);
            if(cost<=0){Notice(FishingText.UpgradeName(track)+"已经升到满级。",4f);return false;}
            bool ok=Career.Upgrade(track);
            if(ok)
            {
                Fleet.Refresh();Save();if(track==UpgradeTrack.Hold)RefreshHold();ActionAudio?.Detail("ShopPurchase",ExplorerPosition+Vector3.up*.7f);Effects?.Celebrate(ExplorerPosition+Vector3.up*.3f);WorldAudio?.Play("RareShimmer",ExplorerPosition+Vector3.up*.7f,.4f);
                int level=Career.Level(track);
                if(ShopOpen)Hands.ReachGesture(Shore.Character.HandReferencePosition+Shore.Actor.forward*.28f);
                Notice(FishingText.UpgradeName(track)+" 升至 Lv."+level+" · "+FishingText.UpgradeEffect(track,level)+" · "+(CreativeMode?"创造模式免费购买":"花费 "+cost+" 金币，余额 "+Career.Coins),7f);
            }
            else Notice("需要 "+cost+" 金币。先捕鱼、返港出售或交付委托吧。");
            return ok;
        }
        public bool RecordWildlife(MarineKind kind)
        {
            if(Paused || !Career.Observe(kind))return false;
            Save();Notice("自然观察 + "+BayClimate.MarineName(kind)+" · 已收录日志，不捕捞野生伙伴。",6f);return true;
        }
        public void RestoreBoatChart() { ChartRangeMeters=_boatChartRange; }
        public void CloseBoatPanels() { _boatChartRange=ChartRangeMeters;ChartRangeMeters=20;ChartExpanded=true;HarborDeskOpen=false;ShowDetails=false;Session.Cancel();AutomationReel=false; }
        public bool RecordShoreTask(int task)
        {
            if(Paused || !Ashore || !Career.CompleteShoreTask(task))return false;
            Save();Notice(FishingExploration.TaskName(task)+" · 已记录！海岸调查 "+FishingExploration.CompletedTasks(Career.ShoreTaskMask)+"/8；回港 E 领取奖励。",6);
            return true;
        }
        public int ClaimShoreRewards()
        {
            if(Paused || !AtHarbor)return 0;
            int payout=Career.ClaimShoreRewards();
            if(payout>0){Save();Notice("海岸调查已提交 +"+payout+" 金币！每区 25，全图额外 60。",7);}
            return payout;
        }
        public void CycleBait()
        {
            if(Paused)return;
            if(!CanChangeBait){Notice("本次捕捞结束后才能换饵。",3f);return;}
            Bait=(BaitKind)(((int)Bait+1)%3);Notice(FishingVoyage.BaitName(Bait)+" · "+FishingVoyage.BaitHelp(Bait),4f);
        }
        public void CycleNavigation() => SelectNavigation((NavigationTarget+1)%NavigationCount);
        public bool NavigateHome()
        {
            if(Paused || Home==null || !Home.isActiveAndEnabled)return false;
            SelectNavigation(HomeNavigationIndex);ChartExpanded=true;
            if(!Ashore && FlatDistance(ExplorerPosition,Home.Origin)>ChartRangeMeters)ChartRangeMeters=320;
            Notice(UnityEngine.XR.XRSettings.isDeviceActive?"已标记回潮渔屋 · B 打开手册查看航海图。":"已标记回潮渔屋 · 按 M 查看航海图，低速靠岸后按 G 登岸。",6);
            return NavigationTarget==HomeNavigationIndex;
        }
        public void NavigateSurvey()
        {
            if(JournalOpen)ToggleJournal();
            if(Paused)return;
            XRNotebook.Close();SelectNavigation(FishingSurvey.Stage(Career)==0?1:2);
        }
        public void SelectNavigation(int target)
        {
            if(Paused || target<0 || target>=NavigationCount)return;
            NavigationTarget=target;Notice("航点："+NavigationName,3f);
        }
        public void ToggleDetails() { if(Paused)return;ShowDetails=!ShowDetails;if(ShowDetails){HarborDeskOpen=false;CancelWorldAction();} }
        public void ToggleChart() { if(!Paused)ChartExpanded=!ChartExpanded; }
        public void CycleChartRange() { if(!Paused)ChartRangeMeters=Ashore?(ChartRangeMeters==20?40:ChartRangeMeters==40?80:20):(ChartRangeMeters==80?160:ChartRangeMeters==160?320:80); }
        public void ToggleHarborDesk()
        {
            if(Paused || !AtHarbor)return;
            HarborDeskOpen=!HarborDeskOpen;if(HarborDeskOpen)ShowDetails=false;
        }
        public void ToggleJournal()
        {
            if(Prologue!=null && (Prologue.Open || Home.Sleeping))return;
            TackleShop?.Close();
            if(InventoryOpen)return;
            if(Paused && !JournalOpen)return;
            JournalOpen=!JournalOpen;if(!JournalOpen){ReadingFieldGuide=false;GuideOpenAmount=0;}TogglePause();
            WorldAudio?.Play(JournalOpen?"BookOpen":"BookClose",ExplorerPosition,.5f);
        }
        public void ToggleComfort()
        {
            ComfortMode=!ComfortMode;View.ResetLook();
            if(!HasIsolatedProfile){PlayerPrefs.SetInt("Fishing.V3.Comfort",ComfortMode?1:0);PlayerPrefs.Save();}
        }
        public void ToggleInstruments() { WorldInstruments=!WorldInstruments; }
        public void ToggleQuiet()
        {
            QuietMode=!QuietMode;ApplyAudioSettings();
            if(!HasIsolatedProfile){PlayerPrefs.SetInt("Fishing.V3.Quiet",QuietMode?1:0);PlayerPrefs.Save();}
        }
        public int DeliverOrder()
        {
            if(Paused || !AtHarbor)return 0;
            var order=Career.CurrentOrder;
            int payout=Career.DeliverOrder();
            if(payout>0){Save();RefreshHold();_feedback.PlayOneShot(_successTone);Notice("委托“"+order.Name+"”完成 +"+payout+" 金币（含鱼价及奖金）！新的委托已贴上公告板。",7f);}
            else Notice("委托鱼货尚未集齐。按 J 查看需求；按 E 出售其他鱼货。",5f);
            return payout;
        }
        public void ReturnToHarbor()
        {
            WaterRoute?.CancelPlanning();
            Prologue?.Suspend();Home?.CancelSleep();
            Letter?.Close();Residents?.Close();Realms?.Abort();
            CancelWorldAction();
            TackleShop?.Close();
            if(Paused)return;if(CatchRig!=null)CatchRig.Recover();Expedition.Cancel();if(Ashore)Shore.BoardImmediately();if(HasPendingCatch){Anchored=false;Session.Cancel();}else LeaveStation();Teleport(spawnPosition,spawnRotation);Notice("已救援回港；鱼箱和金币保留。",5f);
        }
        public void MoveToSpotForAutomation(int index)
        {
            if(!Automated)throw new InvalidOperationException("Automation-only helper.");
            Teleport(fishingSpots[index]+Vector3.up,spawnRotation);UpdateNearestSpot();
        }
        public void SetBaitForAutomation(BaitKind bait)
        {
            if(!Automated)throw new InvalidOperationException("Automation-only helper.");
            for(int i=0;i<3 && Bait!=bait;i++)CycleBait();
        }
        private void Teleport(Vector3 p,Quaternion q){WaterRoute?.CancelPlanning();var rb=Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;Player.transform.SetPositionAndRotation(p,q);rb.position=p;rb.rotation=q;Physics.SyncTransforms();}
        private void UpdateNearestSpot()
        {
            if(Anchored)return;SpotDistance=float.MaxValue;
            for(int i=0;i<fishingSpots.Length;i++){float d=FlatDistance(Player.engine.RB.position,fishingSpots[i]);if(d<SpotDistance){SpotDistance=d;Spot=i;}}
        }
        public void CommitExpedition(string message) { Save();Notice(message,9f); }
        internal void Save() => SaveProgress();
        public void Notice(string text,float seconds=5f,bool catchNotice=false){if(text!=Message && !string.IsNullOrEmpty(text))WorldAudio?.Play("UINotify",ExplorerPosition,.25f);Message=text;MessageIsCatch=catchNotice;_messageUntil=Time.unscaledTime+seconds;}
        public string VisibleMessage=>Time.unscaledTime<_messageUntil?FishingXRText.Adapt(Message):string.Empty;
        public static float FlatDistance(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        public static bool HasArgument(string arg)=>Array.IndexOf(Environment.GetCommandLineArgs(),arg)>=0;

        private void BuildWorld()
        {
            // The copied racing level bounds enclosed the bay and made the four outer islands unreachable by boat.
            foreach(var wall in FindObjectsOfType<BoxCollider>())
                if(wall.gameObject.scene==gameObject.scene && wall.transform.parent!=null && wall.transform.parent.name=="LevelBounds"
                    && wall.transform.parent.parent!=null && wall.transform.parent.parent.name=="Logic")wall.enabled=false;
            // Race buoys are particle impostors with no collision; fishing has its own physical markers.
            foreach(var raceBuoys in FindObjectsOfType<BuoyManager>())raceBuoys.gameObject.SetActive(false);
            // Race-level leftovers read as floating junk on voyages: course arrow rafts, windsurfers, paraglider and blimp.
            foreach(var t in FindObjectsOfType<Transform>(true))
                if(t.name.StartsWith("Props_ArrowSign_Dynamic") || t.name.StartsWith("Windsurfer") || t.name.StartsWith("Props_Paraglider") || t.name.StartsWith("Geo_Props_Blimp"))
                    t.gameObject.SetActive(false);
            for(int i=0;i<fishingSpots.Length;i++)
            {
                FishingArt.Ring("Fishing area "+i,transform,new Vector3(fishingSpots[i].x,0.22f,fishingSpots[i].z),9f,orange);
                var pos=fishingSpots[i]+new Vector3(0,0,11);pos.y=0f;
                _buoys.Add(FishingArt.Buoy(transform,_owned,PaletteShader,pos,.65f).transform);
            }
            FishingArt.Ring("Harbor safe zone",transform,new Vector3(harbor.x,0.25f,harbor.z),11f,teal);
            FishingDetailArt.Market(this,_owned);
            gameObject.AddComponent<FishingScenery>().Build(this,_owned);
        }

        private void BuildEquipment()
        {
            var boat=Player.transform;
            _box=FishingDetailArt.Crate(boat,new Vector3(.55f,.72f,-.28f),wood,teal,dark);
            _box.localScale=new Vector3(.42f,.6f,.42f);
            FishingDetailArt.Rod(boat,dark,wood,cream,orange,out _rodTip,out _reelCrank);
            _net=FishingDetailArt.Net(boat,dark,teal,cream);
            Hands=gameObject.AddComponent<FishingHands>();Hands.Build(this,_rodTip,_reelCrank,_net);
            _fish=MakeFish(transform,FishKind.Coral,false);
            _bobber=FishingArt.Shape("Float",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*0.18f,orange);
            _line=FishingArt.Line("Fishing line",transform,lineMaterial,0.018f);_line.positionCount=25;
            BuildAngling();
            RefreshHold();
        }
        private GameObject MakeFish(Transform parent,FishKind kind,bool rare)=>FishingArt.Fish(parent,_owned,PaletteShader,kind,rare);
        private void RefreshHold()
        {
            foreach(var f in _storedFish)Destroy(f);_storedFish.Clear();
            int i=0;foreach(var record in Career.Inventory)
            {
                var f=MakeFish(_box,record.kind,record.rare);
                // Six fish per layer; an upgraded hold stacks a second layer instead of spilling over the crate.
                int slot=i%6,layer=i/6;
                f.transform.localPosition=new Vector3((slot%3-1)*0.45f,0.28f+layer*0.17f,(slot/3-0.5f)*0.4f);f.transform.localRotation=Quaternion.Euler(0,90,80);f.transform.localScale=Vector3.one*0.55f;_storedFish.Add(f);i++;
            }
        }
        /// <summary>A golden-sail weather vane on the lighthouse marks the first released legend.</summary>
        private void BuildTrophy()
        {
            if(_trophy!=null)return;
            _trophy=new GameObject("Golden sail trophy vane");_trophy.transform.SetParent(transform,false);
            _trophy.transform.position=harbor+new Vector3(5.4f,8.35f,2.5f);
            var fish=MakeFish(_trophy.transform,FishKind.Sailfish,false);
            fish.transform.localScale=Vector3.one*0.8f;fish.transform.localRotation=Quaternion.Euler(-12f,0,0);
        }
        private void AnimateEquipment()
        {
            var phase=Session.Phase;
            if(_reeling && !Paused && Anchored && phase==CatchPhase.Fighting && !UnityEngine.XR.XRSettings.isDeviceActive)
                _reelCrank.Rotate(Vector3.right,Time.deltaTime*300f,Space.Self);
            bool active=Anchored && phase!=CatchPhase.Idle && phase!=CatchPhase.Escaped;
            _line.enabled=active && phase!=CatchPhase.Caught;_bobber.SetActive(active && (phase==CatchPhase.Waiting || phase==CatchPhase.Bite));
            if(!HasPendingCatch)_fish.SetActive(active && (phase==CatchPhase.Fighting || phase==CatchPhase.Landing || (phase==CatchPhase.Caught && Time.time-_caughtAt<0.8f)));
            var t=Player.transform;var yaw=Quaternion.Euler(0,t.eulerAngles.y,0);
            bool legend=Session.Species.Legendary;
            var target=Angling.Target(phase);
            float leap=phase==CatchPhase.Fighting?Session.LeapProgress:0f;
            // The legend clears the water in a readable arc; the HUD warns before the line would snap.
            if(leap>0f)target.y=0.2f+Mathf.Sin(leap*Mathf.PI)*2.4f;
            if(phase==CatchPhase.Landing)target=LandingTarget;
            if(phase==CatchPhase.Waiting && CastMotion.Casting)
            {
                float progress=CastMotion.FlightProgress;
                target=Vector3.Lerp(_castOrigin,target,progress)+Vector3.up*Mathf.Sin(progress*Mathf.PI)*(1+CastMotion.Power);
            }
            else if(phase==CatchPhase.Waiting && !_castSplash)
            {
                _castSplash=true;Effects?.Splash(target,.6f);
                ActionAudio?.Detail("FloatSplash",target);
            }
            _bobber.transform.position=target;
            TickAnglingPresentation();
            // 入网前由钓鱼轨迹驱动；待处理鱼只交给 CatchRig，避免两套姿态互相覆盖。
            if(!HasPendingCatch)
            {
                _fish.transform.position=phase==CatchPhase.Caught?Vector3.Lerp(_catchStart,_box.position+Vector3.up*0.5f,Mathf.Clamp01((Time.time-_caughtAt)/0.7f))+Vector3.up*Mathf.Sin(Mathf.Clamp01((Time.time-_caughtAt)/0.7f)*Mathf.PI)*0.8f:target;
                Effects?.FishSurface(target,phase,Session.Distance,Session.Beat);
                _fish.transform.rotation=leap>0f?yaw*Quaternion.Euler(-Mathf.Cos(leap*Mathf.PI)*50f,-90f,Mathf.Sin(leap*Mathf.PI*2f)*20f)
                    :yaw*Quaternion.Euler(Mathf.Sin(Time.time*7)*18f,-90+Mathf.Sin(Time.time*9)*22f,Mathf.Sin(Time.time*6)*14f);
                _fish.transform.localScale=Vector3.one*FishingSpecies.SizeScale(Session.Kind,Session.Grams);
            }
            for(int i=0;i<25;i++){float u=i/24f;_line.SetPosition(i,Vector3.Lerp(_rodTip.position,target,u)-Vector3.up*Mathf.Sin(u*Mathf.PI)*(phase==CatchPhase.Fighting?0.15f:0.5f));}
            // Stow the basket alongside the hull until it is needed, rather than covering the fish.
            _netDeployment=Mathf.MoveTowards(_netDeployment,Anchored && (phase==CatchPhase.Landing || _netMotion>0f)?1f:0f,Time.deltaTime*4f);
            _netMotion=Mathf.MoveTowards(_netMotion,0,Time.deltaTime*2.8f);
            // 网的 Transform 统一由 CatchRig 在最终物体阶段写入。
            if(_trophy!=null)_trophy.transform.rotation=Quaternion.Euler(0,Mathf.Sin(Time.time*0.23f)*70f+Time.time*6f,0);
            // Marker buoys ride a gentle swell; scaled time keeps them still while paused.
            for(int i=0;i<_buoys.Count;i++)
            {
                float swell=Time.time*1.3f+i*1.7f;var b=_buoys[i];var p=b.position;p.y=Mathf.Sin(swell)*0.07f;
                b.SetPositionAndRotation(p,Quaternion.Euler(Mathf.Sin(swell*0.8f)*4f,i*37f,Mathf.Cos(swell*0.9f)*4f));
            }
        }
        private AudioClip Tone(float frequency,float seconds)
        {
            int samples=(int)(22050*seconds);var data=new float[samples];for(int i=0;i<samples;i++)data[i]=Mathf.Sin(i*frequency*Mathf.PI*2/22050f)*Mathf.Sin(i/(float)samples*Mathf.PI)*0.25f;
            var clip=AudioClip.Create("Original prototype feedback tone",samples,1,22050,false);clip.SetData(data,0);_owned.Add(clip);return clip;
        }
        /// <summary>Rising arpeggio reserved for the legend, so its bite is recognisable by ear alone.</summary>
        private AudioClip Chime(float[] notes,float noteSeconds)
        {
            int per=(int)(22050*noteSeconds);var data=new float[per*notes.Length];
            for(int n=0;n<notes.Length;n++)for(int i=0;i<per;i++)
                data[n*per+i]=Mathf.Sin(i*notes[n]*Mathf.PI*2/22050f)*Mathf.Sin(i/(float)per*Mathf.PI)*0.22f;
            var clip=AudioClip.Create("Original legend chime",data.Length,1,22050,false);clip.SetData(data,0);_owned.Add(clip);return clip;
        }
        private void OnDestroy()
        {
            CloseHomeChart(true);
            RestoreAudioSettings();
            if(Paused)Time.timeScale=_resumeTimeScale;
            Application.targetFrameRate=_previousTargetFrameRate;
            foreach(var o in _owned)if(o!=null)Destroy(o);
        }
    }
}
