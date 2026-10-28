using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _settingDrag;
        public static float SettingValueAt(float pointerX,float trackX,float trackWidth,float min,float max)
            =>Mathf.Lerp(min,max,Mathf.Clamp01((pointerX-trackX)/Mathf.Max(1,trackWidth)));
        private float InteractiveSettingValue(Rect track,string key,float value,float min,float max)
        {
            int id=GUIUtility.GetControlID(key.GetHashCode(),FocusType.Passive,track);
            var e=Event.current;var hit=new Rect(track.x-9,track.y-12,track.width+18,42);
            if(e.type==EventType.MouseDown && e.button==0 && hit.Contains(e.mousePosition))
            {GUIUtility.hotControl=id;_settingDrag=id;e.Use();return SettingValueAt(e.mousePosition.x,track.x,track.width,min,max);}
            if(_settingDrag==id && GUIUtility.hotControl==id)
            {
                if(e.type==EventType.MouseDrag){e.Use();return SettingValueAt(e.mousePosition.x,track.x,track.width,min,max);}
                if(e.type==EventType.MouseUp){GUIUtility.hotControl=0;_settingDrag=0;Controller.SaveSettings();e.Use();}
            }
            return value;
        }
        private void DrawSaveSlots()
        {
            var c=Controller;Dim();
            var r=new Rect(180,(_height-730)*.5f,1240,730);
            StarrySea(r);Panel(new Rect(r.x-14,r.y-6,r.width+28,r.height+12),FishingUITheme.PanelVeil,ref _veilStyle,Color.white);
            string heading=c.SaveMenuMode=="save"?"手动保存":c.SaveMenuMode=="new"?"选择新航程存档槽":"读取存档";
            DisplayText(new Rect(r.x+30,r.y+20,1100,50),Spaced(heading),34,CreamInk,true);
            DisplayText(new Rect(r.x+30,r.y+82,1160,72),"三个存档槽 · 每槽的手动记录与自动记录独立。自动保存不会覆盖手动记录。\n读取会替换当前航程；“继续游戏”只恢复本次内存进度。",18,CreamInk);
            bool modal=c.SaveConfirmation.Length>0;bool enabled=GUI.enabled;GUI.enabled=!modal;
            for(int slot=0;slot<FishingSlotStore.Count;slot++)
            {
                float y=r.y+170+slot*136;var card=new Rect(r.x+28,y,r.width-56,122);
                Rounded(card,new Color(.08f,.13f,.16f,.9f),7);
                DisplayText(new Rect(card.x+16,y+12,150,35),"存档 "+(slot+1)+(c.ActiveSaveSlot==slot?" · 当前":""),22,AmberInk,true);
                for(int kind=0;kind<2;kind++)
                {
                    var info=c.SlotInfo(slot,(FishingSlotKind)kind);float row=y+18+kind*46;
                    DisplayText(new Rect(card.x+190,row,760,38),(kind==0?"手动  ·  ":"自动  ·  ")+info.Summary,17,info.Valid?CreamInk:new Color(.74f,.77f,.77f));
                    if(c.SaveMenuMode=="load" && SeaButton(new Rect(card.xMax-144,row-3,125,38),"读取 →",false,info.Valid,20))c.RequestSlotAction(slot,"load",(FishingSlotKind)kind);
                }
                if(c.SaveMenuMode!="load" && SeaButton(new Rect(card.xMax-144,y+44,125,48),c.SaveMenuMode=="new"?"新建 →":"保存 →",true,c.SaveMenuMode=="new" || c.CanContinue,20))c.RequestSlotAction(slot,c.SaveMenuMode);
            }
            GUI.enabled=enabled;
            DisplayText(new Rect(r.x+30,r.y+588,1160,68),c.SaveMenuMessage.Length>0?c.SaveMenuMessage:"自动保存：交易、鱼获结算、任务/探索、休息与制作，以及每60秒、暂停/返回菜单、失焦/退出。\n游泳、攀登、秘境或动作中保留已获得进度，读档回最近安全点；未完成的动作不续接。",16,AmberInk);
            if(SeaButton(new Rect(r.x+30,r.yMax-58,180,42),"← 返回",false,!modal,22))c.CloseSaveMenu();
            if(modal)
            {
                Fill(r.x,r.y,r.width,r.height,new Color(.01f,.02f,.03f,.76f));
                var confirm=new Rect(390,(_height-250)*.5f,820,250);Rounded(confirm,new Color(.08f,.13f,.17f,1),10);Outline(confirm,AmberInk,10);
                DisplayText(new Rect(confirm.x+28,confirm.y+28,764,112),c.SaveConfirmation,24,CreamInk,true);
                if(SeaButton(new Rect(confirm.x+30,confirm.yMax-68,330,48),"确认",true,true,23))c.ConfirmSaveAction();
                if(SeaButton(new Rect(confirm.x+435,confirm.yMax-68,330,48),"取消",false,true,23))c.CancelSaveConfirmation();
            }
        }
    }
}
