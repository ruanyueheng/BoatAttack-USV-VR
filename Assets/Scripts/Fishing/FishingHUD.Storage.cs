using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private bool _homeStorageOpen;
        public void OpenHomeStorage()=>_homeStorageOpen=true;
        private void DrawHomeStorage()
        {
            Dim();var panel=new Rect(200,72,1200,708);ThemedPaper(panel,FishingPaperTheme.Workshop);
            Label(238,109,760,54,"家园储物箱",38,InkText);
            if(Button(new Rect(1194,115,160,38),"I / Esc 关闭",true,false,16)){Controller.ToggleInventory();return;}
            Label(238,167,530,30,"随身行囊 · "+Controller.Career.Backpack.UsedSlots+" / 36 格 · 点击存入",19,InkText,true);
            Label(844,167,510,30,"家园收藏 · "+Controller.Career.HomeStorage.UsedSlots+" / 36 格 · 点击取出",19,InkText,true);
            Fill(800,169,1,509,Hairline);
            DrawStorageColumn(238,true);DrawStorageColumn(844,false);
            QuietRule(238,702,1116);
            Label(new Rect(247,723,1100,33),"点击有物品的格子，整组存入或取出；空间不足时保留原物品。两边物品都会随存档保存。",16,Muted,false,TextAnchor.MiddleCenter);
        }
        private void DrawStorageColumn(float x,bool deposit)
        {
            var bag=deposit?Controller.Career.Backpack:Controller.Career.HomeStorage;
            for(int slot=0;slot<FishingBackpack.SlotCount;slot++)
            {
                var item=bag.At(slot);var r=new Rect(x+slot%6*86,214+slot/6*78,78,70);
                bool hover=item.count>0 && r.Contains(Event.current.mousePosition);
                Rounded(r,hover?FishingUITheme.Selected:InkRaised,5);Outline(r,hover?Gold:Hairline,5);
                if(item.count>0)
                {
                    FieldIcon(new Rect(r.x+15,r.y+3,48,44),item.item);
                    Label(new Rect(r.x+2,r.y+48,r.width-4,21),FishingBackpack.Name(item.item),11,InkText,false,TextAnchor.MiddleCenter);
                    if(item.count>1)Label(new Rect(r.x+2,r.y+5,r.width-6,20),item.count.ToString(),12,Gold,true,TextAnchor.MiddleRight);
                }
                if(GUI.Button(r,GUIContent.none,GUIStyle.none) && item.count>0)Controller.Home.TransferStorage(deposit,slot);
            }
        }
    }
}
