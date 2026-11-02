using System;
using System.Linq;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        public const int FishSalesPage=38;
        private string _fishSaleQuote;
        private float _fishSaleUntil;
        private FishingCareer _fishSaleProfile;
        public void OpenFishSales(){_fishSaleQuote=null;Open(FishSalesPage);}
        private string FishSaleQuote(bool all)
        {
            var career=_c.Career;
            return all+":"+career.Coins+":"+career.ReservedCount+":"+career.SaleValue+":"+career.HoldValue+":"
                +string.Join(",",career.Inventory.Select(f=>f.id+":"+f.value));
        }
        public bool ConfirmFishSale(bool all)
        {
            var career=_c.Career;
            if(!_c.CanSellFish || _c.Paused || _c.HasPendingCatch || _c.StoryBlocksGameplay || _c.WorldActionBusy)
            {_fishSaleQuote=null;_c.Notice("请先到潮汐渔港，或走到钓具店柜台。页面可设置航点。",5);return false;}
            int value=all?career.HoldValue:career.SaleValue;
            if(value<=0){_fishSaleQuote=null;_c.Notice(career.Count>0?"鱼货已为委托保留，可选择全部出售。":"鱼箱为空，先把钓起的鱼存入鱼箱。",5);return false;}
            string quote=FishSaleQuote(all);
            if(_fishSaleProfile!=career || _fishSaleQuote!=quote || Time.unscaledTime>_fishSaleUntil)
            {
                _fishSaleProfile=career;_fishSaleQuote=quote;_fishSaleUntil=Time.unscaledTime+6;
                _c.Notice("确认"+(all?"全部出售（包括委托鱼货）":"出售非委托鱼货")+"，可得 "+value+" 金币。再次点击按钮确认。",6);
                return false;
            }
            _fishSaleQuote=null;
            return (all?_c.SellAll():_c.Sell())>0;
        }
        private void RefreshFishSales()
        {
            var career=_c.Career;bool near=_c.CanSellFish;
            _status.fontSize=21;
            _status.text="卖鱼 / 鱼货回收\n鱼箱 "+career.Count+" / "+career.Capacity+" · 总值 "+career.HoldValue+" 金币\n"
                +"委托保留 "+career.ReservedCount+" 条 · 可售 "+career.SaleValue+" 金币\n"
                +(near?"已到交易点，点击两次确认出售。":"交易地点：潮汐渔港，或潮汐钓具店柜台。\n选择下方航点跟随导航；店内靠近女店员柜台。")
                +"\n鱼需先从抄网存入鱼箱。\n"+_c.VisibleMessage;
            foreach(bool all in new[]{false,true})
            {
                int value=all?career.HoldValue:career.SaleValue;bool confirm=_fishSaleProfile==career && _fishSaleQuote==FishSaleQuote(all) && Time.unscaledTime<=_fishSaleUntil;
                Set(all?1:0,(confirm?"再次点击确认":"出售"+(all?"全部鱼货":"非委托鱼货"))+"\n"+value+" 金币"+(all?" · 包含委托鱼":" · 保留委托鱼"),
                    near && value>0?(Action)(()=>{ConfirmFishSale(all);Refresh();}):null);
            }
            Set(2,"导航潮汐渔港\n在青色港圈内停船",()=>{_c.SelectNavigation(0);Close();_c.Notice("已导航潮汐渔港。减速停在青色港圈内，再按 B → 卖鱼与委托。",7);});
            Set(3,"导航钓具店回收柜台\n上岸进入商店，靠近柜台",()=>{_c.SelectNavigation(_c.ShopNavigationIndex);Close();_c.Notice("已导航潮汐钓具店。上岸走到女店员柜台，再按 B → 卖鱼与委托。",7);});
            Set(4,"查看鱼箱 / 委托",()=>Open(13));Set(5,"钓具与船舶升级",()=>Open(4));
            Set(6,"港口事务",()=>Open(7));Set(7,"返回首页",()=>Open());Set(8,"关闭",Close);
        }
    }
}
