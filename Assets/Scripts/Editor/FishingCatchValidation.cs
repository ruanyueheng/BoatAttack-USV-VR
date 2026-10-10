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
        private static void Landing(FishingPrototype c)
        {
            c.ReturnToHarbor();c.MoveToSpotForAutomation(0);c.Interact();Check(c.Cast(),"start real production catch");var s=c.Session;
            for(int i=0;i<1500 && s.Phase==CatchPhase.Waiting;i++)s.Tick(.02f,false);Check(c.Hook(),"hook real bite");
            for(int i=0;i<6000 && s.Phase==CatchPhase.Fighting;i++)s.Tick(.02f,s.Tension<.6f);
            Check(s.Phase==CatchPhase.Landing,"existing fish fight reaches landing");s.HoldForNet();for(int i=0;i<300;i++)s.Tick(.1f,false);Check(s.Phase==CatchPhase.Landing,"stable wait lets the left hand leave the reel and take the net");
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
            for(int i=0;i<12;i++){c.DockConsole.Press(FishingDockAction.NextWaypoint);Check(c.NavigationTarget<c.NavigationCount && (c.NavigationTarget==0 || c.NavigationTarget>c.fishingSpots.Length || FishingGrounds.Available(c.NavigationTarget-1,c.Career.Expedition)),"XR navigation includes coast/shop and skips locked fishing destinations "+i);}
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
            c.CatchRig.Apply(.02f);Check(c.ActiveFish.GetComponent<Rigidbody>().isKinematic && c.ActiveFish.GetComponent<Collider>().isTrigger,"fish handling cannot impart forces to player or boat");
            Check(!c.Cast() && !c.Scoop(),"pending fish blocks new cast and duplicate scoop");
            c.LeaveStation();Check(c.Anchored,"pending fish cannot disappear through ordinary sailing command");
            FishingSaveStore.Save(path,c.Career);loaded=FishingSaveStore.Load(path,out warning);
            Check(warning=="" && loaded.HasPendingCatch && loaded.PendingCatch.fish.id==c.Career.PendingCatch.fish.id,"undecided catch survives real JSON round trip");
            c.CatchRig.ToggleInspect();Check(c.CatchRig.Location==FishingCatchRig.Place.Hand,"fish can enter inspection pose");
            c.TogglePause();Check(c.HasPendingCatch && c.CatchRig.Location==FishingCatchRig.Place.Tray,"pause safely parks fish without settling rewards");c.TogglePause();
            c.CatchRig.Apply(.02f);Snapshot(c,"02-pending-fish",c.NetTray+new Vector3(1.3f,.8f,1.6f),c.NetTray);
            Check(c.ResolveCatch(true) && c.Career.Count==hold+1 && c.Career.Catches==catches+1,"explicit storage commits only once");
            Check(!c.ResolveCatch(true),"repeat storage cannot duplicate fish");
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
            c.ReturnToHarbor();Check(c.HasPendingCatch && !c.Anchored && c.AtHarbor,"rescue retains fish and permits harbor cleanup");
            coins=c.Career.Coins;
            Check(!c.DockConsole.Press(FishingDockAction.Release),"release needs a separate confirmation");
            Check(c.DockConsole.Press(FishingDockAction.Release) && !c.HasPendingCatch && c.Career.Coins==coins && c.Career.ReleasedCatches==1,"ordinary release records outcome without money duplication");
            Check(!c.DockConsole.Press(FishingDockAction.Release),"already released fish cannot settle again");
            FishingSaveStore.Save(path,c.Career);loaded=FishingSaveStore.Load(path,out warning);
            Check(warning=="" && !loaded.HasPendingCatch && loaded.ReleasedCatches==1,"resolved outcome persists without phantom fish");
        }
    }
}
