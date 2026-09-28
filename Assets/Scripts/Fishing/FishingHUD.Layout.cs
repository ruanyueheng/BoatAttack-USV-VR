using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        public readonly struct FocusLayout
        {
            public readonly Rect Heading, Pocket, Toolbar, Chart, ChartTab, Action, Notice, Catch, Observe, HarborButton;
            public FocusLayout(float height, bool anchored, CatchPhase phase)
            {
                Heading = new Rect(24, 24, 328, 62);
                Pocket = new Rect(1272, 24, 304, 40);
                Toolbar = new Rect(1240, 76, 336, 34);
                Chart = new Rect(24, height - 274, 224, 246);
                ChartTab = new Rect(24, height - 62, 224, 34);
                bool fight = anchored && (phase == CatchPhase.Fighting || phase == CatchPhase.Landing);
                Action = fight ? new Rect(486, height - 134, 628, 110) : new Rect(510, height - 98, 580, 74);
                Notice = new Rect(480, Action.y - 58, 640, 48);
                Catch = new Rect(24, 102, 350, 96);
                Observe = new Rect(24, 102, 328, 34);
                HarborButton = new Rect(Action.xMax - 144, Action.y + 20, 126, 34);
            }
        }

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
