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
                FishingV016Validation.Capture(c,output,"left-hand-live-fish");
                string receipt=c.Career.PendingCatch.fish.id;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));InputFrame();
                typeof(FishingPrototype).GetMethod("ReadPendingDesktop",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{keyboard});
                Check(c.CatchRig.PlacingInNet,"the production E input starts hand-to-net placement");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputFrame();
                for(int i=0;i<100 && c.CatchRig.PlacingInNet;i++)yield return null;
                c.PresentCharacterFrame();
                var net=c.LandingNet.TransformPoint(new Vector3(FishingDetailArt.NetMouthOffset,-.13f,0));
                Check(c.CatchRig.Location==FishingCatchRig.Place.Net && Vector3.Distance(c.ActiveFish.position,net)<.045f
                    && c.HasPendingCatch && c.Career.PendingCatch.fish.id==receipt && c.Career.Count==0,"placement leaves the same live fish in the real net without granting it twice");
                FishingV016Validation.Capture(c,output,"live-fish-in-net");
                Check(c.ResolveCatch(true) && !c.ResolveCatch(true) && c.Career.Count==1,"netted fish stores exactly once through the original transaction");
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
