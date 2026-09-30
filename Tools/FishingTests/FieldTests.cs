using System;
using BoatAttack.Fishing;
using BoatAttack.VR;
internal static class FieldTests
{
    private static int Count(FishingBackpack bag,FieldItem item){int n=0;for(int i=0;i<36;i++)if(bag.At(i).item==item)n+=bag.At(i).count;return n;}
    public static void Run(Action<bool,string> check)
    {
        var bag=new FishingBackpack();
        check(bag.UsedSlots==1 && bag.Equipped==FieldItem.FieldGuide,"Backpack starts with one guide and 35 free slots");
        check(!bag.TryAdd(FieldItem.Empty,1) && !bag.TryAdd((FieldItem)88,1) && !bag.TryAdd(FieldItem.Shell,0) && !bag.TryAdd(FieldItem.Shell,int.MaxValue),"Backpack invalid/overflow inserts rejected");
        check(bag.TryAdd(FieldItem.Shell,70) && Count(bag,FieldItem.Shell)==70 && bag.At(1).count==64 && bag.At(2).count==6,"Materials stack to 64 then spill to next slot");
        check(bag.TryAdd(FieldItem.Binoculars,2) && bag.At(3).count==1 && bag.At(4).count==1,"Tools never stack");
        check(bag.Move(1,9,true) && bag.At(1).count==32 && bag.At(9).count==32,"Half transfer preserves quantity");
        check(bag.Move(9,2) && bag.At(2).count==38 && bag.At(9).count==0,"Merge clears empty source");
        check(bag.Move(3,1) && bag.At(1).item==FieldItem.Binoculars && bag.At(3).item==FieldItem.Shell,"Different items swap whole stacks");
        check(!bag.Move(1,3,true) && !bag.Move(-1,0) && !bag.Move(0,36) && !bag.Move(0,0),"Invalid and half-swap operations cannot mutate slots");
        check(bag.QuickMove(1) && bag.At(1).count==0 && bag.At(9).item==FieldItem.Binoculars,"Shift transfer hotbar to pack");
        check(bag.QuickMove(9) && bag.At(1).item==FieldItem.Binoculars,"Shift transfer pack to hotbar");
        check(!bag.Select(9) && !bag.Select(-1) && bag.Select(1) && bag.Equipped==FieldItem.Binoculars,"Only nine shortcut slots selectable");
        var copy=bag.At(1);copy.count=99;check(bag.At(1).count==1,"Slot readers cannot mutate inventory");
        check(bag.TryCollect(0,FieldItem.Lantern,1) && !bag.TryCollect(0,FieldItem.Lantern,1),"World pickup claim is exactly once");
        check(!bag.TryCollect(8,FieldItem.Lantern,1) && !bag.TryCollect(-1,FieldItem.Lantern,1),"Unknown pickup IDs rejected");
        check(bag.Observe(LandAnimalKind.Fox) && !bag.Observe(LandAnimalKind.Fox) && !bag.Observe((LandAnimalKind)9),"Animal discoveries are bounded and unique");
        check(FishingBackpack.TryRestore(bag.Snapshot(),bag.Selected,bag.CollectedMask,bag.AnimalMask,out var loaded) && loaded.Collected(0) && loaded.Seen(LandAnimalKind.Fox) && loaded.Equipped==FieldItem.Binoculars,"All bag and exploration state round trips");
        var full=new FishingBackpack(false);check(full.TryAdd(FieldItem.Shell,36*64),"All 36 slots usable");
        check(!full.TryCollect(1,FieldItem.Lantern,1) && !full.Collected(1) && Count(full,FieldItem.Shell)==2304,"Full bag neither consumes world claim nor deletes items");
        check(!full.TryAdd(FieldItem.Shell,1) && !full.QuickMove(0),"Full-stack overflow and transfer safely fail");
        var almost=new FishingBackpack(false);almost.TryAdd(FieldItem.Shell,2303);
        check(!almost.TryAdd(FieldItem.Shell,2) && Count(almost,FieldItem.Shell)==2303,"Partial-capacity insert is atomic");
        foreach(int n in new[]{-1,9,100})check(!FishingBackpack.TryRestore(bag.Snapshot(),n,0,0,out _),"Invalid selection rejects save "+n);
        check(!FishingBackpack.TryRestore(null,0,0,0,out _) && !FishingBackpack.TryRestore(new System.Collections.Generic.List<FieldStack>(),0,0,0,out _),"Missing/wrong-size backpack rejected");
        foreach(int n in new[]{-1,256})check(!FishingBackpack.TryRestore(bag.Snapshot(),0,n,0,out _),"Unknown collected mask rejected "+n);
        foreach(int n in new[]{-1,8})check(!FishingBackpack.TryRestore(bag.Snapshot(),0,0,n,out _),"Unknown animal mask rejected "+n);
        var invalid=bag.Snapshot();invalid[0].count=2;check(!FishingBackpack.TryRestore(invalid,0,0,0,out _),"Overstacked tool save rejected");
        invalid=bag.Snapshot();invalid[0].item=FieldItem.Empty;check(!FishingBackpack.TryRestore(invalid,0,0,0,out _),"Nonempty empty-slot save rejected");
        invalid=bag.Snapshot();invalid[0]=null;check(!FishingBackpack.TryRestore(invalid,0,0,0,out _),"Null stack rejected");
        var random=new Random(80);int shells=Count(bag,FieldItem.Shell),tools=Count(bag,FieldItem.Binoculars);
        for(int i=0;i<3000;i++){int from=random.Next(36),to=random.Next(36);if(i%3==0)bag.QuickMove(from);else bag.Move(from,to,i%2==0);}
        check(Count(bag,FieldItem.Shell)==shells && Count(bag,FieldItem.Binoculars)==tools && FishingBackpack.TryRestore(bag.Snapshot(),bag.Selected,bag.CollectedMask,bag.AnimalMask,out _),"3000 deterministic slot moves conserve all items and stack invariants");
        var career=new FishingCareer();career.Backpack.TryCollect(3,FieldItem.Shell,7);career.Backpack.Observe(LandAnimalKind.Rabbit);career.Backpack.Select(2);
        check(FishingCareer.TryRestore(career.Snapshot(),out var restored) && restored.Backpack.Collected(3) && restored.Backpack.Seen(LandAnimalKind.Rabbit) && restored.Backpack.Selected==2,"Career schema v6 contains backpack and field log");
        var old=career.Snapshot();old.version=5;old.backpack=null;old.selectedSlot=old.collectedPickupMask=old.landAnimalMask=0;
        check(FishingCareer.TryRestore(old,out restored) && restored.Backpack.UsedSlots==1 && restored.Backpack.CollectedMask==0,"v5 migration supplies starter guide without inventing pickups");
        old.backpack=new System.Collections.Generic.List<FieldStack>();check(FishingCareer.TryRestore(old,out _),"Legacy JsonUtility empty-array representation migrates safely");
        old.backpack=career.Backpack.Snapshot();check(!FishingCareer.TryRestore(old,out _),"Legacy saves cannot smuggle v6 inventory");
        var missing=career.Snapshot();missing.backpack=null;check(!FishingCareer.TryRestore(missing,out _),"v6 cannot silently erase a missing backpack");
        var motion=new FishingLocomotion();check(motion.Step(true,true,false,.02f)<=0,"Held jump after spawn must first release");
        motion.Step(true,false,false,.02f);check(motion.Step(true,true,false,.02f)>6,"Grounded jump applies height-derived impulse");
        motion.Step(false,false,false,.02f);float vy=motion.VerticalSpeed;check(motion.Step(false,true,false,.02f)<vy,"Airborne jump cannot add a second impulse");
        motion.HitCeiling();check(motion.VerticalSpeed<=0,"Ceiling cancels upward velocity");
        motion.Reset();motion.Step(true,false,false,.02f);check(motion.Step(true,true,true,.02f)<0,"Crouch cannot jump into overhead obstruction");
        motion.ResetInput();check(motion.Step(true,true,false,.02f)<0,"Modal/focus recovery suppresses held Space");
        vy=motion.VerticalSpeed;check(motion.Step(false,false,false,float.NaN)==vy && motion.Step(false,false,false,float.PositiveInfinity)==vy,"Invalid time cannot poison locomotion");
        check(USVPropulsion.Thrust(-1,0)<0 && USVPropulsion.Thrust(-1,3)<0 && USVPropulsion.Thrust(-1,-1)<0,"S gives reverse force at rest, forwards and astern");
        check(USVPropulsion.Thrust(-1,-4)==0 && USVPropulsion.Thrust(1,-5)==1,"Reverse speed cap never prevents forward recovery");
        check(USVPropulsion.Thrust(1,4)==1 && USVPropulsion.Thrust(0,4)==0,"Existing forward/neutral propulsion preserved");
        check(USVPropulsion.Thrust(float.NaN,0)==0 && USVPropulsion.Thrust(1,float.PositiveInfinity)==0,"Invalid thrust data is neutral");
    }
}
