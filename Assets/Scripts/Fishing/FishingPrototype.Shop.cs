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
                else if (keyboard.digit1Key.wasPressedThisFrame) TackleShop.Buy(UpgradeTrack.Line);
                else if (keyboard.digit2Key.wasPressedThisFrame) TackleShop.Buy(UpgradeTrack.Reel);
                else if (keyboard.digit3Key.wasPressedThisFrame) TackleShop.Buy(UpgradeTrack.Hold);
                else if (keyboard.digit4Key.wasPressedThisFrame) TackleShop.BuyRifle();
                return true;
            }
            if (ShopBlocksGameplay) return true;
            if (allowInput && focused && !xrActive && keyboard != null && !Paused && !InventoryOpen && !JournalOpen
                && !HasPendingCatch && !Expedition.Aligning && TackleShop != null && TackleShop.NearCounter
                && (keyboard.eKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)) return ToggleShop();
            return false;
        }
        private Vector3 NavigationWorldPosition()
        {
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
