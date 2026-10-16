using System;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        private void PrepareAcceptanceFind(FishingAcceptanceCase selected)
        {
            var item=selected==FishingAcceptanceCase.BootCatch?FieldItem.OldBoot:selected==FishingAcceptanceCase.CanCatch?FieldItem.RustyCan:FieldItem.SeaTreasure;
            CatchRecord receipt=null;
            for(int i=0;i<10000;i++)
            {
                var candidate=new CatchRecord{id="v030-acceptance-"+item+"-"+i,kind=FishKind.Coral,grams=500,value=FishingSpecies.Value(FishKind.Coral,false,500)};
                if(FishingCoastRules.SeaFind(candidate,true)==item){receipt=candidate;break;}
            }
            if(receipt==null || !Career.Discovered(FishKind.Coral,false) || !Cast())throw new InvalidOperationException("Sea-find acceptance could not start a real cast.");
            Session.Cancel();
            if(!Session.Cast(FishKind.Coral,false,Career.LineLevel,receipt.id))throw new InvalidOperationException("Sea-find receipt rejected.");
            for(int i=0;i<1500 && Session.Phase==CatchPhase.Waiting;i++)Session.Tick(.02f,false);
            if(!Hook())throw new InvalidOperationException("Sea-find acceptance missed its hook.");
            for(int i=0;i<6000 && Session.Phase==CatchPhase.Fighting;i++)Session.Tick(.02f,Session.Tension<.6f);
            for(int i=0;i<15;i++)Session.Tick(.1f,false);
            if(!CatchRig.CaptureAndStoreForAutomation() || HasPendingCatch || LastCatch.Find!=item || Career.Backpack.Count(item)!=1)
                throw new InvalidOperationException("Sea-find acceptance failed its real net/backpack transaction.");
            _previousPhase=Session.Phase;
        }
    }
}
