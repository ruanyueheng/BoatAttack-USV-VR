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
    /// <summary>The most recent landed fish, for the HUD's catch card.</summary>
    public struct CatchCard
    {
        public CatchRecord Fish;
        public float ShownAt;
        public bool Discovery;
        public bool PersonalBest;
        public int Bounty;
    }

    [DefaultExecutionOrder(100)]
    public sealed partial class FishingPrototype : MonoBehaviour
    {
        public const string Version = "0.4.3";
        public Boat boatPrefab;
        public Camera viewCamera;
        public Vector3 spawnPosition;
        public Quaternion spawnRotation = Quaternion.identity;
        public Vector3 harbor;
        public Vector3[] fishingSpots = new Vector3[3];
        public Material[] fishMaterials;
        public Material wood, cream, dark, orange, teal, lineMaterial;
        public Boat Player { get; private set; }
        public FishingCareer Career { get; private set; }
        public CatchSession Session { get; } = new CatchSession();
        public bool Anchored { get; private set; }
        public bool Paused { get; private set; }
        public bool Automated { get; private set; }
        public FishingViewRig View { get; private set; }
        public FishingShoreWalker Shore { get; private set; }
        public bool IsAshore => Shore != null && Shore.IsAshore;
        public Vector3 ObserverPosition => IsAshore ? Shore.Position : Player.transform.position;
        public FishingWeather Weather { get; private set; }
        public FishingEcology Ecology { get; private set; }
        public Shader PaletteShader { get; private set; }
        public bool FirstPerson => View != null && View.FirstPerson;
        public bool ShowDetails { get; private set; }
        public bool ChartExpanded { get; private set; } = true;
        public int ChartRangeMeters { get; private set; } = 160;
        public bool HarborDeskOpen { get; private set; }
        public BaitKind Bait { get; private set; }
        public bool JournalOpen { get; private set; }
        public bool ComfortMode { get; private set; } = true;
        public bool WorldInstruments { get; private set; }
        public bool QuietMode { get; private set; }
        public int NavigationTarget { get; private set; }
        public Vector3 NavigationPosition => NavigationTarget == 0 ? harbor : fishingSpots[NavigationTarget - 1];
        public string NavigationName => NavigationTarget == 0 ? "潮汐渔港" : FishingSpecies.AreaName(NavigationTarget - 1);
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
        public bool AtHarbor => Player != null && FlatDistance(ObserverPosition,harbor) < 22f && Speed < 3f && !Anchored;
        public float SpotDistance { get; private set; }
        private USVInputRouter _input;
        private FishingHUD _hud;
        private Vector3 _anchor, _catchStart;
        private Quaternion _anchorRotation;
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<GameObject> _storedFish = new List<GameObject>();
        private readonly List<Transform> _buoys = new List<Transform>();
        private GameObject _fish, _bobber, _trophy;
        private Transform _rodTip, _net, _box, _reelCrank;
        private LineRenderer _line;
        private float _netMotion, _caughtAt, _messageUntil;
        private CatchPhase _previousPhase;
        private AudioSource _feedback;
        private AudioClip _biteTone, _successTone, _legendTone;
        private bool _recorded, _initialized;
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
            _previousTargetFrameRate=Application.targetFrameRate;
            _xrActive=UnityEngine.XR.XRSettings.isDeviceActive;Application.targetFrameRate=_xrActive?-1:60;
            ReadAcceptanceOptions();
            Automated = Application.isBatchMode || HasArgument("-fishing-capture") || HasArgument("-fishing-autoplay") || (AcceptanceOptions != null && AcceptanceOptions.Automatic);
            SavePath = AcceptanceOptions != null ? AcceptanceOptions.ProfilePath(Application.temporaryCachePath, false)
                : Path.Combine(Automated ? Application.temporaryCachePath : Application.persistentDataPath,
                Automated ? "fishing-test-" + Guid.NewGuid().ToString("N") + ".json" : "boatattack-fishing-v1.json");
            Career = FishingSaveStore.Load(SavePath, out var warning);
            ComfortMode=HasIsolatedProfile || PlayerPrefs.GetInt("Fishing.V3.Comfort",1)==1;
            QuietMode=!HasIsolatedProfile && PlayerPrefs.GetInt("Fishing.V3.Quiet",0)==1;
            NavigationTarget=1;
            if (viewCamera == null) throw new InvalidOperationException("Fishing scene camera is missing; regenerate the prototype scene.");
            foreach (var brain in FindObjectsOfType<CinemachineBrain>()) brain.enabled=false;
            foreach (var replay in FindObjectsOfType<ReplayCamera>()) replay.enabled=false;
            Player=Instantiate(boatPrefab,spawnPosition,spawnRotation);
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
            BuildWorld(); BuildEquipment();
            Weather=gameObject.AddComponent<FishingWeather>();Weather.Controller=this;
            Ecology=gameObject.AddComponent<FishingEcology>();Ecology.Controller=this;
            View=gameObject.AddComponent<FishingViewRig>();View.Controller=this;
            Shore=gameObject.AddComponent<FishingShoreWalker>();Shore.Controller=this;
            ConfigurePresentation();
            _feedback=gameObject.AddComponent<AudioSource>();_feedback.spatialBlend=0f;_feedback.volume=0.45f;
            _biteTone=Tone(780f,0.13f);_successTone=Tone(1150f,0.2f);
            _legendTone=Chime(new[]{523f,659f,784f,1047f},0.11f);
            _hud=gameObject.AddComponent<FishingHUD>();_hud.Controller=this;
            gameObject.AddComponent<FishingWorldInstruments>().Controller=this;
            _feedback.mute=QuietMode;
            if(LegendTrophy)BuildTrophy();
            Notice(string.IsNullOrEmpty(warning)?"N 选择航点，驶近浮标后减速按 E 停船。M 收起地图，H 展开详情，J 查看图鉴。":warning,10f);
            _initialized=true;
            if(AcceptanceOptions != null) { Acceptance=gameObject.AddComponent<FishingAcceptance>(); Acceptance.Controller=this; }
            else if(HasArgument("-fishing-autoplay") || HasArgument("-fishing-capture")) gameObject.AddComponent<FishingAutoplay>().Controller=this;
            Debug.Log("[FISHING] Prototype v"+Version+" initialized. Original procedural fish; desktop preview only.");
        }

        private void Update()
        {
            if(!_initialized)return;
            bool xr=UnityEngine.XR.XRSettings.isDeviceActive;
            if(xr!=_xrActive)
            {
                _xrActive=xr;Application.targetFrameRate=xr?-1:60;
                viewCamera.GetUniversalAdditionalCameraData().renderPostProcessing=!xr;
                ResetToolInput();
            }
            var k=Keyboard.current;var m=Mouse.current;var g=Gamepad.current;
            bool focused=Automated || Application.isFocused;
            if(!Automated && _input.Current.pause) { if(JournalOpen)ToggleJournal();else TogglePause(); }
            if(!Automated && focused && k!=null && k.jKey.wasPressedThisFrame && (!Paused || JournalOpen))ToggleJournal();
            bool overTools=!_xrActive && m!=null && _hud.BlocksPointer(m.position.ReadValue(),Screen.width,Screen.height);
            bool leftHeld=m!=null && m.leftButton.isPressed;
            bool rightHeld=m!=null && m.rightButton.isPressed;
            bool pointerReady=_pointerGate.Update(focused,Paused,leftHeld,rightHeld);
            bool gamepadReady=_gamepadGate.Update(focused,Paused,g!=null && g.rightShoulder.isPressed,g!=null && g.rightTrigger.ReadValue()>0.5f);
            if(!Paused && focused && !Automated)
            {
                if(k!=null && k.cKey.wasPressedThisFrame)ToggleView();
                if(k!=null && k.gKey.wasPressedThisFrame)Shore.Toggle();
                if(k!=null && k.vKey.wasPressedThisFrame)View.ResetLook();
                if(k!=null && k.hKey.wasPressedThisFrame)ToggleDetails();
                if(k!=null && k.mKey.wasPressedThisFrame)ToggleChart();
                if(k!=null && k.tabKey.wasPressedThisFrame)ToggleHarborDesk();
                if(k!=null && k.bKey.wasPressedThisFrame)CycleBait();
                if(k!=null && k.nKey.wasPressedThisFrame)CycleNavigation();
                if(k!=null && k.oKey.wasPressedThisFrame)DeliverOrder();
                if(k!=null && k.tKey.wasPressedThisFrame)Ecology.Observe();
                if(_input.Current.reset)ReturnToHarbor();
                if(k!=null && k.qKey.wasPressedThisFrame)LeaveStation();
                if(k!=null && (k.uKey.wasPressedThisFrame || k.digit1Key.wasPressedThisFrame))BuyUpgrade(UpgradeTrack.Line);
                if(k!=null && k.digit2Key.wasPressedThisFrame)BuyUpgrade(UpgradeTrack.Reel);
                if(k!=null && k.digit3Key.wasPressedThisFrame)BuyUpgrade(UpgradeTrack.Hold);
                if(k!=null && k.xKey.wasPressedThisFrame)SellAll();
                if(_input.Current.interact)Interact();
                bool press=(pointerReady && !overTools && !rightHeld && m!=null && m.leftButton.wasPressedThisFrame) || (gamepadReady && g!=null && g.rightShoulder.wasPressedThisFrame);
                if(press && Anchored){if(Session.Phase==CatchPhase.Bite)Hook();else Cast();}
            }
            Shore.Tick();
            UpdateNearestSpot();
            if(!AtHarbor)HarborDeskOpen=false;
            _reeling=false;
            if(Anchored && !Paused && focused)
            {
                bool reel=Automated?AutomationReel:(pointerReady && !overTools && !rightHeld && leftHeld)||(gamepadReady && g!=null && g.rightTrigger.ReadValue()>0.5f);
                _reeling=reel && Session.Phase==CatchPhase.Fighting;
                if(Acceptance == null || !Acceptance.FreezeScenario) Session.Tick(Time.deltaTime,reel);
                if(Session.Phase!=_previousPhase)
                {
                    if(Session.Phase==CatchPhase.Bite)
                    {
                        bool legend=Session.Species.Legendary;
                        _feedback.PlayOneShot(legend?_legendTone:_biteTone);
                        if(legend)Notice("金帆旗鱼咬钩了！提竿后看到“跃出水面”立刻松开，别硬拉。",7f);
                    }
                    if(Session.Phase==CatchPhase.Escaped)Notice(FishingText.Failure(Session.Failure),8f);
                    _previousPhase=Session.Phase;
                }
                StoreLandedCatch();
            }
            AnimateEquipment();
        }

        private void FixedUpdate()
        {
            if(!_initialized || Paused || IsAshore)return;
            var rb=Player.engine.RB;
            if(Anchored)
            {
                var p=rb.position;
                rb.AddForce(new Vector3(_anchor.x-p.x,0,_anchor.z-p.z)*3f-new Vector3(rb.velocity.x,0,rb.velocity.z)*2f,ForceMode.Acceleration);
                // Hold yaw only; vertical water response remains simulated.
                var yaw=Quaternion.Euler(0,_anchorRotation.eulerAngles.y,0);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation,yaw,Time.fixedDeltaTime*2f));
                return;
            }
            var state=_input.Current;
            float comfort=ComfortMode?0.65f:1f;
            Player.engine.Accelerate(Mathf.Max(0f,state.throttle)*comfort);Player.engine.Turn(state.steering*comfort);
            float braking=Mathf.Max(state.brake,Mathf.Max(0f,-state.throttle));
            if(braking>0f)
            {
                var v=rb.velocity;var h=Vector3.MoveTowards(new Vector3(v.x,0,v.z),Vector3.zero,7f*braking*Time.fixedDeltaTime);
                rb.velocity=new Vector3(h.x,v.y,h.z);
            }
        }

        private void LateUpdate()
        {
            if(_initialized)View.ApplyView();
        }
        public void ToggleView()
        {
            if(Paused)return;
            if(IsAshore){Notice("岸上使用第一人称 · 右键环顾，G 返回船上。");return;}
            View.Toggle();
            Notice(FirstPerson?"第一人称 · 右键环顾 / V 视线回正 / C 切换":"第三人称 · 右键环绕 / V 回正 / C 切换",4f);
        }
        public void TogglePause()
        {
            if(!Paused){_resumeTimeScale=Time.timeScale;Time.timeScale=0f;Paused=true;}
            else{Time.timeScale=_resumeTimeScale;Paused=false;}
            ResetToolInput();
        }
        private void ResetToolInput()
        {
            _pointerGate.Reset();_gamepadGate.Reset();
            if(View!=null)View.ResetInput();
        }
        private void OnApplicationFocus(bool focused){if(!focused)ResetToolInput();}
        private void ConfigurePresentation()
        {
            // The grade is isolated to this desktop camera, not the original race or XR camera.
            if(UnityEngine.XR.XRSettings.isDeviceActive)return;
            var data=viewCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.volumeLayerMask=1<<30;
            var grade=new GameObject("Fishing-only color grade");grade.layer=30;grade.transform.SetParent(transform);
            var volume=grade.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Fishing desktop grade";_owned.Add(profile);volume.sharedProfile=profile;
            var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);_owned.Add(tone);
            var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(0.15f);color.contrast.Override(5f);color.saturation.Override(4f);_owned.Add(color);
            // Deliberately omit depth of field, motion blur and chromatic distortion.
        }

        public void Interact()
        {
            if(Paused)return;
            UpdateNearestSpot();
            if(Anchored){if(Session.Phase==CatchPhase.Landing)Scoop();return;}
            if(AtHarbor){if(Career.OrderReady)DeliverOrder();else Sell();return;}
            if(IsAshore){Notice("当前在岸上，返回船边按 G 上船后再捕鱼。");return;}
            if(SpotDistance>22f){Notice("先驶入任一黄色渔区，再按 E 停船。");return;}
            if(Speed>3f){Notice("速度过快，按住 Shift 刹车再停船。");return;}
            Anchored=true;ChartExpanded=false;ShowDetails=false;HarborDeskOpen=false;_anchor=Player.engine.RB.position;_anchorRotation=Player.transform.rotation;
            var v=Player.engine.RB.velocity;Player.engine.RB.velocity=new Vector3(0,v.y,0);
            Session.Cancel();_previousPhase=CatchPhase.Idle;
            Notice("已停船。点击左键抛竿；Q 收起工具继续航行。",7f);
        }
        public bool Cast()
        {
            if(!Anchored || Paused)return false;
            if(Career.Count>=Career.Capacity){Notice("鱼箱已满！按 Q 起航，返港按 E 出售。");return false;}
            var weather=Weather.Climate.Current;
            var kind=FishingSpecies.AreaSpecies(Spot);
            bool legend=LegendRules.CanStrike(Spot,weather,Bait,Career.LineLevel) && (ForceLegend || UnityEngine.Random.value<LegendRules.StrikeChance);
            bool rare=!legend && UnityEngine.Random.value<Mathf.Clamp01(FishingVoyage.RareChance(Bait)+BayClimate.RareBonus(weather,kind));
            if(legend)kind=FishKind.Sailfish;
            // Two uniform rolls: average fish are common and record-sized fish stay rare.
            float size=(UnityEngine.Random.value+UnityEngine.Random.value)*0.5f;
            bool cast=Session.Cast(kind,rare,Career.LineLevel,Guid.NewGuid().ToString("N"),Bait,weather,size,Career.ReelLevel);
            if(cast)
            {
                _recorded=false;_previousPhase=CatchPhase.Waiting;
                _fish.SetActive(false);Destroy(_fish);
                _fish=MakeFish(transform,kind,rare);
                if(!legend && LegendRules.Conditions(Spot,weather,Bait) && Career.LineLevel==0)
                    Notice("水下掠过一道金色的影子……鱼线太细，它不屑一顾。回港升级鱼线吧。",7f);
            }
            return cast;
        }
        public bool Hook()=>Anchored && !Paused && Session.Hook();
        public bool Scoop()
        {
            if(!Anchored || Paused || Session.Phase!=CatchPhase.Landing)return false;
            _netMotion=1f;
            bool success=Session.Land();
            // Commit immediately, before another input in this frame can cast/cancel.
            if(success) StoreLandedCatch();
            else Notice("抄网落空！等指针进入绿色区再按 E。",2f);
            return success;
        }
        private void StoreLandedCatch()
        {
            if(Session.Phase!=CatchPhase.Caught || _recorded)return;
            var record=Session.Claim();
            if(record==null)return;
            bool legend=FishingSpecies.Of(record.kind).Legendary;
            bool discovery=!Career.Discovered(record.kind,record.rare);
            int previousBest=Career.BestGrams(record.kind);
            int bounty=0;
            if(legend){bounty=Career.RecordLegend(record);if(bounty<=0)return;}
            else if(!Career.Store(record))return;
            _recorded=true;_caughtAt=Time.time;_catchStart=_fish.transform.position;
            bool best=previousBest>0 && record.grams>previousBest;
            LastCatch=new CatchCard{Fish=record.Copy(),ShownAt=Time.unscaledTime,Discovery=discovery,PersonalBest=best,Bounty=bounty};
            RefreshHold();Save();_feedback.PlayOneShot(legend?_legendTone:_successTone);
            string name=FishingSpecies.Name(record.kind)+" "+FishingText.Weight(record.grams);
            if(legend)
            {
                if(_trophy==null)BuildTrophy();
                Notice("传说！"+name+" 已测量并放生，海洋站奖励 "+bounty+" 金币。灯塔顶升起了金帆风向标。",9f,catchNotice:true);
            }
            else Notice((discovery?"图鉴发现！":best?"新纪录！":record.rare?"稀有！":"捕获成功！")+name+" 已入箱，价值 "+record.value+" 金币。",6f,catchNotice:true);
        }
        public void LeaveStation()
        {
            if(IsAshore){if(!Paused)Notice("船已停泊，走回下船位置按 G 上船。");return;}
            if(Paused)return;Anchored=false;ChartExpanded=true;HarborDeskOpen=false;ShowDetails=false;Session.Cancel();AutomationReel=false;Notice("工具已收起，W 前进，A/D 转向，S/Shift 刹车；青色港口按 E 交易。");
        }
        public int Sell()
        {
            if(!AtHarbor || Paused)return 0;
            int reserved=Career.ReservedCount;
            int sold=Career.Sell();
            if(sold>0)
            {
                Save();RefreshHold();_feedback.PlayOneShot(_successTone);
                Notice("出售成功 +"+sold+" 金币"+(reserved>0?"，已为委托保留 "+reserved+" 条鱼（X 可全部出售）。":"！数字键 1/2/3 在交易台升级。"),7f);
            }
            else Notice(reserved>0?"鱼货都在为委托保留。凑齐后按 E 交付，或按 X 全部出售。":"鱼箱还是空的。前往黄色渔区捕捞，带回这里出售。");
            return sold;
        }
        public int SellAll()
        {
            if(!AtHarbor || Paused){Notice("请先回到青色港口再出售。");return 0;}
            int sold=Career.SellAll();
            if(sold>0){Save();RefreshHold();_feedback.PlayOneShot(_successTone);Notice("全部出售 +"+sold+" 金币。",6f);}
            else Notice("鱼箱还是空的。");
            return sold;
        }
        public bool BuyUpgrade(UpgradeTrack track)
        {
            if(!AtHarbor || Paused){Notice("请先回到青色港口，再在交易台升级。");return false;}
            int cost=Career.NextUpgradeCost(track);
            if(cost<=0){Notice(FishingText.UpgradeName(track)+"已经升到满级。",4f);return false;}
            bool ok=Career.Upgrade(track);
            if(ok)
            {
                Save();if(track==UpgradeTrack.Hold)RefreshHold();_feedback.PlayOneShot(_successTone);
                int level=Career.Level(track);
                Notice(FishingText.UpgradeName(track)+" 升至 Lv."+level+" · "+FishingText.UpgradeEffect(track,level),7f);
            }
            else Notice("需要 "+cost+" 金币。先捕鱼、返港出售或交付委托吧。");
            return ok;
        }
        public bool RecordWildlife(MarineKind kind)
        {
            if(Paused || !Career.Observe(kind))return false;
            Save();Notice("自然观察 + "+BayClimate.MarineName(kind)+" · 已收录日志，不捕捞野生伙伴。",6f);return true;
        }
        public void CycleBait()
        {
            if(Paused)return;
            if(!CanChangeBait){Notice("本次捕捞结束后才能换饵。",3f);return;}
            Bait=(BaitKind)(((int)Bait+1)%3);Notice(FishingVoyage.BaitName(Bait)+" · "+FishingVoyage.BaitHelp(Bait),4f);
        }
        public void CycleNavigation() => SelectNavigation((NavigationTarget+1)%4);
        public void SelectNavigation(int target)
        {
            if(Paused || target<0 || target>fishingSpots.Length)return;
            NavigationTarget=target;Notice("航点："+NavigationName,3f);
        }
        public void ToggleDetails() { if(Paused)return;ShowDetails=!ShowDetails;if(ShowDetails)HarborDeskOpen=false; }
        public void ToggleChart() { if(!Paused)ChartExpanded=!ChartExpanded; }
        public void CycleChartRange() { if(!Paused)ChartRangeMeters=ChartRangeMeters==80?160:ChartRangeMeters==160?320:80; }
        public void ToggleHarborDesk()
        {
            if(Paused || !AtHarbor)return;
            HarborDeskOpen=!HarborDeskOpen;if(HarborDeskOpen)ShowDetails=false;
        }
        public void ToggleJournal()
        {
            if(Paused && !JournalOpen)return;
            JournalOpen=!JournalOpen;TogglePause();
        }
        public void ToggleComfort()
        {
            ComfortMode=!ComfortMode;View.ResetLook();
            if(!HasIsolatedProfile){PlayerPrefs.SetInt("Fishing.V3.Comfort",ComfortMode?1:0);PlayerPrefs.Save();}
        }
        public void ToggleInstruments() { WorldInstruments=!WorldInstruments; }
        public void ToggleQuiet()
        {
            QuietMode=!QuietMode;_feedback.mute=QuietMode;
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
            if(Paused)return;if(Shore!=null)Shore.Board();LeaveStation();Teleport(spawnPosition,spawnRotation);Notice("已救援回港；鱼箱和金币保留。",5f);
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
        private void Teleport(Vector3 p,Quaternion q){var rb=Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;Player.transform.SetPositionAndRotation(p,q);rb.position=p;rb.rotation=q;Physics.SyncTransforms();}
        private void UpdateNearestSpot()
        {
            if(Anchored)return;SpotDistance=float.MaxValue;
            for(int i=0;i<fishingSpots.Length;i++){float d=FlatDistance(Player.engine.RB.position,fishingSpots[i]);if(d<SpotDistance){SpotDistance=d;Spot=i;}}
        }
        private void Save()
        {
            try{FishingSaveStore.Save(SavePath,Career);SaveWarning=string.Empty;}
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException){SaveWarning="存档失败：进度仅保留在内存，请检查磁盘权限后再进行一次交易。";Notice(SaveWarning,10f);Debug.LogWarning("[FISHING] Save failed: "+ex.Message);}
        }
        public void Notice(string text,float seconds=5f,bool catchNotice=false){Message=text;MessageIsCatch=catchNotice;_messageUntil=Time.unscaledTime+seconds;}
        public string VisibleMessage=>Time.unscaledTime<_messageUntil?Message:string.Empty;
        public static float FlatDistance(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        public static bool HasArgument(string arg)=>Array.IndexOf(Environment.GetCommandLineArgs(),arg)>=0;

        private void BuildWorld()
        {
            for(int i=0;i<fishingSpots.Length;i++)
            {
                FishingArt.Ring("Fishing area "+i,transform,new Vector3(fishingSpots[i].x,0.22f,fishingSpots[i].z),9f,orange);
                for(int j=0;j<4;j++)
                {
                    var pos=fishingSpots[i]+Quaternion.Euler(0,j*90,0)*new Vector3(9,0,0);pos.y=0f;
                    _buoys.Add(FishingArt.Buoy(transform,_owned,PaletteShader,pos,0.85f).transform);
                }
            }
            FishingArt.Ring("Harbor safe zone",transform,new Vector3(harbor.x,0.25f,harbor.z),11f,teal);
            FishingDetailArt.Market(this,_owned);
            gameObject.AddComponent<FishingScenery>().Build(this,_owned);
        }

        private void BuildEquipment()
        {
            var boat=Player.transform;
            _box=FishingDetailArt.Crate(boat,new Vector3(0,0.85f,-1.3f),wood,teal,dark);
            FishingDetailArt.Rod(boat,dark,wood,cream,orange,out _rodTip,out _reelCrank);
            _net=FishingDetailArt.Net(boat,dark,teal,cream);
            _fish=MakeFish(transform,FishKind.Coral,false);
            _bobber=FishingArt.Shape("Float",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*0.18f,orange);
            _line=FishingArt.Line("Fishing line",transform,lineMaterial,0.018f);_line.positionCount=25;
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
            if(_reeling)_reelCrank.Rotate(Vector3.right,Time.deltaTime*400f,Space.Self);
            bool active=Anchored && phase!=CatchPhase.Idle && phase!=CatchPhase.Escaped;
            _line.enabled=active && phase!=CatchPhase.Caught;_bobber.SetActive(active && (phase==CatchPhase.Waiting || phase==CatchPhase.Bite));
            _fish.SetActive(active && (phase==CatchPhase.Fighting || phase==CatchPhase.Landing || (phase==CatchPhase.Caught && Time.time-_caughtAt<0.8f)));
            var t=Player.transform;var yaw=Quaternion.Euler(0,t.eulerAngles.y,0);
            bool legend=Session.Species.Legendary;
            float distance=phase==CatchPhase.Fighting?3.4f+Session.Distance*0.24f:5.5f;
            var target=t.position+yaw*new Vector3(distance,0,1.5f);target.y=phase==CatchPhase.Fighting?0.18f+Mathf.Abs(Mathf.Sin(Time.time*3f))*0.20f:0.25f+Mathf.Sin(Time.time*4f)*0.06f;
            float leap=phase==CatchPhase.Fighting?Session.LeapProgress:0f;
            // The legend clears the water in a readable arc; the HUD warns before the line would snap.
            if(leap>0f)target.y=0.2f+Mathf.Sin(leap*Mathf.PI)*2.4f;
            if(phase==CatchPhase.Landing)target=t.position+yaw*new Vector3(3.3f,0.85f+Mathf.Abs(Mathf.Sin(Time.time*5f))*0.25f,1.5f);
            _bobber.transform.position=target;
            _fish.transform.position=phase==CatchPhase.Caught?Vector3.Lerp(_catchStart,_box.position+Vector3.up*0.5f,Mathf.Clamp01((Time.time-_caughtAt)/0.7f))+Vector3.up*Mathf.Sin(Mathf.Clamp01((Time.time-_caughtAt)/0.7f)*Mathf.PI)*0.8f:target;
            _fish.transform.rotation=leap>0f?yaw*Quaternion.Euler(-Mathf.Cos(leap*Mathf.PI)*50f,-90f,Mathf.Sin(leap*Mathf.PI*2f)*20f)
                :yaw*Quaternion.Euler(Mathf.Sin(Time.time*7)*18f,-90+Mathf.Sin(Time.time*9)*22f,Mathf.Sin(Time.time*6)*14f);
            _fish.transform.localScale=Vector3.one*(legend?0.85f:Session.Kind==FishKind.Puffer?1.5f:1.25f);
            for(int i=0;i<25;i++){float u=i/24f;_line.SetPosition(i,Vector3.Lerp(_rodTip.position,target,u)-Vector3.up*Mathf.Sin(u*Mathf.PI)*(phase==CatchPhase.Fighting?0.15f:0.5f));}
            // Stow the basket alongside the hull until it is needed, rather than covering the fish.
            _netDeployment=Mathf.MoveTowards(_netDeployment,Anchored && (phase==CatchPhase.Landing || _netMotion>0f)?1f:0f,Time.deltaTime*4f);
            _netMotion=Mathf.MoveTowards(_netMotion,0,Time.deltaTime*2.8f);
            _net.localPosition=Vector3.Lerp(new Vector3(1.15f,0.65f,-0.7f),new Vector3(1.15f,0.9f,-0.4f),_netDeployment);
            _net.localRotation=Quaternion.Euler(0,Mathf.Lerp(85f,0f,_netDeployment),-Mathf.Sin(_netMotion*Mathf.PI)*35f);
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
            if(Paused)Time.timeScale=_resumeTimeScale;
            Application.targetFrameRate=_previousTargetFrameRate;
            foreach(var o in _owned)if(o!=null)Destroy(o);
        }
    }
}
