using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.OpenXR;
namespace BoatAttack.EditorTools
{
    [InitializeOnLoad]
    public static class FishingCatchValidation
    {
        private const string Key="Fishing.CatchV09.";private static IEnumerator _steps;private static double _next;
        static FishingCatchValidation()
        {
            EditorApplication.update+=Tick;
            Application.logMessageReceived+=(message,stack,type)=>{if(SessionState.GetBool(Key+"active",false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert))SessionState.SetString(Key+"error",message);};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key+"active",false))return;
                SessionState.SetBool(Key+"active",false);string error=SessionState.GetString(Key+"error","");
                if(!SessionState.GetBool(Key+"done",false) && error=="")error="Incomplete validation";
                if(error=="")Debug.Log("[CATCH] PASS: real net transforms, durable pending fish, explicit outcomes, world-space ray/confirmation commands and Android XR configuration. Pose inputs simulated; no headset certification.");
                else Debug.LogError("[CATCH] FAIL: "+error);EditorApplication.Exit(error==""?0:1);
            };
        }
        public static void Capture(){SessionState.SetBool(Key+"capture",true);Run();}
        public static void Run()
        {
            SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"done",false);SessionState.SetString(Key+"error","");
            SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+220);
            SessionState.SetString(Key+"images",Path.GetFullPath(Path.Combine("Logs","catch-preview-"+Guid.NewGuid().ToString("N"))));
            FishingScriptBinding.Ensure();
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Fishing/FishingPrototype.cs");
            Debug.Log("[CATCH] Binding before refresh: "+(script==null?"missing asset":script.GetClass()?.FullName??"null class"));
            AssetDatabase.ImportAsset("Assets/Scripts/Fishing/FishingPrototype.cs",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            script=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Fishing/FishingPrototype.cs");
            Debug.Log("[CATCH] Binding after refresh: "+(script==null?"missing asset":script.GetClass()?.FullName??"null class"));
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
            var controller=UnityEngine.Object.FindObjectOfType<FishingPrototype>();
            if(controller==null){Debug.LogError("[CATCH] Main scene controller binding is missing; refusing a false Play Mode pass");EditorApplication.Exit(1);return;}
            EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key+"active",false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))SessionState.SetString(Key+"error","Timeout");
            if(SessionState.GetString(Key+"error","")!=""){if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();return;}
            if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<_next)return;
            var c=UnityEngine.Object.FindObjectOfType<FishingPrototype>();if(c==null || c.DockConsole==null)return;
            try{if(_steps==null)_steps=Checks(c);_next=EditorApplication.timeSinceStartup+.02;if(!_steps.MoveNext()){SessionState.SetBool(Key+"done",true);EditorApplication.ExitPlaymode();}}
            catch(Exception e){SessionState.SetString(Key+"error",e.ToString());EditorApplication.ExitPlaymode();}
        }
        private static void Check(bool ok,string label){if(!ok)throw new Exception(label);Debug.Log("[CATCH] CHECK: "+label);}
        private static CatchRecord Fish()=>new CatchRecord{id=Guid.NewGuid().ToString("N"),kind=FishKind.Puffer,grams=2000,value=FishingSpecies.Value(FishKind.Puffer,false,2000)};
        public static void KeepOrdinaryReceipt(FishingPrototype c)
        {
            if(!c.HasIsolatedProfile)throw new InvalidOperationException("Only isolated capture fixtures may choose a receipt.");
            var fish=new CatchRecord{kind=c.Session.Kind,rare=c.Session.Rare};
            do{fish.id=Guid.NewGuid().ToString("N");}while(FishingCoastRules.SeaFind(fish,true)!=FieldItem.Empty);
            // These fixtures test fish handling; V028 separately exercises real net-caught sea finds.
            typeof(CatchSession).GetField("_id",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(c.Session,fish.id);
        }
        private static void Landing(FishingPrototype c)
        {
            c.ReturnToHarbor();c.MoveToSpotForAutomation(0);c.Interact();Check(c.Cast(),"start real production catch");var s=c.Session;
            KeepOrdinaryReceipt(c);
            for(int i=0;i<1500 && s.Phase==CatchPhase.Waiting;i++)s.Tick(.02f,false);Check(c.Hook(),"hook real bite");
            for(int i=0;i<6000 && s.Phase==CatchPhase.Fighting;i++)s.Tick(.02f,s.Tension<.6f);
            Check(s.Phase==CatchPhase.Landing,"existing fish fight reaches landing");for(int i=0;i<15;i++)s.Tick(.1f,false);Check(s.CanNet,"bounded drag lets the left hand take the net");
        }
        private static bool Ray(FishingPrototype c,FishingDockAction action,bool held)
        {
            var button=Array.Find(c.DockConsole.GetComponentsInChildren<FishingDockButton>(true),b=>b.Action==action);
            var target=c.DockConsole.ButtonPosition(action);var rotation=button.transform.rotation;
            return c.DockConsole.TickRay(true,target-button.transform.forward*.22f,rotation,held);
        }
        private static void Snapshot(FishingPrototype c,string name,Vector3 from,Vector3 target)
        {
            if(!SessionState.GetBool(Key+"capture",false))return;
            var camera=c.viewCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;float fov=camera.fieldOfView;
            var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);Texture2D image=null;
            try
            {
                camera.transform.position=from;camera.transform.LookAt(target);camera.fieldOfView=52;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                string dir=SessionState.GetString(Key+"images","");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());Debug.Log("[CATCH] IMAGE: "+Path.Combine(dir,name+".png")+" (world-space render; no physical input proof)");
            }
            finally{camera.targetTexture=previous;camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(pos,rot);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
        }
        private static IEnumerator Checks(FishingPrototype c)
        {
            yield return null;Check(c.HasIsolatedProfile,"isolated profile");
            string xrCastHint=FishingXRText.Adapt("已停船。按住左键后摆，松开甩竿；轻点也可近抛。Q 起航。",true);
            Check(xrCastHint.Contains("拿竿后按住右扳机蓄力") && xrCastHint.Contains("按下右摇杆收竿起航") && !xrCastHint.Contains("事务面板") && !xrCastHint.Contains("向前甩手柄"),"Legacy anchor text adapts to the actual trigger-release cast and direct stick-click stow controls");
            Check(FishingXRText.Adapt("鱼箱已满！按 Q 起航，返港按 E 出售。",true).Contains("按下右摇杆收竿起航"),"Full-box recovery points at the real immediate stow control");
            Check(c.XRSeat.Origin!=null && !c.XRSeat.Origin.enabled && !c.viewCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled,"desktop camera is not hijacked by XR rig");
            var android=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var desktop=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            Check(android!=null && android.InitManagerOnStart && android.Manager.activeLoaders.Count==1 && android.Manager.activeLoaders[0] is OpenXRLoader,"Android has exactly one OpenXR loader");
            Check(desktop!=null && !desktop.InitManagerOnStart && desktop.Manager.activeLoaders.Count==0,"Windows stays loader-free by default");
            string path=Path.Combine(Application.temporaryCachePath,"catch-v7-"+Guid.NewGuid().ToString("N")+".json");
            FishingSaveStore.Save(path,c.Career);var loaded=FishingSaveStore.Load(path,out string warning);
            Check(warning=="" && !loaded.HasPendingCatch,"fresh v7 JsonUtility round trip has no phantom pending fish");
            c.DockConsole.PreviewForValidation();Physics.SyncTransforms();Check(c.DockConsole.ButtonCount==9,"nine world-space command targets");
            Ray(c,FishingDockAction.Pause,false);Check(Ray(c,FishingDockAction.Pause,true) && c.Paused,"real collider ray can pause");
            Check(!Ray(c,FishingDockAction.Pause,true) && c.Paused,"held trigger cannot double-toggle pause");
            Ray(c,FishingDockAction.Pause,false);Check(Ray(c,FishingDockAction.Pause,true) && !c.Paused,"release and press can resume");
            c.DockConsole.TickRay(false,Vector3.zero,Quaternion.identity,false);
            Check(!Ray(c,FishingDockAction.Pause,true) && !c.Paused,"recovery with held trigger cannot select");
            Ray(c,FishingDockAction.UpgradeLine,false);Check(Ray(c,FishingDockAction.UpgradeLine,true) && c.Career.LineLevel==0,"disabled action consumes ray click without purchasing");
            Ray(c,FishingDockAction.MarkOrder,false);Check(Ray(c,FishingDockAction.MarkOrder,true) && c.Career.OrderMarked,"world-space order acknowledgement persists");
            for(int i=0;i<12;i++){c.DockConsole.Press(FishingDockAction.NextWaypoint);Check(c.NavigationTarget<c.NavigationCount && (c.NavigationTarget==0 || c.NavigationTarget>c.fishingSpots.Length || FishingGrounds.AvailableForCareer(c.NavigationTarget-1,c.Career)),"XR navigation includes coast/shop and skips locked fishing destinations "+i);}
            c.Career.Store(Fish());c.Career.Store(Fish());
            int value=c.Career.SaleValue;Check(value>30,"fixture has unreserved cargo");
            Check(!c.DockConsole.Press(FishingDockAction.Sell) && c.Career.Coins==0,"sale requires confirmation");
            Check(c.DockConsole.Press(FishingDockAction.Sell) && c.Career.Coins==value,"confirmed sale reuses actual harbor rules");
            int cost=c.Career.NextUpgradeCost(UpgradeTrack.Line),coins=c.Career.Coins;
            Check(!c.DockConsole.Press(FishingDockAction.UpgradeLine),"upgrade requires quote confirmation");
            Check(c.DockConsole.Press(FishingDockAction.UpgradeLine) && c.Career.Coins==coins-cost && c.Career.LineLevel==1,"confirmed upgrade charges exact price once");
            c.DockConsole.Present();var panel=c.DockConsole.GetComponentInChildren<Canvas>().transform;
            Snapshot(c,"01-dock-console",panel.position-panel.forward*1.6f,panel.position);
            Landing(c);int hold=c.Career.Count,catches=c.Career.Catches;
            Check(c.Scoop() && !c.HasPendingCatch && c.Career.Count==hold,"desktop E only starts an assisted net motion");
            for(int i=0;i<7;i++)c.CatchRig.StepDesktop(.02f);Check(!c.HasPendingCatch,"brief overlap is not a capture");
            for(int i=0;i<40 && !c.HasPendingCatch;i++)c.CatchRig.StepDesktop(.02f);
            Check(c.HasPendingCatch && c.Career.Count==hold && c.Career.Catches==catches,"actual net transforms stage fish without automatic reward");
            c.DockConsole.Present();
            var catchPanel=c.DockConsole.GetComponentInChildren<Canvas>();
            var activeButtons=Array.FindAll(c.DockConsole.GetComponentsInChildren<FishingDockButton>(true),b=>b.gameObject.activeInHierarchy);
            Check(activeButtons.Length==2 && Array.Exists(activeButtons,b=>b.Action==FishingDockAction.Store) && Array.Exists(activeButtons,b=>b.Action==FishingDockAction.Release),"Pending catch exposes only the two relevant outcomes instead of nine unrelated dock commands");
            var texts=catchPanel.GetComponentsInChildren<UnityEngine.UI.Text>();
            Check(Array.Exists(texts,t=>t.text.Contains("按 A 放入鱼箱")),"Visible catch card explains the aim-free A-button route");
            Check(Vector3.Distance(catchPanel.transform.position,c.viewCamera.transform.position)<1.5f && catchPanel.GetComponent<UnityEngine.UI.Image>().sprite==FishingUITheme.TornParchmentSprite,"Catch card is within reading reach and uses the Windows parchment asset");
            var portrait=catchPanel.GetComponentInChildren<UnityEngine.UI.RawImage>();
            Check(portrait!=null && portrait.texture==Resources.Load<Texture2D>("FishingFish/"+c.Career.PendingCatch.fish.kind+"Thumbnail"),"Catch card shares the desktop's fish photograph");
            foreach(var text in texts)Check(text.preferredHeight<=text.rectTransform.rect.height+1,"Catch card text fits: "+text.text.Replace('\n',' '));
            if(SessionState.GetBool(Key+"capture",false))FishingXRPresentationValidation.CaptureCanvas(catchPanel,Path.Combine(SessionState.GetString(Key+"images",""),"pending-catch-card.png"));
            c.CatchRig.Apply(.02f);Check(c.ActiveFish.GetComponent<Rigidbody>().isKinematic && c.ActiveFish.GetComponent<Collider>().isTrigger,"fish handling cannot impart forces to player or boat");
            Check(!c.Cast() && !c.Scoop(),"pending fish blocks new cast and duplicate scoop");
            c.LeaveStation();Check(c.Anchored,"pending fish cannot disappear through ordinary sailing command");
            FishingSaveStore.Save(path,c.Career);loaded=FishingSaveStore.Load(path,out warning);
            Check(warning=="" && loaded.HasPendingCatch && loaded.PendingCatch.fish.id==c.Career.PendingCatch.fish.id,"undecided catch survives real JSON round trip");
            c.CatchRig.ToggleInspect();Check(c.CatchRig.Location==FishingCatchRig.Place.Hand,"fish can enter inspection pose");
            c.TogglePause();Check(c.HasPendingCatch && c.CatchRig.Location==FishingCatchRig.Place.Tray,"pause safely parks fish without settling rewards");c.TogglePause();
            c.CatchRig.Apply(.02f);Snapshot(c,"02-pending-fish",c.NetTray+new Vector3(1.3f,.8f,1.6f),c.NetTray);
            Check(c.DockConsole.ResolvePendingFromPrimary() && c.Career.Count==hold+1 && c.Career.Catches==catches+1,"The production A-button handler stores fish exactly once");
            Check(!c.ResolveCatch(true),"repeat storage cannot duplicate fish");
            c.Hands.ResetInteraction();Check(c.Hands.HoldRodForButtons() && c.Hands.HoldingRod,"After storage A can take the rod again without a hard-to-reach physical grip");
            c.Hands.ResetInteraction();Check(!c.Hands.HoldingRod,"Menu, tracking and focus reset still release shortcut-held rods");
            Check(c.Hands.HoldRodForButtons() && c.Cast(),"Start a real cast with the button-held rod before stowing");
            c.LeaveStation();
            Check(!c.Anchored && !c.Hands.HoldingRod && c.Session.Phase==CatchPhase.Idle && !c.CastMotion.Casting
                && !c.CastMotion.Preparing && !c.Hands.XRReeling && !c.CatchRig.NetHeld,"Stowing cancels the active line, rod ownership, net and reeling immediately");
            c.TogglePause();Check(c.ReturnHome() && c.Home.Inside && c.Ashore && !c.Paused,"Quick home works from pause and places the player on the real house floor");
            Check(c.Shore.TrySaveFooting(out var homeFeet) && Vector3.Distance(homeFeet,c.Home.Origin+new Vector3(0,.22f,-3.5f))<.12f,"Home arrival passes real floor support and standing clearance checks");
            int homeCoins=c.Career.Coins,homeHold=c.Career.Count,homeWood=c.Career.Backpack.Count(FieldItem.Driftwood);
            Check(c.ReturnHome() && c.Career.Coins==homeCoins && c.Career.Count==homeHold && c.Career.Backpack.Count(FieldItem.Driftwood)==homeWood,"Repeated quick home preserves coins, fish box and backpack");
            c.ReturnToHarbor();c.SellAll();Landing(c);
            c.Scoop();for(int i=0;i<45 && !c.HasPendingCatch;i++)c.CatchRig.StepDesktop(.02f);
            Check(c.HasPendingCatch && !c.Career.PendingCatch.measured,"ordinary repeat catch does not require inspection");
            c.CatchRig.ConfigureLayout(true);
            var boatYaw=Quaternion.Euler(0,c.Player.transform.eulerAngles.y,0);
            var shoulder=c.BoatPoint(new Vector3(-.25f,1.35f,-.1f));
            var captureGrip=c.LandingTargetForMode(true)-boatYaw*Vector3.left*FishingDetailArt.NetMouthOffset;
            Check(Vector3.Distance(shoulder,captureGrip)<.8f,"XR left-side net capture is within nominal seated reach");
            var box=c.FishBox.position+Vector3.up*.6f;var shaft=boatYaw*new Vector3(-.6f,0,.8f);
            var tipGrip=box-shaft*FishingDetailArt.NetMouthOffset;
            Check(Vector3.Distance(shoulder,tipGrip)<.8f,"net-to-box tilt fits nominal reach without a third hand");
            var tilt=Quaternion.AngleAxis(45,shaft)*Quaternion.FromToRotation(Vector3.right,shaft);
            bool transferred=false;for(int i=0;i<12;i++)transferred=c.CatchRig.TryTipIntoBox(box,tilt,.02f)||transferred;
            Check(transferred && !c.HasPendingCatch && c.Career.Count==1,"actual net/box proximity and gentle tilt quick-store a normal fish");
            c.CatchRig.ConfigureLayout(false);
            c.ReturnToHarbor();c.SellAll();Landing(c);
            while(c.Career.Count<c.Career.Capacity)c.Career.Store(Fish());
            c.Scoop();for(int i=0;i<45 && !c.HasPendingCatch;i++)c.CatchRig.StepDesktop(.02f);
            Check(c.HasPendingCatch && !c.ResolveCatch(true) && c.HasPendingCatch,"full box preserves pending ownership");
            string pendingHome=c.Career.PendingCatch.fish.id;
            Check(c.ReturnHome() && c.Home.Inside && c.HasPendingCatch && c.Career.PendingCatch.fish.id==pendingHome,"Quick home reaches the house with a full fish box and preserves the undecided catch");
            Check(!c.DockConsole.ResolvePendingFromPrimary() && c.HasPendingCatch && c.Career.PendingCatch.fish.id==pendingHome,"The same explicit A catch handler remains safe on shore when the fish box is full");
            c.ReturnToHarbor();Check(c.HasPendingCatch && !c.Anchored && c.AtHarbor,"rescue retains fish and permits harbor cleanup");
            coins=c.Career.Coins;
            Check(!c.DockConsole.Press(FishingDockAction.Release),"release needs a separate confirmation");
            Check(c.DockConsole.Press(FishingDockAction.Release) && !c.HasPendingCatch && c.Career.Coins==coins && c.Career.ReleasedCatches==1,"ordinary release records outcome without money duplication");
            Check(!c.DockConsole.Press(FishingDockAction.Release),"already released fish cannot settle again");
            FishingSaveStore.Save(path,c.Career);loaded=FishingSaveStore.Load(path,out warning);
            Check(warning=="" && !loaded.HasPendingCatch && loaded.ReleasedCatches==1,"resolved outcome persists without phantom fish");
            c.SellAll();Landing(c);c.Scoop();for(int i=0;i<45 && !c.HasPendingCatch;i++)c.CatchRig.StepDesktop(.02f);
            string homeFish=c.Career.PendingCatch.fish.id;int beforeHome=c.Career.Count;
            Check(c.ReturnHome() && c.Home.Inside && c.Career.PendingCatch.fish.id==homeFish,"Home travel preserves an ordinary undecided catch without auto-storing");
            Check(c.DockConsole.ResolvePendingFromPrimary() && !c.HasPendingCatch && c.Career.Count==beforeHome+1,"The production A handler explicitly stores the preserved catch after arriving inside the house");
            c.OpenCrafting();Check(c.InventoryOpen || c.XRNotebook.IsOpen,"The real crafting window opens before quick home");
            Check(c.ReturnHome() && !c.InventoryOpen && !c.XRNotebook.IsOpen && !c.Paused && !c.WorldActionBusy,"Quick home closes the crafting window and restores running gameplay");
            Check(c.Career.Backpack.Count(FieldItem.Driftwood)>0 || c.Career.Backpack.TryAdd(FieldItem.Driftwood,1),"Isolated handcraft fixture has a piece of wood");
            var saw=c.HandGames.Station(2);
            Check(c.Shore.Relocate(c.HandGames.Approach(2),Quaternion.LookRotation(-saw.forward)),"The saw has a real supported approach for the travel boundary check");
            var handcraftEye=c.Shore.Position+Vector3.up*1.65f;
            c.viewCamera.transform.SetPositionAndRotation(handcraftEye,Quaternion.LookRotation(saw.TransformPoint(new Vector3(0,1.1f,0))-handcraftEye));
            Check(c.HandGames.Begin(2) && c.HandGames.Active,"A real unfinished handcraft starts before quick home");
            int sawWood=c.Career.Backpack.Count(FieldItem.Driftwood);
            Check(c.ReturnHome() && !c.HandGames.Active && !c.StoryBlocksGameplay && c.Career.Backpack.Count(FieldItem.Driftwood)==sawWood,"Quick home cancels unfinished handcraft without spending its materials or retaining tabletop mode");
            c.ReturnToHarbor();yield return null;
            var combatBody=c.Player.engine.RB;
            combatBody.position=FishingCoastalPresentation.Berth(1)+Vector3.up*.4f;combatBody.rotation=Quaternion.Euler(0,180,0);combatBody.velocity=combatBody.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(combatBody.position,combatBody.rotation);Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"The optional combat island has a real landing before home travel");
            var board=c.Combat.transform.Find("礁影试炼告示 · 自选挑战");var boardApproach=board.position+Vector3.forward*1.8f;boardApproach.y=0;
            Check(c.Shore.Ground(boardApproach,out var boardFloor) && c.Shore.Relocate(boardFloor,Quaternion.Euler(0,180,0)),"The combat board has a supported travel-test approach");
            Check(c.Combat.TryInteract() && c.Combat.Challenging,"The production combat board starts an optional challenge before quick home");
            int homeWins=c.Career.GuardianWins,combatCoins=c.Career.Coins;
            Check(c.ReturnHome() && !c.Combat.Challenging && !c.Combat.InDangerZone && c.Combat.Health==100,"Quick home synchronously ends combat without waiting for a focused update");
            int warningRings=0,enemyTargets=0;
            // Combat shares the controller GameObject, so its descendants also contain unrelated world art and UI lines.
            foreach(var ring in c.Combat.GetComponentsInChildren<LineRenderer>(true))if(ring.name=="蓄力危险圈")
            {warningRings++;Check(!ring.enabled,"Quick home clears the independent enemy warning ring "+ring.GetInstanceID());}
            foreach(var hitbox in c.Combat.GetComponentsInChildren<BoxCollider>(true))if(hitbox.name=="礁蟹守卫" || hitbox.name=="夜行礁蟹")
            {enemyTargets++;Check(!hitbox.gameObject.activeInHierarchy && !c.Combat.IsTarget(hitbox),"Quick home removes live enemy target "+hitbox.name);}
            Check(warningRings==2 && enemyTargets==2,"Travel cleanup checks cover both real enemy warning rings and both enemy targets");
            Check(c.Career.GuardianWins==homeWins && c.Career.Coins==combatCoins && !c.StoryBlocksGameplay,"Leaving combat for home neither claims rewards nor leaves gameplay blocked");
        }
    }
}
