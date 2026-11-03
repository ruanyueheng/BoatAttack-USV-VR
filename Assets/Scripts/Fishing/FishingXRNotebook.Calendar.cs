using System;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private GameObject _calendarSurface;
        private Text _calendarHeading,_calendarSelection,_calendarHour;
        private readonly Image[] _calendarDays=new Image[28];
        private readonly Text[] _calendarNumbers=new Text[28],_calendarFestivals=new Text[28];
        private Image _calendarJump,_calendarReset;
        private Text _calendarResetLabel,_calendarClockLabel,_calendarContinueLabel;
        private bool _calendarResetConfirm;
        private bool CalendarPage=>_page==3 || _page==ClassroomPage;
        private static readonly string[] CalendarSeasons={"春","夏","秋","冬"};

        private void RefreshCalendarPage()
        {
            if(_calendarSurface==null || (_presentedPage!=_page && _presentedPage!=3 && _presentedPage!=ClassroomPage))
            {_c.SelectCurrentCalendarDate();_calendarResetConfirm=false;}
            _status.text=_c.ClassroomMessage;
        }
        private Image CalendarButton(string name,int index,Rect rect,string text,Action command,int size=20)
        {
            var image=DesktopButton(_calendarSurface.transform,name,index,rect,text,command,size);
            image.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleCenter;
            return image;
        }
        private void ChangeCalendar(int field,int amount)
        {_calendarResetConfirm=false;_c.ChangeClassroomDate(field,amount);Refresh();}
        private void BuildCalendarSurface()
        {
            _calendarSurface=new GameObject("Bay calendar month layout",typeof(RectTransform));_calendarSurface.layer=5;
            _calendarSurface.transform.SetParent(_pageDecor.transform,false);HomeRect((RectTransform)_calendarSurface.transform,new Rect(0,0,1100,780));
            var p=_calendarSurface.transform;
            HomeImage(p,"Calendar paper",new Rect(28,18,1052,710),FishingPaperDesign.PaperTint,FishingPaperDesign.Surface(FishingPaperTheme.Workshop),false);
            _calendarHeading=DesktopText(p,"Calendar month heading",new Rect(48,35,680,58),"",36,HomeInk,true);
            DesktopText(p,"Calendar instruction",new Rect(48,94,670,32),"点击日期选择 · 节日以红字标注 · 每季28天",20,HomeMuted);
            CalendarButton("Calendar previous year",740,new Rect(48,140,94,42),"上一年",()=>ChangeCalendar(0,-1));
            CalendarButton("Calendar next year",741,new Rect(152,140,94,42),"下一年",()=>ChangeCalendar(0,1));
            CalendarButton("Calendar previous season",742,new Rect(267,140,128,42),"上一季",()=>ChangeCalendar(1,-1));
            CalendarButton("Calendar next season",743,new Rect(405,140,128,42),"下一季",()=>ChangeCalendar(1,1));
            CalendarButton("Calendar today",744,new Rect(546,140,148,42),"今天",()=>{_c.SelectCurrentCalendarDate();_calendarResetConfirm=false;Refresh();});
            string[] weekdays={"一","二","三","四","五","六","日"};
            for(int i=0;i<7;i++)
            {
                var text=DesktopText(p,"Calendar weekday "+i,new Rect(48+i*94,195,86,30),weekdays[i],19,HomeMuted);text.alignment=TextAnchor.MiddleCenter;
            }
            for(int i=0;i<28;i++)
            {
                int date=i+1;
                var image=_calendarDays[i]=CalendarButton("Calendar date "+date,700+i,new Rect(48+i%7*94,234+i/7*90,86,81),"",()=>
                {_c.ClassroomDate=date;_calendarResetConfirm=false;Refresh();},22);
                _calendarNumbers[i]=image.GetComponentInChildren<Text>();HomeRect(_calendarNumbers[i].rectTransform,new Rect(3,3,80,33));
                _calendarFestivals[i]=DesktopText(image.transform,"Calendar festival "+date,new Rect(3,39,80,38),"",20,HomeMuted);
                _calendarFestivals[i].alignment=TextAnchor.UpperCenter;
            }
            DesktopText(p,"Calendar jump explanation",new Rect(48,610,646,83),"跳转保留当前航程，只能向前调整。\n选择本季节日会定位下一次节日10:00。\n要回到过去，可单独创建课堂展示档。",19,HomeMuted);
            HomeImage(p,"Calendar settings divider",new Rect(715,140,1,562),DesktopRule,null,false);
            DesktopText(p,"Calendar selected heading",new Rect(739,141,312,38),"选中日期与时刻",27,HomeInk,true);
            CalendarButton("Calendar hour earlier",745,new Rect(739,187,67,43),"−1时",()=>ChangeCalendar(3,-1),18);
            _calendarHour=DesktopText(p,"Calendar selected hour",new Rect(815,192,162,34),"",26,HomeInk);_calendarHour.alignment=TextAnchor.MiddleCenter;
            CalendarButton("Calendar hour later",746,new Rect(986,187,67,43),"+1时",()=>ChangeCalendar(3,1),18);
            _calendarSelection=DesktopText(p,"Calendar selected details",new Rect(739,245,314,134),"",19,HomeInk);
            _calendarJump=CalendarButton("Calendar preserve progress jump",760,new Rect(739,389,314,53),"跳转日期 · 保留进度",()=>{_calendarResetConfirm=false;_c.JumpToSelectedCalendarDate();Refresh();});
            CalendarButton("Calendar next festival",761,new Rect(739,454,314,46),"选择本季的下次节日",()=>{_calendarResetConfirm=false;_c.SelectNextSeasonFestival();Refresh();});
            _calendarClockLabel=CalendarButton("Calendar clock mode",762,new Rect(739,512,314,46),"",()=>{_c.ToggleClock();Refresh();}).GetComponentInChildren<Text>();
            _calendarReset=CalendarButton("Calendar independent demo reset",763,new Rect(739,570,314,51),"",()=>
            {
                if(!_calendarResetConfirm){_calendarResetConfirm=true;Refresh();return;}
                _calendarResetConfirm=false;_c.ApplyClassroomDate();if(IsOpen)Refresh();
            },18);
            _calendarResetLabel=_calendarReset.GetComponentInChildren<Text>();
            _calendarContinueLabel=CalendarButton("Calendar continue or restore voyage",764,new Rect(739,633,314,51),"",()=>
            {if(_c.ClassroomDemoActive)_c.LeaveClassroomDemo();else _c.ContinueClassroomDemo();if(IsOpen)Refresh();},18).GetComponentInChildren<Text>();
            CalendarButton("Calendar return to settings",765,new Rect(48,727,285,40),"返回设置",()=>Open(14));
            CalendarButton("Calendar close",766,new Rect(768,727,285,40),"B 收起日历",Close);
        }
        private void PresentCalendarSurface()
        {
            if(_calendarSurface==null)BuildCalendarSurface();
            _calendarSurface.SetActive(true);_status.gameObject.SetActive(false);_pageDetail.gameObject.SetActive(false);
            for(int i=0;i<9;i++)_plates[i].gameObject.SetActive(false);
            var current=_c.Career.Calendar;
            _calendarHeading.text="第 "+_c.ClassroomYear+" 年 · "+CalendarSeasons[_c.ClassroomSeason]+"季海湾日历";
            _calendarHour.text=_c.ClassroomHour.ToString("00")+":00";
            int start=(_c.ClassroomYear-1)*FishingCalendar.DaysPerYear+_c.ClassroomSeason*FishingCalendar.DaysPerSeason;
            for(int i=0;i<28;i++)
            {
                int festival=FishingFestival.On(start+i);bool today=current.Day==start+i;
                _calendarNumbers[i].text=(i+1).ToString("00")+(today?" 今天":"");
                _calendarFestivals[i].text=festival>=0?FishingFestival.Name(festival):(start+i)%7==5?"集市船":"";
                _calendarFestivals[i].color=festival>=0?FishingUITheme.Coral:HomeMuted;
            }
            int selected=FishingFestival.On(start+_c.ClassroomDate-1);
            string eventName=selected>=0?FishingFestival.Name(selected):"普通日";
            _calendarSelection.text=CalendarSeasons[_c.ClassroomSeason]+"季 "+_c.ClassroomDate+"日 · "+eventName+"\n"+_c.SelectedMarketVisit+"\n"+_c.ClassroomMessage;
            _calendarSelection.fontSize=19;
            while(_calendarSelection.preferredHeight>134 && _calendarSelection.fontSize>16)_calendarSelection.fontSize--;
            _calendarJump.color=_c.CanSetTime?HomeLight:FishingUITheme.Disabled;
            _calendarClockLabel.text=current.Automatic?"时钟自动流逝 · 点击固定":"时钟已固定 · 点击自动";
            _calendarResetLabel.text=_calendarResetConfirm?"确认重建独立展示档\n原航程保留 · 展示进度重置":"创建 / 重置独立展示档";
            _calendarReset.color=_calendarResetConfirm?FishingUITheme.Selected:HomeLight;
            _calendarContinueLabel.text=_c.ClassroomDemoActive?"返回原航程":"继续已有独立展示档";
            UpdateCalendarPointer(_pageHover);
        }
        private void UpdateCalendarPointer(int index)
        {
            if(_calendarSurface==null)return;
            for(int i=0;i<28;i++)_calendarDays[i].color=_c.ClassroomDate==i+1 || index==700+i?FishingUITheme.Selected:HomeLight;
        }
    }
}
