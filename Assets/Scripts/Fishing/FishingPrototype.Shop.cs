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
            if(Paused || InventoryOpen || JournalOpen || HasPendingCatch || (!AtHarbor && (TackleShop==null || !TackleShop.CanPurchase)))return false;
            bool ok=Career.BuySupply(product);
            if(ok)
            {
                Save();ActionAudio?.Detail("ShopPurchase",ExplorerPosition+Vector3.up*.7f);
                Notice("购入 "+FishingBackpack.Name(FishingSupplies.Items[product])+" ×"+FishingSupplies.Quantity(product)+(UnityEngine.XR.XRSettings.isDeviceActive?" · B 打开背包，点选换入快捷位；Y 换装备。":" · 已放入背包，I 查看并装备。"),5);
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
