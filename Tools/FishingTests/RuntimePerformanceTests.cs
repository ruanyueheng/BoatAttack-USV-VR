using System;
using BoatAttack.Fishing;

internal static class RuntimePerformanceTests
{
    internal static void Run(Action<bool,string> check)
    {
        var data=new FishingCareer().Snapshot();data.coins=300;
        FishingCareer.TryRestore(data,out var career);
        check(career.BuyHuntingRifle() && career.Backpack.Select(1)
            && career.KillLandAnimal(0,LandAnimalKind.Rabbit,0,1,0,0),"Performance fixture creates an unclaimed rabbit");
        var receipt=career.HuntedAnimal(0);receipt.collected=true;
        check(!career.HuntedAnimal(0).collected && !career.WildAnimalAvailable(0,LandAnimalKind.Rabbit),
            "Hunt lookup retains defensive copies and hides unclaimed wildlife");
        check(career.CollectHuntedAnimal(0) && !career.WildAnimalAvailable(0,LandAnimalKind.Rabbit),
            "Availability stays false during active recovery");
        CountAvailable(career);
        long before=GC.GetAllocatedBytesForCurrentThread();
        int available=CountAvailable(career);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        check(available==8750 && allocated==0,"10,000 warm wildlife availability queries allocate zero bytes (actual: "+allocated+")");
        career.Calendar.RestUntilMorning();
        check(!career.WildAnimalAvailable(0,LandAnimalKind.Rabbit),"Sleep cannot skip the preserved active recovery timer");
        for(int i=0;i<300;i++)career.TickHuntedRespawns(1);
        check(career.WildAnimalAvailable(0,LandAnimalKind.Rabbit) && career.HuntedAnimal(0)==null,
            "Availability returns at the full recovery boundary");
        check(!career.WildAnimalAvailable(-1,LandAnimalKind.Rabbit) && !career.WildAnimalAvailable(0,LandAnimalKind.Fox)
            && !career.WildAnimalAvailable(12,LandAnimalKind.Gull),"Optimized lookup still rejects invalid IDs and species");
    }
    private static int CountAvailable(FishingCareer career)
    {
        int available=0;
        for(int i=0;i<10000;i++)if(career.WildAnimalAvailable(i%8,(LandAnimalKind)(i%8%3)))available++;
        return available;
    }
}
