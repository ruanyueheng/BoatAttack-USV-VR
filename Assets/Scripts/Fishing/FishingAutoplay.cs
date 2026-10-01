using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace BoatAttack.Fishing
{
    /// <summary>Explicit command-line-only test driver. Never enabled in a normal game.</summary>
    public sealed partial class FishingAutoplay : MonoBehaviour
    {
        public const float TimeoutSeconds = 320f;
        public FishingPrototype Controller;
        public static bool Completed { get; private set; }
        public static string Failure { get; private set; }
        private int _stage,_fishCount,_legendCoins,_legendHold;
        private float _started,_captureAt,_landingCaptureAt;
        private bool _captured, _thirdCaptured, _landingCaptured, _pufferCaptured, _leapSeen, _leapCaptured;
        private string _captureDirectory;
        private void Start()
        {
            Completed=false;Failure=null;_started=Time.realtimeSinceStartup;
            Application.logMessageReceived+=CaptureError;
            _captureDirectory=Argument("-fishing-capture");
            if(!string.IsNullOrEmpty(_captureDirectory))Directory.CreateDirectory(_captureDirectory);
            _stage=-2;_captureAt=Time.realtimeSinceStartup+2f;
        }
        private void Update()
        {
            if(Completed || Failure!=null)return;
            try
            {
                if(Time.realtimeSinceStartup-_started>TimeoutSeconds)throw new Exception("Fishing automation timed out, stage "+_stage+", phase "+Controller.Session.Phase);
                switch(_stage)
                {
                    case -2:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        ValidateDeparture();
                        ValidateBeachShop();
                        ValidatePresentation();
                        ValidateLivingBay();
                        StartCoroutine(Capture("00-first-person-driving.png"));
                        _captureAt=Time.realtimeSinceStartup+1f;_stage=-1;break;
                    case -1:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        Controller.MoveToSpotForAutomation(0);Controller.Interact();_stage=0;break;
                    case 0:
                        if(!Controller.Anchored)throw new Exception("Could not enter fishing station. Spot distance="+Controller.SpotDistance+" harbor="+Controller.HarborDistance+" speed="+Controller.Speed+" message="+Controller.Message);
                        if(Controller.ChartExpanded || Controller.ShowDetails || Controller.HarborDeskOpen)
                            throw new Exception("Anchoring did not collapse the optional HUD panels.");
                        Controller.ToggleHarborDesk();
                        if(Controller.HarborDeskOpen)throw new Exception("Harbor desk opened away from the harbor.");
                        if(!Controller.View.OwnsCamera)throw new Exception("Desktop test unexpectedly found active XR.");
                        var phase=Controller.Session.Phase;int inventory=Controller.Career.Count;bool view=Controller.FirstPerson;
                        Controller.ToggleView();Controller.View.ApplyView();
                        if(Controller.FirstPerson==view || phase!=Controller.Session.Phase || inventory!=Controller.Career.Count)throw new Exception("View switch changed gameplay state.");
                        Controller.ToggleView();Controller.View.ApplyView();
                        if(Controller.FirstPerson!=view || Mathf.Abs(Mathf.DeltaAngle(Controller.viewCamera.transform.eulerAngles.z,0))>0.05f)throw new Exception("View did not preserve level horizon.");
                        Time.timeScale=0.5f;Controller.TogglePause();var cameraPose=Controller.viewCamera.transform.position;Controller.View.ApplyView();
                        if(!Controller.Paused || Controller.viewCamera.transform.position!=cameraPose)throw new Exception("Pause moved view.");
                        Controller.TogglePause();
                        if(Controller.Paused || Time.timeScale!=0.5f)throw new Exception("Resume did not restore prior time scale.");
                        Time.timeScale=1f;
                        if(_fishCount==0)ValidateCameraObstruction();
                        Controller.ToggleJournal();
                        if(!Controller.Paused || !Controller.JournalOpen)throw new Exception("Journal did not pause simulation.");
                        Controller.ToggleJournal();
                        if(Controller.Paused || Controller.JournalOpen || Time.timeScale!=1f)throw new Exception("Journal did not restore simulation.");
                        if(_fishCount>0)Controller.CycleBait();
                        if(!Controller.Cast())throw new Exception("Could not cast.");
                        if(Controller.Session.Kind!=FishingSpecies.AreaSpecies(_fishCount))throw new Exception("Area cast the wrong species: "+Controller.Session.Kind);
                        var bait=Controller.Bait;Controller.CycleBait();
                        if(Controller.Bait!=bait)throw new Exception("Bait changed during an active cast.");
                        _stage=1;break;
                    case 1:
                        if(Controller.Session.Phase==CatchPhase.Bite)Controller.Hook();
                        Controller.AutomationReel=Controller.Session.Tension<0.6f;
                        if(Controller.Session.Phase==CatchPhase.Fighting && !_captured && Time.realtimeSinceStartup-_started>5f)
                        {
                            if(Controller.NetDeployed)throw new Exception("Net must be stowed while fighting.");
                            _captured=true;StartCoroutine(Capture("01-first-person.png"));
                        }
                        if(Controller.Session.Phase==CatchPhase.Fighting && _fishCount==1 && !_thirdCaptured && Controller.Session.Distance<Controller.Session.MaxDistance-3f)
                        {_thirdCaptured=true;StartCoroutine(Capture("03-third-person.png"));}
                        if(Controller.Session.Phase==CatchPhase.Fighting && _fishCount==2 && !_pufferCaptured && Controller.Session.Distance<Controller.Session.MaxDistance-3f)
                        {_pufferCaptured=true;StartCoroutine(Capture("04-puffer.png"));}
                        if(Controller.Session.Phase==CatchPhase.Landing && _fishCount==0 && !_landingCaptured)
                        {
                            if(_landingCaptureAt==0f)_landingCaptureAt=Time.realtimeSinceStartup+0.45f;
                            if(Time.realtimeSinceStartup<_landingCaptureAt)return;
                            if(!Controller.NetDeployed)throw new Exception("Net did not deploy for landing.");
                            _landingCaptured=true;StartCoroutine(Capture("02-landing.png"));return;
                        }
                        if(Controller.Session.Phase==CatchPhase.Landing && Controller.Session.CanLand)
                        {
                            int before=Controller.Career.Count;
                            if(!Controller.CatchRig.CaptureAndStoreForAutomation() || Controller.Career.Count!=before+1)throw new Exception("Assisted geometric net capture and explicit storage failed.");
                            if(Controller.CatchRig.CaptureAndStoreForAutomation() || Controller.Career.Count!=before+1)throw new Exception("Repeated scoop duplicated a catch.");
                            var landed=Controller.LastCatch.Fish;
                            if(landed==null || landed.grams<=0 || !FishingSpecies.ValidGrams(landed.kind,landed.grams) || landed.value!=FishingSpecies.Value(landed.kind,landed.rare,landed.grams))
                                throw new Exception("Landed catch lacks a valid measured weight/price.");
                        }
                        if(Controller.Session.Phase==CatchPhase.Escaped)throw new Exception(FishingText.Failure(Controller.Session.Failure));
                        if(Controller.Career.Count>_fishCount)
                        {
                            if(_fishCount==0)ValidateCatchFeedback();
                            _fishCount++;
                            // Capture before the following stage teleports to the next area.
                            if(_fishCount==1)StartCoroutine(Capture("02b-catch.png"));
                            _captureAt=Time.realtimeSinceStartup+1.2f;_stage=2;
                        }
                        break;
                    case 2:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        if(_fishCount<3)
                        {
                            Controller.LeaveStation();Controller.MoveToSpotForAutomation(_fishCount);Controller.Interact();
                            if((_fishCount==1 && Controller.FirstPerson)||(_fishCount==2 && !Controller.FirstPerson))Controller.ToggleView();
                            _stage=0;
                        }
                        else{Controller.ReturnToHarbor();if(Controller.FirstPerson)Controller.ToggleView();_captureAt=Time.realtimeSinceStartup+2f;_stage=3;}
                        break;
                    case 3:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        ValidateHarborTrade();
                        _captureAt=Time.realtimeSinceStartup+2f;_stage=4;break;
                    case 4:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        BeginLegend();_stage=5;break;
                    case 5:
                        FightLegend();break;
                    case 6:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        var climate=Controller.Weather.Climate;
                        Controller.ForceLegendForAutomation=false;
                        Controller.ReturnToHarbor();
                        if(!climate.Select(BayWeather.Clear,true))throw new Exception("Could not restore clear weather after the legend.");
                        for(int i=0;i<48;i++)climate.Tick(.25f,false);
                        Controller.Weather.ApplyEnvironment();
                        _captureAt=Time.realtimeSinceStartup+2f;_stage=7;break;
                    case 7:
                        if(Time.realtimeSinceStartup<_captureAt)return;
                        StartCoroutine(Finish());_stage=8;break;
                }
            }
            catch(Exception ex){Failure=ex.ToString();Debug.LogError("[FISHING] FAIL: "+Failure);if(!Application.isEditor)Application.Quit(1);}
        }
        /// <summary>Order delivery, reserving sale, sell-all, upgrades, save round trip and backup recovery.</summary>
        private void ValidateHarborTrade()
        {
            var career=Controller.Career;
            if(!Controller.AtHarbor)throw new Exception("Rescue did not return to the harbor trade desk.");
            int value=career.HoldValue;var order=career.CurrentOrder;
            if(career.Count!=3 || !career.OrderReady)throw new Exception("Mixed-species order not ready.");
            int expected=order.Bonus;
            foreach(var fish in career.Inventory)if(order.Required(fish.kind)>0)expected+=fish.value;
            int payout=Controller.DeliverOrder();
            if(payout!=expected || career.Count!=1 || career.OrdersCompleted!=1)throw new Exception("Order delivery failed: "+payout+" != "+expected);
            if(Controller.DeliverOrder()!=0)throw new Exception("Duplicate order paid twice.");
            // The next request needs the remaining puffer, so a normal sale must keep it aboard.
            if(career.CurrentOrder.Required(FishKind.Puffer)>0 && (Controller.Sell()!=0 || career.Count!=1))throw new Exception("Normal sale sold fish reserved for the next order.");
            int remaining=Controller.SellAll();
            if(payout+remaining!=value+order.Bonus || career.Count!=0)throw new Exception("Order/sale total failed.");
            if(Controller.SellAll()!=0 || Controller.Sell()!=0)throw new Exception("Duplicate sale paid twice.");
            int coins=career.Coins;
            if(!Controller.BuyUpgrade(UpgradeTrack.Line) || career.LineLevel!=1 || career.Coins!=coins-30)throw new Exception("Line upgrade failed.");
            coins=career.Coins;bool affordable=coins>=FishingCareer.UpgradeCost(UpgradeTrack.Reel,0);
            if(Controller.BuyUpgrade(UpgradeTrack.Reel)!=affordable || career.Coins!=(affordable?coins-40:coins))throw new Exception("Reel upgrade did not respect the price.");
            var reloaded=FishingSaveStore.Load(Controller.SavePath,out var note);
            if(reloaded.Coins!=career.Coins || reloaded.LineLevel!=1 || reloaded.ReelLevel!=career.ReelLevel || reloaded.Count!=0 || reloaded.OrdersCompleted!=1 || reloaded.Catches!=3 || reloaded.DiscoveryMask!=career.DiscoveryMask || note.Length>0)throw new Exception("Save round trip failed.");
            for(int i=0;i<3;i++)if(reloaded.BestGrams((FishKind)i)!=career.BestGrams((FishKind)i) || career.BestGrams((FishKind)i)<=0)throw new Exception("Personal records did not persist.");
            // Force a backup and verify recovery in the isolated test save, never player progress.
            FishingSaveStore.Save(Controller.SavePath,career);
            File.WriteAllText(Controller.SavePath,"corrupt test payload");
            reloaded=FishingSaveStore.Load(Controller.SavePath,out note);
            if(reloaded.LineLevel!=1 || note.Length==0)throw new Exception("Backup recovery failed.");
            Debug.Log("[FISHING] Harbor trade checks: order payout, reserved sale, sell-all, priced upgrades, records, save and backup recovery.");
        }
        /// <summary>Sets up the legend's documented conditions: channel, golden sunset, pearl bait, reinforced line.</summary>
        private void BeginLegend()
        {
            var climate=Controller.Weather.Climate;
            if(climate.Blend<1f)for(int i=0;i<48;i++)climate.Tick(.25f,false);
            if(!climate.Select(BayWeather.Sunset))throw new Exception("Could not select sunset for the legend.");
            for(int i=0;i<48;i++)climate.Tick(.25f,false);
            Controller.Weather.ApplyEnvironment();
            Controller.MoveToSpotForAutomation(FishingSpecies.LegendArea);Controller.Interact();
            if(!Controller.Anchored || Controller.Spot!=FishingSpecies.LegendArea)throw new Exception("Could not anchor in the legend channel.");
            Controller.SetBaitForAutomation(BaitKind.Pearl);
            if(Controller.Bait!=BaitKind.Pearl || Controller.Career.LineLevel<1)throw new Exception("Legend tackle not prepared.");
            _legendCoins=Controller.Career.Coins;_legendHold=Controller.Career.Count;
            Controller.ForceLegendForAutomation=true;
            if(!Controller.Cast() || Controller.Session.Kind!=FishKind.Sailfish)throw new Exception("Legend conditions did not produce the golden sail.");
        }
        /// <summary>Hooks, releases during every leap, lands in the narrower window and checks the release bounty.</summary>
        private void FightLegend()
        {
            var s=Controller.Session;
            if(s.Phase==CatchPhase.Bite)Controller.Hook();
            bool leap=s.Phase==CatchPhase.Fighting && s.Beat==FishBeat.Leap;
            _leapSeen|=leap && s.LeapProgress>0f;
            Controller.AutomationReel=!leap && s.Tension<0.6f;
            if(leap && !_leapCaptured && s.LeapProgress>0.35f){_leapCaptured=true;StartCoroutine(Capture("09-legend-leap.png"));}
            if(s.Phase==CatchPhase.Escaped)throw new Exception("Legend escaped: "+FishingText.Failure(s.Failure));
            if(s.Phase==CatchPhase.Landing && s.CanLand)Controller.CatchRig.CaptureAndStoreForAutomation();
            if(s.Phase!=CatchPhase.Caught)return;
            var career=Controller.Career;var card=Controller.LastCatch;
            if(!_leapSeen)throw new Exception("Legend fight never showed a leap.");
            if(career.LegendCatches!=1 || career.Count!=_legendHold || card.Bounty<=0 || career.Coins!=_legendCoins+card.Bounty || card.Fish==null || card.Fish.kind!=FishKind.Sailfish)
                throw new Exception("Legend release/bounty failed.");
            if(Controller.transform.Find("Golden sail trophy vane")==null)throw new Exception("Legend trophy vane missing.");
            var reloaded=FishingSaveStore.Load(Controller.SavePath,out var note);
            if(reloaded.LegendCatches!=1 || !reloaded.Discovered(FishKind.Sailfish) || reloaded.BestGrams(FishKind.Sailfish)!=card.Fish.grams || note.Length>0)throw new Exception("Legend save round trip failed.");
            Debug.Log("[FISHING] Legend checks: sunset/pearl/channel strike, leap release, narrow landing, bounty "+card.Bounty+", trophy vane and save.");
            StartCoroutine(Capture("10-legend-release.png"));
            _captureAt=Time.realtimeSinceStartup+2.5f;_stage=6;
        }
        private void ValidateDeparture()
        {
            var c=Controller;
            Debug.Log("[FISHING] Departure: configured="+c.spawnPosition+" transform="+c.Player.transform.position+" rigidbody="+c.Player.engine.RB.position+" harbor="+c.harbor+" yaw="+c.Player.transform.eulerAngles.y);
            if(FishingPrototype.FlatDistance(c.Player.transform.position,c.spawnPosition)>3f || !c.AtHarbor)
                throw new Exception("Boat did not stay at its configured harbor spawn before departure.");
            var camera=c.viewCamera.transform;
            foreach(var hit in Physics.SphereCastAll(camera.position,0.25f,camera.forward,12f,~(1<<4),QueryTriggerInteraction.Ignore))
                if(!hit.collider.transform.IsChildOf(c.Player.transform) && hit.collider.GetComponentInParent<WaterSystem.Water>()==null)
                    throw new Exception("Initial first-person departure is blocked by "+hit.collider.name+" at "+hit.distance+"m.");
            Debug.Log("[FISHING] Departure checks: stable harbor spawn and 12m forward camera clearance.");
        }
        private void ValidateCameraObstruction()
        {
            // Reproduce a newly occluded third-person view, not just its final settled position.
            var c=Controller;
            if(!c.FirstPerson)throw new Exception("Camera obstruction test requires the first-person station.");
            var yaw=Quaternion.Euler(0,c.Player.transform.eulerAngles.y,0);
            var origin=c.viewCamera.transform.position-yaw*new Vector3(0.05f,2f,-0.55f);
            var pivot=origin+yaw*new Vector3(2.7f,0.8f,1f);
            c.ToggleView();c.View.ApplyView();
            var offset=c.viewCamera.transform.position-pivot;
            if(offset.magnitude<3f)throw new Exception("Camera obstruction fixture has insufficient clearance.");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.name="Temporary camera regression wall";
                wall.transform.SetPositionAndRotation(pivot+offset*0.5f,Quaternion.LookRotation(offset));
                wall.transform.localScale=new Vector3(20f,20f,0.3f);
                Physics.SyncTransforms();
                c.View.ApplyView();
                if(Vector3.Distance(c.viewCamera.transform.position,pivot)>offset.magnitude*0.5f-0.25f)
                    throw new Exception("Camera smoothing left the view behind a newly encountered wall.");
                Debug.Log("[FISHING] Camera obstruction check: newly blocked view stays in front of the wall immediately.");
            }
            finally
            {
                wall.GetComponent<Collider>().enabled=false;Destroy(wall);
                c.ToggleView();c.View.ApplyView();
            }
        }
        private void ValidatePresentation()
        {
            // These are geometry/layout regressions, not a substitute for reviewing the rendered pictures.
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1366,768),new Vector2Int(2560,1080),new Vector2Int(1280,1024)})
            {
                float scale=FishingHUD.LayoutScale(size.x,size.y);
                if(1600f*scale>size.x+0.01f || 900f*scale>size.y+0.01f)throw new Exception("HUD safe area exceeds "+size);
            }
            for(int lines=1;lines<=3;lines++)
            {
                var layout=new FishingHUD.HarborLayout(lines);
                float lastEffectBottom=layout.UpgradeY(2)+26f+20f;
                if(layout.Footer.y<lastEffectBottom+8f || layout.Footer.yMax>layout.Panel.yMax-14f)
                    throw new Exception("Harbor footer overlaps upgrade text for "+lines+" order lines.");
                if(layout.Panel.yMax>850f || layout.Delivery.y<layout.OrderY+lines*FishingHUD.OrderLineHeight)
                    throw new Exception("Harbor content exceeds its safe area for "+lines+" order lines.");
            }
            ValidateFocusedHud();
            var sign=Controller.transform.Find("Tide and Fin floating fish market/Market name");
            if(sign==null || Quaternion.Angle(sign.localRotation,Quaternion.identity)>0.01f)throw new Exception("Market sign is facing backwards.");
            int fishMeshes=0;
            foreach(var filter in Controller.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                if(mesh==null || !mesh.name.StartsWith(FishingMeshBuilder.FishMeshPrefix))continue;
                fishMeshes++;
                if(filter.GetComponentsInChildren<Renderer>().Length!=1)throw new Exception("A fish must draw as a single renderer.");
                ValidateFishMesh(mesh);
            }
            if(fishMeshes<3)throw new Exception("Three modeled species must be present in market display.");
            // The legend and pearl variants are built on demand, so check their geometry directly.
            foreach(FishKind kind in Enum.GetValues(typeof(FishKind)))
                foreach(bool rare in new[]{false,true})
                {
                    if(rare && FishingSpecies.Of(kind).Legendary)continue;
                    var mesh=FishingMeshBuilder.Fish(kind,rare);
                    try{ValidateFishMesh(mesh);}finally{Destroy(mesh);}
                }
            var scenery=Controller.GetComponent<FishingScenery>();
            if(scenery==null || scenery.CombinedRenderers>40 || scenery.CombinedVertices>60000)throw new Exception("V03 added scenery exceeded its static geometry budget.");
            if(Controller.GetComponent<FishingWorldInstruments>()==null || FishingWorldInstruments.ReferenceDistance<2f)throw new Exception("World instrument comfort setup missing.");
            int target=Controller.NavigationTarget;for(int i=0;i<Controller.NavigationCount;i++)Controller.CycleNavigation();
            if(Controller.NavigationTarget!=target)throw new Exception("Navigation cycle failed.");
            Debug.Log("[FISHING] Presentation checks: 4 viewport safe areas, 1/2/3-line harbor layouts, readable sign orientation, single-renderer fish, fish normals and vertex budget.");
        }
        private void ValidateFocusedHud()
        {
            var c=Controller;
            if(c.ShowDetails || c.HarborDeskOpen)throw new Exception("Default HUD must not open detail/trading cards.");
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1366,768),new Vector2Int(2560,1080),new Vector2Int(1280,1024)})
            {
                float scale=FishingHUD.LayoutScale(size.x,size.y),height=size.y/scale,offset=(size.x-1600*scale)*.5f;
                var layout=new FishingHUD.FocusLayout(height,true,CatchPhase.Fighting);
                var clear=new Rect(376,150,844,height-390);
                foreach(var rect in new[]{layout.Heading,layout.Pocket,layout.Toolbar,layout.Chart,layout.Action,layout.Notice,layout.Catch,FishingHUD.HotbarRect(height)})
                    if(rect.Overlaps(clear) || rect.y<0 || rect.yMax>height || rect.x<0 || rect.xMax>1600)
                        throw new Exception("Focused HUD obstructs the central view or leaves its safe area: "+size);
                var button=new Vector2(offset+layout.Toolbar.center.x*scale,size.y-layout.Toolbar.center.y*scale);
                if(!FishingHUD.BlocksToolPointer(button,size.x,size.y))throw new Exception("Scaled toolbar leaks pointer input: "+size);
                ValidatePointerRegions(size);
                var middle=new Vector2(offset+800*scale,size.y-400*scale);
                if(FishingHUD.BlocksToolPointer(middle,size.x,size.y))throw new Exception("Central fishing view is blocked: "+size);
            }
            c.ToggleDetails();if(!c.ShowDetails)throw new Exception("Details cannot be opened.");c.ToggleDetails();
            c.ToggleChart();if(c.ChartExpanded)throw new Exception("Chart cannot be collapsed.");c.ToggleChart();
            int range=c.ChartRangeMeters;for(int i=0;i<3;i++)c.CycleChartRange();
            if(c.ChartRangeMeters!=range)throw new Exception("Chart zoom does not cycle.");
            c.ToggleHarborDesk();if(!c.HarborDeskOpen)throw new Exception("Harbor desk cannot be explicitly opened.");
            c.ToggleDetails();if(c.HarborDeskOpen || !c.ShowDetails)throw new Exception("Details and harbor panels overlap.");c.ToggleDetails();
            int target=c.NavigationTarget;c.SelectNavigation(-1);c.SelectNavigation(99);
            if(c.NavigationTarget!=target)throw new Exception("Invalid map target accepted.");
            c.TogglePause();
            try
            {
                c.ToggleDetails();c.ToggleChart();c.CycleChartRange();c.ToggleHarborDesk();c.SelectNavigation((target+1)%4);
                if(c.ShowDetails || !c.ChartExpanded || c.ChartRangeMeters!=range || c.HarborDeskOpen || c.NavigationTarget!=target)
                    throw new Exception("Paused HUD controls changed gameplay state.");
            }
            finally{c.TogglePause();}
            Debug.Log("[FISHING] Focused HUD checks: central clear zone, expanded/collapsed and conditional pointer gates at 4 viewport sizes, default-closed cards, chart zoom, explicit harbor controls and pause guards.");
        }
        private static void ValidatePointerRegions(Vector2Int size)
        {
            float scale=FishingHUD.LayoutScale(size.x,size.y),offset=(size.x-1600*scale)*.5f;
            var layout=new FishingHUD.FocusLayout(size.y/scale,false,CatchPhase.Idle);
            void Gate(Vector2 logical,bool expected,bool chart=true,bool details=false,bool harbor=false,bool atHarbor=false,bool observe=false)
            {
                var screen=new Vector2(offset+logical.x*scale,size.y-logical.y*scale);
                if(FishingHUD.BlocksToolPointer(screen,size.x,size.y,chart,details,harbor,atHarbor,observe)!=expected)
                    throw new Exception("HUD pointer region disagrees with visibility at "+logical+" in "+size);
            }
            Gate(FishingHUD.HotbarRect(size.y/scale).center,true);
            Gate(layout.Chart.center,true);
            Gate(layout.Chart.center,false,chart:false);
            Gate(layout.ChartTab.center,true,chart:false);
            Gate(layout.Heading.center,false);
            Gate(layout.Pocket.center,false);
            Gate(new Vector2(1400,160),false);
            Gate(new Vector2(1400,160),true,details:true);
            Gate(new Vector2(1140,200),false);
            Gate(new Vector2(1140,200),true,harbor:true);
            Gate(layout.Observe.center,false);
            Gate(layout.Observe.center,true,observe:true);
            Gate(layout.HarborButton.center,false);
            Gate(layout.HarborButton.center,true,atHarbor:true);
            Gate(new Vector2(800,400),false,details:true,harbor:true,atHarbor:true,observe:true);
            for(int lines=1;lines<=3;lines++)
            {
                var trade=new FishingHUD.HarborLayout(lines);
                Gate(trade.Delivery.center,true,harbor:true);
                Gate(trade.Sale.center,true,harbor:true);
                Gate(new Vector2(trade.Panel.xMax-90,trade.UpgradeY(2)+23),true,harbor:true);
            }
        }

        private void ValidateCatchFeedback()
        {
            var c=Controller;var hud=c.GetComponent<FishingHUD>();
            if(c.LastCatch.Fish==null || c.SaveWarning.Length>0 || hud.NoticeText.Length>0)
                throw new Exception("Catch feedback test requires a fresh catch and a hidden duplicate notice.");
            string catchNotice=c.Message;
            var bait=c.Bait;c.CycleBait();
            if(c.Bait==bait || hud.NoticeText!=c.VisibleMessage || hud.NoticeText.Length==0)
                throw new Exception("Fresh catch toast suppresses subsequent bait feedback.");
            c.SetBaitForAutomation(bait);
            c.Notice("鱼箱已满！按 Q 起航，返港按 E 出售。",2f);
            if(hud.NoticeText!=c.VisibleMessage)
                throw new Exception("Fresh catch toast suppresses subsequent action feedback.");
            c.Notice(catchNotice,6f,catchNotice:true);
            Debug.Log("[FISHING] Catch feedback checks: duplicate catch copy hidden; subsequent bait/action notices remain visible.");
        }

        private static void ValidateFishMesh(Mesh mesh)
        {
            if(mesh.vertexCount>4000)throw new Exception(mesh.name+" exceeds the 4000-vertex fish budget: "+mesh.vertexCount);
            foreach(var normal in mesh.normals)
                if(float.IsNaN(normal.sqrMagnitude) || normal.sqrMagnitude<0.5f)throw new Exception(mesh.name+" contains invalid/cancelled surface normals.");
        }
        private void ValidateLivingBay()
        {
            var c=Controller;var weather=c.Weather;
            if(c.Ecology.CreatureCount!=9 || c.Ecology.MeshVertices>16000)throw new Exception("Ecology population/geometry budget exceeded.");
            int baseBudget=weather.RainQuadBudget;weather.ToggleReducedEffects();
            if(weather.RainQuadBudget!=32)throw new Exception("Reduced rain budget failed.");
            weather.ToggleReducedEffects();if(weather.RainQuadBudget!=baseBudget)throw new Exception("Rain budget restore failed.");
            var climate=weather.Climate;
            foreach(BayWeather preset in Enum.GetValues(typeof(BayWeather)))
            {
                if(!climate.Select(preset))throw new Exception("Weather selection failed.");
                for(int i=0;i<48;i++)climate.Tick(.25f,false);
                weather.ApplyEnvironment();
                if(RenderSettings.skybox.HasProperty("_Exposure") && RenderSettings.skybox.GetFloat("_Exposure")>1.41f)throw new Exception("Fishing sky exposure is not capped.");
                if(RenderSettings.fogEndDistance<180 || RenderSettings.sun.intensity<=0)throw new Exception("Weather made navigability unsafe.");
            }
            climate.Select(BayWeather.Clear,true);for(int i=0;i<48;i++)climate.Tick(.25f,false);weather.ApplyEnvironment();
            float age=climate.Age;climate.Tick(1,true);if(climate.Age!=age)throw new Exception("Paused weather advanced.");
            // Component lifecycle must not leave a tinted shared sky or suspended race writers behind.
            var sky=RenderSettings.skybox;weather.enabled=false;
            if(RenderSettings.skybox==sky)throw new Exception("Weather did not restore the original sky.");
            weather.enabled=true;weather.ApplyEnvironment();
            if(RenderSettings.skybox==sky || RenderSettings.skybox==null)throw new Exception("Weather did not recreate an isolated sky.");
            var camera=c.viewCamera.transform;Vector3 p=camera.position;Quaternion q=camera.rotation;
            var turtle=c.Ecology.FindCreatureForValidation(MarineKind.Turtle);
            if(turtle==null)throw new Exception("Turtle model missing.");
            try
            {
                camera.position=turtle.position+Vector3.up*3;camera.rotation=Quaternion.LookRotation(turtle.position-camera.position);
                int coins=c.Career.Coins;
                if(!c.Ecology.Observe() || !c.Career.Seen(MarineKind.Turtle))throw new Exception("Sight-based wildlife discovery failed.");
                if(c.Ecology.Observe() || c.Career.Coins!=coins)throw new Exception("Repeated wildlife observation paid or duplicated.");
                var restored=FishingSaveStore.Load(c.SavePath,out var warning);
                if(warning.Length>0 || !restored.Seen(MarineKind.Turtle))throw new Exception("Wildlife record did not persist.");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    // A second unseen species behind an actual collider must not be observable.
                    var ray=c.Ecology.FindCreatureForValidation(MarineKind.Ray);camera.position=ray.position+Vector3.up*3;camera.rotation=Quaternion.LookRotation(Vector3.down);
                    wall.transform.position=ray.position+Vector3.up*1.5f;wall.transform.localScale=new Vector3(12,.3f,12);Physics.SyncTransforms();
                    if(c.Ecology.Observe() || c.Career.Seen(MarineKind.Ray))throw new Exception("Wildlife observed through an obstruction.");
                }
                finally{wall.GetComponent<Collider>().enabled=false;Destroy(wall);}
            }
            finally{camera.SetPositionAndRotation(p,q);}
            if(!FishingHUD.BlocksToolPointer(new Vector2(1280,900-90),1600,900) || FishingHUD.BlocksToolPointer(new Vector2(800,450),1600,900))throw new Exception("Clickable HUD does not gate tool input.");
            Debug.Log("[FISHING] Living bay checks: 5 weather presets, pause, environment restoration, rain/population budgets, visible wildlife discovery, occlusion rejection and save persistence.");
        }
        private IEnumerator Capture(string name)
        {
            if(string.IsNullOrEmpty(_captureDirectory))yield break;
            // WaitForEndOfFrame is unsupported in batchmode; capture is for the real Windows player.
            yield return new WaitForEndOfFrame();
            // Read the composed backbuffer (scene plus IMGUI HUD) and write it ourselves: CaptureScreenshot
            // resolves relative paths on its own and fails silently in some launch contexts.
            var shot=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(_captureDirectory,name),shot.EncodeToPNG());}
            finally{Destroy(shot);}
            CaptureSceneOnly("scene-"+name);
            Debug.Log("[FISHING] Screenshot captured: "+name+" "+Screen.width+"x"+Screen.height+" focused="+Application.isFocused);
        }
        // Explicit off-screen camera render: unlike the desktop backbuffer this works
        // with a hidden test window. These images intentionally exclude the IMGUI HUD.
        private void CaptureSceneOnly(string name)
        {
            if(string.IsNullOrEmpty(_captureDirectory))return;
            var camera=Controller.viewCamera;var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);
            Texture2D image=null;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(_captureDirectory,name),image.EncodeToPNG());
            }
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;RenderTexture.ReleaseTemporary(target);if(image!=null)Destroy(image);}
        }
        private IEnumerator CaptureWildlifeGallery()
        {
            if(string.IsNullOrEmpty(_captureDirectory))yield break;
            yield return new WaitForEndOfFrame();
            var camera=Controller.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;
            try
            {
                foreach(MarineKind kind in Enum.GetValues(typeof(MarineKind)))
                {
                    var creature=Controller.Ecology.FindCreatureForValidation(kind);bool active=creature.gameObject.activeSelf;creature.gameObject.SetActive(true);
                    try
                    {
                        var pose=creature.position+creature.rotation*new Vector3(2.1f,2.2f,2.6f);
                        camera.SetPositionAndRotation(pose,Quaternion.LookRotation(creature.position-pose));
                        CaptureSceneOnly("wildlife-"+kind+".png");
                    }
                    finally{creature.gameObject.SetActive(active);}
                }
            }
            finally{camera.SetPositionAndRotation(position,rotation);}
            var climate=Controller.Weather.Climate;
            for(int i=0;i<48;i++)climate.Tick(.25f,false);
            foreach(BayWeather preset in Enum.GetValues(typeof(BayWeather)))
            {
                if(!climate.Select(preset))throw new Exception("Capture weather selection failed.");
                for(int i=0;i<48;i++)climate.Tick(.25f,false);
                Controller.Weather.RefreshForCapture();CaptureSceneOnly("weather-"+preset+".png");
            }
            climate.Select(BayWeather.Clear,true);for(int i=0;i<48;i++)climate.Tick(.25f,false);Controller.Weather.RefreshForCapture();
        }
        private IEnumerator Finish()
        {
            yield return Capture("05-harbor.png");
            yield return CaptureWildlifeGallery();
            var hud=Controller.GetComponent<FishingHUD>();
            Controller.ToggleJournal();
            if(!Controller.Paused || !Controller.JournalOpen)throw new Exception("Journal capture state failed.");
            for(int page=0;page<3;page++)
            {
                hud.JournalPage=page;
                if(hud.JournalPage!=page)throw new Exception("Journal page selection failed.");
                if(!string.IsNullOrEmpty(_captureDirectory))yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture("06-journal-"+page+".png");
            }
            hud.JournalPage=0;
            Controller.ToggleJournal();Controller.TogglePause();
            if(!string.IsNullOrEmpty(_captureDirectory))yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("07-comfort-settings.png");
            Controller.TogglePause();Controller.ToggleInstruments();
            if(!Controller.FirstPerson)Controller.ToggleView();
            if(!string.IsNullOrEmpty(_captureDirectory))yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("08-world-instruments.png");
            Controller.ToggleInstruments();
            if(!string.IsNullOrEmpty(_captureDirectory))yield return new WaitForSecondsRealtime(1.5f);
            Completed=true;Debug.Log("[FISHING] PASS: 3 species with measured weights, both views, immediate camera obstruction recovery, level horizon, pause/time-scale recovery, net staging, mesh normals, HUD safe areas, inventory, three baits, order delivery, reserved sale, sell-all, priced upgrades, personal records, golden-sail legend (sunset/pearl/channel, leap release, bounty, trophy), discovery persistence, journal pages, navigation, geometry budget, weather lifecycle, wildlife sight/occlusion/persistence, save and backup recovery. Automation did not test real input or VR.");
            if(!Application.isEditor)Application.Quit(0);
        }
        private void CaptureError(string message,string stack,LogType type)
        {
            if(Completed || Failure!=null || (type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert))return;
            Failure=message;
            if(!Application.isEditor)Application.Quit(1);
        }
        private void OnDestroy(){Application.logMessageReceived-=CaptureError;}
        private static string Argument(string key)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length && !args[i+1].StartsWith("-")?args[i+1]:null;
        }
    }
}
