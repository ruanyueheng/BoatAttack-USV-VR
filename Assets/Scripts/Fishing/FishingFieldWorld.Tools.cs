using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        public Transform HeldTool=>_held;
        public Renderer HeldToolRenderer=>_heldRenderer;
        public FieldItem PresentedItem { get; private set; }
        public static readonly Vector3 PickPalm=new Vector3(.067f,-.09f,.005f);
        public static readonly Vector3 PickTip=new Vector3(.28f,.26f,0);
        public static readonly Vector3 BinocularLeftPalm=new Vector3(-.31f,0,-.055f);
        public static readonly Vector3 BinocularRightPalm=new Vector3(.31f,0,-.055f);
        public const float PickScale=.9f;
        public const float BinocularScale=.52f;
        private Renderer _heldRenderer;
        private FieldItem _previewItem;
        private float _previewUntil;
        public void ShowPickupPreview(FieldItem item){_previewItem=item;_previewUntil=Time.time+1.4f;}
        public void ClearToolPresentation(){_previewUntil=0;CancelHunt();PresentedItem=FieldItem.Empty;if(_held!=null)_held.gameObject.SetActive(false);if(_lamp!=null)_lamp.enabled=false;}
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
            bool tool=pick || facility || item==FieldItem.HuntingSpear || item==FieldItem.FieldGuide || item==FieldItem.Binoculars || item==FieldItem.Lantern || preview;
            bool visible=_c.Ashore && !_c.StoryBlocksGameplay && !_c.Shore.Swimming && !_c.Shore.Climbing && (_c.XRNotebook==null || !_c.XRNotebook.IsOpen) && _c.Hands.CanPresentTools && !_c.ShopBlocksGameplay && !_c.Hands.HasGesture && !_c.Hands.PresentingPickup && !_c.Expedition.Aligning && !_c.ActionSuppressesHeldTool && tool;
            _held.gameObject.SetActive(visible);_lamp.enabled=false;
            PresentedItem=visible?item:FieldItem.Empty;
            if(!visible)return;
            if(_heldRenderer==null)_heldRenderer=_held.GetComponent<Renderer>();
            _heldRenderer.enabled=!(item==FieldItem.Binoculars && _c.View.OpticalView);
            _heldMesh.sharedMesh=ToolMesh(item);
            _c.Hands.GetToolFrame(out _,out var frame);
            var eye=_c.Shore.Character.EyePosition;
            Vector3 leftPalm=Vector3.zero,rightPalm=Vector3.zero;
            Quaternion leftRotation=Quaternion.identity,rightRotation=Quaternion.identity;
            float scale=.65f,curl=.65f;bool twoHands=false;
            var fingers=FishingFingerPose.Uniform(curl);
            Vector3 position=eye+frame*new Vector3(.18f,-.15f,.34f);
            Quaternion rotation=frame;
            switch(item)
            {
                case FieldItem.FieldGuide:
                    twoHands=true;scale=.72f;curl=.3f;fingers=FishingFingerPose.Book;
                    position=eye+frame*new Vector3(0,-.14f,.34f);rotation=frame*Quaternion.Euler(20,0,0);
                    leftPalm=new Vector3(-.18f,-.045f,-.06f);rightPalm=new Vector3(.18f,-.045f,-.06f);
                    leftRotation=rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
                case FieldItem.Binoculars:
                    twoHands=true;scale=BinocularScale;curl=.5f;fingers=FishingFingerPose.Barrel;
                    // 握点在镜筒外侧，手指绕镜筒横截面弯曲；目镜后端与眼睛保留明确间距。
                    position=eye+frame*Vector3.Lerp(new Vector3(0,-.115f,.35f),new Vector3(0,0,.18f),Mathf.SmoothStep(0,1,_c.View.BinocularBlend));
                    leftPalm=BinocularLeftPalm;rightPalm=BinocularRightPalm;
                    leftRotation=Quaternion.LookRotation(Vector3.up,Vector3.left);rightRotation=Quaternion.LookRotation(Vector3.up,Vector3.right);break;
                case FieldItem.Lantern:
                    scale=.7f;fingers=FishingFingerPose.Lantern;
                    rightPalm=new Vector3(0,.65f,0);rightRotation=Quaternion.LookRotation(Vector3.right,Vector3.up);
                    position=eye+frame*new Vector3(.20f,-.06f,.34f)-rotation*(rightPalm*scale);break;
                case FieldItem.StonePickaxe:case FieldItem.IronPickaxe:
                    scale=PickScale;rightPalm=PickPalm;curl=.94f;fingers=FishingFingerPose.Handle;
                    // 镐柄轴为 +Y；掌背向外，指向 +Z，不能复用沿 +Z 的钓竿手势。
                    rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.right);
                    rotation=frame*Quaternion.Euler(25,-20,14);
                    position=eye+frame*new Vector3(.19f,-.14f,.35f)-rotation*(rightPalm*scale);break;
                case FieldItem.HuntingSpear:
                    scale=1;rightPalm=SpearPalm;rightRotation=Quaternion.LookRotation(Vector3.up,Vector3.right);
                    fingers=FishingFingerPose.Handle;position=eye+frame*new Vector3(.20f,-.32f,.35f);
                    if(_huntTarget!=null)
                    {
                        var target=_huntTarget.Root.position+Vector3.up*.22f;
                        rotation=Quaternion.LookRotation(target-position,Vector3.up);
                        var desired=target-rotation*SpearTip;
                        position=Vector3.Lerp(position,desired,Mathf.Sin(Mathf.Clamp01(_thrust/.48f)*Mathf.PI));
                    }
                    break;
                case FieldItem.Workbench:case FieldItem.Furnace:
                    twoHands=true;scale=.35f;leftPalm=new Vector3(-.52f,.6f,-.3f);rightPalm=new Vector3(.52f,.6f,-.3f);
                    position=eye+frame*new Vector3(0,-.46f,.35f);leftRotation=rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
                default:
                    rightPalm=new Vector3(0,-.035f,0);rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
            }
            _held.localScale=Vector3.one*scale;_held.SetPositionAndRotation(position,rotation);
            bool tracked=XRSettings.isDeviceActive;
            if(tracked)
            {
                if(!_c.Hands.TryControllerPalm(false,out var palm,out var palmFrame)){ClearToolPresentation();_held.gameObject.SetActive(false);return;}
                _held.rotation=palmFrame*Quaternion.Inverse(rightRotation);
                _held.position=palm-_held.rotation*(rightPalm*scale);
                twoHands=twoHands && _c.Hands.Left.Visible && Vector3.Distance(_c.Hands.Left.Palm.position,_held.TransformPoint(leftPalm))<.18f;
            }
            if(!tracked && !_c.Hands.FitToolPose(_held,leftPalm,rightPalm,twoHands,true)){_held.gameObject.SetActive(false);PresentedItem=FieldItem.Empty;return;}
            if(_c.ActionUsesHeldTool && pick)
            {
                // 镐尖与接触许可使用同一个目标，不能先夹短手臂再让远处矿石扣减。
                var contactRotation=_c.ActionToolRotation;
                var contactPalm=PickContactPalm(_c.ActionTargetPosition,contactRotation);
                var palm=Vector3.Lerp(_c.ActionStartPalm,contactPalm,_c.ActionContactBlend);
                _held.rotation=Quaternion.Slerp(rotation,contactRotation,_c.ActionContactBlend);
                _held.position=palm-_held.rotation*(rightPalm*scale);
            }
            if(twoHands)_c.Hands.SetToolTarget(true,_held,leftPalm,leftRotation,curl,fingers:fingers);
            else if(!tracked)_c.Hands.RestHand(true);
            _c.Hands.SetToolTarget(false,_held,rightPalm,rightRotation,curl,fingers:fingers);
            _lamp.enabled=item==FieldItem.Lantern && _c.LanternOn;
            _lamp.transform.position=_held.TransformPoint(Vector3.up*.27f);
            if(item==FieldItem.HuntingSpear)CheckSpearContact(tracked);
        }
    }
}
