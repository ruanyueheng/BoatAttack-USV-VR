using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawClassroomMenu()
        {
            var c=Controller;var r=new Rect(270,(_height-680)*.5f,1060,680);MenuSheet(r,false);
            Label(r.x+35,r.y+25,990,50,"航程设置",34,InkText,true);
            Label(r.x+35,r.y+87,990,44,c.ClassroomDemoActive?"正在使用独立自定义存档；退出即可恢复原航程。":"进入自定义航程会保留当前航程；不会覆盖正常的三个存档槽。",19,InkText);
            Label(r.x+35,r.y+143,990,130,FishingPrototype.ClassroomSummary,21,InkText);
            string[] names={"年份","季节","日期","时刻"};
            string[] values={"第 "+c.ClassroomYear+" 年",new[]{"春","夏","秋","冬"}[c.ClassroomSeason],c.ClassroomDate+" 日",c.ClassroomHour.ToString("00")+":00"};
            for(int i=0;i<4;i++)
            {
                float x=r.x+35+i*250;Label(x,r.y+290,235,28,names[i],17,Muted);
                if(Button(new Rect(x,r.y+327,45,42),"−",true,false,23))c.ChangeClassroomDate(i,-1);
                Label(new Rect(x+50,r.y+334,130,35),values[i],22,InkText,true,TextAnchor.MiddleCenter);
                if(Button(new Rect(x+190,r.y+327,45,42),"+",true,false,23))c.ChangeClassroomDate(i,1);
            }
            Label(r.x+35,r.y+382,990,30,c.SelectedMarketVisit+"；本季14日10时可购节日限定。",17,Gold);
            Label(r.x+35,r.y+416,990,38,"按所选日期开始会重置此航程进度并补满物资，时钟固定；正常航程保留。",16,Muted);
            if(Button(new Rect(r.x+35,r.y+459,470,55),"按所选日期开始 / 重置自定义存档",true,true,21))c.ApplyClassroomDate();
            if(Button(new Rect(r.x+535,r.y+459,490,55),c.ClassroomDemoActive?"返回原航程":"继续已有自定义存档",true,false,21))
            {if(c.ClassroomDemoActive)c.LeaveClassroomDemo();else c.ContinueClassroomDemo();}
            Label(r.x+35,r.y+534,990,65,c.ClassroomMessage,17,Gold);
            if(Button(new Rect(r.x+35,r.yMax-61,300,40),"返回",true,false,20))c.CloseClassroomMenu();
        }
    }
}
