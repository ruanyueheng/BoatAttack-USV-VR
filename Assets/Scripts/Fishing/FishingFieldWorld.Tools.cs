using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        public Transform HeldTool=>_held;
        public Renderer HeldToolRenderer=>_heldRenderer;
        public FieldItem PresentedItem { get; private set; }
        public static readonly Vector3 PickPalm=new Vector3(.034f,0,0);
        public static readonly Vector3 PickTip=new Vector3(.1531086f,.39003575f,.02396736f);
        private static readonly FishingFingerPose PickFingers=new FishingFingerPose(.65f,.44f,.44f,.42f,1f);
        public static readonly Vector3 BinocularLeftPalm=new Vector3(-.365f,0,-.055f);
        public static readonly Vector3 BinocularRightPalm=new Vector3(.365f,0,-.055f);
        public const float PickScale=.85f;
        public const float LanternScale=.75f;
        public static readonly Vector3 LanternPalm=new Vector3(.09134315f,.3567367f,-.017f);
        public static readonly Vector3 LanternHangPoint=new Vector3(0,.394f,0);
        public static readonly Vector3 LanternLightPoint=new Vector3(0,.14f,0);
        private Material[] _plainToolMaterials;
        private MaterialPropertyBlock _lanternGlow;
        private bool _lanternGlowApplied;
        public const float BinocularScale=.52f;
        private Renderer _heldRenderer;
        private FieldItem _previewItem;
        private float _previewUntil;
        public void ShowPickupPreview(FieldItem item){_previewItem=item;_previewUntil=Time.time+1.4f;}
        public void ClearToolPresentation(){_previewUntil=0;CancelHunt();PresentedItem=FieldItem.Empty;if(_held!=null)_held.gameObject.SetActive(false);if(_lamp!=null)_lamp.enabled=false;}
        public static Vector3 PickContactPalm(Vector3 contact,Quaternion rotation)=>contact+rotation*((PickPalm-PickTip)*PickScale);
        public static Quaternion PickContactRotation(Vector3 heading)=>Quaternion.LookRotation(heading)*Quaternion.FromToRotation(PickTip-PickPalm,new Vector3(0,-.24f,.30f));
        private static Quaternion PickHandRotation(Quaternion tool,Quaternion frame)
        {
            var shaft=tool*Vector3.up;var fingers=Vector3.ProjectOnPlane(frame*Vector3.forward,shaft).normalized;
            if(fingers.sqrMagnitude<.1f)fingers=tool*Vector3.forward;
            return Quaternion.Inverse(tool)*Quaternion.LookRotation(fingers,Vector3.Cross(shaft,fingers));
        }
        private Mesh ToolMesh(FieldItem item)
        {
            if(_tools.TryGetValue(item,out var mesh))return mesh;
            mesh=FishingWorkshopArt.Make(item);_tools.Add(item,mesh);_owned.Add(mesh);return mesh;
        }
        private void PresentHeldTool()
        {
            if(_held==null)return;
            var item=_c.Career.Backpack.Equipped;
            if(_c.InspectingItem)item=_c.InspectedItem;
            bool preview=_previewUntil>Time.time && !_c.WorldActionBusy;
            if(preview)item=_previewItem;
            bool pick=item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe;
            bool facility=FishingRecipes.IsStation(item);
            bool tool=FishingBackpack.Defined(item) && item!=FieldItem.Empty && (item!=FieldItem.HuntingRifle || _c.InspectingItem);
            bool visible=(_c.Ashore || _c.InspectingItem) && !_c.StoryBlocksGameplay && !_c.Shore.Swimming && !_c.Shore.Climbing && (_c.XRNotebook==null || !_c.XRNotebook.BlocksGameplay) && _c.Hands.CanPresentTools && !_c.ShopBlocksGameplay && !_c.Hands.HasGesture && !_c.Hands.PresentingPickup && !_c.Expedition.Aligning && !_c.ActionSuppressesHeldTool && tool;
            if(item==FieldItem.Lantern && !_lanternInHand && !_c.InspectingItem && !preview)visible=false;
            _held.gameObject.SetActive(visible);_lamp.enabled=false;
            PresentedItem=visible?item:FieldItem.Empty;
            if(!visible)return;
            if(_heldRenderer==null)_heldRenderer=_held.GetComponent<Renderer>();
            _heldRenderer.enabled=!(item==FieldItem.Binoculars && _c.View.OpticalView);
            _heldMesh.sharedMesh=ToolMesh(item);
            if(_lanternGlowApplied){_heldRenderer.SetPropertyBlock(null,1);_lanternGlowApplied=false;}
            if(_plainToolMaterials==null)_plainToolMaterials=new[]{_palette};
            _heldRenderer.sharedMaterials=FishingUserTools.Materials(item) ?? _plainToolMaterials;
            _heldRenderer.shadowCastingMode=item==FieldItem.Lantern?UnityEngine.Rendering.ShadowCastingMode.Off:UnityEngine.Rendering.ShadowCastingMode.On;
            if(item==FieldItem.HuntingRifle && _c.RifleHunting.Rifle!=null)_heldRenderer.sharedMaterials=_c.RifleHunting.Rifle.GetComponentInChildren<Renderer>().sharedMaterials;
            _c.Hands.GetToolFrame(out var shoulderFrame,out var frame);
            var bodyFrame=Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0);
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
                    twoHands=true;scale=.48f;curl=.3f;fingers=new FishingFingerPose(.06f,.08f,.10f,.12f,.65f);
                    position=shoulderFrame+frame*new Vector3(0,.065f,.43f);rotation=frame*Quaternion.Euler(-32,0,0);
                    leftPalm=new Vector3(-.235f,-.08f,-.10f);rightPalm=new Vector3(.235f,-.08f,-.10f);
                    leftRotation=Quaternion.LookRotation(Vector3.right,Vector3.down);rightRotation=Quaternion.LookRotation(Vector3.left,Vector3.down);break;
                case FieldItem.Binoculars:
                    twoHands=true;scale=BinocularScale;curl=.5f;fingers=FishingFingerPose.Barrel;
                    // 握点在镜筒外侧，手指绕镜筒横截面弯曲；目镜后端与眼睛保留明确间距。
                    position=eye+frame*Vector3.Lerp(new Vector3(0,-.115f,.35f),new Vector3(0,0,.18f),Mathf.SmoothStep(0,1,_c.View.BinocularBlend));
                    leftPalm=BinocularLeftPalm;rightPalm=BinocularRightPalm;
                    leftRotation=Quaternion.LookRotation(Vector3.up,Vector3.left);rightRotation=Quaternion.LookRotation(Vector3.up,Vector3.right);break;
                case FieldItem.Lantern:
                    scale=LanternScale;fingers=FishingFingerPose.Handle;
                    rotation=bodyFrame;rightPalm=LanternPalm;rightRotation=Quaternion.Euler(-50,0,0);
                    position=shoulderFrame+bodyFrame*new Vector3(_c.Shore.Character.HandSide(false)*.15f,.14f,.42f)-rotation*(rightPalm*scale);break;
                case FieldItem.StonePickaxe:case FieldItem.IronPickaxe:
                    scale=PickScale;rightPalm=PickPalm;curl=.94f;fingers=PickFingers;
                    // Keep the wrist beside the chest, not beside the eye. Palm orientation
                    // follows the real shaft while fingers remain forward instead of rolled inward.
                    rotation=bodyFrame*Quaternion.Euler(5,-45,0);
                    rightRotation=PickHandRotation(rotation,bodyFrame);
                    position=shoulderFrame+bodyFrame*new Vector3(_c.Shore.Character.HandSide(false)*.12f,.015f,.43f)-rotation*(rightPalm*scale);break;
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
                case FieldItem.Workbench:
                    twoHands=true;scale=.35f;leftPalm=new Vector3(-.68f,.88f,0);rightPalm=new Vector3(.68f,.88f,0);
                    fingers=FishingFingerPose.Book;

                    position=eye+frame*new Vector3(0,-.46f,.35f);leftRotation=rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
                case FieldItem.Forge:
                    twoHands=true;scale=.35f;leftPalm=new Vector3(-.52f,.6f,-.3f);rightPalm=new Vector3(.52f,.6f,-.3f);
                    position=eye+frame*new Vector3(0,-.46f,.35f);leftRotation=rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
                case FieldItem.Furnace:
                    twoHands=true;scale=.30f;leftPalm=new Vector3(-.56f,.43f,0);rightPalm=new Vector3(.56f,.43f,0);
                    fingers=FishingFingerPose.Book;
                    position=eye+frame*new Vector3(0,-.37f,.38f);leftRotation=rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
                case FieldItem.Plank:case FieldItem.Driftwood:
                    twoHands=true;scale=.65f;leftPalm=new Vector3(-.20f,-.025f,0);rightPalm=new Vector3(.20f,-.025f,0);
                    fingers=FishingFingerPose.Book;
                    position=eye+frame*new Vector3(0,-.24f,.36f);leftRotation=rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);break;
                case FieldItem.IronIngot:case FieldItem.Brick:case FieldItem.Glass:case FieldItem.Rope:
                    rightPalm=new Vector3(0,-.025f,0);rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);
                    fingers=FishingFingerPose.Book;break;
                default:
                    rightPalm=new Vector3(0,-.035f,0);rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);
                    fingers=FishingFingerPose.Book;break;
            }
            if(_c.InspectingItem && !XRSettings.isDeviceActive)
            {
                var bounds=_heldMesh.sharedMesh.bounds;
                scale=.34f/Mathf.Max(.01f,bounds.size.x,bounds.size.y,bounds.size.z);
                rotation=frame*Quaternion.Euler(_c.InspectionAngles.x,_c.InspectionAngles.y,0);
                rightPalm=bounds.center+Vector3.down*bounds.extents.y;
                twoHands=false;fingers=FishingFingerPose.Book;rightRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down);
                position=shoulderFrame+frame*new Vector3(.10f,.17f,.32f)-rotation*(bounds.center*scale);
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
            if(!tracked && !_c.Hands.FitToolPose(_held,leftPalm,rightPalm,leftRotation,rightRotation,twoHands,true)){_held.gameObject.SetActive(false);PresentedItem=FieldItem.Empty;return;}
            if(_c.ActionUsesHeldTool && pick)
            {
                // 镐尖与接触许可使用同一个目标，不能先夹短手臂再让远处矿石扣减。
                var contactRotation=_c.ActionToolRotation;
                var contactPalm=PickContactPalm(_c.ActionTargetPosition,contactRotation);
                var palm=Vector3.Lerp(_c.ActionStartPalm,contactPalm,_c.ActionContactBlend);
                _held.rotation=Quaternion.Slerp(rotation,contactRotation,_c.ActionContactBlend);
                _held.position=palm-_held.rotation*(rightPalm*scale);
            }
            if(pick && !tracked)rightRotation=PickHandRotation(_held.rotation,bodyFrame);
            if(twoHands)_c.Hands.SetToolTarget(true,_held,leftPalm,leftRotation,curl,fingers:fingers);
            else if(!tracked)_c.Hands.RestHand(true);
            _c.Hands.SetToolTarget(false,_held,rightPalm,rightRotation,curl,fingers:fingers);
            if(pick || item==FieldItem.Lantern)_c.Hands.Right.SetForearmRoll(.65f);
            _lamp.enabled=item==FieldItem.Lantern && _c.LanternOn;
            _lamp.transform.position=_held.TransformPoint(LanternLightPoint);
            if(item==FieldItem.Lantern)
            {
                if(_lanternGlow==null)_lanternGlow=new MaterialPropertyBlock();
                _lanternGlow.Clear();_lanternGlow.SetColor("_EmissionColor",_c.LanternOn?new Color(1,.55f,.16f)*2f:Color.black);
                _heldRenderer.SetPropertyBlock(_lanternGlow,1);_lanternGlowApplied=true;
            }
            if(item==FieldItem.HuntingSpear)CheckSpearContact(tracked);
        }
    }
}
