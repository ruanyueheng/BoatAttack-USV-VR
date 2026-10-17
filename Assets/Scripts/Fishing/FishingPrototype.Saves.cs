using System;
using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public int ActiveSaveSlot { get; private set; } = -1;
        public bool CanContinue => _sessionResumable;
        public bool SaveMenuOpen { get; private set; }
        public string SaveMenuMode { get; private set; } = "load";
        public string SaveMenuMessage { get; private set; } = string.Empty;
        public string SaveConfirmation { get; private set; } = string.Empty;
        public string LastAutoSave { get; private set; } = string.Empty;
        private readonly FishingSlotInfo[,] _slotInfos = new FishingSlotInfo[FishingSlotStore.Count,2];
        private string _runId = Guid.NewGuid().ToString("N");
        private bool _sessionResumable, _savingReady;
        private float _autoClock;
        private FishingResumeData _safeResume = new FishingResumeData();
        private int _confirmSlot = -1;
        private FishingSlotKind _confirmKind;
        private string _confirmAction;

        private void InitializeSaveSlots(string legacyPath)
        {
            _sessionResumable = HasIsolatedProfile || !DesktopDisplay;
            _savingReady = true;
            ActiveSaveSlot = _sessionResumable ? 0 : -1;
            try
            {
                if(!HasIsolatedProfile && (File.Exists(SavePath) || File.Exists(SavePath+".bak") || File.Exists(legacyPath) || File.Exists(legacyPath+".bak")))
                    FishingSlotStore.ImportLegacy(SavePath,Career,File.Exists(SavePath) || File.Exists(SavePath+".bak")?SavePath:legacyPath);
                RefreshSaveSlots();
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
            { SaveWarning = "无法读取或迁移存档："+ex.Message; }
            RefreshSaveSlots();
            if(!HasIsolatedProfile && !DesktopDisplay)
            {
                var manual=SlotInfo(0,FishingSlotKind.Manual);var auto=SlotInfo(0,FishingSlotKind.Auto);
                var latest=auto.Valid && (!manual.Valid || string.CompareOrdinal(auto.Data.savedUtc,manual.Data.savedUtc)>=0)?auto:manual;
                if(latest.Valid && FishingCareer.TryRestore(latest.Data.career,out var restored))
                {
                    _savingReady=false;try{ApplySavedCareer(restored,latest.Data.resume);}finally{_savingReady=true;}
                    _runId=latest.Data.runId;
                }
                else if(manual.Exists || auto.Exists)
                {
                    ActiveSaveSlot=-1;SaveWarning="存档槽1已有但无法恢复；不会自动覆盖。当前进度仅在内存，请在桌面存档菜单恢复备份或明确选择新槽。";
                    Notice(SaveWarning,15);
                }
            }
            RememberSafeResume();
        }
        public FishingSlotInfo SlotInfo(int slot,FishingSlotKind kind) => _slotInfos[slot,(int)kind];
        public void RefreshSaveSlots()
        {
            for(int slot=0;slot<FishingSlotStore.Count;slot++)
                for(int kind=0;kind<2;kind++)_slotInfos[slot,kind]=FishingSlotStore.Read(SavePath,slot,(FishingSlotKind)kind);
        }
        public void OpenSaveMenu(string mode)
        {
            if((!StartMenuOpen && !Paused) || (mode!="load" && mode!="save" && mode!="new"))return;
            SaveSettings();SaveMenuMode=mode;SaveMenuOpen=true;MenuControls=false;
            SaveMenuMessage=mode=="save" && !CanContinue?"本次尚未开始航程。请先新建游戏或读取存档。":string.Empty;
            CancelSaveConfirmation();RefreshSaveSlots();
        }
        public void CloseSaveMenu(){SaveMenuOpen=false;CancelSaveConfirmation();SaveSettings();}
        public void CancelSaveConfirmation(){SaveConfirmation=string.Empty;_confirmSlot=-1;_confirmAction=null;}
        public void RequestSlotAction(int slot,string action,FishingSlotKind kind=FishingSlotKind.Manual)
        {
            if(slot<0 || slot>=FishingSlotStore.Count || (!StartMenuOpen && !Paused) || (action!="load" && action!="save" && action!="new"))return;
            var info=SlotInfo(slot,kind);
            if(action=="load" && !info.Valid){SaveMenuMessage=info.Exists?info.Message:"这是空存档，没有可读取的进度。";return;}
            if(action=="save" && kind!=FishingSlotKind.Manual)return;
            if(action=="save" && (!CanContinue || !CanSaveCheckpoint())){SaveMenuMessage="当前动作尚未结束。请先结束捕鱼、游泳、攀登或秘境交互，再手动保存。";return;}
            _confirmSlot=slot;_confirmKind=kind;_confirmAction=action;
            bool occupied=SlotInfo(slot,FishingSlotKind.Manual).Exists || SlotInfo(slot,FishingSlotKind.Auto).Exists;
            if(action=="new" && occupied)SaveConfirmation="在存档 "+(slot+1)+" 新建航程？原有手动与自动进度将被覆盖（保留备份）。";
            else if(action=="save" && occupied)SaveConfirmation="保存到存档 "+(slot+1)+"？已有手动记录将被覆盖；此槽后续自动记录由当前航程接管。其他槽不变（保留备份）。";
            else if(action=="load" && CanContinue)SaveConfirmation="读取磁盘存档将替换当前内存航程；未保存的进度将丢失。确定读取？";
            else if(action=="new" && CanContinue)SaveConfirmation="开始新航程将替换当前内存进度。确定新建？";
            else ConfirmSaveAction();
        }
        public void ConfirmSaveAction()
        {
            int slot=_confirmSlot;var kind=_confirmKind;string action=_confirmAction;
            CancelSaveConfirmation();if(slot<0 || action==null)return;
            if(action=="save")
            {
                var auto=SlotInfo(slot,FishingSlotKind.Auto);
                bool takeover=auto.Exists && (!auto.Valid || auto.Data.runId!=_runId);
                bool saved=false;
                if(takeover)
                {
                    try{RememberSafeResume();FishingSlotStore.WriteNewRun(SavePath,slot,Career,_safeResume,_runId);saved=true;}
                    catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                    {SaveMenuMessage="存档接管失败，原活动槽未改变："+ex.Message;}
                }
                else saved=WriteCheckpoint(slot,FishingSlotKind.Manual);
                if(saved)
                {
                    ActiveSaveSlot=slot;SaveMenuMessage=takeover?"已确认接管此槽，手动和自动记录均已写入当前航程。":"手动存档已保存；后续自动保存写入此槽的独立自动记录。";
                }
                RefreshSaveSlots();return;
            }
            FishingSlotData data;
            if(action=="new")
            {
                var fresh=new FishingCareer();
                data=new FishingSlotData { career=fresh.Snapshot(),resume=new FishingResumeData(),runId=Guid.NewGuid().ToString("N") };
            }
            else
            {
                var latest=FishingSlotStore.Read(SavePath,slot,kind);
                if(!latest.Valid){SaveMenuMessage=latest.Message;RefreshSaveSlots();return;}
                data=latest.Data;
            }
            if(!FishingCareer.TryRestore(data.career,out var career)){SaveMenuMessage="存档进度无效，当前航程未改变。";return;}
            if(action=="new")
            {
                try{FishingSlotStore.WriteNewRun(SavePath,slot,career,data.resume,data.runId);}
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                {SaveMenuMessage="新航程保存失败，原内存航程未改变："+ex.Message;RefreshSaveSlots();return;}
            }
            _savingReady=false;
            try{ApplySavedCareer(career,data.resume);}finally{_savingReady=true;}
            ActiveSaveSlot=slot;_runId=data.runId;_sessionResumable=true;_autoClock=0;
            SaveMenuOpen=false;MenuControls=false;
            if(action=="new")
            {
                if(StartMenuOpen)TitleVoyage.Sail();else if(Paused)TogglePause();
            }
            else
            {
                if(StartMenuOpen){TitleVoyage.Close();StartMenuOpen=false;}
                if(Paused)TogglePause();_titleStarted=true;ApplyAudioSettings();
                Notice("已读取存档 "+(slot+1)+" · "+(kind==FishingSlotKind.Auto?"自动":"手动")+"记录。未结束的动作从安全点重新开始。",7);
            }
            RefreshSaveSlots();
        }
        public void ContinueFromMenu()
        {
            if(!StartMenuOpen || TitleVoyage.Sailing)return;
            if(!CanContinue){SaveMenuMessage="没有本次可恢复的航程。请选择新建游戏或读取存档。";return;}
            SaveSettings();TitleVoyage.Close(true);StartMenuOpen=false;MenuControls=false;SaveMenuOpen=false;
            if(Paused)TogglePause();Notice("继续当前航程 · 场景和进度未重新加载。",4);
        }
        private bool CanSaveCheckpoint() => _initialized && !HasPendingCatch && !WorldActionBusy && !GuideOpening
            && !Prologue.Open && !Home.Sleeping && !Realms.Inside && !Realms.Transitioning
            && !Shore.Swimming && !Shore.Climbing
            && (Session.Phase==CatchPhase.Idle || Session.Phase==CatchPhase.Escaped || Session.Phase==CatchPhase.Caught);
        private void RememberSafeResume()
        {
            if(!CanSaveCheckpoint())return;
            var boat=Player.engine.RB.position;
            var data=new FishingResumeData { positioned=true,ashore=Ashore,firstPerson=FirstPerson,anchored=Anchored,
                boatX=boat.x,boatY=boat.y,boatZ=boat.z,boatYaw=Player.engine.RB.rotation.eulerAngles.y,
                navigation=NavigationTarget,chartRange=ChartRangeMeters,chartExpanded=ChartExpanded };
            if(Ashore)
            {
                if(!Shore.TrySaveFooting(out var feet))return;
                data.actorX=feet.x;data.actorY=feet.y;data.actorZ=feet.z;data.actorYaw=Shore.Actor.eulerAngles.y;
            }
            _safeResume=data;
        }
        private bool WriteCheckpoint(int slot,FishingSlotKind kind)
        {
            try
            {
                RememberSafeResume();
                var data=FishingSlotStore.Write(SavePath,slot,kind,Career,_safeResume,_runId);
                if(kind==FishingSlotKind.Auto)LastAutoSave=DateTime.Parse(data.savedUtc).ToLocalTime().ToString("HH:mm:ss");
                SaveWarning=string.Empty;return true;
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            { SaveWarning="存档失败：进度仍在内存中，请检查磁盘权限和可用空间。";SaveMenuMessage=SaveWarning;Notice(SaveWarning,10);Debug.LogWarning("[FISHING] Save failed: "+ex.Message);return false; }
        }
        private void TickAutosave()
        {
            if(!_savingReady || !CanContinue || Paused || (!Automated && !Application.isFocused))return;
            RememberSafeResume();_autoClock+=Time.deltaTime;
            if(_autoClock>=60){_autoClock=0;Save();}
        }
        private void SaveProgress()
        {
            if(!_initialized || !_savingReady || !CanContinue)return;
            // Explicit isolated acceptance keeps its historical single-file test contract.
            if(HasIsolatedProfile)
            {
                try{FishingSaveStore.Save(SavePath,Career);SaveWarning=string.Empty;}
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException)
                { SaveWarning="存档失败：进度仅保留在内存。";Notice(SaveWarning,10); }
            }
            if(ActiveSaveSlot>=0)WriteCheckpoint(ActiveSaveSlot,FishingSlotKind.Auto);
        }
        private void ApplySavedCareer(FishingCareer career,FishingResumeData resume)
        {
            CloseHomeChart();Prologue.Suspend();Home.CancelSleep();Letter.Close();Residents.Close();Realms.Abort();
            if(InventoryOpen)ToggleInventory();if(JournalOpen)ToggleJournal();
            CancelGuideOpening();ResetToolInput();CoastWorld.CloseMenu();
            if(Ashore)Shore.BoardImmediately();
            Anchored=false;Session.Cancel();CastMotion.Reset();CatchRig.ResetForLoad();
            Career=career;LastCatch=default;_previousPhase=CatchPhase.Idle;_netMotion=_netDeployment=0;
            Fleet.Refresh();Islands.Refresh();Shore.RefreshMarkers();FieldWorld.RefreshPickups();
            WorkshopWorld.RefreshFromCareer();WorkshopWorld.EnsureHomeStations();Expedition.RefreshFromCareer();
            CoastWorld.RefreshSavedProgress();
            RefreshHold();if(_trophy!=null){_trophy.SetActive(false);Destroy(_trophy);_trophy=null;}if(LegendTrophy)BuildTrophy();
            bool positioned=resume!=null && resume.positioned;
            Teleport(positioned?new Vector3(resume.boatX,resume.boatY,resume.boatZ):spawnPosition,
                positioned?Quaternion.Euler(0,resume.boatYaw,0):spawnRotation);
            UpdateNearestSpot();
            NavigationTarget=positioned?Mathf.Clamp(resume.navigation,0,NavigationCount-1):1;
            bool ashoreRestored=positioned && resume.ashore && Shore.RestoreSavedFooting(new Vector3(resume.actorX,resume.actorY,resume.actorZ),resume.actorYaw);
            if(positioned && resume.ashore && !ashoreRestored)Notice("原落脚点已不安全，已恢复到停泊船只；背包与任务进度保留。",8);
            ChartRangeMeters=positioned?resume.chartRange:160;_boatChartRange=ChartRangeMeters>=80?ChartRangeMeters:160;
            ChartExpanded=!positioned || resume.chartExpanded;HarborDeskOpen=ShowDetails=false;
            NavigationTarget=positioned?Mathf.Clamp(resume.navigation,0,NavigationCount-1):1;
            if(positioned && resume.anchored && !Ashore){Anchored=true;_anchor=Player.engine.RB.position;_anchorRotation=Player.engine.RB.rotation;}
            if(FirstPerson!=(resume==null || !positioned || resume.firstPerson))View.Toggle();
            View.ResetLook();_safeResume=positioned?resume:new FishingResumeData();RememberSafeResume();
        }
    }
}
