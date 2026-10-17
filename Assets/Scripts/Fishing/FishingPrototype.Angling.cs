using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingAnglingPresentation Angling { get; private set; }
        public float RodControl { get; private set; }
        private bool _rodControlReady;
        private readonly FishingMouseAngling _mouseAngling=new FishingMouseAngling();
        public bool DesktopFishingDrag=>_mouseAngling.Captured && Anchored && !HasPendingCatch && !Paused;
        public bool DesktopReeling=>DesktopFishingDrag && Session.Phase==CatchPhase.Fighting && Mouse.current!=null
            && Mouse.current.leftButton.isPressed && !Mouse.current.rightButton.isPressed;
        // Shared production adapter; isolated validation feeds real Input System mouse events here.
        public void TickDesktopAngling(Mouse mouse,bool ready,bool overUI)
        {
            bool allowed=ready && mouse!=null && Anchored && !HasPendingCatch && !Paused && !_xrActive
                && !InventoryOpen && !JournalOpen && !StoryBlocksGameplay && !ShopBlocksGameplay && !DockConsole.BlocksGameplay
                && !mouse.rightButton.isPressed;
            bool pressed=mouse!=null && mouse.leftButton.wasPressedThisFrame;
            bool held=mouse!=null && mouse.leftButton.isPressed;
            bool release=_mouseAngling.Step(allowed,pressed,held,mouse!=null && mouse.leftButton.wasReleasedThisFrame,overUI,mouse==null?0:mouse.delta.ReadValue().x);
            if(!allowed){CastMotion.CancelPreparation();RodControl=0;return;}
            if(pressed && _mouseAngling.Captured)
            {
                if(Session.Phase==CatchPhase.Bite)Hook();
                else if(Session.Phase==CatchPhase.Landing)CatchRig.RequestLiftForInspection();
                else if(CanChangeBait && !CastMotion.Casting)CastMotion.Prepare();
            }
            if(release && CastMotion.Preparing && CanChangeBait)Cast(CastMotion.Release());
            RodControl=Session.Phase==CatchPhase.Fighting && held && _mouseAngling.Captured?_mouseAngling.Side:0;
        }
        private bool AnglingTrackingReady=>!_xrActive || (XRSeat.Ready && !XRSeat.HeadBlocked
            && FishingXRTracking.TryPose(viewCamera,XRNode.RightHand,out _,out _)
            && FishingXRTracking.TryPose(viewCamera,XRNode.LeftHand,out _,out _));
        private bool AnglingAvailable=>Anchored && !HasPendingCatch && !Paused && !InventoryOpen && !JournalOpen && !StoryBlocksGameplay
            && !ShopBlocksGameplay && !XRNotebook.BlocksGameplay && !DockConsole.BlocksGameplay && AnglingTrackingReady
            && (Application.isFocused || (Automated && HasIsolatedProfile));
        private float ReadRodControl(Keyboard keyboard,Gamepad gamepad,bool pointerReady,bool gamepadReady)
        {
            if(Automated || !Anchored || Paused || Session.Phase!=CatchPhase.Fighting || InventoryOpen || JournalOpen
                || StoryBlocksGameplay || ShopBlocksGameplay || XRNotebook.BlocksGameplay || DockConsole.BlocksGameplay)return 0;
            if(_xrActive)return AnglingTrackingReady?Hands.XRSteer:0;
            float side=0;
            if(pointerReady && _mouseAngling.Captured)side=_mouseAngling.Side;
            if(gamepadReady && gamepad!=null && gamepad.rightTrigger.ReadValue()>.5f && Mathf.Abs(gamepad.leftStick.x.ReadValue())>.2f)side=gamepad.leftStick.x.ReadValue();
            if(_mouseAngling.Captured)return side;
            if(Mathf.Abs(side)<.12f)_rodControlReady=true;
            return _rodControlReady?Mathf.Clamp(side,-1,1):0;
        }
        private void BuildAngling()
        {
            Angling=gameObject.AddComponent<FishingAnglingPresentation>();
            Angling.Build(this,_rodTip,_line,_bobber.transform);
        }
        private void TickAnglingPresentation()
        {
            bool active=AnglingAvailable;
            Angling.Tick(active?Time.deltaTime:0,_reeling,active);
            if(!active){RodControl=0;_rodControlReady=false;_mouseAngling.Reset();}
        }
    }
}
