using System;
using BoatAttack.Fishing;
internal static class ClassroomDemoTests
{
    public static void Run(Action<bool,string> check)
    {
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
