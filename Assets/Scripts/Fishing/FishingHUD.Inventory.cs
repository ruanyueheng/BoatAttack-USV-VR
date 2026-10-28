using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _moveFrom=-1,_discardSlot=-1,_discardCount;
        private FieldItem _discardItem;
        public void ClearFieldSelection(){_moveFrom=-1;_discardSlot=-1;_homeStorageOpen=false;_ecologyPage=false;}
        private void DrawInventoryNotice()
        {
            string text=Controller.SaveWarning.Length>0?Controller.SaveWarning:Controller.VisibleMessage;
            if(string.IsNullOrEmpty(text))return;
            var r=new Rect(320,790,958,64);
            WorldLabel(new Rect(r.x+16,r.y+9,r.width-32,r.height-18),text,17,Controller.SaveWarning.Length>0?Danger:Mint);
        }
        public static Rect BackpackPanel => new Rect(288,130,1024,646);
        public static Rect HotbarRect(float height)=>new Rect(556,height-70,488,52);
        private static Rect BagSlot(int slot)
        {
            int n=slot<9?slot:slot-9;
            return new Rect(320+(n%9)*66,slot<9?546:272+(n/9)*74,60,66);
        }
        private void DrawHotbar()
        {
            var c=Controller;var r=HotbarRect(_height);
            WorldLabel(new Rect(r.x,r.y-23,r.width,20),"I 背包 · "+FishingBackpack.Name(c.Career.Backpack.Equipped)+" · F 使用 · Z 检视",13,Muted,false,TextAnchor.MiddleCenter);
            for(int i=0;i<9;i++)
            {
                var stack=c.Career.Backpack.At(i);bool selected=c.Career.Backpack.Selected==i;
                var slot=selected?new Rect(r.x+i*54,r.y-4,56,56):new Rect(r.x+4+i*54,r.y+2,48,48);
                bool filled=stack.count>0;
                Rounded(new Rect(slot.x,slot.y+2,slot.width,slot.height),new Color(.01f,.03f,.06f,.30f),10);
                Rounded(slot,filled?(selected?new Color(.98f,.93f,.80f,.96f):new Color(.95f,.91f,.82f,.88f)):new Color(.06f,.11f,.19f,selected?.72f:.48f),10);
                Outline(slot,selected?AmberInk:AmberInk*new Color(1,1,1,filled?.75f:.38f),10,selected?2.5f:1f);
                if(stack.count>0)
                {
                    FieldIcon(new Rect(slot.x+slot.width*.14f,slot.y+slot.height*.12f,slot.width*.72f,slot.height*.72f),stack.item);
                    if(stack.count>1)DisplayText(new Rect(slot.xMax-27,slot.yMax-21,24,20),Number(stack.count),13,SeaInk,false,TextAnchor.MiddleRight);
                }
                DisplayText(new Rect(slot.x+5,slot.y+2,14,16),Number(i+1),11,stack.count>0?new Color(.42f,.32f,.18f):new Color(.86f,.80f,.66f,.75f),false,TextAnchor.UpperLeft);
                if(selected)GiltStar(new Vector2(slot.center.x,slot.y-2),6,AmberInk);
                if(GUI.Button(slot,GUIContent.none,GUIStyle.none))c.SelectFieldSlot(i);
            }
        }
        private void DrawInventory()
        {
            if(_homeStorageOpen){DrawHomeStorage();return;}
            if(_ecologyPage){DrawHomeEcology();return;}
            var panel=BackpackPanel;MenuSheet(panel);
            Label(320,150,420,46,Spaced(_craftPage?"海岸工坊":"随身行囊"),32,Color.white,true);GiltRule(320,200,240);
            if(Button(new Rect(807,163,145,34),"背包 / 手册",true,!_craftPage,15))_craftPage=false;
            if(Button(new Rect(965,163,145,34),"制作 / 熔炼",true,_craftPage,15)){_craftPage=true;Controller.WorkshopWorld.SelectNearby();}
            Label(322,206,590,26,"世界已暂停  ·  "+Controller.Career.Backpack.UsedSlots+" / 36 格  ·  物资与鱼箱独立保存",17,Muted);
            if(Button(new Rect(1154,160,124,38),"I / Esc 关闭",true,false,14)){Controller.ToggleInventory();_moveFrom=-1;return;}
            if(_craftPage){DrawCraftingContents();return;}
            Section(320,238,588,"储物区 · 27 格","材料最多 64 / 格，工具不堆叠");
            Section(320,510,588,"快捷栏 · 9 格","数字键选择，下方金边为当前物品");
            for(int i=0;i<36;i++)
            {
                var r=BagSlot(i);DrawFieldSlot(r,i,i==Controller.Career.Backpack.Selected,i==_moveFrom);
                var e=Event.current;
                bool half=e.button==1;
                // Event coordinates are transformed by IMGUI along with GUI.matrix.
                if(e.type==EventType.MouseDown && (e.button==0 || e.button==1) && r.Contains(e.mousePosition))
                {
                    _discardSlot=-1;
                    if(e.shift){Controller.MoveFieldItem(i,0,false,true);_moveFrom=-1;}
                    else if(_moveFrom<0){if(Controller.Career.Backpack.At(i).count>0)_moveFrom=i;}
                    else if(_moveFrom==i)_moveFrom=-1;
                    else{Controller.MoveFieldItem(_moveFrom,i,half);_moveFrom=-1;}
                    e.Use();
                }
            }
            Label(320,630,586,56,_moveFrom<0?"点击物品，再点击目标格：移动 / 合并 / 交换\nShift + 点击：快捷栏与储物区之间快速转移":"已选中 "+FishingBackpack.Name(Controller.Career.Backpack.At(_moveFrom).item)+"：点击目标格转移；右键目标格分出一半。",17,Muted);
            var selected=Controller.Career.Backpack.At(_moveFrom);
            bool disposable=selected.count>0 && selected.item!=FieldItem.FieldGuide && !FishingBackpack.IsQuestItem(selected.item);
            bool confirming=_discardSlot==_moveFrom && _discardItem==selected.item && _discardCount==selected.count;
            if(Button(new Rect(320,697,210,35),confirming?"确认永久销毁 ×"+selected.count:"销毁选中物品…",disposable,false,14))
            {
                if(confirming){Controller.DiscardFieldItem(_moveFrom,selected.item,selected.count);ClearFieldSelection();}
                else{_discardSlot=_moveFrom;_discardItem=selected.item;_discardCount=selected.count;}
            }
            if(Button(new Rect(545,697,190,35),"拿起检视",selected.count>0,false,14)){int slot=_moveFrom;ClearFieldSelection();Controller.InspectFieldItem(slot);return;}
            Label(747,696,180,48,FishingBackpack.IsQuestItem(selected.item)?"航海遗物 · 可存入家园箱":confirming?"不可撤销":"Z 检视快捷栏物品",15,confirming?Danger:Faint);
            Fill(946,235,1,495,Hairline);
            Label(984,239,290,30,"野外手册",24,Mint,true);
            string[] notes={"野兔掉落兔肉和兔毛。","赤狐掉落狐狸肉和狐皮。","龟类掉落海龟肉和龟壳；海鸥掉落肉和羽毛。"};
            for(int i=0;i<3;i++)
            {
                bool seen=Controller.Career.Backpack.Seen((LandAnimalKind)i);float y=288+i*93;
                Rounded(new Rect(984,y,290,81),InkRaised,9);
                Label(998,y+8,264,26,(seen?"已记录 · ":"待观察 · ")+FishingBackpack.AnimalName((LandAnimalKind)i),18,seen?Mint:Muted,true);
                Label(998,y+38,264,40,notes[i],15,Faint);
            }
            if(Button(new Rect(984,735,290,31),"家园生态 · 鱼缸 / 活捉 / 繁殖",true,false,15)){OpenHomeEcologyPage();return;}
            Label(984,592,290,30,"工具使用",21,Gold,true);
            Label(984,628,290,104,"手册 F 翻开 · T 观察 · 望远镜 F\n提灯 F 点亮 · 夜间照路\n猎矛 蹲下接近，F 刺击\n生兽肉 + 燃料 → 熔炉炭烤",15,Muted);
        }
        private void DrawFieldSlot(Rect r,int index,bool equipped,bool source)
        {
            var stack=Controller.Career.Backpack.At(index);
            // 空格是纸面上的浅凹槽，有物品才抬起成亮卡，避免一片白格。
            bool filled=stack.count>0;var inset=FishingUITheme.Blend(new Color(.55f,.45f,.30f,.12f),new Color(0,0,0,.25f));
            Rounded(r,source?ButtonHover:filled?InkRaised:inset,8);Outline(r,source?Mint:equipped?Gold:filled?Hairline:Hairline*new Color(1,1,1,.6f),8,equipped||source?2:1);
            if(stack.count>0)
            {
                FieldIcon(new Rect(r.x+3,r.y+5,r.width-6,r.height-24),stack.item);
                Label(new Rect(r.x+2,r.yMax-21,r.width-4,20),FishingBackpack.Name(stack.item),11,Color.white,false,TextAnchor.MiddleCenter);
                if(stack.count>1){Rounded(new Rect(r.xMax-23,r.y+18,21,18),InkRaised,3);Label(new Rect(r.x+2,r.y+18,r.width-5,20),stack.count.ToString(),12,Gold,true,TextAnchor.MiddleRight);}

            }
            if(index<9)Label(r.x+4,r.y+2,18,16,(index+1).ToString(),11,equipped?Gold:Faint,true);
        }
        private void FieldIcon(Rect r,FieldItem item)
        {
            var image=FishingV023Art.Icon(item) ?? FishingItemIcons.Get(item);if(image==null)return;
            var previous=GUI.color;GUI.color=Faded(Color.white);
            GUI.DrawTexture(r,image,ScaleMode.ScaleToFit,true);GUI.color=previous;

        }
    }
}
