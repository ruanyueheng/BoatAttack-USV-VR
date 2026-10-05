using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Static, user-supplied character; trading remains owned by FishingTackleShop.</summary>
    public sealed class FishingShopCashier : MonoBehaviour
    {
        public const string ResourcePath = "FishingCashier/FantasyGirl/Cashier";

        public static FishingShopCashier Create(FishingTackleShop shop)
        {
            if (shop == null) return null;
            var existing = shop.GetComponentInChildren<FishingShopCashier>(true);
            if (existing != null) return existing;
            if (shop.clerkSpaces == null || shop.clerkSpaces.Length == 0 || shop.clerkSpaces[0] == null) return null;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[CASHIER] FantasyGirl prefab missing. Run BoatAttack/Fishing/Build FantasyGirl cashier assets.", shop);
                return null;
            }
            var go = Instantiate(prefab, shop.clerkSpaces[0], false);
            go.name = "Cashier - FantasyGirl";
            // Imported character faces local +Z. The customer side is shop-local -Z.
            go.transform.localRotation = Quaternion.Euler(0, 180, 0);
            return go.GetComponent<FishingShopCashier>();
        }
    }
}
