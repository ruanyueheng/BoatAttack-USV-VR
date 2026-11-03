using System;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        private bool _creativeRealmTravel;
        public bool CreativeRealmTravel=>CreativeMode && _creativeRealmTravel;
        public void SetCreativeRealmTravel(bool enabled)
        {
            _creativeRealmTravel=CreativeMode && enabled;
            Notice(CreativeRealmTravel?"本次游玩已开启四岛入口传送。":"四岛入口传送已关闭。",4);
        }
        public bool TeleportToRealmEntrance(int island)
        {
            if(!CreativeRealmTravel){Notice("请先开启创造模式和秘境入口传送。",5);return false;}
            return MoveToRealmEntrance(island);
        }
        private bool CanUseCreativeDemo()
        {
            if(!CreativeMode || !_initialized || !CanContinue || StartMenuOpen || HasPendingCatch || WorldActionBusy
                || Realms.Transitioning || Shore.Swimming || Shore.Climbing || Prologue.Open || DialogueOpen || HandGames.Active || Home.Sleeping)
            {Notice("请开启创造模式并结束当前捕鱼、剧情、游泳或手作后再操作。",6);return false;}
            if(!HasIsolatedProfile && !ClassroomDemoActive && ActiveSaveSlot<0)
            {Notice("请先在存档菜单保存并接管一个槽，再修改演示进度。",6);return false;}
            return true;
        }
        private bool MoveToRealmEntrance(int island)
        {
            if(!CanUseCreativeDemo())return false;
            if(!Realms.TryDemoEntranceFooting(island,out var feet,out var rotation))
            {Notice("入口附近没有足够的安全站立和操作空间，当前位置保留。",6);return false;}
            XRNotebook.Close();CloseSaveMenu();CloseHomeChart(true);
            Home.CancelSleep();Home.StandUp(true);Letter.Close();Residents.Close();CoastWorld.CloseMenu();
            if(InventoryOpen)ToggleInventory();if(JournalOpen)ToggleJournal();if(Paused)TogglePause();
            CancelGuideOpening();ResetToolInput();WaterRoute.CancelPlanning();Combat?.CancelChallenge();
            if(Ashore)Shore.BoardImmediately();else Realms.Abort();
            Expedition.Cancel();Anchored=false;Session.Cancel();CastMotion.Reset();AutomationReel=_reeling=false;
            Teleport(FishingIslandWorld.Berth(island)+Vector3.up,Quaternion.identity);UpdateNearestSpot();
            bool arrived=Shore.RestoreSavedFooting(feet,rotation.eulerAngles.y);
            XRSeat.Apply();View.ResetInput();Shore.ResetMoveInput();Hands.ResetInteraction();XRNotebook.ResetInput();
            NavigationTarget=IslandNavigationIndex(island);Save();
            Notice(arrived?"已抵达"+FishingAdventure.Name(island)+"的秘境入口，按 A / E 进入。":"已到达岛屿泊位，落脚点不可用；随身物品和进度保留。",7);
            return arrived;
        }
        public bool ResetRealmsForDemo(int island)
        {
            if(island< -1 || island>=4 || !CanUseCreativeDemo())return false;
            if(Realms.Inside && !MoveToRealmEntrance(Realms.Island))return false;
            return CommitCreativeDemo(()=>
            {
                if(island>=0)return Career.Lore.ResetRealm(island);
                for(int i=0;i<4;i++)Career.Lore.ResetRealm(i);
                return true;
            },(island<0?"四个秘境":FishingLore.ShrineName(island))+"已还原机关、星图和回响贝壳。已领取奖励保留，重演不重复领取。");
        }
        public bool PrepareAnimalShowcase()
        {
            if(!CanUseCreativeDemo() || Realms.Inside){Notice("请在秘境外补齐养殖动物。",5);return false;}
            return CommitCreativeDemo(()=>Career.PrepareAnimalShowcase(),"五类住所已补齐成年动物，可回渔屋展示和饲喂。");
        }
        private bool CommitCreativeDemo(Func<bool> change,string message)
        {
            if(!CanSaveCheckpoint()){Notice("当前状态不能安全保存，请先结束正在进行的动作。",6);return false;}
            Save();if(!string.IsNullOrEmpty(SaveWarning))return false;
            var original=Career.Snapshot();
            if(!change()){Notice("演示修改未执行，请检查动物总数和容量。",6);return false;}
            Save();
            if(!string.IsNullOrEmpty(SaveWarning))
            {
                if(FishingCareer.TryRestore(original,out var restored))
                {
                    _savingReady=false;
                    try{ApplySavedCareer(restored,_safeResume);}finally{_savingReady=true;}
                }
                Notice("演示修改未保存，已恢复原航程；请检查存储空间与权限。",8);return false;
            }
            HomeEcology.Refresh();Notice(message,8);return true;
        }
    }
}
