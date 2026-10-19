using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingXRNotebook XRNotebook { get; private set; }
        public FishingEffects Effects { get; private set; }
        public bool CanSetTime => Career!=null && !HasPendingCatch && !WorldActionBusy && !StoryBlocksGameplay && CanChangeBait;
        public void SetDayNight(bool night)
        {
            if(!CanSetTime || !Career.Calendar.AdvanceToHour(night?21:8))return;
            Weather.ApplyEnvironment();Save();Notice("时间已推进至 "+Career.Calendar.DateLabel+" "+Career.Calendar.TimeLabel,5);
        }
        public void ToggleClock()
        {
            if(!CanSetTime)return;
            Career.Calendar.ToggleAutomatic();Save();Notice(Career.Calendar.Automatic?"昼夜自动流逝已开启":"时间已固定；可在日历中切换昼夜",4);
        }
        public bool CanRest => Career!=null && Career.Calendar.Day<FishingCalendar.MaxDay && !HasPendingCatch && !WorldActionBusy && !StoryBlocksGameplay && CanChangeBait &&
            (AtHarbor || (Home!=null && Home.NearBed) || (Ashore && Vector3.Distance(Shore.Position,Shore.LandingPoint)<4));
        public void RestUntilMorning()
        {
            if(!CanRest)return;
            if(JournalOpen)ToggleJournal();
            if(Paused)return;
            CompleteRest();
        }
        internal void CompleteRest()
        {
            Career.Calendar.RestUntilMorning();
            Career.Farm.AdvanceDay(Career.Calendar.Day);
            Farm?.Refresh();
            for(int i=0;i<64;i++)Career.Workshop.Tick(1);
            Weather.ApplyEnvironment();Save();Notice("海风渐暖 · "+Career.Calendar.DateLabel+" 06:00",6);
        }
    }
}
