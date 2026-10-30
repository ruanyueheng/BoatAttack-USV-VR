using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHome
    {
        public Transform StorageChest {get;private set;}
        public Transform StorageLid {get;private set;}
        private Quaternion _storageLidRest;
        private float _storageLidAngle;
        private float _storageOpening;
        private readonly FishingPressEdge[] _lidGrip = { new FishingPressEdge(), new FishingPressEdge() };
        private int _lidHand = -1;
        private float _lidPalmY, _lidOpeningAtGrab;
        public Vector3 StorageLidHandle
        {
            get
            {
                if (StorageLid == null) return Vector3.zero;
                var b = StorageLid.GetComponent<MeshFilter>().sharedMesh.bounds;
                return StorageLid.TransformPoint(new Vector3(b.center.x, b.center.y, b.center.z >= 0 ? b.max.z : b.min.z));
            }
        }
        private void BuildStorage()
        {
            var p=new Vector3(4.8f,.20f,-2.1f);
            StorageChest=FishingV019Art.Spawn("HomeChest",House,p,1.45f,0,"FishingFeedbackTools");StorageChest.name="Home keepsake storage chest";
            var palette=Resources.Load<Material>("FishingV019/MAT-V019-vertex-palette");
            foreach(var renderer in StorageChest.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=palette;FishingPaintedArt.Apply(StorageChest,"FishingFeedbackTools/HomeChest");
            foreach(var part in StorageChest.GetComponentsInChildren<Transform>())if(string.Equals(part.name,"lid",System.StringComparison.OrdinalIgnoreCase))StorageLid=part;
            if(StorageLid!=null)
            {
                _storageLidRest=StorageLid.localRotation;var mesh=StorageLid.GetComponent<MeshFilter>().sharedMesh;
                _storageLidAngle=-Mathf.Sign(mesh.bounds.center.z)*105;
                var cover=StorageLid.gameObject.AddComponent<BoxCollider>();cover.center=mesh.bounds.center;cover.size=mesh.bounds.size;
            }
            var renderers=StorageChest.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            bool first=true;
            foreach(var renderer in renderers)
            {
                if(StorageLid!=null && renderer.transform.IsChildOf(StorageLid))continue;
                if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
            }
            var collider=StorageChest.gameObject.AddComponent<BoxCollider>();collider.center=StorageChest.InverseTransformPoint(bounds.center);collider.size=bounds.size;
            RegisterInteraction(6,StorageChest,p+new Vector3(-.25f,.65f,-.20f));
            FishingV019Art.Sign("家园储物箱 · 存入 / 取出",House,p+Vector3.up*1.25f,.012f,new Color(.93f,.84f,.61f));
        }
        public bool OpenStorage()
        {
            if(!ReachInteraction(6) || _c.Paused || _c.StoryBlocksGameplay || _c.ShopBlocksGameplay || _c.HasPendingCatch)return false;
            // World cues are muted once the inventory pauses, so the lid creak sounds before the panel opens.
            _c.WorldAudio?.Play("ChestOpen",StorageChest.position+Vector3.up*.5f,.55f);
            if(XRSettings.isDeviceActive){_c.XRNotebook.Open(18);return true;}
            if(!_c.InventoryOpen)_c.ToggleInventory();if(!_c.InventoryOpen)return false;
            _c.GetComponent<FishingHUD>().OpenHomeStorage();return true;
        }
        private void UpdateStorageAnimation()
        {
            if (_c != null) ReadStorageLidGrip();
            if(_c==null || StorageLid==null || (_c.Paused && !_c.InventoryOpen && !_c.XRNotebook.IsOpen))return;
            if (_lidHand >= 0) return;
            bool open=ReachInteraction(6) && (_c.InventoryOpen || _c.XRNotebook.IsOpen);
            float next=Mathf.MoveTowards(_storageOpening,open?1:0,(open?Time.unscaledDeltaTime:Time.deltaTime)/.75f);
            if(next==_storageOpening)return;
            if(_storageOpening==0 && open)_c.WorldAudio?.Play("MechanismClick",StorageChest.position,.35f);
            _storageOpening=next;StorageLid.localRotation=_storageLidRest*Quaternion.Euler(_storageLidAngle*Mathf.SmoothStep(0,1,next),0,0);
        }
        private void ReadStorageLidGrip()
        {
            bool available = XRSettings.isDeviceActive && Application.isFocused;
            for (int i = 0; i < 2; i++)
            {
                var hand = i == 0 ? XRNode.LeftHand : XRNode.RightHand;
                bool tracked = available && FishingXRTracking.TryPose(_c.viewCamera, hand, out _, out _);
                Vector3 palm = Vector3.zero;
                if (tracked) tracked = _c.Hands.TryControllerPalm(i == 0, out palm, out _);
                var device = InputDevices.GetDeviceAtXRNode(hand);
                bool held = FishingXRInput.Grip(device, .65f);
                StepStorageLidGrip(i == 0, tracked, held, palm);
            }
        }
        /// <summary>Grip the real cover edge and lift it; tracking recovery requires a released hand.</summary>
        public void StepStorageLidGrip(bool left, bool tracked, bool held, Vector3 palm)
        {
            int hand = left ? 0 : 1;
            bool available = tracked && StorageLid != null && ReachInteraction(6) && !_c.Paused && !_c.StoryBlocksGameplay
                && !_c.ShopBlocksGameplay && !_c.WorldActionBusy && !_c.HasPendingCatch && !_c.InventoryOpen && !_c.JournalOpen && !_c.XRNotebook.IsOpen
                && !float.IsNaN(palm.sqrMagnitude) && !float.IsInfinity(palm.sqrMagnitude);
            bool press = _lidGrip[hand].Step(available, held);
            if (!available || !held) { if (_lidHand == hand) _lidHand = -1; return; }
            if (press && _lidHand < 0 && Vector3.Distance(palm, StorageLidHandle) < .25f)
            {
                _lidHand = hand; _lidPalmY = palm.y; _lidOpeningAtGrab = _storageOpening;
                _c.WorldAudio?.Play("MechanismClick", StorageLidHandle, .3f);
            }
            if (_lidHand != hand) return;
            // A controller teleport cannot fling the cover open after recentering or tracking loss.
            if (Mathf.Abs(palm.y - _lidPalmY) > .65f || FishingPrototype.FlatDistance(palm, StorageChest.position) > 1.4f)
            { _lidHand = -1; _lidGrip[hand].Reset(); return; }
            _storageOpening = Mathf.Clamp01(_lidOpeningAtGrab + (palm.y - _lidPalmY) / .35f);
            StorageLid.localRotation = _storageLidRest * Quaternion.Euler(_storageLidAngle * Mathf.SmoothStep(0, 1, _storageOpening), 0, 0);
            if (_storageOpening >= .7f) { _lidHand = -1; OpenStorage(); }
        }
        public bool TransferStorage(bool deposit,int slot)
        {
            if(!ReachInteraction(6) || (!_c.InventoryOpen && !_c.XRNotebook.IsOpen))return false;
            var source=deposit?_c.Career.Backpack:_c.Career.HomeStorage;
            var destination=deposit?_c.Career.HomeStorage:_c.Career.Backpack;var item=source.At(slot);
            if(!source.TransferTo(destination,slot))
            {_c.WorldAudio?.Play("UIReject",_c.ExplorerPosition,.3f);_c.Notice(item.count==0?"这个格子是空的。":(deposit?"储物箱":"随身行囊")+"空间不足，请先腾出格子。",4);return false;}
            _c.LowerFieldTools();_c.FieldWorld.ClearToolPresentation();_c.Save();
            _c.WorldAudio?.Play("StorageMove",StorageChest.position+Vector3.up*.4f,.45f);
            _c.Notice((deposit?"已存入 ":"已取出 ")+FishingBackpack.Name(item.item)+" ×"+item.count,4);return true;
        }
    }
}
