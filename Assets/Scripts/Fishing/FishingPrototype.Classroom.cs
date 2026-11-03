using System;
using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public bool ClassroomMenuOpen {get;private set;}
        public bool ClassroomDemoActive=>_classroomReturn!=null;
        public int ClassroomYear=1,ClassroomSeason,ClassroomDate=14,ClassroomHour=10;
        public string ClassroomMessage {get;private set;}=string.Empty;
        private string _classroomPlayerPath;
        private int _classroomPlayerSlot;
        private bool _classroomPlayerContinue;
        private bool _classroomReturnToTitle;
        private FishingSlotData _classroomReturn;
        public const string ClassroomSummary="10万金币起始 · 满级船具\n九格工具 · 储物箱材料 · 四境蓝图\n8格水族箱 / 4尾鱼 · 6只家养动物\n成熟菜畦 · 时钟默认固定";
        public string SelectedMarketVisit
        {
            get
            {
                int day=(ClassroomYear-1)*FishingCalendar.DaysPerYear+ClassroomSeason*FishingCalendar.DaysPerSeason+ClassroomDate-1;
                return FishingSeaMarket.Open(day,ClassroomHour)?"集市船营业 · "+FishingGrounds.Name(FishingSeaMarket.Ground(day))
                    +(FishingFestival.On(day)>=0?" · 节日限定在售":"") : "集市船未营业 · 周六和节日 08:00–19:00";
            }
        }
        public void OpenClassroomMenu()
        {
            if(!CanSaveCheckpoint()){Notice("请先结束当前捕鱼、剧情、游泳或手作，再打开自定义航程。",5);return;}
            ClassroomMenuOpen=true;MenuControls=false;SaveMenuOpen=false;
            if(!UnityEngine.XR.XRSettings.isDeviceActive && !Paused)TogglePause();
        }
        public void CloseClassroomMenu()=>ClassroomMenuOpen=false;
        public void ChangeClassroomDate(int field,int step)
        {
            if(field==0)ClassroomYear=Mathf.Clamp(ClassroomYear+step,1,999);
            else if(field==1)ClassroomSeason=(ClassroomSeason+step+4)%4;
            else if(field==2)ClassroomDate=Mathf.Clamp(ClassroomDate+step,1,28);
            else if(field==3)ClassroomHour=(ClassroomHour+step+24)%24;
        }
        public void SelectCurrentCalendarDate()
        {
            var date=Career.Calendar;ClassroomYear=date.Year;ClassroomSeason=date.Season;ClassroomDate=date.Date;
            ClassroomHour=Mathf.Min(23,Mathf.CeilToInt(date.Hour));ClassroomMessage=string.Empty;
        }
        public void SelectNextSeasonFestival()
        {
            int year=Mathf.Max(ClassroomYear,Career.Calendar.Year);
            int day=(year-1)*FishingCalendar.DaysPerYear+ClassroomSeason*FishingCalendar.DaysPerSeason+FishingFestival.Date-1;
            if(day*1440d+600<Career.Calendar.TotalMinutes)year++;
            if(year>999){ClassroomMessage="当前季节已没有可跳转的节日，请选择其他季节或独立展示档。";return;}
            ClassroomYear=year;ClassroomDate=FishingFestival.Date;ClassroomHour=10;
            ClassroomMessage="已选中下次"+FishingFestival.Name(ClassroomSeason)+"，10:00集市船营业。";
        }
        public bool JumpToSelectedCalendarDate()
        {
            if(!CanSaveCheckpoint() || !CanSetTime){ClassroomMessage="请先结束捕鱼、秘境、游泳或手作，再调整日期。";return false;}
            if(!Career.Calendar.AdvanceToDate(ClassroomYear,ClassroomSeason,ClassroomDate,ClassroomHour))
            {ClassroomMessage="保留进度只能向前跳转；要展示过去的日期，请创建独立展示档。";return false;}
            Career.Farm.AdvanceDay(Career.Calendar.Day);Farm?.Refresh();HomeEcology?.Refresh();
            Weather.ApplyEnvironment();Save();
            ClassroomMessage="已跳转至"+Career.Calendar.DateLabel+" "+Career.Calendar.TimeLabel+"，航程进度保留。";
            Notice(ClassroomMessage,6);return true;
        }
        public void ContinueClassroomDemo()=>LoadClassroom(false);
        public void ApplyClassroomDate()=>LoadClassroom(true);
        private void LoadClassroom(bool reset)
        {
            if(!CanSaveCheckpoint() || !FishingClassroomDemo.ValidDate(ClassroomYear,ClassroomSeason,ClassroomDate,ClassroomHour))return;
            string demoPath=ClassroomDemoActive?SavePath:Path.Combine(Path.GetDirectoryName(Path.GetFullPath(SavePath)),"ClassroomDemo","classroom-demo.json");
            try
            {
                FishingSlotData demo;
                FishingSlotInfo latest=null;int demoSlot=0;bool exists=false;
                for(int slot=0;slot<FishingSlotStore.Count;slot++)
                    foreach(FishingSlotKind kind in Enum.GetValues(typeof(FishingSlotKind)))
                    {
                        var info=FishingSlotStore.Read(demoPath,slot,kind);exists|=info.Exists;
                        if(info.Valid && (latest==null || string.CompareOrdinal(info.Data.savedUtc,latest.Data.savedUtc)>0))
                        {latest=info;demoSlot=slot;}
                    }
                if(!reset && latest!=null)demo=latest.Data;
                else
                {
                    if(!reset && exists){ClassroomMessage="自定义存档已有但不可读，请明确选择下方的重置自定义存档。";return;}
                    demoSlot=0;
                    var seed=FishingClassroomDemo.Create(ClassroomYear,ClassroomSeason,ClassroomDate,ClassroomHour);
                    string run="classroom-"+Guid.NewGuid().ToString("N");
                    FishingSlotStore.WriteNewRun(demoPath,0,seed,new FishingResumeData(),run);
                    demo=FishingSlotStore.Read(demoPath,0,FishingSlotKind.Manual).Data;
                }
                if(demo==null || !FishingCareer.TryRestore(demo.career,out var career)){ClassroomMessage="自定义存档验证失败，当前航程保留。";return;}
                if(!ClassroomDemoActive)
                {
                    if(CanContinue)
                    {
                        if(ActiveSaveSlot<0){ClassroomMessage="请先在存档菜单选择一个槽保存当前航程，再进入自定义航程。";return;}
                        if(!WriteCheckpoint(ActiveSaveSlot,FishingSlotKind.Auto)){ClassroomMessage=SaveWarning;return;}
                        if(HasIsolatedProfile)Save();
                    }
                    RememberSafeResume();
                    _classroomPlayerPath=SavePath;_classroomPlayerSlot=ActiveSaveSlot;_classroomPlayerContinue=_sessionResumable;_classroomReturnToTitle=StartMenuOpen;
                    _classroomReturn=new FishingSlotData{career=Career.Snapshot(),resume=_safeResume,runId=_runId};
                }
                _savingReady=false;
                try{SavePath=demoPath;ActiveSaveSlot=demoSlot;_runId=demo.runId;_sessionResumable=true;ApplySavedCareer(career,demo.resume);}
                finally{_savingReady=true;}
                RefreshSaveSlots();EnterClassroomWorld();
                ClassroomMessage=Career.Calendar.DateLabel+" "+Career.Calendar.TimeLabel;
                if(reset || !demo.resume.positioned)ReturnHome();
                Weather.ApplyEnvironment();Save();Notice(ClassroomMessage+"\n"+SelectedMarketVisit,8);
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            {ClassroomMessage="自定义存档未能载入："+ex.Message;Notice(ClassroomMessage,8);}
        }
        public void LeaveClassroomDemo()
        {
            if(!ClassroomDemoActive || !CanSaveCheckpoint())return;
            var original=_classroomReturn;
            if(!FishingCareer.TryRestore(original.career,out var career)){ClassroomMessage="原航程验证失败，自定义存档仍保留。";return;}
            if(!WriteCheckpoint(ActiveSaveSlot,FishingSlotKind.Auto)){ClassroomMessage=SaveWarning;return;}
            _savingReady=false;
            try
            {
                SavePath=_classroomPlayerPath;ActiveSaveSlot=_classroomPlayerSlot;_runId=original.runId;
                _sessionResumable=_classroomPlayerContinue;ApplySavedCareer(career,original.resume);_classroomReturn=null;
            }
            finally{_savingReady=true;}
            RefreshSaveSlots();EnterClassroomWorld();Weather.ApplyEnvironment();
            if(_classroomReturnToTitle)OpenStartMenu();
            Notice("已退出自定义航程，恢复原航程；自定义存档独立保留。",6);
        }
        private void EnterClassroomWorld()
        {
            ClassroomMenuOpen=false;XRNotebook.Close();CloseSaveMenu();
            if(StartMenuOpen){TitleVoyage.Close();StartMenuOpen=false;}_titleStarted=true;MenuControls=false;
            if(Paused)TogglePause();ApplyAudioSettings();
        }
    }
}
