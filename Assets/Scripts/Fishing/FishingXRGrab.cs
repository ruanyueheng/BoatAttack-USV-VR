using UnityEngine;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    /// <summary>VR: close the right hand (grip) on a dropped item or a hunted animal to take it, the same transaction as A.</summary>
    public sealed class FishingXRGrab : MonoBehaviour
    {
        public const float Reach=.24f;
        private FishingPrototype _c;private readonly FishingPressEdge _grip=new FishingPressEdge(),_leftGrip=new FishingPressEdge();private bool _wasHolding;
        public enum FarmHand{None,TakeCan,ReturnCan,Refill,Water}
        /// <summary>Real-hand farm intent: grip the handle to take, open the hand over the stand to return,
        /// grip the windlass crank with the other hand to draw water, tip the spout down over a plot to water.</summary>
        public static FarmHand DecideFarm(bool carrying,bool rightPressed,bool rightHeld,bool leftPressed,Vector3 rightPalm,Vector3 leftPalm,
            Vector3 handle,Vector3 stand,Vector3 crank,float canTilt,float spoutToPlot)
        {
            if(!carrying)return rightPressed && Vector3.Distance(rightPalm,handle)<.15f?FarmHand.TakeCan:FarmHand.None;
            if(!rightHeld && Vector3.Distance(handle,stand+Vector3.up*.12f)<.3f)return FarmHand.ReturnCan;
            if(leftPressed && Vector3.Distance(leftPalm,crank)<.18f)return FarmHand.Refill;
            if(canTilt>40 && spoutToPlot<.9f)return FarmHand.Water;
            return FarmHand.None;
        }
        public void Build(FishingPrototype c)=>_c=c;
        private void Update()
        {
            if(_c==null || !XRSettings.isDeviceActive)return;
            bool ready=_c.Ashore && Application.isFocused && !_c.Paused && !_c.StoryBlocksGameplay && !_c.ShopBlocksGameplay && !_c.XRNotebook.BlocksGameplay
                && !_c.WorldActionBusy && !_c.XRSeat.HeadBlocked && !_c.Hands.HoldingRod && !_c.Shore.Climbing && !_c.Shore.Swimming
                && !_c.StartMenuOpen && !_c.InventoryOpen && !_c.JournalOpen && !_c.HasPendingCatch && !_c.HandGames.Active;
            bool grip=FishingXRInput.Grip(InputDevices.GetDeviceAtXRNode(XRNode.RightHand));
            bool leftGrip=FishingXRInput.Grip(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand));
            bool rightTracked=_c.Hands.TryControllerPalm(false,out var palm,out _),leftTracked=_c.Hands.TryControllerPalm(true,out var leftPalm,out _);
            bool pressed=_grip.Step(ready && rightTracked && leftTracked,grip),leftPressed=_leftGrip.Step(ready && rightTracked && leftTracked,leftGrip);
            if(pressed && _c.Home.TryGripOrchardFruit(palm) || leftPressed && _c.Home.TryGripOrchardFruit(leftPalm))return;
            if(ready && TryFarmHand(pressed,grip,leftPressed))return;
            if(pressed && TryGrab(palm))return;
            if(leftPressed)TryGrab(leftPalm);
        }
        private bool TryGrab(Vector3 palm)
        {
            if(_c.FieldWorld.TryPickupTarget(palm,out var pickup))
            {_c.FieldWorld.PickupById(pickup.Id,pickup.Item,pickup.Count);return true;}
            if(!FishingBackpack.IsGatherTool(_c.Career.Backpack.Equipped) && _c.WorkshopWorld.TryHarvestTarget(palm,Reach,out var resource))
            {_c.WorkshopWorld.HarvestNode(resource.Id,resource.Item,_c.Career.Backpack.Equipped);return true;}
            if(_c.RifleHunting.TryLootTarget(out var animal))
            {
                var contact=FishingContactGeometry.ClosestPoint(animal.Root,palm);
                if(Vector3.Distance(palm,contact)<Reach && _c.Shore.ClearSight(palm,contact))return _c.RifleHunting.CollectAnimal(animal.Id);
            }
            return false;
        }
        private bool TryFarmHand(bool pressed,bool held,bool leftPressed)
        {
            var farm=_c.Farm;if(farm==null || farm.WateringCan==null || !_c.Hands.TryControllerPalm(false,out var right,out _))return false;
            _c.Hands.TryControllerPalm(true,out var left,out _);
            var can=farm.WateringCan;var handle=can.TransformPoint(FishingFarmWorld.CanPalm);
            int plot=-1;float near=float.MaxValue;var spout=can.TransformPoint(FishingFarmWorld.CanSpout);
            for(int i=0;i<12;i++){float d=FishingPrototype.FlatDistance(spout,farm.Target(i).position);if(d<near){near=d;plot=i;}}
            // A carried can is released by opening the hand; on the ground it only counts over the stand.
            bool releasing=_wasHolding && !held;_wasHolding=held;
            var intent=DecideFarm(farm.CarryingCan,pressed,!releasing,leftPressed,right,left,handle,farm.StandPosition,farm.CrankGrip,Vector3.Angle(can.up,Vector3.up),near);
            switch(intent)
            {
                case FarmHand.TakeCan:case FarmHand.ReturnCan:return farm.InteractAt(15);
                case FarmHand.Refill:return farm.InteractAt(12);
                case FarmHand.Water:return farm.Work(plot)==FarmWork.Water && farm.InteractAt(plot);
            }
            return false;
        }
    }
}
