using System;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        public const int SeaMarketPage=41;
        public bool SeaMarketOpen=>IsOpen && _page==SeaMarketPage;
        private int _marketPhrase;
        private void RefreshSeaMarketPage()
        {
            var market=_c.SeaMarket;var calendar=_c.Career.Calendar;int festival=FishingFestival.On(calendar.Day);
            bool available=market!=null && market.CanBrowse;
            _status.fontSize=21;
            _status.text=FishingSeaMarket.Owner+" · 海上集市\n"+calendar.DateLabel+" · "+calendar.TimeLabel
                +"\n"+(festival>=0?FishingFestival.Name(festival)+"限定商品在售":"本季商品与海上邮局")
                +"\n余额 "+_c.Career.CoinDisplay+" 金币\n靠近商船并停止行驶，指向商品后按扳机选择。\n"+(available?market.FeedbackMessage:"请先靠近营业中的商船。交易尚未发生。");
            for(int i=0;i<5;i++)
            {
                var goods=(MarketGoods)i;bool offered=goods!=MarketGoods.Keepsake || festival>=0;
                bool sold=offered && market!=null && market.Sold(goods);
                string name=offered?FishingSeaMarket.Name(goods,calendar.Season,festival):"节日限定纪念品";
                string detail=!offered?"仅在节日期间售卖":sold?"已收藏 / 今日已购":goods==MarketGoods.Buyback?"收购船舱里的稀有鱼":(_c.CreativeMode?"创造模式免费":FishingSeaMarket.Price(goods)+" 金币");
                Set(i,name+"\n"+detail,available && offered && !sold?(Action)(()=>market.TradeFromNotebook(goods)):null);
            }
            Set(5,"寄出漂流信\n“"+FishingSeaMarket.Phrases[_marketPhrase]+"”",available?(Action)(()=>market.TradeFromNotebook(MarketGoods.Letter,_marketPhrase)):null);
            Set(6,"切换寄信短句",()=>{_marketPhrase=(_marketPhrase+1)%FishingSeaMarket.Phrases.Length;Refresh();});
            Set(7,"海湾日历 / 节日",()=>Open(3));Set(8,"结束浏览",Close);
        }
    }
}
