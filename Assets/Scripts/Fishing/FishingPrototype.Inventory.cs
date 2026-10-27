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
        public FishingRifleHunting RifleHunting { get; private set; }
        public void LowerFieldTools(){BinocularsRaised=false;LanternOn=false;}
        public void ToggleInventory()
        {
            if(Prologue!=null && (Prologue.Open || Home.Sleeping))return;
            TackleShop?.Close();
            if((Paused && !InventoryOpen) || _xrActive || JournalOpen)return;
            InventoryOpen=!InventoryOpen;if(_hud!=null)_hud.ClearFieldSelection();BinocularsRaised=false;TogglePause();
            WorldAudio?.Play(InventoryOpen?"UIConfirm":"UIBack",ExplorerPosition,.3f);
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
            if(ShopBlocksGameplay || StoryBlocksGameplay || Career==null)return;
            int previous=Career.Backpack.Selected;
            EndItemInspection();
            if(!Career.Backpack.Select(slot))return;
            if(previous!=Career.Backpack.Selected){CancelWorldAction();FieldWorld?.ClearToolPresentation();}
            BinocularsRaised=false;if(Career.Backpack.Count(FieldItem.Lantern)==0)LanternOn=false;Save();
        }
        public void MoveFieldItem(int from,int to,bool half=false,bool quick=false)
        {
            if(!InventoryOpen)return;
            if(quick?Career.Backpack.QuickMove(from):Career.Backpack.Move(from,to,half))
            {BinocularsRaised=false;LanternOn=false;Save();WorldAudio?.Play("StorageMove",ExplorerPosition,.3f);}
        }
        public bool DiscardFieldItem(int slot,FieldItem expected,int count)
        {
            if(!InventoryOpen || !Career.Backpack.Discard(slot,expected,count))return false;
            LowerFieldTools();Save();Notice("已销毁 "+FishingBackpack.Name(expected)+" ×"+count+"，空格可重新使用。",4);return true;
        }
        public void UseFieldItem()
        {
            if(InspectingItem){EndItemInspection();return;}
            if(Paused || ShopBlocksGameplay || StoryBlocksGameplay || WorldActionBlocksGameplay || !Ashore || Expedition.Aligning)return;
            if((Shore.Swimming || Shore.Climbing) && Career.Backpack.Equipped!=FieldItem.CookedMeat && Career.Backpack.Equipped!=FieldItem.GrilledFish){Notice("游泳或攀登时先收起工具；可选择口粮恢复体力。",3);return;}
            switch(Career.Backpack.Equipped)
            {
                case FieldItem.HuntingRifle:RifleHunting.TryFire();break;
                case FieldItem.CookedMeat:if(!Shore.EatRation())Notice("体力充足时不消耗口粮；快跑、游泳或攀登后可食用。",3);break;
                case FieldItem.GrilledFish:if(!Shore.EatRation())Notice("体力充足时不消耗烤鱼；快跑、游泳或攀登后可食用。",3);break;
                case FieldItem.SeaTreasure:
                    if(Career.OpenSeaTreasure()){Save();CoastWorld.ShowSeaFind(FieldItem.SeaTreasure);Notice("打开沉海宝匣 · 金币 +"+FishingCoastRules.TreasureCoins,5);}
                    break;
                case FieldItem.HuntingSpear:if(Combat==null || !Combat.TrySpear())FieldWorld.BeginHunt();break;
                case FieldItem.Axe:case FieldItem.Shovel:RequestHarvest();break;
                case FieldItem.Rake:if(Farm.TryTarget(out int plot,out var root) && plot<12 && Farm.Work(plot)==FarmWork.Till)RequestFarmInteraction(plot,root);else Notice("靠近菜畦，F / 右扳机整地；旧默认锄头入口仍保留。",4);break;
                case FieldItem.CaptureNet:HomeEcology.UseNet();break;
                case FieldItem.AnimalTrap:HomeEcology.PlaceTrap();break;
                case FieldItem.Aquarium:case FieldItem.FishFeed:case FieldItem.RabbitHutch:case FieldItem.FoxDen:case FieldItem.TortoisePen:case FieldItem.BirdAviary:case FieldItem.SeaTurtlePond:HomeEcology.Open();break;
                case FieldItem.Workbench:case FieldItem.Furnace:case FieldItem.Forge:RequestPlaceEquipped();break;
                case FieldItem.StonePickaxe:case FieldItem.IronPickaxe:RequestHarvest();break;
                case FieldItem.FieldGuide: OpenFieldGuide();break;
                case FieldItem.Binoculars:
                    BinocularsRaised=!BinocularsRaised;Notice(BinocularsRaised?(_xrActive?"望远镜 · A 记录动物 / 扳机放下":"望远镜 · T 记录动物 / F 放下"):"已放下望远镜",3);break;
                case FieldItem.Lantern:LanternOn=!LanternOn;WorldAudio?.Play("LanternSwitch",ExplorerPosition+Vector3.up,.5f);Notice(LanternOn?"提灯已点亮":"提灯已熄灭",3);break;
                default:Notice(UnityEngine.XR.XRSettings.isDeviceActive?"Y 换装备，右扳机使用工具。B → 制作查看配方；设施换入快捷栏后按右扳机放置。":"1–9 选择自然手册、望远镜或提灯，再按 F 使用。I → 制作查看配方；工作台 / 熔炉放入快捷栏后 F 放置。",4);break;
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
                if(Mathf.Abs(scroll)>.1f)
                {
                    if(BinocularsRaised && Career.Backpack.Equipped==FieldItem.Binoculars)View.AdjustMagnification(Mathf.Sign(scroll));
                    else if(!InspectingItem)SelectFieldSlot((Career.Backpack.Selected+(scroll>0?8:1))%9);
                }
            }
        }
        public bool CollectFieldItem(int id,FieldItem item,int count)
        {
            if(Paused || !Ashore)return false;
            if(!Career.Backpack.TryCollect(id,item,count)){Notice(UnityEngine.XR.XRSettings.isDeviceActive?"背包已满或物品已领取；B 查看背包后再试。":"背包已满或物品已领取；I 整理背包后再试。",4);return false;}
            ActionAudio?.Pickup(item,ExplorerPosition+Vector3.up*.6f);Shore.Character.PlayInteraction();
            Save();Notice("已拾取 "+FishingBackpack.Name(item)+" ×"+count+(UnityEngine.XR.XRSettings.isDeviceActive?" · B 打开背包":" · I 打开背包"),4);return true;
        }
        public bool RecordLandAnimal(LandAnimalKind kind)
        {
            if(Paused || !Ashore)return false;
            bool fresh=Career.Backpack.Observe(kind);
            if(fresh){Save();Notice("自然手册新增："+FishingBackpack.AnimalName(kind)+(UnityEngine.XR.XRSettings.isDeviceActive?" · B 查看自然观察记录":" · F 翻开自然手册"),5);}
            else Notice("已记录："+FishingBackpack.AnimalName(kind)+(UnityEngine.XR.XRSettings.isDeviceActive?"。缓慢接近或轻蹲可以靠得更近。":"。按住 Ctrl 蹲下可以靠得更近。"),4);
            return fresh;
        }
    }
}
