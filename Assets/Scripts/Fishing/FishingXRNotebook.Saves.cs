using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        private const int SavePage=30;
        private bool _saveSlotsActive,_savePriorPause;
        private FishingSlotKind _saveKind;
        private void OpenSaveSlots()
        {
            _savePriorPause=_c.Paused;_saveSlotsActive=true;_saveKind=FishingSlotKind.Manual;
            if(!_c.Paused)_c.TogglePause();
            _c.OpenSaveMenu("save");Open(SavePage);
        }
        private void CloseSaveSlots()
        {
            if(!_saveSlotsActive)return;
            _saveSlotsActive=false;_c.CloseSaveMenu();
            if(!_savePriorPause && _c.Paused)_c.TogglePause();
        }
        private void RefreshSaveSlotsPage()
        {
            _status.fontSize=20;
            _status.text="存档 · "+(_c.ActiveSaveSlot<0?"尚无活动槽":"当前槽 "+(_c.ActiveSaveSlot+1))+"\n手动记录和自动记录独立保存\n覆盖或切换航程前会要求确认\n"+_c.SaveMenuMessage;
            if(!string.IsNullOrEmpty(_c.SaveConfirmation))
            {
                _status.text=_c.SaveConfirmation;
                Set(0,"确认操作",()=>
                {
                    string action=_c.SaveMenuMode;_c.ConfirmSaveAction();
                    if(!_c.SaveMenuOpen){_saveSlotsActive=false;Close();if(action=="new")_c.Prologue.Begin();}
                    else Refresh();
                });
                Set(1,"取消操作",()=>{_c.CancelSaveConfirmation();Refresh();});
                Set(7,"返回设置",()=>Open(14));Set(8,"返回海湾",Close);return;
            }
            foreach(var mode in new[]{("load","读取进度",0),("save","手动保存",1),("new","新建航程",2)})
            {
                string value=mode.Item1;
                Set(mode.Item3,(_c.SaveMenuMode==value?"● ":"")+mode.Item2,()=>{_c.OpenSaveMenu(value);Refresh();});
            }
            for(int i=0;i<FishingSlotStore.Count;i++)
            {
                int slot=i;var info=_c.SlotInfo(slot,_c.SaveMenuMode=="load"?_saveKind:FishingSlotKind.Manual);
                string summary=info.Valid?"第 "+(info.Data.career.calendar.day+1)+" 天 · "+info.Data.career.coins+" 金币":info.Exists?"已有记录，读取时校验":"空存档";
                Set(i+3,"存档 "+(i+1)+(_c.ActiveSaveSlot==i?" · 当前":"")+"\n"+summary,()=>{_c.RequestSlotAction(slot,_c.SaveMenuMode,_c.SaveMenuMode=="load"?_saveKind:FishingSlotKind.Manual);Refresh();});
            }
            Set(6,"读取："+(_saveKind==FishingSlotKind.Manual?"手动记录":"自动记录"),()=>{_saveKind=_saveKind==FishingSlotKind.Manual?FishingSlotKind.Auto:FishingSlotKind.Manual;Refresh();});
            Set(7,"返回设置",()=>Open(14));Set(8,"返回海湾",Close);
        }
    }
}
