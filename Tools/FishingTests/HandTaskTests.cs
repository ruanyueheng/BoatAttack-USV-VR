using System;
using BoatAttack.Fishing;

internal static class HandTaskTests
{
    public static void Run(Action<bool,string> check)
    {
        var sieve=new FishingHandTask(FishingHandTaskKind.Sieve);
        for(int i=0;i<100;i++)sieve.Sample(0,0,.02f);
        check(sieve.Progress==0,"Holding the sieve still never crafts anything");
        for(int i=0;i<100;i++)sieve.Sample(i%2*.008f,0,.02f);
        check(sieve.Progress==0,"Controller jitter does not count as full sieve strokes");
        sieve.Release();sieve.Sample(-.1f,0,.02f);sieve.Sample(.1f,0,1);
        check(sieve.Progress==0,"Long frames cannot bridge unobserved hand motion");
        for(int i=0;i<20;i++)
        {
            float x=i%2==0?-.1f:.1f;
            for(int j=0;j<12;j++)sieve.Sample(x,0,.02f);
        }
        check(sieve.Complete,"Deliberate alternating sieve strokes complete the activity");
        var bag=new FishingBackpack();
        check(!sieve.Claim(bag) && !sieve.Claimed,"Completion cannot grant loot without source materials");
        check(bag.TryAdd(FieldItem.Sand,2),"Prepare the material transaction");
        check(sieve.Claim(bag) && bag.Count(FieldItem.Sand)==0 && bag.Count(FieldItem.Shell)==1,"Sieve atomically exchanges two sand for one shell");
        check(!sieve.Claim(bag) && bag.Count(FieldItem.Shell)==1,"A completed round cannot pay twice");
        foreach(int direction in new[]{-1,1})
        {
            var winder=new FishingHandTask(FishingHandTaskKind.Winder);
            for(int i=0;i<250;i++)
            {
                double a=i*direction*Math.PI/36;
                winder.Sample((float)Math.Cos(a)*.1f,(float)Math.Sin(a)*.1f,.02f);
            }
            check(winder.Complete,"Winding three complete circles supports direction "+direction);
            var fibers=new FishingBackpack();fibers.TryAdd(FieldItem.Fiber,3);
            check(winder.Claim(fibers) && fibers.Count(FieldItem.Fiber)==0 && fibers.Count(FieldItem.Rope)==1,"Winding makes a real usable rope with the existing recipe");
        }
        var lost=new FishingHandTask(FishingHandTaskKind.Winder);
        lost.Sample(.1f,0,.02f);lost.Release();lost.Sample(0,.1f,.02f);
        check(lost.Progress==0,"Tracking reacquisition never joins disconnected positions into a turn");
        lost.Sample(float.NaN,0,.02f);lost.Sample(float.PositiveInfinity,0,.02f);
        check(lost.Progress==0,"Invalid hand coordinates cannot grant progress");
        var workshop=new FishingWorkshop();
        check(!workshop.HasTimedWork,"Idle workshops have no reason for periodic disk writes");
        workshop.Harvest(new FishingBackpack(),0,FieldItem.Sand,1);
        check(workshop.HasTimedWork,"Real resource recovery still requests progress checkpoints");
        for(int i=0;i<901;i++)workshop.Tick(.1f);
        check(!workshop.HasTimedWork,"Finished recovery stops redundant progress checkpoints");
    }
}
