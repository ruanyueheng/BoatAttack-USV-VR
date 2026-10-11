using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _shopCategory;
        public int ShopCategory { get=>_shopCategory; set=>_shopCategory=Mathf.Clamp(value,0,4); }
        public void BuyShopSlot(int slot)
        {
            if(slot<0 || slot>=(_shopCategory>=2?6:3))return;
            if(_shopCategory>=2)Controller.TackleShop.BuySupply((_shopCategory-2)*6+slot);
            else Controller.TackleShop.Buy((UpgradeTrack)((_shopCategory==1?3:0)+slot));
        }
        private void DrawTackleShop()
        {
            var c=Controller;var r=new Rect(270,Mathf.Max(20,(_height-680)*.5f),1060,680);
            Dim();Card(r,Gold);Label(r.x+30,r.y+20,680,44,"潮汐钓具店",32,Color.white,true);
            Label(r.x+255,r.y+33,390,22,"TIDE & TACKLE  /  远航的准备",14,Muted);
            Label(new Rect(r.xMax-250,r.y+28,220,35),c.Career.Coins+" 金币",23,Gold,true,TextAnchor.MiddleRight);
            string[] tabs={"钓具升级","船舶升级","探索工具","制作补给","远航物资"};
            for(int i=0;i<5;i++)if(Tab(new Rect(r.x+30+i*200,r.y+78,194,40),tabs[i],_shopCategory==i))_shopCategory=i;
            bool supplies=_shopCategory>=2;
            int count=supplies?6:3;
            for(int i=0;i<count;i++)
            {
                int product=(_shopCategory-2)*6+i;
                var track=(UpgradeTrack)((_shopCategory==1?3:0)+i);
                int level=supplies?0:c.Career.Level(track),cost=supplies?FishingSupplies.Price(product):c.Career.NextUpgradeCost(track);
                float x=r.x+24+(supplies?i%2*506:0),y=r.y+140+(supplies?i/2:i)*132,w=supplies?494:r.width-48;
                Rounded(new Rect(x,y,w,118),InkRaised,10);
                Outline(new Rect(x,y,w,118),Hairline,10);
                var icon=new Rect(x+14,y+18,78,78);Rounded(icon,new Color(.025f,.055f,.085f),8);
                if(supplies)ItemIllustration(icon,FishingSupplies.Items[product]);
                else ArtIllustration(icon,track==UpgradeTrack.Rudder?"Compass":track==UpgradeTrack.Cabin?"Lantern":track==UpgradeTrack.Hold?"chest":track==UpgradeTrack.Line?"RopeCoil":track==UpgradeTrack.Reel?"WindBell":"Telescope");
                string name=supplies?FishingBackpack.Name(FishingSupplies.Items[product])+" ×"+FishingSupplies.Quantity(product):FishingText.ShopProductName(track)+" · Lv."+level;
                Label(x+108,y+12,supplies?350:710,28,name,20,Color.white,true);
                string description=supplies?(FishingSupplies.Items[product]==FieldItem.CookedMeat?"F食用恢复50%体力 · 体力充足时不消耗":"放入背包 · I装备到快捷栏 / 用于制作"):FishingText.ShopProductDescription(track);
                Label(x+108,y+46,w-126,24,description,13,Muted);
                if(!supplies)Label(x+108,y+80,650,22,cost>0?FishingText.UpgradeEffect(track,level)+" → "+FishingText.UpgradeEffect(track,level+1):FishingText.UpgradeEffect(track,level),14,Mint);
                bool affordable=cost>0 && c.Career.Coins>=cost;
                var buy=new Rect(x+w-180,y+(supplies?76:15),164,32);
                if(Button(buy,cost<=0?"已满级":"购买 "+cost+" 金币",affordable,affordable,15))
                {if(supplies)c.TackleShop.BuySupply(product);else c.TackleShop.Buy(track);}
                if(supplies)Label(x+108,y+83,190,22,"已有 "+c.Career.Backpack.Count(FishingSupplies.Items[product]),14,Mint);
            }
            Fill(r.x+30,r.y+548,r.width-60,1,Hairline);
            Label(r.x+30,r.y+565,r.width-60,30,c.VisibleMessage,14,Gold);
            if(Button(new Rect(r.x+30,r.y+618,196,38),"与汐禾交谈",true,false,15))c.Residents.Talk(4);
            Label(r.x+248,r.y+626,570,23,"升级立即装备 · 用品进入背包 · 自动保存",14,Muted);
            if(Button(new Rect(r.xMax-196,r.yMax-62,166,38),"E / Esc 关闭",true,false,15))c.ToggleShop();
        }
    }
}
