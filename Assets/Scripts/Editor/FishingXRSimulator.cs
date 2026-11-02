using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using BoatAttack.VR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>Editor-only, file-controlled OpenXR simulator. Inputs pass through the native XR runtime and production game code.</summary>
    [InitializeOnLoad]
    public static class FishingXRSimulator
    {
        private const string Key="Fishing.XRSimulator", Profile="/interaction_profiles/oculus/touch_controller";
        private static readonly Type Mock=Type.GetType("UnityEngine.XR.OpenXR.Features.Mock.MockRuntime, Unity.XR.OpenXR.Features.MockRuntime");
        private static readonly Type Conformance=Type.GetType("UnityEngine.XR.OpenXR.Features.ConformanceAutomation.ConformanceAutomationFeature, Unity.XR.OpenXR.Features.ConformanceAutomation");
        private static readonly string Root=Path.GetFullPath("Logs/XRSimulator");
        private static readonly Dictionary<OpenXRFeature,bool> Features=new Dictionary<OpenXRFeature,bool>();
        private static readonly Dictionary<string,float?> Calibration=new Dictionary<string,float?>();
        private static FishingPrototype _c;
        private static OpenXRLoader _loader;
        private static MethodInfo _pump;
        private static Camera _mirror;
        private static RenderTexture _target;
        private static Command _command=new Command();
        private static DateTime _lastWrite;
        private static int _commandFrame;
        private static double _nextState, _captureAt;
        private static string _capture;
        private static USVInputMode _previousMode;
        private static bool _started, _ready;

        [Serializable] public sealed class Command
        {
            public int sequence;
            public Vector3 head=new Vector3(0,1.65f,0),headEuler;
            public Vector3 left=new Vector3(-.24f,1.2f,.35f),right=new Vector3(.24f,1.2f,.35f),leftEuler,rightEuler;
            public Vector2 leftStick,rightStick;
            public bool a,b,x,y,leftClick,rightClick;
            public float leftTrigger,rightTrigger,leftGrip,rightGrip;
            public string capture,fixture,equip;
            public int table=-1;
            public int orchard=-1;
            public bool homeReview;
            public bool stop;
        }
        [Serializable] private sealed class Button { public int index; public string label; public Vector3 center; }
        [Serializable] private sealed class State
        {
            public int commandSequence,commandFrame;
            public int frame,spot,page,holdCount,health,shotsFired,photosTaken,day,coins,marketTrades;
            public bool classroom,marketVisible,creativeMode,lanternOn,lanternLightEnabled,lanternHolstered,lanternHeld;
            public Vector3 lanternHangPoint;
            public bool lanternEquipped,leftInspecting,gripperVisible,dockVisible,rodReeling;
            public string catchLocation,pendingId;
            public float rodBend,rodSideBend;
            public Vector3 leftPalm,rightPalm,fishPosition,fishMouth,fishHead,gripperPalm,gripperJaw,rodDeformedTip,rodUnloadedTip,lineStart,lineTarget;
            public string coinDisplay;
            public Vector3 marketPosition;
            public bool xr,focused,isolated,automated,headTracked,leftTracked,rightTracked,seatReady,headBlocked,motionReady,notebook,anchored,ashore,pending;
            public bool a,b,canLand,netHeld,holdingRod,boostActive,toolActive,carryingCan,riflePresented,rifleObstructed,cameraRaised,triggerButton,binocularRaised;
            public bool binocularOpticsActive,binocularEyeAlignment;
            public float binocularLensSeparation,binocularFov;
            public Vector3 binocularLeftEyepiece,binocularRightEyepiece;
            public string hintContext,marketTarget;
            public Vector3 home;
            public float triggerValue,leftPalmRoll,rightPalmRoll;
            public float speed,spotDistance,tension,distance,remaining,walkSpeed,stamina,boostHeat;
            public string phase,beat,message,inputSource,profile,rayTarget,detail,feedback,heldItem;
            public Vector3 camera,cameraEuler,boat,boatEuler,left,right,origin,originEuler,landingTarget,netPosition,netEuler,rodPosition,rodForward,rodTip,rightForward,rodPalm,feet,heldUp,heldForward,heldPosition,canUp,canForward;
            public Vector3[] fishingSpots;
            public USVInputState input;
            public Button[] buttons,marketLabels;
            public bool handcraftActive,handcraftHeld,orchardFruitLanded;
            public int handcraftKind,handcraftStrokes,driftwood,planks,ironIngots,pickHeads,gunParts,islandFruit;
            public Vector3 toolGrip,toolContact,workPoint,toolEuler,rightPalmEuler,orchardFruit;
            public int chestCapacity,pageOffset,bagTreasure,aquariumCount;
            public string chestLastItem,cashierGesture;
            public Vector3 heldRight,rightKnuckles,visualRightPalm,aquarium;
            public float gripRadius;
        }

        static FishingXRSimulator()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingPlayMode)Cleanup();};
            AssemblyReloadEvents.beforeAssemblyReload+=Cleanup;
        }
        public static void Run()
        {
            if(!FishingPrototype.XRSimulatorRequested || FishingAcceptanceOptions.Parse(Environment.GetCommandLineArgs())==null
                || !FishingPrototype.HasArgument(FishingMobileQuality.PreviewFlag))
                throw new InvalidOperationException("Use an explicit acceptance profile and PICO quality flag.");
            Directory.CreateDirectory(Root);
            SessionState.SetBool(Key,true);
            FishingScriptBinding.ValidateScene();
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
            EditorApplication.EnterPlaymode();
        }
        private static object Invoke(Type type,string name,params object[] args)
        {
            var result=type.GetMethod(name,BindingFlags.Public|BindingFlags.Static).Invoke(null,args);
            if(type==Conformance && result is bool ok)Require(ok,"OpenXR rejected "+name+" "+string.Join(",",args));
            return result;
        }
        private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private static void StartRuntime()
        {
            Require(Mock!=null && Conformance!=null,"Installed OpenXR testing features are missing.");
            Require(_c.HasIsolatedProfile && !_c.Automated,"Simulator must use real input and an isolated acceptance profile.");
            foreach(var key in new[]{"FishingXR.LeftPalmXY","FishingXR.RightPalmXY"})Calibration[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetFloat(key):(float?)null;
            _c.Acceptance.enabled=false;
            foreach(var f in OpenXRSettings.Instance.GetFeatures<OpenXRFeature>())
            {
                Features.Add(f,f.enabled);
                f.enabled=f.GetType()==Mock || f.GetType()==Conformance || f.GetType().Name=="OculusTouchControllerProfile";
            }
            Require(Features.Keys.Any(f=>f.GetType()==Mock) && Features.Keys.Any(f=>f.GetType()==Conformance),"XR features have not been discovered.");
            _previousMode=USVInputRouter.Instance.Settings.editorMode;
            USVInputRouter.Instance.Settings.editorMode=USVInputMode.XR;
            _loader=ScriptableObject.CreateInstance<OpenXRLoader>();
            _pump=typeof(OpenXRLoader).GetMethod("ProcessOpenXRMessageLoop",BindingFlags.Instance|BindingFlags.NonPublic);
            Require(_loader.Initialize() && _loader.Start(),"Mock OpenXR could not start.");
            _started=true;
            var go=new GameObject("XR simulator mono mirror");_mirror=go.AddComponent<Camera>();
            _mirror.enabled=false;_mirror.stereoTargetEye=StereoTargetEyeMask.None;
            _target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);_target.Create();
            File.WriteAllText(Path.Combine(Root,"command.json"),JsonUtility.ToJson(_command,true));
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            Debug.Log("[XR-SIM] Started native Mock Runtime; A/B/X/Y layout matches PICO common usages. Mono mirror is not headset performance/stereo certification.");
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            try
            {
                if(_c==null)_c=Object.FindObjectOfType<FishingPrototype>();
                if(_c==null || _c.Acceptance==null)return;
                // A batch editor has no Game view repaint to upload the shoreline before XR starts.
                if(!_c.CoastalDepthReady){if(Application.isBatchMode)_c.viewCamera.Render();return;}
                if(!_started){StartRuntime();return;}
                _pump.Invoke(_loader,null);
                if(!XRSettings.isDeviceActive)return;
                if(!_ready)
                {
                    Require((bool)Invoke(Conformance,"ConformanceAutomationSetActive",Profile,"/user/hand/left",true),"Left controller activation failed.");
                    Require((bool)Invoke(Conformance,"ConformanceAutomationSetActive",Profile,"/user/hand/right",true),"Right controller activation failed.");
                    _ready=true;
                }
                var file=Path.Combine(Root,"command.json");var written=File.GetLastWriteTimeUtc(file);
                if(written!=_lastWrite)
                {
                    string json;
                    try
                    {
                        using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                        using(var reader=new StreamReader(stream))json=reader.ReadToEnd();
                    }
                    catch(IOException ex) when((ex.HResult&0xffff)==32 || (ex.HResult&0xffff)==33){return;}
                    var next=JsonUtility.FromJson<Command>(json);
                    Validate(next);
                    if(!string.IsNullOrEmpty(next.fixture) && next.fixture!=_command.fixture)
                    {
                        _c.Acceptance.SelectCase((FishingAcceptanceCase)Enum.Parse(typeof(FishingAcceptanceCase),next.fixture));
                        if(_c.Acceptance.FreezeScenario)_c.Acceptance.ToggleFreeze();
                        Debug.Log("[XR-SIM] Isolated fixture selected, live input: "+next.fixture);
                    }
                    if(!string.IsNullOrEmpty(next.equip) && next.equip!=_command.equip)
                    {
                        Require(_c.HasIsolatedProfile,"Tool fixtures require the explicit temporary profile.");
                        var item=(FieldItem)Enum.Parse(typeof(FieldItem),next.equip);var bag=_c.Career.Backpack;
                        if(bag.Count(item)==0)Require(bag.TryAdd(item,1),"Tool fixture does not fit.");
                        int slot=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==item);
                        if(slot>=FishingBackpack.HotbarCount){Require(bag.Move(slot,bag.Selected),"Cannot equip fixture.");slot=bag.Selected;}
                        _c.SelectFieldSlot(slot);
                    }
                    if(next.table>=2 && next.table!=_command.table)
                    {
                        Require(_c.HasIsolatedProfile && _c.Ashore,"Handcraft fixture requires an isolated shore profile.");
                        _c.HandGames.Cancel();var station=_c.HandGames.Station(next.table);
                        Require(_c.Shore.Relocate(_c.HandGames.Approach(next.table),Quaternion.LookRotation(-station.forward)),"No supported handcraft approach.");
                        var item=next.table==2?FieldItem.Driftwood:FieldItem.IronIngot;int needed=next.table==2?1:6;
                        if(_c.Career.Backpack.Count(item)<needed)Require(_c.Career.Backpack.TryAdd(item,needed-_c.Career.Backpack.Count(item)),"Handcraft fixture material does not fit.");
                        _c.View.ResetLook();
                    }
                    if(next.orchard>=0 && next.orchard!=_command.orchard)
                    {
                        Require(_c.HasIsolatedProfile && _c.Ashore,"Orchard fixture requires an isolated shore profile.");
                        var tree=_c.Home.OrchardTreeRoot(next.orchard);
                        Require(_c.Shore.Relocate(tree.position+Vector3.back*1.55f,Quaternion.identity),"No supported orchard approach.");
                        Require(_c.Home.ShakeOrchardTree(next.orchard),"Orchard fixture cannot drop a fresh fruit.");
                        _c.View.ResetLook();
                    }
                    if(next.homeReview && !_command.homeReview)
                    {
                        Require(_c.HasIsolatedProfile && _c.Ashore,"Home review requires an isolated shore profile.");
                        _c.CancelWorldAction();_c.XRNotebook.Close();var career=_c.Career;
                        while(career.HomeStorage.UsedSlots<143)Require(career.HomeStorage.TryAdd(FieldItem.Glass,64),"Home review chest fill failed.");
                        Require(career.HomeStorage.TryAdd(FieldItem.SeaTreasure,1) && career.HomeStorage.At(143).item==FieldItem.SeaTreasure,"Home review final slot failed.");
                        if(career.AquariumLevel==0)Require(career.Backpack.TryAdd(FieldItem.Aquarium,1) && career.BuildAquarium(),"Home review aquarium failed.");
                        if(career.AquariumCount==0)
                        {
                            var fish=new CatchRecord{id="v059-review-fish",kind=FishKind.Coral,grams=800,value=FishingSpecies.Value(FishKind.Coral,false,800)};
                            Require(career.Store(fish) && career.DisplayFish(fish.id),"Home review displayed fish failed.");
                        }
                        _c.HomeEcology.Refresh();FishingSaveStore.Save(_c.SavePath,career);
                        Require(_c.Shore.Relocate(_c.Home.Origin+new Vector3(3.1f,.2f,-1.35f),Quaternion.Euler(0,90,0)),"Home review chest approach failed.");
                        _c.View.ResetLook();
                    }
                    _command=next;_lastWrite=written;_commandFrame=Time.frameCount;
                    if(next.stop){SessionState.SetBool(Key,false);Cleanup();EditorApplication.Exit(0);return;}
                    EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
                    if(!string.IsNullOrEmpty(next.capture)){_capture=next.capture;_captureAt=EditorApplication.timeSinceStartup+.5;}
                    Head(_command.head,Quaternion.Euler(_command.headEuler));
                    Hand("left",_command.left,Quaternion.Euler(_command.leftEuler),_command.leftStick,_command.x,_command.y,_command.leftClick,_command.leftTrigger,_command.leftGrip);
                    Hand("right",_command.right,Quaternion.Euler(_command.rightEuler),_command.rightStick,_command.a,_command.b,_command.rightClick,_command.rightTrigger,_command.rightGrip);
                    File.AppendAllText(Path.Combine(Root,"commands.jsonl"),JsonUtility.ToJson(_command)+"\n");
                }
                if(EditorApplication.timeSinceStartup>=_nextState){WriteState();_nextState=EditorApplication.timeSinceStartup+.25;}
                if(_capture!=null && EditorApplication.timeSinceStartup>=_captureAt){Capture(_capture);_capture=null;}
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch(Exception e)
            {
                Debug.LogException(e);File.WriteAllText(Path.Combine(Root,"failure.txt"),e.ToString());
                SessionState.SetBool(Key,false);Cleanup();EditorApplication.Exit(1);
            }
        }
        private static void Validate(Command c)
        {
            Require(c!=null,"Missing command.");
            Require(c.table==-1 || c.table==2 || c.table==3,"Unknown handcraft table fixture.");
            Require(c.orchard>=-1 && c.orchard<FishingCareer.HomeFruitTreeCount,"Unknown orchard fixture.");
            foreach(var v in new[]{c.head,c.headEuler,c.left,c.right,c.leftEuler,c.rightEuler,new Vector3(c.leftStick.x,c.leftStick.y,c.leftTrigger),new Vector3(c.rightStick.x,c.rightStick.y,c.rightTrigger),new Vector3(c.leftGrip,c.rightGrip,0)})
                Require(!float.IsNaN(v.sqrMagnitude) && !float.IsInfinity(v.sqrMagnitude) && v.magnitude<1000,"Non-finite/out-of-range simulator input.");
            Require(string.IsNullOrEmpty(c.capture) || (c.capture.Length<65 && c.capture.All(ch=>char.IsLetterOrDigit(ch)||ch=='-'||ch=='_')),"Capture name must be a filename stem.");
            Require(string.IsNullOrEmpty(c.fixture) || (Enum.TryParse(c.fixture,out FishingAcceptanceCase selected) && Enum.GetName(typeof(FishingAcceptanceCase),selected)==c.fixture),"Unknown fixture.");
            Require(string.IsNullOrEmpty(c.equip) || (Enum.TryParse(c.equip,out FieldItem item) && Enum.GetName(typeof(FieldItem),item)==c.equip && item!=FieldItem.Empty),"Unknown tool fixture.");
        }
        private static void Head(Vector3 position,Quaternion rotation)
        {
            var method=Mock.GetMethods().Single(m=>m.Name=="SetSpace" && m.GetParameters()[0].ParameterType.IsEnum);
            var types=method.GetParameters();
            var flags=Enum.Parse(types[3].ParameterType,"PositionValid, OrientationValid, PositionTracked, OrientationTracked");
            method.Invoke(null,new[]{Enum.Parse(types[0].ParameterType,"View"),(object)position,rotation,flags});
            var stereo=Enum.Parse(Mock.GetMethod("SetViewPose").GetParameters()[0].ParameterType,"PrimaryStereo");
            for(int eye=0;eye<2;eye++)Invoke(Mock,"SetViewPose",stereo,eye,position+rotation*Vector3.right*(eye==0?-.032f:.032f),rotation,new Vector4(-.7f,.7f,.7f,-.7f));
        }
        private static void Hand(string side,Vector3 position,Quaternion rotation,Vector2 stick,bool primary,bool secondary,bool clicked,float trigger,float grip)
        {
            string user="/user/hand/"+side;
            Invoke(Conformance,"ConformanceAutomationSetPose",user,user+"/input/grip/pose",position,rotation);
            Invoke(Conformance,"ConformanceAutomationSetPose",user,user+"/input/aim/pose",position,rotation);
            Invoke(Conformance,"ConformanceAutomationSetVec2",user,user+"/input/thumbstick",Vector2.ClampMagnitude(stick,1));
            Invoke(Conformance,"ConformanceAutomationSetFloat",user,user+"/input/trigger/value",Mathf.Clamp01(trigger));
            Invoke(Conformance,"ConformanceAutomationSetFloat",user,user+"/input/squeeze/value",Mathf.Clamp01(grip));
            Invoke(Conformance,"ConformanceAutomationSetBool",user,user+"/input/"+(side=="left"?"x":"a")+"/click",primary);
            Invoke(Conformance,"ConformanceAutomationSetBool",user,user+"/input/"+(side=="left"?"y":"b")+"/click",secondary);
            Invoke(Conformance,"ConformanceAutomationSetBool",user,user+"/input/thumbstick/click",clicked);
        }
        private static bool Tracked(XRNode node)=>InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.isTracked,out bool value)&&value;
        private static void WriteState()
        {
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            right.TryGetFeatureValue(CommonUsages.primaryButton,out bool a);right.TryGetFeatureValue(CommonUsages.secondaryButton,out bool b);
            right.TryGetFeatureValue(CommonUsages.triggerButton,out bool triggerButton);right.TryGetFeatureValue(CommonUsages.trigger,out float triggerValue);
            FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out var left,out _);
            FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out var r,out var q,true);
            string hitName=Physics.Raycast(r,q*Vector3.forward,out var hit,3,1<<5,QueryTriggerInteraction.Collide)?hit.collider.name:"";
            _c.Hands.TryControllerPalm(true,out var lp,out _);_c.Hands.TryControllerPalm(false,out var rp,out var rq);
            bool fishReady=_c.HasPendingCatch && _c.ActiveFish!=null;var gripper=_c.CatchRig.FishGripper;
            var dynamics=_c.Hands.Rod.GetComponent<FishingRodDynamics>();
            var restTip=dynamics!=null?(Vector3)typeof(FishingRodDynamics).GetField("_restTip",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dynamics):Vector3.zero;
            var fishingLine=(LineRenderer)typeof(FishingAnglingPresentation).GetField("_line",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_c.Angling);
            var s=new State{commandSequence=_command.sequence,commandFrame=_commandFrame,frame=Time.frameCount,xr=XRSettings.isDeviceActive,focused=Application.isFocused,isolated=_c.HasIsolatedProfile,automated=_c.Automated,
                chestCapacity=_c.Career.HomeStorage.Capacity,chestLastItem=_c.Career.HomeStorage.At(143).item.ToString(),bagTreasure=_c.Career.Backpack.Count(FieldItem.SeaTreasure),
                pageOffset=(int)typeof(FishingXRNotebook).GetField("_offset",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_c.XRNotebook),
                aquarium=_c.HomeEcology.AquariumPosition,aquariumCount=_c.Career.AquariumCount,cashierGesture=_c.TackleShop.Cashier.Gesture,
                heldRight=_c.FieldWorld.HeldTool.right,rightKnuckles=_c.Hands.Right.Palm.forward,visualRightPalm=_c.Shore.Character.PalmPosition(false),gripRadius=_c.Hands.Right.GripRadius,
                headTracked=Tracked(XRNode.Head),leftTracked=Tracked(XRNode.LeftHand),rightTracked=Tracked(XRNode.RightHand),seatReady=_c.XRSeat.Ready,headBlocked=_c.XRSeat.HeadBlocked,
                motionReady=_c.XRNotebook.MotionReady,notebook=_c.XRNotebook.IsOpen,page=(int)typeof(FishingXRNotebook).GetField("_page",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_c.XRNotebook),
                anchored=_c.Anchored,ashore=_c.Ashore,pending=_c.HasPendingCatch,phase=_c.Session.Phase.ToString(),message=_c.VisibleMessage,speed=_c.Speed,spot=_c.Spot,spotDistance=_c.SpotDistance,tension=_c.Session.Tension,distance=_c.Session.Distance,
                inputSource=USVInputRouter.Instance.ActiveSource.ToString(),input=USVInputRouter.Instance.Current,a=a,b=b,profile=_c.SavePath,
                holdCount=_c.Career.Count,health=_c.Combat.Health,canLand=_c.Session.CanLand,remaining=_c.Session.Remaining,beat=_c.Session.Beat.ToString(),netHeld=_c.CatchRig.NetHeld,holdingRod=_c.Hands.HoldingRod,
                shotsFired=_c.RifleHunting.ShotsFired,riflePresented=_c.RifleHunting.Presented,rifleObstructed=_c.RifleHunting.Obstructed,
                photosTaken=_c.Life.PhotosTaken,cameraRaised=_c.Life.VrRaised,triggerButton=triggerButton,triggerValue=triggerValue,
                binocularRaised=_c.BinocularsRaised,hintContext=FishingXRHints.Current(_c).ToString(),
                binocularOpticsActive=_c.Life.Binoculars.Active,binocularEyeAlignment=_c.FieldWorld.BinocularEyeAlignment,
                binocularLensSeparation=_c.Life.Binoculars.LensSeparation,binocularFov=_c.Life.Binoculars.OpticalCamera!=null?_c.Life.Binoculars.OpticalCamera.fieldOfView:0,
                binocularLeftEyepiece=_c.Life.Binoculars.LeftEyepiece!=null?_c.Life.Binoculars.LeftEyepiece.position:Vector3.zero,
                binocularRightEyepiece=_c.Life.Binoculars.RightEyepiece!=null?_c.Life.Binoculars.RightEyepiece.position:Vector3.zero,
                leftPalmRoll=FishingHands.ControllerRoll(true),rightPalmRoll=FishingHands.ControllerRoll(false),
                classroom=_c.ClassroomDemoActive,day=_c.Career.Calendar.Day,coins=_c.Career.Coins,creativeMode=_c.CreativeMode,coinDisplay=_c.Career.CoinDisplay,
                lanternOn=_c.LanternOn,lanternLightEnabled=_c.FieldWorld.LanternLight!=null && _c.FieldWorld.LanternLight.enabled,
                lanternHolstered=_c.FieldWorld.HolsteredLantern!=null && _c.FieldWorld.HolsteredLantern.gameObject.activeInHierarchy,
                lanternHeld=_c.FieldWorld.LanternHeld,lanternHangPoint=_c.FieldWorld.HolsteredLantern!=null?_c.FieldWorld.HolsteredLantern.TransformPoint(FishingFieldWorld.LanternHangPoint):Vector3.zero,
                lanternEquipped=_c.LanternEquipped,leftInspecting=_c.CatchRig.LeftHandInspecting,gripperVisible=gripper!=null && gripper.gameObject.activeInHierarchy,dockVisible=_c.DockConsole.Visible,
                catchLocation=_c.CatchRig.Location.ToString(),pendingId=fishReady?_c.Career.PendingCatch.fish.id:"",leftPalm=lp,rightPalm=rp,
                fishPosition=fishReady?_c.ActiveFish.position:Vector3.zero,fishMouth=fishReady?_c.ActiveFish.TransformPoint(_c.CatchRig.FishMouthLocal):Vector3.zero,
                fishHead=fishReady?_c.ActiveFish.rotation*_c.CatchRig.FishHeadAxis:Vector3.zero,
                gripperPalm=gripper!=null?gripper.TransformPoint(FishingCatchRig.GripperPalm):Vector3.zero,gripperJaw=gripper!=null?gripper.TransformPoint(FishingCatchRig.GripperJaw):Vector3.zero,
                rodBend=dynamics!=null?dynamics.CurrentBend:0,rodSideBend=dynamics!=null?dynamics.CurrentSideBend:0,rodReeling=_c.Hands.XRReeling,
                rodDeformedTip=dynamics!=null?dynamics.Tip.position:Vector3.zero,rodUnloadedTip=_c.Hands.Rod.TransformPoint(restTip),
                lineStart=fishingLine!=null && fishingLine.positionCount>0?fishingLine.GetPosition(0):Vector3.zero,lineTarget=_c.Session.Phase==CatchPhase.Landing?_c.LandingTarget:_c.Angling.WaterTarget,
                marketVisible=_c.SeaMarket.Visible,marketPosition=_c.SeaMarket.Position,marketTrades=_c.SeaMarket.Trades,
                handcraftActive=_c.HandGames.Active,handcraftHeld=_c.HandGames.XRToolHeld,handcraftKind=_c.HandGames.Active?(int)_c.HandGames.Task.Kind:-1,handcraftStrokes=_c.HandGames.Task?.Strokes??0,
                driftwood=_c.Career.Backpack.Count(FieldItem.Driftwood),planks=_c.Career.Backpack.Count(FieldItem.Plank),ironIngots=_c.Career.Backpack.Count(FieldItem.IronIngot),pickHeads=_c.Career.Backpack.Count(FieldItem.IronPickHead),gunParts=_c.Career.Backpack.Count(FieldItem.GunParts),
                toolGrip=_c.HandGames.Active?_c.HandGames.HandlePoint:_command.table>=2?((Transform[])typeof(FishingHandGames).GetField("_workRight",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_c.HandGames))[_command.table-2].position:Vector3.zero,
                toolContact=_c.HandGames.ToolContact,workPoint=_c.HandGames.WorkPoint,toolEuler=_c.HandGames.Active?_c.HandGames.ActiveTool.eulerAngles:Vector3.zero,rightPalmEuler=rq.eulerAngles,
                orchardFruit=_command.orchard>=0?_c.Home.OrchardFruit(_command.orchard).position:Vector3.zero,orchardFruitLanded=_command.orchard>=0 && _c.Home.OrchardFruitLanded(_command.orchard),islandFruit=_c.Career.Backpack.Count(FieldItem.IslandFruit),
                home=_c.Home.Origin,marketTarget=_c.SeaMarket.Target?.ToString()??"",
                marketLabels=_c.SeaMarket.transform.Find("V044 roaming market ship").GetComponentsInChildren<TextMesh>().Select(t=>new Button{label=t.text,center=t.transform.position}).ToArray(),
                feet=_c.Shore.Position,walkSpeed=_c.Shore.WalkSpeed,stamina=_c.Shore.Stamina,boostActive=_c.Boost.Active,boostHeat=_c.Boost.Heat,
                heldItem=_c.FieldWorld.PresentedItem.ToString(),toolActive=_c.FieldWorld.HeldTool!=null&&_c.FieldWorld.HeldTool.gameObject.activeInHierarchy,
                heldUp=_c.FieldWorld.HeldTool!=null?_c.FieldWorld.HeldTool.up:Vector3.zero,heldForward=_c.FieldWorld.HeldTool!=null?_c.FieldWorld.HeldTool.forward:Vector3.zero,heldPosition=_c.FieldWorld.HeldTool!=null?_c.FieldWorld.HeldTool.position:Vector3.zero,
                carryingCan=_c.Farm.CarryingCan,canUp=_c.Farm.WateringCan.up,canForward=_c.Farm.WateringCan.TransformDirection(new Vector3(FishingFarmWorld.CanSpout.x,0,FishingFarmWorld.CanSpout.z).normalized),
                origin=_c.XRSeat.Origin.transform.position,originEuler=_c.XRSeat.Origin.transform.eulerAngles,landingTarget=_c.LandingTarget,netPosition=_c.LandingNet.position,netEuler=_c.LandingNet.eulerAngles,
                detail=((Text)typeof(FishingXRNotebook).GetField("_status",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_c.XRNotebook)).text,
                feedback=((Text)typeof(FishingXRNotebook).GetField("_feedbackText",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_c.XRNotebook)).text,
                camera=_c.viewCamera.transform.position,cameraEuler=_c.viewCamera.transform.eulerAngles,boat=_c.Player.transform.position,boatEuler=_c.Player.transform.eulerAngles,left=left,right=r,rayTarget=hitName,fishingSpots=_c.fishingSpots,
                rodPosition=_c.Hands.Rod.position,rodForward=_c.Hands.Rod.forward,rodTip=_c.Hands.Rod.TransformPoint(new Vector3(0,-.015f,2.03f)),rightForward=q*Vector3.forward,rodPalm=_c.Hands.Right.Palm.position,
                buttons=Object.FindObjectsOfType<FishingNotebookButton>().Where(x=>x.Owner==_c.XRNotebook).Select(x=>new Button{index=x.Index,label=string.Join(" / ",x.GetComponentsInChildren<Text>().Select(t=>t.text)),center=x.GetComponent<Collider>().bounds.center}).ToArray()};
            File.WriteAllText(Path.Combine(Root,"state.json"),JsonUtility.ToJson(s,true));
        }
        private static void Capture(string name)
        {
            _mirror.CopyFrom(_c.viewCamera);_mirror.enabled=false;_mirror.stereoTargetEye=StereoTargetEyeMask.None;
            _mirror.transform.SetPositionAndRotation(_c.viewCamera.transform.position,_c.viewCamera.transform.rotation);
            _mirror.targetTexture=_target;_mirror.fieldOfView=80;_mirror.aspect=1280f/720;_mirror.ResetProjectionMatrix();_mirror.ResetWorldToCameraMatrix();
            _mirror.GetUniversalAdditionalCameraData().allowXRRendering=false;
            var previous=RenderTexture.active;
            try
            {
                _mirror.Render();RenderTexture.active=_target;
                var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(Root,name+".png"),texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            finally{RenderTexture.active=previous;}
            Debug.Log("[XR-SIM] CAPTURE "+name);
        }
        private static void Cleanup()
        {
            foreach(var entry in Calibration){if(entry.Value.HasValue)PlayerPrefs.SetFloat(entry.Key,entry.Value.Value);else PlayerPrefs.DeleteKey(entry.Key);}
            if(Calibration.Count>0){Calibration.Clear();PlayerPrefs.Save();}
            if(_loader!=null){if(_started)_loader.Stop();_loader.Deinitialize();Object.DestroyImmediate(_loader);}
            foreach(var pair in Features)if(pair.Key!=null)pair.Key.enabled=pair.Value;Features.Clear();
            if(USVInputRouter.Instance!=null && _started)USVInputRouter.Instance.Settings.editorMode=_previousMode;
            if(_mirror!=null)Object.DestroyImmediate(_mirror.gameObject);
            if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}
            _started=_ready=false;
        }
    }
}
