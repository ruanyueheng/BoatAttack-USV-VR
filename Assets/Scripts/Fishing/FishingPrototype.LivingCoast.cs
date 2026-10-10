using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingXRNotebook XRNotebook { get; private set; }
        public FishingEffects Effects { get; private set; }
        public bool CanRest => Career!=null && !HasPendingCatch && !WorldActionBusy && CanChangeBait &&
            (AtHarbor || (Ashore && Vector3.Distance(Shore.Position,Shore.LandingPoint)<4));
        public void RestUntilMorning()
        {
            if(!CanRest)return;
            if(JournalOpen)ToggleJournal();
            if(Paused)return;
            Career.Calendar.RestUntilMorning();
            for(int i=0;i<64;i++)Career.Workshop.Tick(1);
            Weather.ApplyEnvironment();Save();Notice("海风渐暖 · "+Career.Calendar.DateLabel+" 06:00",6);
        }
    }
}
