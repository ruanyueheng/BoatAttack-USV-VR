using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        internal static readonly Rect AcceptanceBanner = new Rect(384, 24, 832, 72);
        private static readonly Rect AcceptanceProbe = new Rect(1204, 32, 4, 4);

        internal static Vector2 AcceptanceProbeScreen(int width, int height)
        {
            float scale = LayoutScale(width, height), offset = (width - 1600 * scale) * .5f;
            return new Vector2(offset + AcceptanceProbe.center.x * scale, AcceptanceProbe.center.y * scale);
        }

        private void DrawAcceptanceToolbar()
        {
            var a = Controller.Acceptance;
            var r = AcceptanceBanner;
            Glass(r);
            Label(r.x + 12, r.y + 5, r.width - 34, 25,
                "验收 SANDBOX  ·  " + ((int)a.CurrentCase + 1) + "/" + FishingAcceptanceCases.Count + "  " + FishingAcceptanceCases.Title(a.CurrentCase), 16, Mint, true);
            Label(r.x + 12, r.y + 31, r.width - 24, 18,
                "F5 / F6 切换场景    F7 " + (a.FreezeScenario ? "解除定格" : "定格阶段") + "    F8 全 HUD 截图    F9 返回实玩", 13, Color.white);
            Label(r.x + 12, r.y + 52, r.width - 24, 17, a.Status, 12, FishingAcceptance.Failure == null ? Muted : Danger);
            // An opaque end-of-frame probe makes black/scene-only captures fail instead of passing as HUD evidence.
            if (a.IsCapturing) Fill(AcceptanceProbe.x, AcceptanceProbe.y, AcceptanceProbe.width, AcceptanceProbe.height, Color.magenta);
        }
    }
}
