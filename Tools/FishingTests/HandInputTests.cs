using System;
using BoatAttack.Fishing;
internal static class HandInputTests
{
    public static void Run(Action<bool,string> check)
    {
        check(FishingHandInput.CaptureShorePointer(true,true,true,false,false,false,false),"Shore captures without right mouse");
        for(int flag=0;flag<6;flag++)check(!FishingHandInput.CaptureShorePointer(flag!=0,flag!=1,flag!=2,flag==3,flag==4,flag==5,false),"Shore modal/focus/XR gate "+flag);
        check(!FishingHandInput.CaptureShorePointer(true,true,true,false,false,false,true),"Alt releases shore pointer");
        var s=new FishingHandInput();
        s.Update(true,1,true,true,1,true,0);check(!s.Holding && !s.Activated,"Recovery with held grip cannot grab or activate");
        s.Update(true,0,false,true,1,true,0);s.Update(true,1,false,false,1,true,0);check(!s.Holding,"Grab requires nearby socket");
        s.Update(true,1,false,true,1,true,0);check(s.Holding && !s.Activated,"Grip grabs nearby rod without casting");
        s.Update(true,1,true,true,1,true,0);check(s.Activated && !s.Reeling,"Trigger edge activates; stationary crank cannot reel");
        s.Update(true,1,true,true,1,true,12);check(!s.Activated && s.Reeling,"Held trigger does not repeat; moving crank reels");
        s.Update(true,1,true,true,1,true,150);check(!s.Reeling,"Tracking-angle jump cannot reel");
        s.Update(true,1,false,true,1,false,160);check(!s.Reeling,"Crank requires proximity");
        s.Update(false,1,true,true,1,true,170);check(!s.Holding && !s.Activated && !s.Reeling,"Tracking loss clears tool state");
        s.Update(true,1,true,true,1,true,175);check(!s.Holding,"Tracking recovery requires release");
        s.Update(true,0,false,true,1,true,179);s.Update(true,1,false,true,1,true,179);s.Update(true,1,false,true,1,true,-177);check(s.Reeling,"Crank wraps across 180 degrees");
        s.Update(true,float.NaN,true,true,1,true,0);check(!s.Holding && !s.Activated,"Non-finite grip rejected");
    }
}
