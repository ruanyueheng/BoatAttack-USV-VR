using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _moveFrom=-1,_discardSlot=-1,_discardCount;
        private FieldItem _discardItem;
        public void ClearFieldSelection(){_moveFrom=-1;_discardSlot=-1;}
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
                var slot=new Rect(r.x+4+i*54,r.y+2,48,48);
                var stack=c.Career.Backpack.At(i);bool selected=c.Career.Backpack.Selected==i;
                if(stack.count>0)
                {
                    FieldIcon(new Rect(slot.x+5,slot.y+1,38,36),stack.item);
                    if(stack.count>1)DisplayText(new Rect(slot.x+23,slot.y+23,24,20),stack.count.ToString(),13,CreamInk,false,TextAnchor.MiddleRight);
                }
                DisplayText(new Rect(slot.x,slot.yMax-16,slot.width,18),(i+1).ToString(),12,selected?AmberInk:CreamInk,false,TextAnchor.MiddleCenter);
                if(selected)Fill(slot.x+9,slot.yMax+3,30,2,AmberInk);
                if(GUI.Button(slot,GUIContent.none,GUIStyle.none))c.SelectFieldSlot(i);
            }
        }
        private void DrawInventory()
        {
            Fill(-_offset/_scale,0,Screen.width/_scale,_height,new Color(.01f,.025f,.03f,.78f));
            var panel=BackpackPanel;Card(panel,Mint);
            Label(320,156,340,45,_craftPage?"海岸工坊":"随身行囊",32,Color.white,true);
            if(Button(new Rect(807,163,145,34),"背包 / 手册",true,!_craftPage,15))_craftPage=false;
            if(Button(new Rect(965,163,145,34),"制作 / 熔炼",true,_craftPage,15)){_craftPage=true;Controller.WorkshopWorld.SelectNearby();}
            Label(322,206,590,24,"世界已暂停  ·  "+Controller.Career.Backpack.UsedSlots+" / 36 格  ·  物资与鱼箱独立保存",15,Muted);
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
            Label(320,634,586,50,_moveFrom<0?"点击物品，再点击目标格：移动 / 合并 / 交换\nShift + 点击：快捷栏与储物区之间快速转移":"已选中 "+FishingBackpack.Name(Controller.Career.Backpack.At(_moveFrom).item)+"：点击目标格转移；右键目标格分出一半。",15,Muted);
            var selected=Controller.Career.Backpack.At(_moveFrom);
            bool disposable=selected.count>0 && selected.item!=FieldItem.FieldGuide;
            bool confirming=_discardSlot==_moveFrom && _discardItem==selected.item && _discardCount==selected.count;
            if(Button(new Rect(320,697,210,35),confirming?"确认永久销毁 ×"+selected.count:"销毁选中物品…",disposable,false,14))
            {
                if(confirming){Controller.DiscardFieldItem(_moveFrom,selected.item,selected.count);ClearFieldSelection();}
                else{_discardSlot=_moveFrom;_discardItem=selected.item;_discardCount=selected.count;}
            }
            if(Button(new Rect(545,697,190,35),"拿起检视",selected.count>0,false,14)){int slot=_moveFrom;ClearFieldSelection();Controller.InspectFieldItem(slot);return;}
            Label(747,698,164,45,confirming?"不可撤销":"Z 检视快捷栏物品",13,confirming?Danger:Faint);
            Fill(933,235,1,495,Hairline);
            Label(958,239,300,30,"野外手册",23,Mint,true);
            string[] notes={"野兔掉落兔肉和兔毛。","赤狐掉落狐狸肉和狐皮。","龟类掉落海龟肉和龟壳；海鸥掉落肉和羽毛。"};
            for(int i=0;i<3;i++)
            {
                bool seen=Controller.Career.Backpack.Seen((LandAnimalKind)i);float y=288+i*93;
                Rounded(new Rect(958,y,316,81),InkRaised,9);
                Label(972,y+9,286,25,(seen?"已记录 · ":"待观察 · ")+FishingBackpack.AnimalName((LandAnimalKind)i),17,seen?Mint:Muted,true);
                Label(972,y+40,286,34,notes[i],13,Faint);
            }
            Label(958,595,316,30,"工具使用",20,Gold,true);
            Label(958,635,312,92,"自然手册：F 翻开 · T 观察 · 望远镜：F 举镜\n提灯：F 点亮 · 夜间照路\n猎矛：蹲下接近，F 刺击野兔 / 赤狐\n生兽肉 + 燃料 → 熔炉炭烤兽肉",14,Muted);
        }
        private void DrawFieldSlot(Rect r,int index,bool equipped,bool source)
        {
            var stack=Controller.Career.Backpack.At(index);
            Rounded(r,source?ButtonHover:InkRaised,7);Outline(r,source?Mint:equipped?Gold:Hairline,7,equipped||source?2:1);
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
