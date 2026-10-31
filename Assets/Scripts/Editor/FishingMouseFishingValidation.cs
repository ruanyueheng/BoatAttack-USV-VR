using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace BoatAttack.EditorTools
{
    public static class FishingMouseFishingValidation
    {
        public static void Run()=>FishingV011Validation.Begin("MOUSE-FISHING");
        private sealed class TestCursor:IFishingCursor
        {public CursorLockMode LockState{get;set;}public bool Visible{get;set;}=true;}
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Debug.Log("[MOUSE-FISHING] CHECK: "+text);}
        private static void InputFrame()
        {
            // Batch editor has no focused Game view. Select the same Dynamic update used by
            // MonoBehaviour.Update, rather than the editor-only device state buffer.
            typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null)
                .Invoke(null,new object[]{InputUpdateType.Dynamic});
        }
        private static void MouseFrame(FishingPrototype c,Mouse mouse,FishingPointerGate gate,bool held,float dx=0,bool overUI=false,bool focused=true)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.45f),delta=new Vector2(dx,0),buttons=(ushort)(held?1:0)});
            InputFrame();
            // This is the production prelude that used to cancel preparation on every desktop frame.
            c.XRNotebook.Tick(false);c.Hands.ReadXRTools();
            bool ready=gate.Update(focused,c.Paused,held,false);
            c.TickDesktopAngling(mouse,ready,overUI);c.View.ApplyView(focused,false);
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            var oldMouse=Mouse.current;var oldKeyboard=Keyboard.current;var oldCursor=c.View.Pointer;
            var oldBackground=InputSystem.settings.backgroundBehavior;
            var oldEditorRouting=InputSystem.settings.editorInputBehaviorInPlayMode;
            bool oldRunInBackground=Application.runInBackground;
            Application.runInBackground=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();var cursor=new TestCursor();c.View.Pointer=cursor;
            var gate=new FishingPointerGate();var original=c.Career.Snapshot();
            string output=Path.GetFullPath("Logs/MouseFishing/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
            try
            {
                Check(c.Automated && c.HasIsolatedProfile,"mouse tests use an isolated career");
                c.ReturnToHarbor();c.RestoreShopTestProfile(new FishingCareer().Snapshot());c.MoveToSpotForAutomation(0);c.Interact();
                if(!c.FirstPerson)c.ToggleView();c.View.ResetLook();
                for(int i=0;i<8;i++)yield return null;
                MouseFrame(c,mouse,gate,false);MouseFrame(c,mouse,gate,true);
                Debug.Log("[MOUSE-FISHING] PRESS: anchored="+c.Anchored+" phase="+c.Session.Phase+" held="+mouse.leftButton.isPressed+" down="+mouse.leftButton.wasPressedThisFrame+" ready="+gate.Ready+" drag="+c.DesktopFishingDrag+" paused="+c.Paused+" story="+c.StoryBlocksGameplay+" shop="+c.ShopBlocksGameplay+" dock="+c.DockConsole.BlocksGameplay);
                Check(c.CastMotion.Preparing && c.DesktopFishingDrag,"actual left press begins a captured desktop cast");
                for(int i=0;i<12;i++){c.CastMotion.Tick(.05f);MouseFrame(c,mouse,gate,true);}
                Check(c.CastMotion.Preparing && c.CastMotion.Charge>.45f,"closed XR notebook does not erase preparation across held frames");
                MouseFrame(c,mouse,gate,false);
                Check(c.Session.Phase==CatchPhase.Waiting && c.CastMotion.Casting && !c.CastMotion.Preparing,"real mouse release launches the float");
                Check(cursor.LockState==CursorLockMode.None && cursor.Visible,"release returns the desktop cursor");
                c.Session.Cancel();c.CastMotion.Reset();MouseFrame(c,mouse,gate,false);
                MouseFrame(c,mouse,gate,true,0,true);MouseFrame(c,mouse,gate,false);
                Check(c.Session.Phase==CatchPhase.Idle && !c.CastMotion.Preparing,"a press that begins on HUD cannot launch a cast");
                MouseFrame(c,mouse,gate,true);MouseFrame(c,mouse,gate,true,80,true);MouseFrame(c,mouse,gate,false,0,true);
                Check(c.Session.Phase==CatchPhase.Waiting,"a world-started drag still launches when released over HUD");
                c.Session.Cancel();c.CastMotion.Reset();MouseFrame(c,mouse,gate,false);MouseFrame(c,mouse,gate,true);
                MouseFrame(c,mouse,gate,true,0,false,false);MouseFrame(c,mouse,gate,true);
                Check(!c.CastMotion.Preparing && c.Session.Phase==CatchPhase.Idle,"focus loss cancels the cast and a held return cannot restart it");
                MouseFrame(c,mouse,gate,false);MouseFrame(c,mouse,gate,true);MouseFrame(c,mouse,gate,false);
                Check(c.Session.Phase==CatchPhase.Waiting,"a fresh tap after recovery casts normally");
                FishingCatchValidation.KeepOrdinaryReceipt(c);
                for(int i=0;i<20;i++)c.CastMotion.Tick(.1f);
                c.Angling.Target(CatchPhase.Waiting);c.Angling.Tick(.05f,false,true);c.Angling.PresentRod(.05f);
                for(int i=0;i<500 && c.Session.Phase==CatchPhase.Waiting;i++)c.Session.Tick(.02f,false);
                MouseFrame(c,mouse,gate,true);
                Check(c.Session.Phase==CatchPhase.Fighting && c.DesktopReeling,"fresh mouse press hooks and held left button reels");
                var camera=c.viewCamera.transform.rotation;
                MouseFrame(c,mouse,gate,true,110);
                Check(c.RodControl>.5f && Quaternion.Angle(camera,c.viewCamera.transform.rotation)<.1f && cursor.LockState==CursorLockMode.Locked,"held mouse-right motion steers the rod without spinning the camera");
                MouseFrame(c,mouse,gate,true,-220);
                Check(c.RodControl<-.5f,"held mouse-left motion bends toward the opposite side");
                var dynamics=c.Hands.Rod.GetComponent<FishingRodDynamics>();
                Check(dynamics.SpoolVertexCount>0,"the original spool coil/lip vertices are identified separately from the handle");
                float distance=c.Session.Distance,turns=dynamics.SpoolTurns;
                for(int i=0;i<5;i++){c.Session.Tick(.05f,c.DesktopReeling,c.RodControl);c.Angling.Tick(.05f,true,true);c.Angling.PresentRod(.05f);}
                Check(c.Session.Distance<distance && dynamics.SpoolTurns>turns,"held input retrieves real line and winds the original spool inward");
                MouseFrame(c,mouse,gate,false);distance=c.Session.Distance;turns=dynamics.SpoolTurns;
                for(int i=0;i<5;i++){c.Session.Tick(.05f,c.DesktopReeling,c.RodControl);c.Angling.Tick(.05f,false,true);c.Angling.PresentRod(.05f);}
                Check(!c.DesktopReeling && c.RodControl==0 && c.Session.Distance>distance && dynamics.SpoolTurns<turns,"releasing left lets the fish take line and reverses spool travel");
                for(int i=0;i<6500 && c.Session.Phase==CatchPhase.Fighting;i++)c.Session.Tick(.02f,c.Session.Tension<.55f && c.Session.Beat!=FishBeat.Leap,-Math.Sign(c.Session.FishDirection));
                Check(c.Session.Phase==CatchPhase.Landing,"the mouse fight reaches the landing phase");
                yield return null;
                MouseFrame(c,mouse,gate,true);
                Check(c.HasPendingCatch && c.CatchRig.LeftHandInspecting && c.Career.Count==0,"left click lifts a durable undecided live fish before it enters the net");
                MouseFrame(c,mouse,gate,false);
                Check(!c.ResolveCatch(true),"the hand-held fish must be placed in the net before storage");
                for(int i=0;i<100 && !c.CatchRig.LeftInspectionSettled;i++)yield return null;
                c.PresentCharacterFrame();
                var grip=c.ActiveFish.TransformPoint(c.CatchRig.LeftInspectionGrip);
                Check(c.CatchRig.LeftInspectionSettled && c.Hands.Left.Visible && Vector3.Distance(c.Shore.Character.PalmPosition(true),grip)<.035f,"the real left hand reaches the fish grip without stretching the source arm");
                Check(c.Hands.Rod.gameObject.activeInHierarchy && c.Hands.Right.Visible,"the right hand keeps the rod during left-hand inspection");
                Debug.Log("[MOUSE-FISHING] INSPECTION GEOMETRY: eye="+c.viewCamera.transform.position+" palm="+grip+" fish="+c.ActiveFish.position+" viewport="+c.viewCamera.WorldToViewportPoint(c.ActiveFish.position)+" mesh="+c.ActiveFish.GetComponentInChildren<MeshRenderer>().bounds);
                var fishView=c.viewCamera.WorldToViewportPoint(c.ActiveFish.GetComponentInChildren<MeshRenderer>().bounds.center);
                Check(fishView.z>.08f && fishView.x>.04f && fishView.x<.9f && fishView.y>.08f && fishView.y<.95f,"the left-held live fish is actually visible in first-person view");
                FishingV016Validation.Capture(c,output,"left-hand-live-fish");
                string receipt=c.Career.PendingCatch.fish.id;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));InputFrame();
                typeof(FishingPrototype).GetMethod("ReadPendingDesktop",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{keyboard});
                Check(c.CatchRig.PlacingInNet,"the production E input starts hand-to-net placement");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputFrame();
                for(int i=0;i<100 && c.CatchRig.PlacingInNet;i++)yield return null;
                for(int i=0;i<3;i++)yield return null;
                c.PresentCharacterFrame();
                var net=c.LandingNet.TransformPoint(new Vector3(FishingDetailArt.NetMouthOffset,-.13f,0));
                Check(c.CatchRig.Location==FishingCatchRig.Place.Net && Vector3.Distance(c.ActiveFish.position,net)<.045f
                    && c.HasPendingCatch && c.Career.PendingCatch.fish.id==receipt && c.Career.Count==0,"placement leaves the same live fish in the real net without granting it twice");
                Check(c.Hands.Right.Visible && Vector3.Distance(c.Shore.Character.PalmPosition(false),c.LandingNet.TransformPoint(new Vector3(.05f,.035f,0)))<.035f,"the right hand visibly supports the net after the rod is stowed");
                FishingV016Validation.Capture(c,output,"live-fish-in-net");
                // Exercise the real tracked-hand path with synthetic controller poses,
                // without claiming an attached HMD or sending physical input.
                Check(c.CatchRig.TryGrabLeftFish(c.ActiveFish.TransformPoint(c.CatchRig.LeftInspectionGrip)),"left grip can take the pending fish back out of its net");
                var trackedFrame=Quaternion.Euler(0,c.Shore.Character.transform.eulerAngles.y,0);
                var trackedPalm=c.Shore.Character.ShoulderPosition(true)+trackedFrame*new Vector3(-.02f,.16f,.30f);
                var trackedRotation=trackedFrame*FishingCatchRig.GripperPalmRotation;
                c.CatchRig.ApplyTrackedLeftInspection(trackedPalm,trackedRotation,true,.02f);
                var gripper=c.CatchRig.FishGripper;
                Check(c.CatchRig.LeftHandInspecting && gripper!=null && gripper.gameObject.activeInHierarchy && Vector3.Distance(gripper.TransformPoint(FishingCatchRig.GripperPalm),trackedPalm)<.002f,"tracked inspection visibly holds the gripper handle at the calibrated palm");
                Check(Vector3.Distance(c.ActiveFish.TransformPoint(c.CatchRig.FishMouthLocal),gripper.TransformPoint(FishingCatchRig.GripperJaw))<.002f,"real fish snout touches the metal jaw with no floating gap");
                Check(Vector3.Distance(FishingContactGeometry.ClosestPoint(gripper,gripper.TransformPoint(FishingCatchRig.GripperJaw)),gripper.TransformPoint(FishingCatchRig.GripperJaw))<.006f,"the rendered metal jaw exists at the fish mouth rather than a missing short arc");
                Check(Vector3.Dot(c.ActiveFish.rotation*c.CatchRig.FishHeadAxis,Vector3.up)>.99f && c.ActiveFish.position.y<trackedPalm.y-.15f,"the fish hangs head-up below the left hand under gravity");
                c.Shore.Character.ApplyHandTargets(c.Hands.Left,c.Hands.Right,true);
                Check(c.Hands.Left.HasGripSurface && Vector3.Distance(c.Shore.Character.PalmPosition(true),gripper.TransformPoint(FishingCatchRig.GripperPalm))<.035f,"the actual rendered hand reaches the real handle and supplies finger contact geometry");
                var skin=c.Shore.Character.FirstPersonSkin;var desktopSkin=skin.sharedMesh;bool recalc=skin.forceMatrixRecalculationPerRender;
                skin.sharedMesh=c.Shore.Character.TrackedHandsMesh;skin.forceMatrixRecalculationPerRender=true;
                var baked=new Mesh();
                try
                {
                    skin.BakeMesh(baked,false);var vertices=baked.vertices;var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                    var visible=new System.Collections.Generic.HashSet<int>(skin.sharedMesh.triangles);
                    float nearest=float.MaxValue;int samples=0;
                    var a=c.Hands.Left.GripStart;var ab=c.Hands.Left.GripEnd-a;
                    foreach(int i in visible)
                    {
                        var w=weights[i];
                        bool hand=(w.weight0>.4f && bones[w.boneIndex0].IsChildOf(c.Shore.Character.LeftWrist))
                            || (w.weight1>.4f && bones[w.boneIndex1].IsChildOf(c.Shore.Character.LeftWrist));
                        if(!hand)continue;samples++;
                        var p=skin.transform.TransformPoint(vertices[i]);
                        float handleDistance=Vector3.Distance(p,a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/ab.sqrMagnitude));
                        nearest=Mathf.Min(nearest,handleDistance);
                    }
                    Check(samples>20 && nearest<c.Hands.Left.GripRadius+.015f,"visible tracked-hand skinned vertices really touch the rubber handle");
                }
                finally{UnityEngine.Object.Destroy(baked);}
                var cameraPosition=c.viewCamera.transform.position;var cameraRotation=c.viewCamera.transform.rotation;float cameraFov=c.viewCamera.fieldOfView;
                c.viewCamera.fieldOfView=95; // Wide HMD-like diagnostic framing, not an actual eye projection.
                c.viewCamera.transform.LookAt(Vector3.Lerp(trackedPalm,c.ActiveFish.position,.55f));
                FishingV016Validation.Capture(c,output,"tracked-gripper-reference");
                skin.sharedMesh=desktopSkin;skin.forceMatrixRecalculationPerRender=recalc;
                c.viewCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);c.viewCamera.fieldOfView=cameraFov;
                var priorHandleRotation=gripper.rotation;
                trackedPalm+=Vector3.right*.15f;trackedRotation*=Quaternion.Euler(0,0,45);
                c.CatchRig.ApplyTrackedLeftInspection(trackedPalm,trackedRotation,true,.02f);
                Check(Vector3.Distance(gripper.TransformPoint(FishingCatchRig.GripperPalm),trackedPalm)<.002f && Quaternion.Angle(priorHandleRotation,gripper.rotation)>30 && Vector3.Dot(c.ActiveFish.rotation*c.CatchRig.FishHeadAxis,Vector3.up)>.99f,"wrist roll rotates the handle while its swivel keeps the fish hanging downward");
                Check(Vector3.Distance(c.ActiveFish.TransformPoint(c.CatchRig.FishMouthLocal),gripper.TransformPoint(FishingCatchRig.GripperJaw))<.002f,"mouth and jaw remain joined while the controller turns");
                c.CatchRig.ApplyTrackedLeftInspection(trackedPalm,trackedRotation,false,.02f);
                Check(!gripper.gameObject.activeSelf && c.CatchRig.Location==FishingCatchRig.Place.Tray && c.HasPendingCatch && c.Career.PendingCatch.fish.id==receipt && c.Career.Count==0,"releasing away from the net stows the gripper and safely parks the same fish without settling its receipt");
                Check(c.CatchRig.TryGrabLeftFish(c.ActiveFish.TransformPoint(c.CatchRig.LeftInspectionGrip)),"the same safely parked fish can be grabbed again");
                c.CatchRig.ApplyTrackedLeftInspection(trackedPalm,trackedRotation,true,0);
                var trackedMouth=c.LandingNet.TransformPoint(new Vector3(FishingDetailArt.NetMouthOffset,-.13f,0));
                trackedPalm=trackedMouth+c.ActiveFish.rotation*Vector3.Scale(c.CatchRig.FishMouthLocal,c.ActiveFish.lossyScale)-gripper.rotation*(FishingCatchRig.GripperJaw-FishingCatchRig.GripperPalm);
                c.CatchRig.ApplyTrackedLeftInspection(trackedPalm,trackedRotation,true,0);
                c.CatchRig.ApplyTrackedLeftInspection(trackedPalm,trackedRotation,false,0);
                Check(c.CatchRig.Location==FishingCatchRig.Place.Net && c.HasPendingCatch && c.Career.Count==0,"releasing a tracked left-held fish over the mouth returns it to the net exactly once");
                c.PresentCharacterFrame();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));InputFrame();
                typeof(FishingPrototype).GetMethod("ReadPendingDesktop",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{keyboard});
                Check(c.WorldActionBusy && c.Actions.Kind==FishingActionKind.Store && c.Career.Count==0,
                    "the production F input starts storage without granting the fish early");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputFrame();
                c.TickWorldAction(.1f,true,false);
                Check(c.HasPendingCatch && c.Career.Count==0,"storage retains the receipt until hand contact");
                c.TickWorldAction(1,true,false);
                Check(c.LastActionSucceeded && !c.HasPendingCatch && !c.RequestCatchResolution(true)
                    && !c.ResolveCatch(true) && c.Career.Count==1,"the production storage action commits exactly once");
                var loaded=FishingSaveStore.Load(c.SavePath,out var warning);
                Check(warning=="" && loaded.Count==1 && !loaded.HasPendingCatch,"new hand-first workflow survives the existing save format");
                Debug.Log("[MOUSE-FISHING] EVIDENCE: "+output);
            }
            finally
            {
                c.View.Pointer=oldCursor;InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorRouting;
                Application.runInBackground=oldRunInBackground;
                if(oldMouse!=null)oldMouse.MakeCurrent();if(oldKeyboard!=null)oldKeyboard.MakeCurrent();
                if(c.Paused)c.TogglePause();c.ReturnToHarbor();c.RestoreShopTestProfile(original);
            }
        }
    }
}
