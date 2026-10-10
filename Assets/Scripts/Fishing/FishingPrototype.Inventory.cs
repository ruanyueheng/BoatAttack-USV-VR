using UnityEngine;
using UnityEngine.InputSystem;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public bool InventoryOpen { get; private set; }
        public bool BinocularsRaised { get; private set; }
        public bool LanternOn { get; private set; }
        private bool _workshopReceipt;
        internal void NotifyWorkshopTransaction(){_workshopReceipt=true;}
        public FishingWorkshopWorld WorkshopWorld { get; private set; }
        public FishingFieldWorld FieldWorld { get; private set; }
        public void LowerFieldTools(){BinocularsRaised=false;LanternOn=false;}
        public void ToggleInventory()
        {
            TackleShop?.Close();
            if((Paused && !InventoryOpen) || _xrActive || JournalOpen)return;
            InventoryOpen=!InventoryOpen;if(_hud!=null)_hud.ClearFieldSelection();BinocularsRaised=false;TogglePause();
            if(InventoryOpen){ShowDetails=false;HarborDeskOpen=false;WorkshopWorld?.SelectNearby();}
            else if(_workshopReceipt)
            {
                // 回执只表现已经完成的事务，不重新启动制作或熔炉计时。
                _workshopReceipt=false;Hands.ReachGesture(Shore.Character.HandReferencePosition+Shore.Actor.forward*.28f);
            }
        }
        public void OpenCrafting()
        {
            if(UnityEngine.XR.XRSettings.isDeviceActive){XRNotebook.Open(2);return;}
            if(!InventoryOpen)ToggleInventory();
            if(InventoryOpen && _hud!=null)_hud.OpenCraftingPage();
        }
        public void SelectFieldSlot(int slot)
        {
            if(ShopBlocksGameplay || Career==null)return;
            int previous=Career.Backpack.Selected;
            if(!Career.Backpack.Select(slot))return;
            if(previous!=Career.Backpack.Selected){CancelWorldAction();FieldWorld?.ClearToolPresentation();}
            BinocularsRaised=false;LanternOn=false;Save();
        }
        public void MoveFieldItem(int from,int to,bool half=false,bool quick=false)
        {
            if(!InventoryOpen)return;
            if(quick?Career.Backpack.QuickMove(from):Career.Backpack.Move(from,to,half))
            {BinocularsRaised=false;LanternOn=false;Save();}
        }
        public bool DiscardFieldItem(int slot,FieldItem expected,int count)
        {
            if(!InventoryOpen || !Career.Backpack.Discard(slot,expected,count))return false;
            LowerFieldTools();Save();Notice("已销毁 "+FishingBackpack.Name(expected)+" ×"+count+"，空格可重新使用。",4);return true;
        }
        public void UseFieldItem()
        {
            if(Paused || ShopBlocksGameplay || WorldActionBlocksGameplay || !Ashore || Expedition.Aligning)return;
            switch(Career.Backpack.Equipped)
            {
                case FieldItem.HuntingSpear:FieldWorld.BeginHunt();break;
                case FieldItem.Workbench:case FieldItem.Furnace:RequestPlaceEquipped();break;
                case FieldItem.StonePickaxe:case FieldItem.IronPickaxe:RequestHarvest();break;
                case FieldItem.FieldGuide: FieldWorld.Observe();break;
                case FieldItem.Binoculars:
                    BinocularsRaised=!BinocularsRaised;Notice(BinocularsRaised?(_xrActive?"望远镜 · A 记录动物 / 扳机放下":"望远镜 · T 记录动物 / F 放下"):"已放下望远镜",3);break;
                case FieldItem.Lantern:LanternOn=!LanternOn;Notice(LanternOn?"提灯已点亮":"提灯已熄灭",3);break;
                default:Notice("1–9 选择自然手册、望远镜或提灯，再按 F 使用。I → 制作查看配方；工作台 / 熔炉放入快捷栏后 F 放置。",4);break;
            }
        }
        private void ReadFieldShortcuts(Keyboard k,Mouse m)
        {
            if(k!=null)
            {
                var keys=new[]{Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9};
                for(int i=0;i<keys.Length;i++)if(k[keys[i]].wasPressedThisFrame)SelectFieldSlot(i);
                if(k.fKey.wasPressedThisFrame)UseFieldItem();
            }
            if(Ashore && m!=null && (k==null || (!k.leftAltKey.isPressed && !k.rightAltKey.isPressed)))
            {
                float scroll=m.scroll.ReadValue().y;
                if(Mathf.Abs(scroll)>.1f)SelectFieldSlot((Career.Backpack.Selected+(scroll>0?8:1))%9);
            }
        }
        public bool CollectFieldItem(int id,FieldItem item,int count)
        {
            if(Paused || !Ashore)return false;
            if(!Career.Backpack.TryCollect(id,item,count)){Notice("背包已满或物品已领取；I 整理背包后再试。",4);return false;}
            Save();Notice("已拾取 "+FishingBackpack.Name(item)+" ×"+count+" · I 打开背包",4);return true;
        }
        public bool RecordLandAnimal(LandAnimalKind kind)
        {
            if(Paused || !Ashore)return false;
            bool fresh=Career.Backpack.Observe(kind);
            if(fresh){Save();Notice("自然手册新增："+FishingBackpack.AnimalName(kind)+" · I 查看陆地记录",5);}
            else Notice("已记录："+FishingBackpack.AnimalName(kind)+"。按住 Ctrl 蹲下可以靠得更近。",4);
            return fresh;
        }
    }
}
