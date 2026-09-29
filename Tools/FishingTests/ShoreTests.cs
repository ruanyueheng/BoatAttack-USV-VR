using System;
using BoatAttack.Fishing;
internal static class ShoreTests
{
    public static void Run(Action<bool,string> check)
    {
        var c=new FishingCareer();
        check(!c.CompleteShoreTask(-1) && !c.CompleteShoreTask(8),"Shore invalid task rejected");
        check(c.ClaimShoreRewards()==0,"Shore empty survey cannot pay");
        check(c.CompleteShoreTask(0) && !c.CompleteShoreTask(0),"Shore field task records exactly once");
        check(c.ClaimShoreRewards()==0,"Shore partial region cannot pay");
        c.CompleteShoreTask(1);
        check(c.ClaimShoreRewards()==25 && c.ClaimShoreRewards()==0,"Shore region reward exactly once");
        for(int i=2;i<8;i++)c.CompleteShoreTask(i);
        check(c.ClaimShoreRewards()==135 && c.Coins==160 && c.ClaimShoreRewards()==0,"Shore full expedition bonus exactly once");
        var data=c.Snapshot();
        check(data.version==4 && FishingCareer.TryRestore(data,out var restored) && restored.ShoreTaskMask==255 && restored.ClaimShoreRewards()==0,"Shore save round trip cannot duplicate rewards");
        check(FishingCareer.TryRestore(new FishingSaveData{version=3,coins=44},out restored) && restored.Coins==44 && restored.ShoreTaskMask==0,"Shore migrates v3 without inventing exploration");
        check(!FishingCareer.TryRestore(new FishingSaveData{version=3,shoreTaskMask=1},out _),"Shore legacy format rejects new progress");
        check(!FishingCareer.TryRestore(new FishingSaveData{shoreTaskMask=1,shoreRewardMask=1},out _),"Shore rejects rewards for incomplete site");
        check(!FishingCareer.TryRestore(new FishingSaveData{shoreTaskMask=256},out _) && !FishingCareer.TryRestore(new FishingSaveData{shoreRewardMask=16},out _),"Shore rejects unknown mask bits");
        check(!FishingCareer.TryRestore(new FishingSaveData{version=5},out _),"Shore rejects future save format");
        for(int mask=0;mask<=255;mask++)
        {
            int sites=0;
            for(int i=0;i<4;i++)if(FishingExploration.SiteDone(mask,i))sites++;
            var all=new FishingSaveData{shoreTaskMask=mask};
            check(FishingCareer.TryRestore(all,out var partial) && partial.ClaimShoreRewards()==sites*25+(sites==4?60:0),"Shore reward mask "+mask);
        }
    }
}
