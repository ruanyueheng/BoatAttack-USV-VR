using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        // Fish remain in the boat's persistent fish box while the owner trades at the shop.
        public bool CanSellFish=>Career!=null && !HasPendingCatch &&
            (AtHarbor || (TackleShop!=null && TackleShop.NearCounter));
    }
}
