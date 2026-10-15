using System;
using BoatAttack.Fishing;

internal static class CoastRulesTests
{
    private static CatchRecord Fish(string id,FishKind kind=FishKind.Coral)=>new CatchRecord
        {id=id,kind=kind,grams=FishingSpecies.Grams(kind,.5f),value=FishingSpecies.Value(kind,false,FishingSpecies.Grams(kind,.5f))};
    public static void Run(Action<bool,string> check)
    {
        var c=new FishingCareer();var fish=Fish("bbq-1");c.Store(fish);
        check(c.BeginBarbecue(fish.id) && c.Count==0 && c.Barbecue.fish.id==fish.id,"Barbecue takes the selected real fish out of the shared fishbox exactly once");
        check(!c.BeginBarbecue(fish.id) && !c.Store(fish) && c.SaleValue==0,"A fish on the grill cannot be cooked twice, sold or reinserted");
        check(!c.TickBarbecue(float.NaN) && !c.TickBarbecue(float.PositiveInfinity) && !c.TickBarbecue(-1)
            && c.Barbecue.remaining==FishingCoastRules.RoastSeconds,"Invalid cooking time never advances the saved grill");
        var data=c.Snapshot();check(FishingCareer.TryRestore(data,out var loaded) && loaded.Barbecue.fish.id==fish.id,"Reload restores reserved fish identity and cooking progress");
        var copy=loaded.Barbecue;copy.fish.id="tampered";copy.remaining=0;
        check(loaded.Barbecue.fish.id==fish.id && loaded.Barbecue.remaining>0,"Grill snapshots cannot mutate the underlying transaction");
        for(int i=0;i<72;i++)loaded.TickBarbecue(.25f);
        check(loaded.BarbecueReady && !loaded.TickBarbecue(1),"Grill reaches ready once after eighteen active seconds");
        loaded.Backpack.TryAdd(FieldItem.Shell,34*64);loaded.Backpack.TryAdd(FieldItem.Driftwood,64);
        check(!loaded.CollectGrilledFish() && loaded.BarbecueReady,"A full backpack leaves the cooked fish safely on the grill");
        loaded.Backpack.Exchange(new[]{FishingRecipes.Stack(FieldItem.Driftwood,64)},FieldItem.Empty,0);
        check(loaded.CollectGrilledFish() && loaded.Backpack.Count(FieldItem.GrilledFish)==1 && loaded.Barbecue.fish==null,"Collection atomically exchanges the saved cooked fish for one backpack meal");
        check(!loaded.CollectGrilledFish() && loaded.Backpack.Count(FieldItem.GrilledFish)==1,"Repeated collection cannot duplicate food");
        data=c.Snapshot();data.barbecue.remaining=float.NaN;check(!FishingCareer.TryRestore(data,out _),"NaN grill progress is rejected on load");
        data=c.Snapshot();data.barbecue.remaining=-1;check(!FishingCareer.TryRestore(data,out _),"Negative grill progress is rejected on load");
        data=c.Snapshot();data.barbecue.remaining=FishingCoastRules.RoastSeconds+1;check(!FishingCareer.TryRestore(data,out _),"Out-of-range grill progress is rejected on load");
        data=c.Snapshot();data.inventory.Add(fish.Copy());check(!FishingCareer.TryRestore(data,out _),"An ID present both on the grill and in the fishbox is rejected");
        data=c.Snapshot();data.recordedIds.Remove(fish.id);check(!FishingCareer.TryRestore(data,out _),"A grilled fish must have a real capture receipt");
        data=c.Snapshot();data.version=12;check(!FishingCareer.TryRestore(data,out _),"Legacy schemas cannot smuggle a new cooking transaction");
        var legacy=new FishingCareer().Snapshot();legacy.version=12;legacy.barbecue=null;
        check(FishingCareer.TryRestore(legacy,out var migrated) && migrated.Barbecue.fish==null,"V12 progress migrates to an empty grill without loss");
        var blank=new FishingCareer().Snapshot();blank.barbecue.fish=new CatchRecord();
        check(FishingCareer.TryRestore(blank,out var blankLoaded) && blankLoaded.Barbecue.fish==null,"JsonUtility's blank inline catch record normalizes to an empty grill");
        blank.barbecue.remaining=.1f;check(!FishingCareer.TryRestore(blank,out _),"A blank grill cannot carry cooking progress");
        var venom=new FishingCareer();var puffer=Fish("puffer",FishKind.Puffer);venom.Store(puffer);
        check(!venom.BeginBarbecue(puffer.id) && venom.Count==1,"Only supported whole sea fish enter the grill");
        check(FishingCoastRules.SeaFind(Fish("first"),false)==FieldItem.Empty
            && FishingCoastRules.SeaFind(Fish("legend",FishKind.Sailfish),true)==FieldItem.Empty,"First discoveries and legendary catches are protected from surprise replacement");
        int junk=0,treasures=0;bool stable=true;CatchRecord treasure=null;FieldItem treasureItem=FieldItem.Empty;
        for(int i=0;i<10000;i++)
        {
            var candidate=Fish("sea-find-"+i);var result=FishingCoastRules.SeaFind(candidate,true);
            stable&=result==FishingCoastRules.SeaFind(candidate,true);
            if(result==FieldItem.SeaTreasure){treasures++;treasure=candidate;treasureItem=result;}
            else if(result!=FieldItem.Empty)junk++;
        }
        check(stable,"Sea-find rolls stay stable across ten thousand catch IDs");
        check(treasures>200 && treasures<400 && junk>900 && junk<1300,"Deterministic sample retains occasional three-percent treasure and eleven-percent litter");
        var finds=new FishingCareer();finds.Store(Fish("discover-coral"));
        check(finds.CollectSeaFind(treasure,treasureItem) && !finds.CollectSeaFind(treasure,treasureItem),"A sea-find receipt can add one real object only once");
        int coins=finds.Coins;
        check(finds.OpenSeaTreasure() && finds.Coins==coins+FishingCoastRules.TreasureCoins && finds.Backpack.Count(FieldItem.SeaTreasure)==0,"Opening a treasure consumes one box before adding its coins");
        check(!finds.OpenSeaTreasure() && finds.Coins==coins+FishingCoastRules.TreasureCoins,"An empty treasure stack cannot pay twice");
        check(FishingCareer.TryRestore(finds.Snapshot(),out var findsLoaded) && !findsLoaded.CollectSeaFind(treasure,treasureItem)
            && !findsLoaded.Store(treasure) && findsLoaded.Coins==coins+FishingCoastRules.TreasureCoins,"A redeemed treasure receipt remains spent after reloading");
        var capped=finds.Snapshot();capped.coins=100000000;capped.backpack[1]=FishingRecipes.Stack(FieldItem.SeaTreasure,1);
        check(FishingCareer.TryRestore(capped,out var rich) && !rich.OpenSeaTreasure() && rich.Backpack.Count(FieldItem.SeaTreasure)==1,"The coin cap never consumes an unpaid treasure");
        var longRun=new FishingCareer();var reserved=Fish("grill-long-history");longRun.Store(reserved);longRun.BeginBarbecue(reserved.id);
        bool stored=true;
        for(int i=0;i<300;i++){stored&=longRun.Store(Fish("after-grill-"+i));longRun.SellAll();}
        check(stored && longRun.Snapshot().recordedIds.Count==FishingCareer.RecentIdLimit
            && FishingCareer.TryRestore(longRun.Snapshot(),out var longLoaded) && longLoaded.Barbecue.fish.id==reserved.id
            && !longLoaded.Store(reserved),"A reserved grill fish survives three hundred later catches, history trimming and reload without duplication");
        var full=new FishingCareer();full.Store(Fish("discover-full"));full.Backpack.TryAdd(FieldItem.Shell,34*64);full.Backpack.TryAdd(FieldItem.Driftwood,64);
        check(!full.CollectSeaFind(treasure,treasureItem) && full.Store(treasure),"A full find bag leaves the original catch valid for the normal pending-fish path");
    }
}
