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
        var missed=new FishingHandTask(FishingHandTaskKind.Forge);
        missed.SampleContact(0,0,.02f,true);missed.SampleContact(0,.12f,.02f,true);
        missed.SampleContact(.3f,0,.02f,false);missed.SampleContact(0,0,.02f,true);
        check(missed.Strokes==0,"A raised hammer missing the billet cannot cash in that strike when slid back onto the metal");
        missed.SampleContact(0,.12f,.02f,true);missed.SampleContact(0,0,.02f,true);
        check(missed.Strokes==1,"The same held hammer can be lifted and strike normally after a miss");
        var liftedSaw=new FishingHandTask(FishingHandTaskKind.Saw);
        liftedSaw.SampleContact(0,0,.02f,true);liftedSaw.SampleContact(.12f,0,.02f,false);
        liftedSaw.SampleContact(-.12f,0,.02f,true);
        check(liftedSaw.Strokes==0,"Lifting a saw out of the kerf and replacing it cannot join disconnected points into a cut");
        liftedSaw.SampleContact(0,0,.02f,true);
        check(liftedSaw.Strokes==1,"The same saw resumes a real push through the wood after being replaced");
        foreach(var kind in new[]{FishingHandTaskKind.Saw,FishingHandTaskKind.Sieve})foreach(int sign in new[]{-1,1})
        {
            var shortStroke=new FishingHandTask(kind);float previous=0;shortStroke.Sample(0,0,.02f);
            for(int frame=1;frame<=20;frame++)shortStroke.Sample(sign*.045f*frame/20,0,.02f);
            check(shortStroke.Strokes==0,"The initial 4.5 cm half-stroke stays below the unchanged 7.5 cm threshold "+kind+" / "+sign);
            previous=sign*.045f;
            for(int stroke=0;stroke<14 && !shortStroke.Complete;stroke++)
            {
                float next=stroke%2==0?-sign*.045f:sign*.045f;
                for(int frame=1;frame<=20;frame++)shortStroke.Sample(previous+(next-previous)*frame/20,0,.02f);
                previous=next;
                if(stroke==0)check(shortStroke.Strokes==1,"A real 9 cm reversal starts from its turning point rather than the initial centre "+kind+" / "+sign);
            }
            check(shortStroke.Complete,"Continuous centre-started short strokes finish without an artificial long priming gesture "+kind+" / "+sign);
            var longStroke=new FishingHandTask(kind);longStroke.Sample(0,0,.02f);
            for(int frame=1;frame<=24;frame++)longStroke.Sample(sign*.12f*frame/24,0,.02f);
            check(longStroke.Strokes==1,"A normal smooth 12 cm initial push still counts once "+kind+" / "+sign);
            for(int frame=0;frame<80;frame++)longStroke.Sample(sign*.12f+(frame%2==0?.004f:-.004f),0,.02f);
            check(longStroke.Strokes==1,"Stationary millimetre jitter cannot add a stroke after normal work "+kind+" / "+sign);
            var noisyPush=new FishingHandTask(kind);noisyPush.Sample(0,0,.02f);
            for(int centimetre=1;centimetre<=12;centimetre++)
            {
                noisyPush.Sample(sign*centimetre*.01f,0,.02f);
                noisyPush.Sample(sign*(centimetre*.01f-.00075f),0,.02f);
                noisyPush.Sample(sign*centimetre*.01f,0,.02f);
            }
            check(noisyPush.Strokes==1,"A real 12 cm first push survives 0.75 mm tracking rebounds and counts once "+kind+" / "+sign);
        }
        var looseCrank=new FishingHandTask(FishingHandTaskKind.Winder);
        looseCrank.SampleContact(.1f,0,.02f,true);looseCrank.SampleContact(0,.1f,.02f,false);
        looseCrank.SampleContact(-.1f,0,.02f,true);
        check(looseCrank.Progress==0,"Leaving the wheel plane cannot bank an unobserved half-turn on re-entry");
        foreach(int version in new[]{15,16,17})foreach(var tool in new[]{FieldItem.Axe,FieldItem.Shovel,FieldItem.Rake})
        {
            var source=new FishingCareer().Snapshot();Program.Legacy(source,version);source.backpack[1]=new FieldStack{item=tool,count=1};
            check(!FishingCareer.TryRestore(source,out _),"Old schema rejects new tools in backpack "+version+" "+tool);
            source=new FishingCareer().Snapshot();Program.Legacy(source,version);source.homeStorage[0]=new FieldStack{item=tool,count=1};
            check(!FishingCareer.TryRestore(source,out _),"Old schema rejects new tools in home storage "+version+" "+tool);
        }
    }
}
