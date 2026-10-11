using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _shopCategory;
        public int ShopCategory { get=>_shopCategory; set=>_shopCategory=Mathf.Clamp(value,0,3); }
        public void BuyShopSlot(int slot)
        {
            if(slot<0 || slot>=(_shopCategory>=2?6:3))return;
            if(_shopCategory>=2)Controller.TackleShop.BuySupply((_shopCategory==3?6:0)+slot);
            else Controller.TackleShop.Buy((UpgradeTrack)((_shopCategory==1?3:0)+slot));
        }
        private void DrawTackleShop()
        {
            var c=Controller;var r=new Rect(270,Mathf.Max(20,(_height-680)*.5f),1060,680);
            Dim();Card(r,Mint);Label(r.x+30,r.y+20,600,44,"潮汐钓具店 · 海湾百货",31,Color.white,true);
            Label(new Rect(r.xMax-250,r.y+28,220,35),c.Career.Coins+" 金币",23,Gold,true,TextAnchor.MiddleRight);
            string[] tabs={"钓具升级","船舶升级","探索工具","制作补给"};
            for(int i=0;i<4;i++)if(Tab(new Rect(r.x+30+i*250,r.y+78,240,40),tabs[i],_shopCategory==i))_shopCategory=i;
            bool supplies=_shopCategory>=2;
            int count=supplies?6:3;
            for(int i=0;i<count;i++)
            {
                int product=(_shopCategory==3?6:0)+i;
                var track=(UpgradeTrack)((_shopCategory==1?3:0)+i);
                int level=supplies?0:c.Career.Level(track),cost=supplies?FishingSupplies.Price(product):c.Career.NextUpgradeCost(track);
                float x=r.x+24+(supplies?i%2*506:0),y=r.y+140+(supplies?i/2:i)*132,w=supplies?494:r.width-48;
                Rounded(new Rect(x,y,w,118),InkRaised,10);
                string name=supplies?FishingBackpack.Name(FishingSupplies.Items[product])+" ×"+FishingSupplies.Quantity(product):FishingText.ShopProductName(track)+" · Lv."+level;
                Label(x+18,y+10,supplies?280:680,28,name,20,Color.white,true);
                Label(x+18,y+44,w-36,24,supplies?"放入背包 · I 查看 / 移入快捷栏使用":FishingText.ShopProductDescription(track),14,Muted);
                if(!supplies)Label(x+18,y+77,700,24,cost>0?FishingText.UpgradeEffect(track,level)+" → "+FishingText.UpgradeEffect(track,level+1):FishingText.UpgradeEffect(track,level),14,Mint);
                bool affordable=cost>0 && c.Career.Coins>=cost;
                var buy=new Rect(x+w-190,y+(supplies?75:12),172,34);
                if(Button(buy,cost<=0?"已满级":"购买 "+cost+" 金币",affordable,affordable,15))
                {if(supplies)c.TackleShop.BuySupply(product);else c.TackleShop.Buy(track);}
                if(supplies)Label(x+18,y+80,260,23,"背包已有 "+c.Career.Backpack.Count(FishingSupplies.Items[product]),14,Mint);
            }
            Label(r.x+30,r.y+553,r.width-60,42,c.VisibleMessage,14,Gold);
            Label(r.x+30,r.y+618,760,25,"升级立即装备 · 用品进入背包 · E / Tab / Esc 关闭 · 自动保存",15,Muted);
            if(Button(new Rect(r.xMax-196,r.yMax-66,166,40),"关闭商店",true,false,16))c.ToggleShop();
        }
    }
}
