namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        public const int ClassroomPage=34;
        private void OpenClassroomPage(){_c.OpenClassroomMenu();if(_c.ClassroomMenuOpen)Open(ClassroomPage);}
        private void RefreshClassroomPage()
        {
            _status.fontSize=20;
            _status.text="航程设置 · 独立存档\n"+(_c.ClassroomDemoActive?"当前自定义航程，正常航程保留":"进入时自动保留正常航程")
                +"\n"+FishingPrototype.ClassroomSummary+"\n"+_c.SelectedMarketVisit+"\n本季14日10时：可购节日限定\n按日期重置：重建进度并补满物资\n"+_c.ClassroomMessage;
            Set(0,"年份 −\n第 "+_c.ClassroomYear+" 年",()=>{_c.ChangeClassroomDate(0,-1);Refresh();});
            Set(1,"年份 +\n第 "+_c.ClassroomYear+" 年",()=>{_c.ChangeClassroomDate(0,1);Refresh();});
            Set(2,"季节\n"+new[]{"春","夏","秋","冬"}[_c.ClassroomSeason]+" · 点击切换",()=>{_c.ChangeClassroomDate(1,1);Refresh();});
            Set(3,"日期 −\n"+_c.ClassroomDate+" 日",()=>{_c.ChangeClassroomDate(2,-1);Refresh();});
            Set(4,"日期 +\n"+_c.ClassroomDate+" 日",()=>{_c.ChangeClassroomDate(2,1);Refresh();});
            Set(5,"时刻\n"+_c.ClassroomHour.ToString("00")+":00 · 点击下一小时",()=>{_c.ChangeClassroomDate(3,1);Refresh();});
            Set(6,"按日期重置",_c.ApplyClassroomDate);
            Set(7,_c.ClassroomDemoActive?"返回原航程":"继续自定义存档",_c.ClassroomDemoActive?(System.Action)_c.LeaveClassroomDemo:_c.ContinueClassroomDemo);
            Set(8,"返回设置",()=>{_c.CloseClassroomMenu();Open(14);});
        }
    }
}
