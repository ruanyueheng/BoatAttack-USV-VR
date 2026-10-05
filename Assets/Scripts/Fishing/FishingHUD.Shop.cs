using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawTackleShop()
        {
            var c = Controller;
            var r = new Rect(360, Mathf.Max(20, (_height - 680) * .5f), 880, 680);
            Dim(); Card(r, Mint);
            Label(r.x + 30, r.y + 22, 530, 42, "潮汐钓具店", 32, Color.white, true);
            Label(r.x + 30, r.y + 67, 710, 28, "捕鱼收入通用 · 返港售鱼 / 交付委托赚取金币", 16, Mint);
            Label(new Rect(r.xMax - 245, r.y + 28, 210, 35), c.Career.Coins + " 金币", 23, Gold, true, TextAnchor.MiddleRight);
            for (int i = 0; i < 3; i++)
            {
                var track = (UpgradeTrack)i;
                int level = c.Career.Level(track), cost = c.Career.NextUpgradeCost(track);
                float y = r.y + 116 + i * 132;
                Rounded(new Rect(r.x + 24, y, r.width - 48, 118), InkRaised, 10);
                Label(r.x + 42, y + 12, 540, 28, FishingText.ShopProductName(track) + " · Lv." + level, 21, Color.white, true);
                Label(r.x + 42, y + 47, 540, 23, FishingText.ShopProductDescription(track), 14, Muted);
                Label(r.x + 42, y + 78, 560, 23, cost > 0 ? FishingText.UpgradeEffect(track, level) + " → " + FishingText.UpgradeEffect(track, level + 1) : "已购齐全部等级 · " + FishingText.UpgradeEffect(track, level), 14, Mint);
                bool affordable = cost > 0 && c.Career.Coins >= cost;
                string label = cost <= 0 ? "已满级" : "[" + (i + 1) + "] 购买 " + cost + " 金币";
                if (Button(new Rect(r.xMax - 228, y + 28, 180, 44), label, affordable, affordable, 16)) c.Shop.Buy(track);
                string balance = cost <= 0 ? "无需重复购买" : affordable ? "购买后余额 " + (c.Career.Coins - cost) : "还差 " + (cost - c.Career.Coins) + " 金币";
                Label(new Rect(r.xMax - 228, y + 79, 180, 21), balance, 12, affordable ? Mint : Faint, false, TextAnchor.MiddleCenter);
            }
            Label(r.x + 30, r.y + 516, r.width - 60, 28, "现有钓竿已配备：鱼线套装直接升级当前钓竿，无需重复买竿。", 15, Mint);
            Label(r.x + 30, r.y + 549, r.width - 60, 35, c.VisibleMessage, 14, Gold);
            Label(r.x + 30, r.y + 619, 560, 25, "1 / 2 / 3 购买 · E / Tab / Esc 关闭 · 自动装备并保存", 15, Muted);
            if (Button(new Rect(r.xMax - 196, r.yMax - 69, 166, 42), "关闭商店", true, false, 16)) c.ToggleShop();
        }
    }
}
