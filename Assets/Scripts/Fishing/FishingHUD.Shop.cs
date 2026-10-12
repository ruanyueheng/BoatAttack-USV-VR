using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawTackleShop()
        {
            var c = Controller;
            var r = new Rect(360, Mathf.Max(20, (_height - 784) * .5f), 880, 784);
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
                if (Button(new Rect(r.xMax - 228, y + 28, 180, 44), label, affordable, affordable, 16)) c.TackleShop.Buy(track);
                string balance = cost <= 0 ? "无需重复购买" : affordable ? "购买后余额 " + (c.Career.Coins - cost) : "还差 " + (cost - c.Career.Coins) + " 金币";
                Label(new Rect(r.xMax - 228, y + 79, 180, 21), balance, 12, affordable ? Mint : Faint, false, TextAnchor.MiddleCenter);
            }
            float gunY=r.y+512;int price=FishingCareer.HuntingRiflePrice;
            bool owned=c.Career.Backpack.Count(FieldItem.HuntingRifle)>0;
            Rounded(new Rect(r.x+24,gunY,r.width-48,118),InkRaised,10);
            Label(r.x+42,gunY+12,540,28,"猎枪 · 岸上狩猎",21,Color.white,true);
            Label(r.x+42,gunY+47,540,23,"左键 / F 单发 · 一枪击倒动物 · E 收取兽肉和毛皮",14,Muted);
            Label(r.x+42,gunY+78,540,23,"价格为基础钓竿升级（"+FishingCareer.UpgradeCost(UpgradeTrack.Line,0)+" 金币）的三倍",14,Mint);
            if(Button(new Rect(r.xMax-228,gunY+28,180,44),owned?"已持有":"[4] 购买 "+price+" 金币",!owned && c.Career.Coins>=price,!owned && c.Career.Coins>=price,16))c.TackleShop.BuyRifle();
            Label(new Rect(r.xMax-228,gunY+79,180,21),owned?"背包 / 快捷栏装备":"购买后自动装备",12,Mint,false,TextAnchor.MiddleCenter);
            Label(r.x+30,r.y+650,r.width-60,48,c.VisibleMessage,14,Gold);
            Label(r.x + 30, r.y + 723, 590, 25, "1 / 2 / 3 / 4 购买 · E / Tab / Esc 关闭 · 自动保存", 15, Muted);
            if (Button(new Rect(r.xMax - 196, r.yMax - 69, 166, 42), "关闭商店", true, false, 16)) c.ToggleShop();
        }
    }
}
