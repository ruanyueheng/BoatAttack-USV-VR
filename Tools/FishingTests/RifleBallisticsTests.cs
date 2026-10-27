using System;
using BoatAttack.Fishing;

internal static class RifleBallisticsTests
{
    public static void Run(Action<bool,string> check)
    {
        check(FishingRifleBallistics.Spread(0,true,false)<FishingRifleBallistics.Spread(0,false,false),"Rifle aiming tightens first-shot spread");
        check(FishingRifleBallistics.Spread(0,true,true)>FishingRifleBallistics.Spread(0,false,false),"Rifle movement still costs accuracy while aiming");
        check(FishingRifleBallistics.Spread(15,false,false)>FishingRifleBallistics.Spread(0,false,false),"Rifle sustained fire widens spread");
        check(FishingRifleBallistics.Spread(int.MaxValue,false,false)==FishingRifleBallistics.Spread(20,false,false),"Rifle spread has a finite upper bound");
        FishingRifleBallistics.Recoil(0,out var firstPitch,out var firstYaw);
        FishingRifleBallistics.Recoil(12,out var burstPitch,out var burstYaw);
        check(firstPitch==0 && firstYaw==0 && burstPitch>firstPitch && Math.Abs(burstYaw)>.01f,"Rifle first shot is centered and a burst rises and sways");
        FishingRifleBallistics.Recoil(int.MaxValue,out var maxPitch,out var maxYaw);
        check(maxPitch<=5 && Math.Abs(maxYaw)<=1.5f,"Rifle recoil is bounded after long continuous fire");
        check(FishingRifleBallistics.RecoverySeconds>.1f && FishingRifleBallistics.RecoverySeconds<1,"Rifle stopping briefly restores the burst");
    }
}
