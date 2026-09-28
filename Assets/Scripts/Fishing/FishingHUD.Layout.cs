using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        public const float OrderLineHeight = 44f;
        public const float UpgradeRowHeight = 56f;

        /// <summary>Shared by rendering and smoke tests; content, not a fixed card height, places the footer.</summary>
        public readonly struct HarborLayout
        {
            public readonly Rect Panel, Delivery, Sale, Footer;
            public readonly float OrderY, SaleHeadingY, ReservedY, UpgradeHeadingY, UpgradeStartY;

            public HarborLayout(int orderLines)
            {
                const float left = 1122f, top = 130f, width = 450f;
                float x = left + 22f, w = width - 44f;
                OrderY = top + 116f;
                Delivery = new Rect(x, OrderY + Mathf.Clamp(orderLines, 1, 3) * OrderLineHeight + 4f, w, 40f);
                SaleHeadingY = Delivery.yMax + 16f;
                Sale = new Rect(x, SaleHeadingY + 34f, w, 40f);
                ReservedY = Sale.yMax + 6f;
                UpgradeHeadingY = ReservedY + 32f;
                UpgradeStartY = UpgradeHeadingY + 36f;
                Footer = new Rect(x, UpgradeStartY + 3f * UpgradeRowHeight + 8f, w, 20f);
                Panel = new Rect(left, top, width, Footer.yMax + 14f - top);
            }

            public float UpgradeY(int track) => UpgradeStartY + track * UpgradeRowHeight;
        }
    }
}
