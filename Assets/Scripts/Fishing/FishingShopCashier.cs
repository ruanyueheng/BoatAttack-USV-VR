using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>使用原美女店员模型，交谈由居民系统处理，交易由原商店处理。</summary>
    public sealed class FishingShopCashier : MonoBehaviour
    {
        public const string ResourcePath = "FishingCashier/FantasyGirl/Cashier";
        public const float AdultHeight = 1.72f;
        public Vector3 FacePosition => transform.TransformPoint(new Vector3(0, 1.56f, .06f));

        public static FishingShopCashier Create(FishingTackleShop shop)
        {
            if (shop == null) return null;
            // 同时检查预先放置及未激活的子物体，避免重载或重复 Bind
            // 在同一预留站位生成第二名店员。
            var existing = shop.GetComponentInChildren<FishingShopCashier>(true);
            if (existing != null) return existing;
            if (shop.clerkSpaces == null || shop.clerkSpaces.Length == 0 || shop.clerkSpaces[0] == null)
            {
                Debug.LogError("[CASHIER] The shop has no first clerk anchor.", shop);
                return null;
            }
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null || prefab.GetComponent<FishingShopCashier>() == null)
            {
                Debug.LogError("[CASHIER] FantasyGirl cashier prefab missing or invalid. Restore FishingCashier/FantasyGirl and validate before building the Player.", shop);
                return null;
            }
            var instance = Instantiate(prefab, shop.clerkSpaces[0], false);
            instance.name = "Cashier - FantasyGirl";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0, 180, 0);
            instance.transform.localScale = Vector3.one;
            return instance.GetComponent<FishingShopCashier>();
        }
    }
}
