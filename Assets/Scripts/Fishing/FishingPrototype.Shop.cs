using UnityEngine;
using UnityEngine.InputSystem;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingTackleShop TackleShop { get; private set; }
        public bool ShopOpen => TackleShop != null && TackleShop.Open;
        private int _shopConsumedFrame = -1;
        public bool ShopBlocksGameplay => ShopOpen || _shopConsumedFrame == Time.frameCount;
        public int ShopNavigationIndex => 1 + fishingSpots.Length + FishingExploration.SiteCount;
        public Vector3 ShopPosition => TackleShop != null && TackleShop.Available ? TackleShop.Position : harbor;
        public string ShopName => FishingTackleShop.Name;
        public Transform ShopStaticRoot => TackleShop != null && TackleShop.Available ? TackleShop.transform : null;

        private void BindTackleShop()
        {
            foreach (var shop in FindObjectsOfType<FishingTackleShop>())
            {
                if (shop.gameObject.scene != gameObject.scene) continue;
                if (TackleShop != null) throw new System.InvalidOperationException("Duplicate Tide & Tackle scene instances.");
                TackleShop = shop; shop.Bind(this);
            }
        }
        public bool ToggleShop() => TackleShop != null && TackleShop.Toggle();
        public bool BuySupply(int product)
        {
            if(Paused || InventoryOpen || JournalOpen || HasPendingCatch || (!AtHarbor && (TackleShop==null || !TackleShop.CanPurchase)))
                return RejectSupplyPurchase("请在港口或店内柜台购买，或使用手册中的菜单快购。");
            return CompleteSupplyPurchase(product);
        }
        // The notebook is a real delivery order: it uses the same wallet/backpack transaction as the counter.
        // Its own modal is allowed, without temporarily changing inventory, pause or journal flags.
        public bool BuySupplyFromMenu(int product)
        {
            string reason=SupplyMenuUnavailableReason(product);
            if(reason.Length>0)return RejectSupplyPurchase(reason);
            return CompleteSupplyPurchase(product);
        }
        public bool CanBuySupplyFromMenu(int product)=>SupplyMenuUnavailableReason(product).Length==0;
        public string SupplyMenuUnavailableReason(int product)
        {
            if(!FishingSupplies.Valid(product))return "该商品不存在。";
            if(Career==null || StartMenuOpen || GuideOpening || (Paused && !InventoryOpen && !JournalOpen))return "请先返回游戏，再打开菜单快购。";
            if(HasPendingCatch || !CanChangeBait)return "请先结束垂钓并处理鱼获，再购买物资。";
            if(WorldActionBusy || StoryBlocksGameplay || Expedition.Aligning || (Realms!=null && Realms.Inside))return "请先完成当前互动，再购买物资。";
            if(Shore!=null && (Shore.Swimming || Shore.Climbing))return "请先回到平稳的陆地或船上，再购买物资。";
            if(Combat!=null && (Combat.Challenging || (Combat.InDangerZone && Career.Calendar.Night)))return "请先离开战斗区域，再购买物资。";
            if(!Ashore && Speed>=.8f)return "请先停稳船只，再购买物资。";
            if(!Career.CanAfford(FishingSupplies.Price(product)))return "金币不足：需要 "+FishingSupplies.Price(product)+" 金币。";
            var item=FishingSupplies.Items[product];int free=0,limit=FishingBackpack.Limit(item);
            for(int i=0;i<FishingBackpack.SlotCount;i++)
            {var slot=Career.Backpack.At(i);if(slot.count==0 || slot.item==item)free+=limit-slot.count;}
            if(free<FishingSupplies.Quantity(product))return "背包空间不足：请先整理背包或存入家园仓库。";
            return "";
        }
        private bool RejectSupplyPurchase(string message)
        {WorldAudio?.Play("UIReject",ExplorerPosition,.3f);Notice("无法购买："+message,5);return false;}
        private bool CompleteSupplyPurchase(int product)
        {
            bool ok=Career.BuySupply(product);
            if(ok)
            {
                Save();ActionAudio?.Detail("ShopPurchase",ExplorerPosition+Vector3.up*.7f);
                var item=FishingSupplies.Items[product];
                Notice("已入背包："+FishingBackpack.Name(item)+" ×"+FishingSupplies.Quantity(product)+" · 当前持有 "+Career.Backpack.Count(item)+(CreativeMode?" · 创造模式免费":" · 已付 "+FishingSupplies.Price(product)+" 金币")+(UnityEngine.XR.XRSettings.isDeviceActive?" · B 查看背包":" · I 查看背包"),5);
            }
            else {WorldAudio?.Play("UIReject",ExplorerPosition,.3f);Notice("无法购买：请检查金币余额和背包空位。",4);}
            return ok;
        }
        internal void ShopModalChanged()
        {
            CancelWorldAction();FieldWorld?.ClearToolPresentation();
            _shopConsumedFrame = Time.frameCount;
            _pointerGate.Reset(); _gamepadGate.Reset();
            View?.ResetInput(); Shore?.ResetMoveInput(); Hands?.ResetInteraction();
            LowerFieldTools(); ShowDetails = false; HarborDeskOpen = false;
        }
        /// <summary>One modal owns the entire frame, including opening, closing and invalidation.
        /// E/Tab/Esc close; I/J replace the shop with inventory/journal; R rescues. Digits never select a hotbar slot here.</summary>
        internal bool TickShopInput(Keyboard keyboard, bool allowInput, bool focused, bool xrActive)
        {
            bool wasOpen = ShopOpen;
            TackleShop?.ValidateContext(focused, xrActive);
            if (wasOpen)
            {
                if (!ShopOpen || _shopConsumedFrame == Time.frameCount || !allowInput || keyboard == null) return true;
                if (keyboard.rKey.wasPressedThisFrame) ReturnToHarbor();
                else if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) TackleShop.Close();
                else if (keyboard.iKey.wasPressedThisFrame) ToggleInventory();
                else if (keyboard.jKey.wasPressedThisFrame) ToggleJournal();
                else if (keyboard.digit1Key.wasPressedThisFrame) _hud.BuyShopSlot(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) _hud.BuyShopSlot(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) _hud.BuyShopSlot(2);
                else if (keyboard.digit4Key.wasPressedThisFrame) _hud.BuyShopSlot(3);
                else if (keyboard.digit5Key.wasPressedThisFrame) _hud.BuyShopSlot(4);
                else if (keyboard.digit6Key.wasPressedThisFrame) _hud.BuyShopSlot(5);
                return true;
            }
            if (ShopBlocksGameplay) return true;
            if (allowInput && focused && !xrActive && keyboard != null && !Paused && !InventoryOpen && !JournalOpen
                && !HasPendingCatch && !Expedition.Aligning && TackleShop != null && TackleShop.NearCounter
                && (keyboard.eKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame))
                return keyboard.eKey.wasPressedThisFrame?Residents.Talk(4):ToggleShop();
            return false;
        }
        private Vector3 NavigationWorldPosition()
        {
            if(NavigationTarget>HomeNavigationIndex)return Ashore?FishingCoastalPresentation.Islets[NavigationTarget-HomeNavigationIndex-1]:FishingCoastalPresentation.Berth(NavigationTarget-HomeNavigationIndex-1);
            if(NavigationTarget==HomeNavigationIndex && Home!=null)return Ashore?Home.ApproachPosition:Home.Berth;
            if (NavigationTarget > ShopNavigationIndex)
                return Ashore ? FishingIslandWorld.Center(NavigationTarget-ShopNavigationIndex-1) : FishingIslandWorld.Berth(NavigationTarget-ShopNavigationIndex-1);
            if (NavigationTarget == ShopNavigationIndex)
                return TackleShop == null || !TackleShop.Available ? harbor : Ashore ? TackleShop.entrance.position : TackleShop.mooringPoint.position;
            if (NavigationTarget == 0) return harbor;
            if (NavigationTarget <= fishingSpots.Length) return fishingSpots[NavigationTarget - 1];
            int site = NavigationTarget - fishingSpots.Length - 1;
            return Shore != null && site >= 0 && site < Shore.Sites.Length
                ? Ashore ? Shore.Sites[site].Landing : Shore.Sites[site].Berth : harbor;
        }
    }
}
