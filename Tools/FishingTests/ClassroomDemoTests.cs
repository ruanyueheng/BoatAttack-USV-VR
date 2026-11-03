using System;
using BoatAttack.Fishing;
internal static class ClassroomDemoTests
{
    public static void Run(Action<bool,string> check)
    {
        var calendar=new FishingCalendar();calendar.ToggleAutomatic();
        check(calendar.AdvanceToDate(1,0,14,10) && calendar.Day==13 && calendar.Hour==10 && !calendar.Automatic,"Calendar jumps to a festival without changing fixed-clock policy");
        check(!calendar.AdvanceToDate(1,0,14,9) && !calendar.AdvanceToDate(1,0,13,23)
            && calendar.Day==13 && calendar.Hour==10,"Calendar refuses past dates and hours without mutating time");
        foreach(var invalid in new[]{new[]{0,0,1,10},new[]{1000,0,1,10},new[]{1,-1,1,10},new[]{1,4,1,10},new[]{1,0,0,10},new[]{1,0,29,10},new[]{1,0,14,-1},new[]{1,0,14,24}})
            check(!calendar.AdvanceToDate(invalid[0],invalid[1],invalid[2],invalid[3]) && calendar.Day==13,"Calendar rejects invalid date components without mutation");
        check(calendar.AdvanceToDate(999,3,28,23) && calendar.Day==FishingCalendar.MaxDay
            && FishingCalendar.TryRestore(calendar.Snapshot(),out _),"Calendar supports the final legal date and round trips");
        var retained=FishingClassroomDemo.Create(1,0,14,10);retained.Lore.Clear(0);
        int balance=retained.Coins,animals=retained.HomeAnimalCount;
        check(retained.Calendar.AdvanceToDate(2,1,14,10) && retained.Farm.AdvanceDay(retained.Calendar.Day)
            && retained.Coins==balance && retained.HomeAnimalCount==animals && retained.Lore.Cleared(0)
            && FishingCareer.TryRestore(retained.Snapshot(),out _),"Forward classroom date jump preserves wallet, animals and realm progress with a valid save");
        foreach(var d in new[]{new[]{1,0,1,0},new[]{1,1,14,12},new[]{2,3,28,23},new[]{999,3,28,23}})
        {
            var c=FishingClassroomDemo.Create(d[0],d[1],d[2],d[3]);var day=c.Calendar;
            check(day.Year==d[0] && day.Season==d[1] && day.Date==d[2] && (int)day.Hour==d[3] && !day.Automatic,"Classroom exact date / fixed clock");
            check(c.Coins>=90000 && c.AquariumLevel==2 && c.AquariumCount==4 && c.HomeAnimalCount==6,"Classroom funds and populated home");
            check(c.Backpack.At(1).item==FieldItem.Binoculars && c.Backpack.At(4).item==FieldItem.HuntingRifle && c.Backpack.At(8).item==FieldItem.Rake,"Classroom tool hotbar");
            check(c.HomeStorage.Count(FieldItem.IronIngot)==32 && c.Farm.Ready(0) && !c.Farm.Ready(11),"Classroom materials and crop stages");
            check(FishingCareer.TryRestore(c.Snapshot(),out _),"Classroom complete save round trip");
        }
        check(!FishingClassroomDemo.ValidDate(0,0,1,10) && !FishingClassroomDemo.ValidDate(1,4,1,10)
            && !FishingClassroomDemo.ValidDate(1,0,29,10) && !FishingClassroomDemo.ValidDate(1,0,1,24),"Classroom date rejects out of range");
    }
}
