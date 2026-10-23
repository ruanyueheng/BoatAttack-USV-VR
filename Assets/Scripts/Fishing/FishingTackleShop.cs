using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>潮汐钓具店，共用居民对话和原商店交易。</summary>
    public sealed class FishingTackleShop : MonoBehaviour
    {
        public Transform entrance, purchasePoint, mooringPoint;
        public Transform[] clerkSpaces, customerSpaces;
        [System.NonSerialized] public FishingPrototype Controller;
        private Collider _landingDeck;
        public FishingShopCashier Cashier { get; private set; }
        public const string Name = "潮汐钓具店";
        public string DisplayName => Name;
        public Vector3 Position => transform.position;
        public Vector3 LandingPosition => _landingDeck == null ? Position : _landingDeck.bounds.center + Vector3.up * _landingDeck.bounds.extents.y;
        public bool Available => isActiveAndEnabled && entrance != null && purchasePoint != null && mooringPoint != null
            && _landingDeck != null && _landingDeck.enabled && _landingDeck.gameObject.activeInHierarchy;
        public bool Open { get; private set; }
        public bool Inside
        {
            get
            {
                if (!Available || Controller == null || !Controller.Ashore) return false;
                var p = transform.InverseTransformPoint(Controller.ExplorerPosition);
                return Mathf.Abs(p.x) < 5.7f && p.z > -4.9f && p.z < 4.8f && p.y > -.2f && p.y < .5f;
            }
        }
        // The customer must be inside, close to the counter, and not separated by a wall/fixture.
        public bool NearCounter => Inside && Vector3.Distance(Controller.ExplorerPosition, purchasePoint.position) < 2.6f
            && Controller.Shore.ClearSight(Controller.ExplorerPosition + Vector3.up * 1.2f, purchasePoint.position + Vector3.up * 1.2f);
        public bool CanPurchase => NearCounter && ((Open && ContextAllowsBrowsing) || (XRSettings.isDeviceActive && Controller.XRNotebook!=null
            && Controller.XRNotebook.StorePage && !Controller.Paused && !Controller.HasPendingCatch && !Controller.Expedition.Aligning));
        private bool ContextAllowsBrowsing => Controller != null && !Controller.Paused && !Controller.InventoryOpen
            && !Controller.JournalOpen && !Controller.HasPendingCatch && !Controller.Expedition.Aligning
            && !XRSettings.isDeviceActive && (Controller.Automated || Application.isFocused);

        private void Awake() => ResolveLandingDeck();
        private void Start() { EnsureCashier(); FishingShopDisplay.Ensure(this); }
        public void Bind(FishingPrototype controller)
        {
            Controller = controller; ResolveLandingDeck(); EnsureCashier(); FishingShopDisplay.Ensure(this);
            if(transform.Find("Shop foundation")==null)
                FishingArt.Shape("Shop foundation",PrimitiveType.Cube,transform,new Vector3(0,-2.55f,0),new Vector3(12,4.8f,10),controller.wood,true);
            var barrier=transform.Find("Boat building barrier");
            if(barrier==null)
            {
                barrier=new GameObject("Boat building barrier").transform;barrier.SetParent(transform,false);barrier.localPosition=Vector3.up*.25f;
                barrier.gameObject.AddComponent<BoxCollider>().size=new Vector3(12,8.6f,10);
            }
            if(controller.Shore!=null)Physics.IgnoreCollision(controller.Shore.Body,barrier.GetComponent<Collider>());
        }
        public FishingShopCashier EnsureCashier()
        {
            if (Cashier == null) Cashier = FishingShopCashier.Create(this);
            return Cashier;
        }
        private void ResolveLandingDeck()
        {
            var deck = transform.Find("04 Porch and beach access/Shore landing deck");
            _landingDeck = deck == null ? null : deck.GetComponent<Collider>();
        }
        public bool Toggle()
        {
            if (Open) { Close(); return true; }
            if (!NearCounter || !ContextAllowsBrowsing || Controller.ShopBlocksGameplay) return false;
            Open = true;
            Controller.ShopModalChanged();
            Controller.Notice(Cashier != null
                ? "店员：欢迎来到潮汐钓具店！请选择装备，金币和装备自动保存。"
                : "欢迎来到潮汐钓具店 · 选择装备升级，金币和装备自动保存。", 5f);
            return true;
        }
        public void Close()
        {
            if (!Open) return;
            Open = false;
            if (Controller != null) Controller.ShopModalChanged();
        }
        public bool Buy(UpgradeTrack track)
        {
            if (!CanPurchase) return false;
            return Controller.BuyUpgrade(track);
        }
        public bool BuyRifle()
        {
            if(!CanPurchase)return false;
            if(!Controller.Career.BuyHuntingRifle())
            {Controller.Notice("购买失败：需要 "+FishingCareer.HuntingRiflePrice+" 金币、一个空背包格，且尚未持有猎枪。",5);return false;}
            var bag=Controller.Career.Backpack;
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(bag.At(i).item==FieldItem.HuntingRifle)
            {
                if(i>=FishingBackpack.HotbarCount){bag.Move(i,8);i=8;}
                bag.Select(i);break;
            }
            Controller.Save();Controller.ActionAudio?.Detail("ShopPurchase",Controller.ExplorerPosition+Vector3.up*.7f);
            Controller.Notice(XRSettings.isDeviceActive?"已购买猎枪 · 右扳机单发；命中后靠近，将右手伸到猎物旁按 A 收取材料。":"已购买猎枪 · 左键 / F 单发 · 命中动物后一枪击倒，靠近 E 收取兽肉和毛皮。",7);return true;
        }
        // Called by the controller BEFORE world input. No independent Update ordering race.
        public bool BuySupply(int product)
        {
            if(!CanPurchase)return false;
            return Controller.BuySupply(product);
        }
        public void ValidateContext(bool focused, bool xrActive)
        {
            if (Open && (!focused || xrActive || !NearCounter || !ContextAllowsBrowsing)) Close();
        }
        private void OnDisable() => Close();
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.15f, .8f, .75f, .5f);
            Gizmos.DrawWireCube(new Vector3(0, .9f, -1.1f), new Vector3(3.2f, 1.8f, 6f));
            Gizmos.matrix = Matrix4x4.identity;
            DrawSpaces(clerkSpaces, new Color(1, .7f, .2f), new Vector3(1.4f, 1.8f, 1.4f));
            DrawSpaces(customerSpaces, Color.cyan, new Vector3(1.2f, 1.8f, 1.2f));
        }
        private static void DrawSpaces(Transform[] spaces, Color color, Vector3 size)
        {
            if (spaces == null) return;
            Gizmos.color = color;
            foreach (var space in spaces) if (space != null) Gizmos.DrawWireCube(space.position + Vector3.up * .9f, size);
        }
    }
}
