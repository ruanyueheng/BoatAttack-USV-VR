using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Scene-authored shop. Reserved NPC markers contain no characters or autonomous behaviour.</summary>
    public sealed class FishingTackleShop : MonoBehaviour
    {
        public Transform entrance, purchasePoint, mooringPoint;
        public Transform[] clerkSpaces, customerSpaces;
        [System.NonSerialized] public FishingPrototype Controller;
        public bool IsOpen { get; private set; }
        public bool Inside
        {
            get
            {
                if (Controller == null || !Controller.IsAshore) return false;
                var p = transform.InverseTransformPoint(Controller.ObserverPosition);
                return Mathf.Abs(p.x) < 5.7f && p.z > -4.9f && p.z < 4.8f && p.y > -.2f && p.y < .5f;
            }
        }
        public bool CanBrowse => Inside && purchasePoint != null &&
            Vector3.Distance(Controller.ObserverPosition, purchasePoint.position) < 2.6f;
        public bool Toggle()
        {
            if (IsOpen) { Close(); return true; }
            if (!CanBrowse || Controller.Paused) return false;
            IsOpen = true;
            Controller.Notice("欢迎来到潮汐钓具店 · 选择装备升级，金币和装备自动保存。", 5f);
            return true;
        }
        public void Close() { IsOpen = false; }
        public bool Buy(UpgradeTrack track)
        {
            if (!IsOpen || !CanBrowse || Controller.Paused) return false;
            return Controller.BuyUpgrade(track);
        }
        private void Update()
        {
            if (IsOpen && (!CanBrowse || Controller.Paused)) Close();
        }
        private void OnDisable() { Close(); }
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
