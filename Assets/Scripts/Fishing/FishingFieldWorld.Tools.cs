using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        public Transform HeldTool=>_held;
        public FieldItem PresentedItem { get; private set; }
        public static readonly Vector3 PickPalm=new Vector3(.035f,-.09f,0);
        public static readonly Vector3 PickTip=new Vector3(.28f,.26f,0);
        public const float PickScale=.9f;
        private FieldItem _previewItem;
        private float _previewUntil;
        public void ShowPickupPreview(FieldItem item){_previewItem=item;_previewUntil=Time.time+1.4f;}
        public void ClearToolPresentation(){_previewUntil=0;if(_held!=null)_held.gameObject.SetActive(false);if(_lamp!=null)_lamp.enabled=false;}
        public static Vector3 PickContactPalm(Vector3 contact,Quaternion rotation)=>contact+rotation*((PickPalm-PickTip)*PickScale);
        private Mesh ToolMesh(FieldItem item)
        {
            if(_tools.TryGetValue(item,out var mesh))return mesh;
            mesh=FishingWorkshopArt.Make(item);_tools.Add(item,mesh);_owned.Add(mesh);return mesh;
        }
        private void PresentHeldTool()
        {
            if(_held==null)return;
            var item=_c.Career.Backpack.Equipped;
            bool preview=_previewUntil>Time.time && !_c.WorldActionBusy;
            if(preview)item=_previewItem;
            bool pick=item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe;
            bool facility=item==FieldItem.Workbench || item==FieldItem.Furnace;
            bool tool=pick || facility || item==FieldItem.FieldGuide || item==FieldItem.Binoculars || item==FieldItem.Lantern || preview;
            bool visible=_c.Ashore && _c.Hands.CanPresentTools && !_c.ShopBlocksGameplay && !_c.Hands.HasGesture && !_c.Hands.PresentingPickup && !_c.Expedition.Aligning && !_c.ActionSuppressesHeldTool && tool && !XRSettings.isDeviceActive;
            _held.gameObject.SetActive(visible);_lamp.enabled=false;
            PresentedItem=visible?item:FieldItem.Empty;
            if(!visible)return;
            _heldMesh.sharedMesh=ToolMesh(item);
            _c.Hands.GetToolFrame(out var origin,out var frame);
            var holdFrame=Quaternion.Euler(0,frame.eulerAngles.y,0);
            Vector3 leftPalm=Vector3.zero,rightPalm=Vector3.zero;
            Quaternion leftRotation=Quaternion.identity,rightRotation=Quaternion.identity;
            float scale=.65f,curl=.65f;bool twoHands=false;
            Vector3 position=origin+holdFrame*new Vector3(.17f,-.22f,.23f);
            Quaternion rotation=frame;
            switch(item)
            {
                case FieldItem.FieldGuide:
                    twoHands=true;scale=.72f;curl=.35f;position=origin+holdFrame*new Vector3(0,-.22f,.27f);rotation=frame*Quaternion.Euler(20,0,0);
                    leftPalm=new Vector3(-.18f,-.03f,-.06f);rightPalm=new Vector3(.18f,-.03f,-.06f);
                    leftRotation=rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
                case FieldItem.Binoculars:
                    twoHands=true;scale=.45f;curl=.65f;
                    position=_c.BinocularsRaised?_c.Shore.Character.EyePosition+frame*Vector3.forward*.025f:origin+holdFrame*new Vector3(0,-.18f,.24f);
                    leftPalm=new Vector3(-.20f,0,-.05f);rightPalm=new Vector3(.20f,0,-.05f);
                    leftRotation=Quaternion.LookRotation(Vector3.forward,Vector3.left);rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.right);break;
                case FieldItem.Lantern:
                    scale=.7f;rightPalm=new Vector3(0,.63f,0);rightRotation=Quaternion.LookRotation(Vector3.right,Vector3.up);
                    position=origin+holdFrame*new Vector3(.23f,-.25f,.18f)-rotation*(rightPalm*scale);break;
                case FieldItem.StonePickaxe:case FieldItem.IronPickaxe:
                    scale=PickScale;rightPalm=PickPalm;curl=.88f;rightRotation=Quaternion.LookRotation(Vector3.up,Vector3.right);
                    rotation=frame*Quaternion.Euler(0,0,-18);position=origin+holdFrame*new Vector3(.22f,-.24f,.24f)-rotation*(rightPalm*scale);break;
                case FieldItem.Workbench:case FieldItem.Furnace:
                    twoHands=true;scale=.35f;leftPalm=new Vector3(-.52f,.6f,-.3f);rightPalm=new Vector3(.52f,.6f,-.3f);
                    position=origin+holdFrame*new Vector3(0,-.38f,.22f);leftRotation=rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
                default:
                    rightPalm=new Vector3(0,-.035f,0);rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
            }
            _held.localScale=Vector3.one*scale;_held.SetPositionAndRotation(position,rotation);
            if(!_c.Hands.FitToolPose(_held,leftPalm,rightPalm,twoHands,true)){_held.gameObject.SetActive(false);return;}
            if(_c.ActionUsesHeldTool && pick)
            {
                // 镐尖与接触许可使用同一个目标，不能先夹短手臂再让远处矿石扣减。
                var contactRotation=_c.ActionToolRotation;
                var contactPalm=PickContactPalm(_c.ActionTargetPosition,contactRotation);
                var palm=Vector3.Lerp(_c.ActionStartPalm,contactPalm,_c.ActionContactBlend);
                _held.rotation=Quaternion.Slerp(rotation,contactRotation,_c.ActionContactBlend);
                _held.position=palm-_held.rotation*(rightPalm*scale);
            }
            if(twoHands)_c.Hands.SetToolTarget(true,_held,leftPalm,leftRotation,curl);
            else _c.Hands.RestHand(true);
            _c.Hands.SetToolTarget(false,_held,rightPalm,rightRotation,curl);
            _lamp.enabled=item==FieldItem.Lantern && _c.LanternOn;
            _lamp.transform.position=_held.TransformPoint(Vector3.up*.27f);
        }
    }
}
