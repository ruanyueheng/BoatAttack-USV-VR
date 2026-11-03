namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        public const int ClassroomPage=34;
        private void OpenClassroomPage(){_c.OpenClassroomMenu();if(_c.ClassroomMenuOpen)Open(ClassroomPage);}
        private void RefreshClassroomPage()
        {
            RefreshCalendarPage();
        }
    }
}
