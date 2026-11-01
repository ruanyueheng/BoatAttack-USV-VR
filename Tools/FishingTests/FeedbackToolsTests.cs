using System;
using BoatAttack.Fishing;
internal static class FeedbackToolsTests
{
    public static void Run(Action<bool,string> check)
    {
        check(FishingSupplies.Items.Length==36 && FishingSupplies.CategoryCount==6 && FishingSupplies.CategoryName(5)=="农林工具","A new tool category appends without replacing existing shops");
        check((int)FieldItem.GuardianTrophy==63 && (int)FieldItem.Axe==64 && (int)FieldItem.Rake==66,"Tools append after stable combat IDs");
        check(FishingBackpack.ToolMatchesResource(FieldItem.Axe,FieldItem.Driftwood) && !FishingBackpack.ToolMatchesResource(FieldItem.Axe,FieldItem.Sand),"Axe targets actual wood rather than sand");
        check(FishingBackpack.ToolMatchesResource(FieldItem.Shovel,FieldItem.Sand) && FishingBackpack.ToolMatchesResource(FieldItem.Shovel,FieldItem.Clay) && !FishingBackpack.ToolMatchesResource(FieldItem.Shovel,FieldItem.IronOre),"Shovel gathers sand/clay while ore keeps pick requirement");
        foreach(var item in new[]{FieldItem.Axe,FieldItem.Shovel,FieldItem.Rake})check(FishingBackpack.Limit(item)==1 && FishingBackpack.Name(item)!="空槽","New tool has a real name and singleton stack "+item);
        foreach(var kind in new[]{FishingHandTaskKind.Saw,FishingHandTaskKind.Forge})
        {
            var task=new FishingHandTask(kind);var bag=new FishingBackpack();var input=kind==FishingHandTaskKind.Saw?FieldItem.Driftwood:FieldItem.IronIngot;int amount=kind==FishingHandTaskKind.Saw?1:3;
            bag.TryAdd(input,amount);task.Sample(0,0,.02f);for(int i=0;i<60;i++)task.Sample(i%2*.008f,i%2*.008f,.02f);
            check(task.Progress==0 && !task.Claim(bag) && bag.Count(input)==amount,"Stationary/jitter/cancelled craft retains inputs "+kind);
            task.Release();for(int i=0;i<40;i++)for(int j=0;j<12;j++){float v=i%2==0?-.1f:.1f;task.Sample(kind==FishingHandTaskKind.Saw?v:0,kind==FishingHandTaskKind.Forge?v:0,.02f);}
            check(task.Complete,"Purposeful bounded strokes complete "+kind);
            check(task.Claim(bag) && bag.Count(input)==0 && bag.Count(task.Output)==task.OutputCount && !task.Claim(bag),"Handcraft atomically produces the actual requested item once "+kind);
        }
        var parts=new FishingHandTask(FishingHandTaskKind.Forge,true);var iron=new FishingBackpack();iron.TryAdd(FieldItem.IronIngot,6);for(int i=0;i<40;i++)for(int f=0;f<12;f++)parts.Sample(0,i%2==0?-.1f:.1f,.02f);check(parts.Complete && parts.Claim(iron) && iron.Count(FieldItem.GunParts)==1 && iron.Count(FieldItem.IronIngot)==0,"Forge can shape the six-ingot real gun-parts recipe");
        var forge=new FishingHandTask(FishingHandTaskKind.Forge);for(int i=0;i<80;i++)for(int j=0;j<12;j++)forge.Sample(i%2==0?-.1f:.1f,0,.02f);
        check(forge.Progress==0,"Sideways movement cannot fake downward forge strikes");
        var airborne=new FishingHandTask(FishingHandTaskKind.Forge);
        for(int i=0;i<30;i++)for(int j=0;j<12;j++)airborne.Sample(0,i%2==0?.10f:.20f,.02f);
        check(airborne.Strokes==0,"A hammer moving above the billet cannot forge in mid-air");
        for(int i=0;i<12;i++)airborne.Sample(0,0,.02f);
        check(airborne.Strokes==1,"An armed hammer counts once when its face reaches the billet");
        for(int i=0;i<30;i++)airborne.Sample(0,.01f,.02f);
        check(airborne.Strokes==1,"Resting on the billet cannot repeat a strike");
        foreach(int version in new[]{15,16,17})foreach(var tool in new[]{FieldItem.Axe,FieldItem.Shovel,FieldItem.Rake})
        {
            var source=new FishingCareer().Snapshot();source.version=version;source.backpack[1]=new FieldStack{item=tool,count=1};
            check(!FishingCareer.TryRestore(source,out _),"Old schema rejects new tools in backpack "+version+" "+tool);
            source=new FishingCareer().Snapshot();source.version=version;source.homeStorage[0]=new FieldStack{item=tool,count=1};
            check(!FishingCareer.TryRestore(source,out _),"Old schema rejects new tools in home storage "+version+" "+tool);
        }
    }
}
