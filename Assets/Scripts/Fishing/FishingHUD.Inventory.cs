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
            var r=new Rect(320,790,958,64);Glass(r);
            Label(new Rect(r.x+16,r.y+9,r.width-32,r.height-18),text,17,Controller.SaveWarning.Length>0?Danger:Mint);
        }
        public static Rect BackpackPanel => new Rect(288,130,1024,646);
        public static Rect HotbarRect(float height)=>new Rect(486,height-82,628,68);
        private static Rect BagSlot(int slot)
        {
            int n=slot<9?slot:slot-9;
            return new Rect(320+(n%9)*66,slot<9?546:272+(n/9)*74,60,66);
        }
        private void DrawHotbar()
        {
            var c=Controller;var r=HotbarRect(_height);Glass(new Rect(r.x-8,r.y-29,r.width+16,r.height+36));
            Label(new Rect(r.x,r.y-25,r.width,22),"I 背包  ·  1–9 / 滚轮选择  ·  F 使用："+FishingBackpack.Name(c.Career.Backpack.Equipped),14,Muted,false,TextAnchor.MiddleCenter);
            for(int i=0;i<9;i++)
            {
                var slot=new Rect(r.x+7+i*69,r.y+2,62,62);
                DrawFieldSlot(slot,i,c.Career.Backpack.Selected==i,false);
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
            Label(545,698,365,45,confirming?"不可撤销；点击其他格取消确认。":"自然手册受保护；满包不吞拾取物。",13,confirming?Danger:Faint);
            Fill(933,235,1,495,Hairline);
            Label(958,239,300,30,"野外手册",23,Mint,true);
            string[] notes={"蹲下慢慢接近，留意跳跃的小耳朵。","奔跑会惊动赤狐；远距离使用望远镜。","在海岸内陆缓慢活动，不捕捉、不伤害。"};
            for(int i=0;i<3;i++)
            {
                bool seen=Controller.Career.Backpack.Seen((LandAnimalKind)i);float y=288+i*93;
                Rounded(new Rect(958,y,316,81),InkRaised,9);
                Label(972,y+9,286,25,(seen?"已记录 · ":"待观察 · ")+FishingBackpack.AnimalName((LandAnimalKind)i),17,seen?Mint:Muted,true);
                Label(972,y+40,286,34,notes[i],13,Faint);
            }
            Label(958,595,316,30,"工具使用",20,Gold,true);
            Label(958,635,312,92,"自然手册：F 记录 · 望远镜：F 举镜\n提灯：F 点亮 · 夜间照路\n猎矛：蹲下接近，F 刺击野兔 / 赤狐\n生兽肉 + 燃料 → 熔炉炭烤兽肉",14,Muted);
        }
        private void DrawFieldSlot(Rect r,int index,bool equipped,bool source)
        {
            var stack=Controller.Career.Backpack.At(index);
            Rounded(r,source?ButtonHover:InkRaised,7);Outline(r,source?Mint:equipped?Gold:Hairline,7,equipped||source?2:1);
            if(index<9)Label(r.x+4,r.y+2,18,16,(index+1).ToString(),11,equipped?Gold:Faint,true);
            if(stack.count>0)
            {
                FieldIcon(new Rect(r.x+19,r.y+8,27,30),stack.item);
                Label(new Rect(r.x+2,r.yMax-21,r.width-4,20),FishingBackpack.Name(stack.item),11,Color.white,false,TextAnchor.MiddleCenter);
                if(stack.count>1)Label(new Rect(r.x+30,r.y+23,r.width-33,20),stack.count.ToString(),13,Gold,true,TextAnchor.MiddleRight);
            }
        }
        private void FieldIcon(Rect r,FieldItem item)
        {
            float x=r.x,y=r.y,w=r.width,h=r.height;
            if(item==FieldItem.Binoculars)
            {
                Rounded(new Rect(x,y+4,w*.4f,h*.7f),Sky,4);Rounded(new Rect(x+w*.6f,y+4,w*.4f,h*.7f),Sky,4);
                Fill(x+w*.3f,y+h*.35f,w*.4f,5,Gold);Disc(new Vector2(x+w*.2f,y+h*.65f),4,Ink);Disc(new Vector2(x+w*.8f,y+h*.65f),4,Ink);
            }
            else if(item==FieldItem.Lantern)
            {
                Outline(new Rect(x+8,y,w-16,10),Gold,4,2);Rounded(new Rect(x+4,y+8,w-8,h-12),Gold,5);
                Fill(x+2,y+h-5,w-4,4,Muted);Fill(x+w*.5f-1,y+8,2,h-12,Ink);
            }
            else if(item==FieldItem.FieldGuide)
            {
                Rounded(new Rect(x+1,y+2,w-2,h-3),Mint,3);Fill(x+5,y+3,2,h-5,Ink);Fill(x+11,y+9,w-16,2,Ink);Fill(x+11,y+15,w-16,2,Ink);
            }
            else if(item==FieldItem.Shell)
            {
                Disc(new Vector2(x+w*.5f,y+h*.5f),w*.48f,Gold);for(int i=0;i<3;i++)Fill(x+7+i*6,y+8,2,15,InkRaised);
            }
            else if(item==FieldItem.Driftwood)
            {
                Rounded(new Rect(x,y+12,w,10),new Color(.68f,.46f,.25f),4);Fill(x+6,y+16,w-12,2,Gold);
            }            else if(item!=FieldItem.Empty)
            {
                bool metal=item==FieldItem.IronIngot || item==FieldItem.IronPickaxe;
                var color=metal?Sky:item==FieldItem.Fiber || item==FieldItem.Rope?Mint:item==FieldItem.Coal || item==FieldItem.Charcoal?Muted:Gold;
                Rounded(new Rect(x+2,y+4,w-4,h-7),color,3);
                string mark=item==FieldItem.Workbench?"台":item==FieldItem.Furnace?"炉":item==FieldItem.StonePickaxe || item==FieldItem.IronPickaxe?"镐":FishingBackpack.Name(item).Substring(0,1);
                Label(new Rect(x,y,w,h),mark,17,Ink,true,TextAnchor.MiddleCenter);
            }

        }
    }
}
