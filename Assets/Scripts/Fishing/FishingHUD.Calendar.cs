using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawCalendarPage(Rect r)
        {
            var c=Controller;var day=c.Career.Calendar;
            Label(r.x,r.y,710,43,day.DateLabel,29,InkText,true);
            Label(r.x,r.y+46,710,25,day.TimeLabel+"    /    "+day.TideName+"    /    每季28天",17,Muted);
            Label(r.x,r.y+71,710,22,"今日潮汐  "+FishingTide.Today(day),14,Gold);
            string[] week={"一","二","三","四","五","六","日"};
            for(int i=0;i<7;i++)Label(new Rect(r.x+i*94,r.y+104,84,27),week[i],16,Muted,true,TextAnchor.MiddleCenter);
            for(int i=0;i<28;i++)
            {
                bool today=i+1==day.Date;var cell=new Rect(r.x+(i%7)*94,r.y+147+(i/7)*78,83,66);
                Rounded(cell,today?FishingUITheme.Selected:InkRaised,6);if(today)Outline(cell,Gold,6,2);
                Label(new Rect(cell.x,cell.y+9,83,34),(i+1).ToString("00"),23,today?Gold:InkText,true,TextAnchor.MiddleCenter);
                int fest=FishingFestival.On(day.Day-day.Date+1+i);
                if(today || fest>=0)Label(new Rect(cell.x-4,cell.y+44,91,19),(today?"今天":"")+(today && fest>=0?" · ":"")+FishingFestival.Name(fest),12,fest>=0?FishingUITheme.Coral:Mint,false,TextAnchor.MiddleCenter);
            }
            if(Button(new Rect(r.x,r.y+484,312,42),"岸边休息至清晨",c.CanRest,false,17))c.RestUntilMorning();
            if(Button(new Rect(r.x+328,r.y+484,312,42),"导航回潮渔屋",true,false,17)){c.ToggleJournal();c.SelectNavigation(c.HomeNavigationIndex);}
            var d=new Rect(r.x+700,r.y,430,527);Rounded(d,InkRaised,8);ChapterImage(new Rect(d.x,d.y,430,190),day.Night?7:4);
            WeatherIcon(new Rect(d.x+26,d.y+217,55,55),c.Weather.Climate.Current);
            Label(d.x+102,d.y+218,300,40,day.Night?"月下海湾":"今日海风",28,InkText,true);
            Label(d.x+28,d.y+290,374,63,"现在 · "+BayClimate.Name(c.Weather.Climate.Current)+"\n明日 · "+BayClimate.Name(FishingCalendar.Forecast(day.Day+1)),18,Muted);
            Label(d.x+28,d.y+350,374,22,BayClimate.FishingEffect(c.Weather.Climate.Current) ?? "",14,Gold);
            if(Button(new Rect(d.x+28,d.y+375,178,40),"白昼 08:00",c.CanSetTime,!day.Night,16))c.SetDayNight(false);
            if(Button(new Rect(d.x+222,d.y+375,180,40),"夜晚 21:00",c.CanSetTime,day.Night,16))c.SetDayNight(true);
            if(Button(new Rect(d.x+28,d.y+434,374,38),day.Automatic?"时间流逝 · 点击固定":"时间固定 · 点击恢复",c.CanSetTime,!day.Automatic,15))c.ToggleClock();
            if(!c.Prologue.Unlocked(_replay))_replay=c.Prologue.Latest;
            if(Button(new Rect(d.x+28,d.y+485,40,30),"‹",true,false,14))_replay=StepReplay(-1);
            if(Button(new Rect(d.x+74,d.y+485,282,30),"重看剧情 · "+FishingPrologue.Name(_replay),c.Prologue.CanBegin,false,14))c.Prologue.Begin(_replay);
            if(Button(new Rect(d.x+362,d.y+485,40,30),"›",true,false,14))_replay=StepReplay(1);
        }
        private FishingPrologue.Sequence _replay;
        private FishingPrologue.Sequence StepReplay(int step)
        {
            int n=System.Enum.GetValues(typeof(FishingPrologue.Sequence)).Length,i=(int)_replay;
            for(int k=0;k<n;k++){i=(i+step+n)%n;if(Controller.Prologue.Unlocked((FishingPrologue.Sequence)i))return (FishingPrologue.Sequence)i;}
            return _replay;
        }
    }
}
