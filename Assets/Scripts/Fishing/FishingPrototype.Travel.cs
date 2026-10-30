using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public bool ReturnHome()
        {
            if(!_initialized || Career==null || Home==null || !Home.isActiveAndEnabled || StartMenuOpen)return false;
            var feet=Home.Origin+new Vector3(0,.22f,-3.5f);
            if(!Shore.Ground(feet,out var supported) || Mathf.Abs(supported.y-feet.y)>.3f || !Shore.Clearance(supported))
            {Notice("渔屋落脚点暂不可用，已保留当前位置和随身物品。",6);return false;}
            XRNotebook.Close();CloseSaveMenu();CloseHomeChart(true);
            Prologue.Suspend(false);Home.CancelSleep();Home.StandUp(true);Letter.Close();Residents.Close();Realms.Abort();CoastWorld.CloseMenu();
            if(InventoryOpen)ToggleInventory();if(JournalOpen)ToggleJournal();if(Paused)TogglePause();
            MenuControls=false;CancelGuideOpening();ResetToolInput();WaterRoute.CancelPlanning();
            Combat?.CancelChallenge();
            _storyConsumedFrame=-1;
            if(Ashore)Shore.BoardImmediately();
            Anchored=false;Session.Cancel();CastMotion.Reset();AutomationReel=_reeling=false;
            _previousPhase=CatchPhase.Idle;ChartExpanded=true;HarborDeskOpen=ShowDetails=false;
            Teleport(Home.Berth+Vector3.up,Quaternion.identity);UpdateNearestSpot();
            XRSeat.Apply();
            // Reuse supported saved-footing placement so an undecided catch survives travel without blocking landing.
            bool arrived=Shore.RestoreSavedFooting(supported,0) && Shore.Relocate(feet,Quaternion.identity);
            XRSeat.Apply();
            View.ResetInput();Shore.ResetMoveInput();Hands.ResetInteraction();XRNotebook.ResetInput();
            if(HasPendingCatch)DockConsole.Show();else DockConsole.Hide();
            NavigationTarget=HomeNavigationIndex;Save();
            if(!arrived){Notice("已回到渔屋泊位，屋内被物品遮挡；金币、背包和鱼获都已保留。",7);return false;}
            Regions.Show("回潮渔屋","歇脚、整理行囊，再去下一片海。",0);
            Notice(HasPendingCatch?"已回到家中 · 金币和背包保留；请先在鱼获卡片存放或放生待处理的鱼。":"已回到家中 · 金币、背包和鱼箱全部保留。",7);
            return true;
        }
    }
}
