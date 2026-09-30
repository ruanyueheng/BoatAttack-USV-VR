using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _moveFrom=-1;
        public void ClearFieldSelection()=>_moveFrom=-1;
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
            Label(320,156,670,45,"随身行囊",32,Color.white,true);
            Label(322,206,590,24,"世界已暂停  ·  "+Controller.Career.Backpack.UsedSlots+" / 36 格  ·  物资与鱼箱独立保存",15,Muted);
            if(Button(new Rect(1154,160,124,38),"I / Esc 关闭",true,false,14)){Controller.ToggleInventory();_moveFrom=-1;return;}
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
                    if(e.shift){Controller.MoveFieldItem(i,0,false,true);_moveFrom=-1;}
                    else if(_moveFrom<0){if(Controller.Career.Backpack.At(i).count>0)_moveFrom=i;}
                    else if(_moveFrom==i)_moveFrom=-1;
                    else{Controller.MoveFieldItem(_moveFrom,i,half);_moveFrom=-1;}
                    e.Use();
                }
            }
            Label(320,634,586,50,_moveFrom<0?"点击物品，再点击目标格：移动 / 合并 / 交换\nShift + 点击：快捷栏与储物区之间快速转移":"已选中 "+FishingBackpack.Name(Controller.Career.Backpack.At(_moveFrom).item)+"：点击目标格转移；右键目标格分出一半。",15,Muted);
            Label(320,707,586,28,"不丢弃物品 · 满包拾取不会消失 · 鱼竿与抄网仍在船上",13,Faint);
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
            Label(958,635,312,85,"自然手册：F 记录看向的动物\n望远镜：F 放大 / 放下，T 记录\n提灯：F 点亮 / 熄灭，照亮附近",15,Muted);
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
            }
        }
    }
}
