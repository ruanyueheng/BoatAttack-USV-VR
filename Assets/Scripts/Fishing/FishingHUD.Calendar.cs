using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawCalendarPage(Rect r)
        {
            var day=Controller.Career.Calendar;
            Label(r.x+32,r.y+116,900,42,day.DateLabel+"    "+day.TimeLabel,28,InkText,true);
            Label(r.x+32,r.y+163,1000,24,"每季 28 天 · 四季轮转 · 15 分钟一昼夜 · 日志、背包与暂停时停表",16,Muted);
            string[] week={"一","二","三","四","五","六","日"};
            for(int i=0;i<7;i++)Label(new Rect(r.x+32+i*132,r.y+202,120,30),"周"+week[i],16,Muted,true,TextAnchor.MiddleCenter);
            for(int i=0;i<28;i++)
            {
                var cell=new Rect(r.x+32+(i%7)*132,r.y+240+(i/7)*72,122,62);
                bool today=i+1==day.Date; Rounded(cell,today?new Color(.12f,.30f,.25f):InkRaised,6);
                Label(cell.x+12,cell.y+7,100,27,(i+1).ToString(),22,today?Mint:InkText,today);
                Label(cell.x+12,cell.y+36,100,20,today?"今天":(i==0?"季节开始":i==27?"季末":""),12,Muted);
            }
            var side=new Rect(r.x+990,r.y+202,310,330);Rounded(side,InkRaised,12);
            Label(side.x+20,side.y+20,270,36,day.Night?"月下海湾":"今日海风",24,InkText,true);
            Label(side.x+20,side.y+68,270,88,"现在 · "+BayClimate.Name(Controller.Weather.Climate.Current)+"\n明日白昼 · "+BayClimate.Name(FishingCalendar.Forecast(day.Day+1)),17,Muted);
            Label(side.x+20,side.y+168,270,142,day.Night?"提灯可照亮近处地面。星灯鱼只在夜晚出现；留意礁区的赤羽狮子鱼。":"白昼适合调查与采集。不同季节会带来不同鱼群，详情见鱼获图鉴。",17,Muted);
            if(Button(new Rect(r.x+32,r.y+553,340,45),"在安全岸边休息至清晨",Controller.CanRest,false,17))Controller.RestUntilMorning();
            Label(r.x+398,r.y+558,850,36,"动物在 5 分钟有效游玩后恢复；休息会推进恢复，暂停与重载保留进度。",15,Muted);
        }
    }
}
