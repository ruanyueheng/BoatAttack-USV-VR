using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        private Transform _holsteredLantern;
        private Renderer _holsterRenderer;
        private readonly FishingPressEdge _lanternGrip=new FishingPressEdge();
        private bool _lanternInHand;
        private MaterialPropertyBlock _holsterGlow;
        public Transform HolsteredLantern=>_holsteredLantern;
        public bool LanternHeld=>PresentedItem==FieldItem.Lantern;

        private void UpdateLanternGrip()
        {
            if(!XRSettings.isDeviceActive){_lanternInHand=false;_lanternGrip.Reset();return;}
            bool ready=_c.Ashore && Application.isFocused && !_c.Paused && !_c.StoryBlocksGameplay && !_c.ShopBlocksGameplay && !_c.XRNotebook.BlocksGameplay
                && !_c.WorldActionBusy && !_c.Expedition.Aligning && !_c.XRSeat.HeadBlocked
                && _c.Hands.TryControllerPalm(false,out _,out _) && !_c.Shore.Swimming && !_c.Shore.Climbing;
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);right.TryGetFeatureValue(CommonUsages.gripButton,out bool grip);
            bool grab=_lanternGrip.Step(ready,grip);
            if(!ready || !grip || _c.Career.Backpack.Equipped!=FieldItem.Lantern)_lanternInHand=false;
            if(!grab || _holsteredLantern==null || !_holsteredLantern.gameObject.activeInHierarchy || !_c.Hands.TryControllerPalm(false,out var palm,out _))return;
            if(Vector3.Distance(palm,FishingContactGeometry.ClosestPoint(_holsteredLantern,palm))>.22f)return;
            var bag=_c.Career.Backpack;int slot=-1;
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(bag.At(i).item==FieldItem.Lantern){slot=i;break;}
            if(slot<0)return;
            if(slot>=FishingBackpack.HotbarCount){if(!bag.Move(slot,bag.Selected))return;slot=bag.Selected;}
            _c.SelectFieldSlot(slot);_lanternInHand=true;
        }

        private void PresentLanternHolster()
        {
            bool visible=_c.Career.Backpack.Count(FieldItem.Lantern)>0 && !LanternHeld && !_c.Shore.Swimming && !_c.Shore.Climbing
                && !_c.Home.Sleeping && !_c.Prologue.Open && !_c.Letter.Open && !_c.Realms.Transitioning;
            if(_holsteredLantern==null && visible)
            {
                _holsteredLantern=MeshObject("Waist lantern · grip to lift",ToolMesh(FieldItem.Lantern));
                _holsterRenderer=_holsteredLantern.GetComponent<Renderer>();_holsterRenderer.sharedMaterials=FishingUserTools.Materials(FieldItem.Lantern);
                _holsteredLantern.localScale=Vector3.one*LanternScale;
            }
            if(_holsteredLantern==null)return;
            _holsteredLantern.gameObject.SetActive(visible);if(!visible)return;
            var character=_c.Shore.Character;var rotation=Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0);
            var bounds=ToolMesh(FieldItem.Lantern).bounds;
            float side=character.WaistHalfWidth+(LanternPalm.x-bounds.min.x)*LanternScale+.05f;
            var center=character.HipPosition+rotation*new Vector3(side,0,.025f);
            _holsteredLantern.SetPositionAndRotation(center-rotation*(LanternPalm*LanternScale),rotation);
            if(_holsterGlow==null)_holsterGlow=new MaterialPropertyBlock();
            _holsterGlow.SetColor("_EmissionColor",_c.LanternOn?new Color(1,.55f,.16f)*3.5f:Color.black);_holsterRenderer.SetPropertyBlock(_holsterGlow,1);
            if(_c.LanternOn)
            {_lamp.enabled=true;_lamp.transform.position=_holsteredLantern.TransformPoint(LanternLightPoint);}
        }
    }
}
