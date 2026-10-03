using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>PR #17's authored Tide & Tackle shop, integrated with v10 progression. Empty markers are not NPC AI.</summary>
    public sealed class FishingTackleShop : MonoBehaviour
    {
        public Transform entrance, purchasePoint, mooringPoint;
        public Transform[] clerkSpaces, customerSpaces;
        [System.NonSerialized] public FishingPrototype Controller;
        private Collider _landingDeck;
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
        public bool CanPurchase => Open && NearCounter && ContextAllowsBrowsing;
        private bool ContextAllowsBrowsing => Controller != null && !Controller.Paused && !Controller.InventoryOpen
            && !Controller.JournalOpen && !Controller.HasPendingCatch && !Controller.Expedition.Aligning
            && !XRSettings.isDeviceActive && (Controller.Automated || Application.isFocused);

        private void Awake() => ResolveLandingDeck();
        public void Bind(FishingPrototype controller) { Controller = controller; ResolveLandingDeck(); }
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
            Controller.Notice("欢迎来到潮汐钓具店 · 选择装备升级，金币和装备自动保存。", 5f);
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
        // Called by the controller BEFORE world input. No independent Update ordering race.
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
