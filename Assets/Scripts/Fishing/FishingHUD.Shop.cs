using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawTackleShop()
        {
            var c = Controller;
            var r = new Rect(360, Mathf.Max(20, (_height - 640) * .5f), 880, 640);
            Dim(); Card(r, Mint);
            Label(r.x + 30, r.y + 22, 530, 42, "潮汐钓具店", 32, Color.white, true);
            Label(r.x + 30, r.y + 67, 710, 28, "TIDE & TACKLE  /  沙滩装备站", 16, Mint);
            Label(new Rect(r.xMax - 245, r.y + 28, 210, 35), c.Career.Coins + " 金币", 23, Gold, true, TextAnchor.MiddleRight);
            string[] names = { "钓竿 / 强化鱼线套装", "精密卷线器", "扩容鱼箱" };
            string[] descriptions = { "升级抗拉鱼线，降低张力增长，挑战更大的鱼。", "提升收线速度，缩短与鱼的拉锯时间。", "增加储鱼空间，让每次出航带回更多收获。" };
            for (int i = 0; i < 3; i++)
            {
                var track = (UpgradeTrack)i;
                int level = c.Career.Level(track), cost = c.Career.NextUpgradeCost(track);
                float y = r.y + 116 + i * 132;
                Rounded(new Rect(r.x + 24, y, r.width - 48, 118), InkRaised, 10);
                Label(r.x + 42, y + 12, 510, 28, names[i] + " · Lv." + level, 21, Color.white, true);
                Label(r.x + 42, y + 47, 530, 23, descriptions[i], 14, Muted);
                Label(r.x + 42, y + 78, 560, 23, cost > 0 ? FishingText.UpgradeEffect(track, level) + " → " + FishingText.UpgradeEffect(track, level + 1) : "已达到最高级 · " + FishingText.UpgradeEffect(track, level), 14, Mint);
                bool affordable = cost > 0 && c.Career.Coins >= cost;
                string label = cost <= 0 ? "已满级" : "[" + (i + 1) + "]  " + cost + " 金币";
                if (Button(new Rect(r.xMax - 228, y + 28, 180, 44), label, affordable, affordable, 17)) c.TackleShop.Buy(track);
                Label(new Rect(r.xMax - 228, y + 79, 180, 21), cost > 0 && !affordable ? "金币不足，捕鱼出售可赚取" : "购买后立即生效", 12, affordable ? Mint : Faint, false, TextAnchor.MiddleCenter);
            }
            Label(r.x + 30, r.y + 518, r.width - 60, 35, c.VisibleMessage, 14, Gold);
            Label(r.x + 30, r.y + 579, 560, 25, "1 / 2 / 3 购买 · E / Tab / Esc 关闭 · 自动保存", 15, Muted);
            if (Button(new Rect(r.xMax - 196, r.yMax - 69, 166, 42), "关闭商店", true, false, 16)) c.ToggleShop();
        }
    }
}
