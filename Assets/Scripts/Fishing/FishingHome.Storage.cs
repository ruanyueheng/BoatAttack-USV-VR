using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHome
    {
        public Transform StorageChest {get;private set;}
        private void BuildStorage()
        {
            var p=new Vector3(4.8f,.20f,-2.1f);
            StorageChest=FishingV019Art.Spawn("chest",House,p,1.45f,0,"FishingV019");StorageChest.name="Home keepsake storage chest";
            var renderers=StorageChest.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var collider=StorageChest.gameObject.AddComponent<BoxCollider>();collider.center=StorageChest.InverseTransformPoint(bounds.center);collider.size=bounds.size;
            RegisterInteraction(6,StorageChest,p+new Vector3(-.25f,.65f,-.20f));
            FishingV019Art.Sign("家园储物箱 · 存入 / 取出",House,p+Vector3.up*1.25f,.012f,new Color(.93f,.84f,.61f));
        }
        public bool OpenStorage()
        {
            if(!ReachInteraction(6) || _c.Paused || _c.StoryBlocksGameplay || _c.ShopBlocksGameplay || _c.HasPendingCatch)return false;
            if(XRSettings.isDeviceActive){_c.XRNotebook.Open(18);return true;}
            if(!_c.InventoryOpen)_c.ToggleInventory();if(!_c.InventoryOpen)return false;
            _c.GetComponent<FishingHUD>().OpenHomeStorage();return true;
        }
        public bool TransferStorage(bool deposit,int slot)
        {
            if(!ReachInteraction(6) || (!_c.InventoryOpen && !_c.XRNotebook.IsOpen))return false;
            var source=deposit?_c.Career.Backpack:_c.Career.HomeStorage;
            var destination=deposit?_c.Career.HomeStorage:_c.Career.Backpack;var item=source.At(slot);
            if(!source.TransferTo(destination,slot))
            {_c.Notice(item.count==0?"这个格子是空的。":(deposit?"储物箱":"随身行囊")+"空间不足，请先腾出格子。",4);return false;}
            _c.LowerFieldTools();_c.FieldWorld.ClearToolPresentation();_c.Save();
            _c.Notice((deposit?"已存入 ":"已取出 ")+FishingBackpack.Name(item.item)+" ×"+item.count,4);return true;
        }
    }
}
