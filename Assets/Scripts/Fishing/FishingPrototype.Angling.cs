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
            if(pointerReady && keyboard!=null)
            {
                if(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)side-=1;
                if(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)side+=1;
            }
            if(gamepadReady && gamepad!=null && Mathf.Abs(gamepad.leftStick.x.ReadValue())>.2f)side=gamepad.leftStick.x.ReadValue();
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
            if(!active){RodControl=0;_rodControlReady=false;}
        }
    }
}
