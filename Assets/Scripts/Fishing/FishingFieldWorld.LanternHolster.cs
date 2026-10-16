using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        private Transform _holsteredLantern;
        private Transform _lanternMount;
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
                _holsterRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                _holsteredLantern.localScale=Vector3.one*LanternScale;
                var mount=new FishingMeshBuilder();var brass=new Color(.43f,.31f,.14f);var leather=new Color(.16f,.09f,.045f);
                mount.Tube(new Vector3(-.075f,0,0),new Vector3(.024f,0,0),.012f,.012f,leather);
                mount.Arc(new Vector3(.025f,-.012f,0),.018f,.004f,20,310,brass,8);
                mount.Tube(new Vector3(.031f,-.029f,0),new Vector3(.072f,-.067f,0),.007f,.007f,leather,8);
                mount.Arc(new Vector3(.075f,-.073f,0),.012f,.0035f,0,360,brass,6);
                var mesh=mount.Finish("Belt hook and short leather lantern strap");_owned.Add(mesh);
                _lanternMount=MeshObject("Belt hook and short leather lantern strap",mesh);
            }
            if(_holsteredLantern==null)return;
            _holsteredLantern.gameObject.SetActive(visible);_lanternMount.gameObject.SetActive(visible);if(!visible)return;
            var character=_c.Shore.Character;var rotation=Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0);
            var bounds=ToolMesh(FieldItem.Lantern).bounds;
            float side=character.WaistHalfWidth+(LanternHangPoint.x-bounds.min.x)*LanternScale+.035f;
            var center=character.BeltPosition+rotation*new Vector3(side,-.073f,.025f);
            _holsteredLantern.SetPositionAndRotation(center-rotation*(LanternHangPoint*LanternScale),rotation);
            _lanternMount.SetPositionAndRotation(center-rotation*new Vector3(.075f,-.073f,0),rotation);
            if(_holsterGlow==null)_holsterGlow=new MaterialPropertyBlock();
            _holsterGlow.SetColor("_EmissionColor",_c.LanternOn?new Color(1,.55f,.16f)*2f:Color.black);_holsterRenderer.SetPropertyBlock(_holsterGlow,1);
            if(_c.LanternOn)
            {_lamp.enabled=true;_lamp.transform.position=_holsteredLantern.TransformPoint(LanternLightPoint);}
        }
    }
}
