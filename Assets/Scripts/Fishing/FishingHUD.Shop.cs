using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _shopCategory;
        public int ShopCategory { get=>_shopCategory; set=>_shopCategory=Mathf.Clamp(value,0,FishingSupplies.CategoryCount+2); }
        public void BuyShopSlot(int slot)
        {
            if(_shopCategory==5){if(slot==0)Controller.TackleShop.BuyRifle();return;}
            if(slot<0 || slot>=(_shopCategory>=2?6:3))return;
            if(_shopCategory>=2)Controller.TackleShop.BuySupply((_shopCategory>=6?_shopCategory-3:_shopCategory-2)*FishingSupplies.CategorySize+slot);
            else Controller.TackleShop.Buy((UpgradeTrack)((_shopCategory==1?3:0)+slot));
        }
        private void DrawTackleShop()
        {
            var c=Controller;var r=new Rect(270,Mathf.Max(20,(_height-680)*.5f),1060,680);
            MenuSheet(r);Label(r.x+30,r.y+16,680,46,Spaced("潮汐钓具店"),32,Color.white,true);
            Label(r.x+255,r.y+33,390,22,"TIDE & TACKLE  /  远航的准备",14,Muted);
            Label(new Rect(r.xMax-250,r.y+28,220,35),c.Career.CoinDisplay+" 金币",23,Gold,true,TextAnchor.MiddleRight);
            int categories=FishingSupplies.CategoryCount+3;float tabWidth=(r.width-60)/categories;
            for(int i=0;i<categories;i++)
            {
                string title=i==0?"钓具升级":i==1?"船舶升级":i==5?"狩猎装备":FishingSupplies.CategoryName(i>=6?i-3:i-2);
                if(Tab(new Rect(r.x+30+i*tabWidth,r.y+78,tabWidth-6,40),title,_shopCategory==i))_shopCategory=i;
            }
            bool rifle=_shopCategory==5,supplies=_shopCategory>=2 && !rifle;
            int count=rifle?1:supplies?6:3;
            for(int i=0;i<count;i++)
            {
                int product=(_shopCategory>=6?_shopCategory-3:_shopCategory-2)*FishingSupplies.CategorySize+i;
                var track=(UpgradeTrack)((_shopCategory==1?3:0)+i);
                int level=rifle||supplies?0:c.Career.Level(track),cost=rifle?FishingCareer.HuntingRiflePrice:supplies?FishingSupplies.Price(product):c.Career.NextUpgradeCost(track);
                float x=r.x+24+(supplies?i%2*506:0),y=r.y+140+(supplies?i/2:i)*132,w=supplies?494:r.width-48;
                Rounded(new Rect(x,y,w,118),InkRaised,10);
                Outline(new Rect(x,y,w,118),Hairline,10);
                var icon=new Rect(x+14,y+18,78,78);Rounded(icon,Ink,8);
                if(rifle)FieldIcon(icon,FieldItem.HuntingRifle);
                else if(supplies)ItemIllustration(icon,FishingSupplies.Items[product]);
                else ArtIllustration(icon,track==UpgradeTrack.Rudder?"Compass":track==UpgradeTrack.Cabin?"Lantern":track==UpgradeTrack.Hold?"chest":track==UpgradeTrack.Line?"RopeCoil":track==UpgradeTrack.Reel?"WindBell":"Telescope");
                string name=rifle?"猎枪 · 岸上狩猎":supplies?FishingBackpack.Name(FishingSupplies.Items[product])+" ×"+FishingSupplies.Quantity(product):FishingText.ShopProductName(track)+" · Lv."+level;
                Label(x+108,y+12,supplies?350:710,28,name,20,Color.white,true);
                string description=rifle?"左键长按连射 · 右键瞄准 · F单发 · 动物掉落肉类与素材":supplies?(FishingSupplies.Items[product]==FieldItem.CookedMeat?"F食用恢复50%体力 · 体力充足时不消耗":"放入背包 · I装备到快捷栏 / 用于制作"):FishingText.ShopProductDescription(track);
                Label(x+108,y+46,w-126,24,description,13,Muted);
                if(rifle)Label(x+108,y+80,650,22,"基础钓竿升级价 ×3 · 购买自动装备 · [1] 购买",14,Mint);
                else if(!supplies)Label(x+108,y+80,650,22,cost>0?FishingText.UpgradeEffect(track,level)+" → "+FishingText.UpgradeEffect(track,level+1):FishingText.UpgradeEffect(track,level),14,Mint);
                bool owned=rifle && c.Career.Backpack.Count(FieldItem.HuntingRifle)>0;
                bool locked=!rifle && !supplies && c.Career.SpecializationBlocked(track);
                bool affordable=!owned && !locked && cost>0 && c.Career.CanAfford(cost);
                var buy=new Rect(x+w-180,y+(supplies?76:15),164,32);
                string buyLabel=owned?"已持有":!rifle && !supplies?c.Career.UpgradePurchaseLabel(track):cost<=0?"已满级":c.CreativeMode?"创造模式免费":"购买 "+cost+" 金币";
                if(Button(buy,buyLabel,affordable,affordable,15))
                {if(rifle)c.TackleShop.BuyRifle();else if(supplies)c.TackleShop.BuySupply(product);else c.TackleShop.Buy(track);}

                if(supplies)Label(x+108,y+83,190,22,"已有 "+c.Career.Backpack.Count(FishingSupplies.Items[product]),14,Mint);
            }
            Fill(r.x+30,r.y+548,r.width-60,1,Hairline);
            Label(r.x+30,r.y+565,r.width-60,30,c.VisibleMessage,14,Gold);
            if(Button(new Rect(r.x+30,r.y+618,196,38),"与汐禾交谈",true,false,15))c.Residents.Talk(4);
            if(Button(new Rect(r.x+240,r.y+618,196,38),"卖鱼 / 再次点击确认",c.CanSellFish && c.Career.SaleValue>0,false,15))c.XRNotebook.ConfirmFishSale(false);
            Label(r.x+450,r.y+626,350,23,"二级船舶专精三选一 · 自动保存",14,Muted);
            if(Button(new Rect(r.xMax-196,r.yMax-62,166,38),"E / Esc 关闭",true,false,15))c.ToggleShop();
        }
    }
}
