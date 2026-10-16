using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private readonly Dictionary<string,Texture2D> _artIcons=new Dictionary<string,Texture2D>();
        private int _loreIsland;
        private void ArtIllustration(Rect rect,string name)
        {
            if(!_artIcons.TryGetValue(name,out var image))_artIcons[name]=image=Resources.Load<Texture2D>((name.StartsWith("Resident")?"FishingV022":"FishingV019")+"/Thumbnails/"+name);
            if(image==null)return;
            var prior=GUI.color;GUI.color=Faded(Color.white);GUI.DrawTexture(rect,image,ScaleMode.ScaleToFit,true);GUI.color=prior;
        }
        private void ItemIllustration(Rect rect,FieldItem item)
        {
            string art=item==FieldItem.Binoculars?"Binoculars":item==FieldItem.Lantern?"Lantern":item==FieldItem.Rope?"RopeCoil"
                :item==FieldItem.CookedMeat?"whole-ham":item==FieldItem.Glass?"bottle":item==FieldItem.Furnace?"pot":item==FieldItem.Workbench?"chest"
                :item==FieldItem.Plank||item==FieldItem.Stick?"tool-paddle":item==FieldItem.Fiber?"plant_bush":item==FieldItem.Stone?"stone_tallC"
                :item==FieldItem.Coal||item==FieldItem.Charcoal?"campfire_logs":item==FieldItem.Sand?"bag":null;
            if(art!=null)ArtIllustration(rect,art);else FieldIcon(new Rect(rect.x+16,rect.y+14,rect.width-32,rect.height-28),item);
        }
        internal bool RegionTitleVisible=>Controller.Regions!=null && Controller.Regions.Alpha>.001f && !Controller.Realms.Transitioning && !CatchVisible;
        private void DrawRegionTitle()
        {
            var region=Controller.Regions;if(!RegionTitleVisible)return;
            float previous=_fade;_fade=region.Alpha;
            float y=Mathf.Max(174,_height*.16f)+(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(region.Age/1.1f)))*8;
            Rounded(new Rect(600,y-8,400,90),Ink,12);Outline(new Rect(600,y-8,400,90),Hairline,12);
            Label(new Rect(616,y+2,368,44),region.Title,30,Color.white,true,TextAnchor.MiddleCenter);
            Label(new Rect(616,y+52,368,26),region.Subtitle,13,Muted,false,TextAnchor.UpperCenter);
            _fade=previous;
        }
        private void DrawDialogue()
        {
            var c=Controller;var resident=c.Residents;int id=resident.ActiveId;
            Fill(-_offset/_scale,0,Screen.width/_scale,_height,new Color(.01f,.02f,.035f,.28f));
            var r=new Rect(240,_height-396,1120,352);Card(r,Gold);
            if(id!=4)ArtIllustration(new Rect(r.x+28,r.y+26,74,88),"Resident"+id);
            else ArtIllustration(new Rect(r.x+30,r.y+25,70,80),"Lantern");
            Label(r.x+120,r.y+22,510,38,FishingLore.ResidentName(id),27,Gold,true);
            Label(r.x+122,r.y+64,590,23,FishingLore.ResidentRole(id)+"  /  "+resident.TopicTitle,14,Muted);
            Fill(r.x+30,r.y+115,716,1,Hairline);
            int speechSize=21;_label.font=_font;_label.fontSize=speechSize;
            while(speechSize>15 && _label.CalcHeight(new GUIContent(resident.Speech),712)>176)_label.fontSize=--speechSize;
            Label(r.x+34,r.y+136,712,176,resident.VisibleSpeech,speechSize,Color.white);
            for(int i=0;i<resident.ChoiceCount;i++)if(Button(new Rect(r.x+778,r.y+24+i*53,310,47),(i+1)+"  "+resident.Choice(i),true,false,16))resident.Choose(i);
            if(id==4 && Button(new Rect(r.x+778,r.y+240,310,34),"查看商品",true,true,16))resident.BrowseCashierGoods();
            if(Button(new Rect(r.x+778,r.y+278,310,42),"Esc  结束交谈",true,false,16))resident.Close();
            if(resident.VisibleSpeech.Length<resident.Speech.Length)
            {if(Button(new Rect(r.x+34,r.y+314,230,24),"E 显示完整对话",true,false,13))resident.Reveal();}
            else Label(r.x+34,r.y+320,640,20,(resident.ChoiceCount==4?"1 / 2 / 3 / 4":"1 / 2 / 3")+" 选择话题 · J 日志记录线索",13,Faint);
        }
        private void DrawExplorationStatus()
        {
            var c=Controller;if(!c.Ashore)return;
            var r=new Rect(1110,_height-171,165,74);Glass(r);
            Label(r.x+12,r.y+9,141,20,c.Shore.MovementName+" · 体力",14,Muted);
            Bar(new Rect(r.x+12,r.y+40,141,5),c.Shore.Stamina,c.Shore.Stamina<.2f?Gold:Mint);
            if(!c.Realms.Inside)return;
            var goal=new Rect(1188,126,388,190);Card(goal,Gold);
            Label(goal.x+18,goal.y+18,352,28,FishingLore.ShrineName(c.Realms.Island),21,Gold,true);
            Label(goal.x+18,goal.y+60,352,76,FishingLore.TrialObjective(c.Realms.Island),17,Muted);
            Label(goal.x+18,goal.y+148,352,27,c.Career.Lore.Cleared(c.Realms.Island)?"回响已收录 · 可自由重访":c.Realms.Trial.Ready?"共鸣已成 · 前往回响台":"解开机关 · 回响之门尚未打开",15,Mint);
        }
        private void DrawLorePage(Rect r)
        {
            var c=Controller;var lore=c.Career.Lore;
            string[] illustrations={"WindBell","tree_oak","ship-wreck","StarPrism"};
            for(int i=0;i<4;i++)
            {
                var row=new Rect(r.x+32,r.y+118+i*110,820,98);Rounded(row,InkRaised,10);
                Outline(row,_loreIsland==i?Gold*new Color(1,1,1,.55f):Hairline,10);
                ArtIllustration(new Rect(row.x+12,row.y+8,90,82),illustrations[i]);
                Label(row.x+116,row.y+12,500,27,FishingAdventure.Name(i)+" · "+FishingLore.ShrineName(i),20,Gold,true);
                Label(row.x+116,row.y+45,500,24,FishingLore.ResidentName(i)+" · "+FishingLore.ResidentRole(i),15,Muted);
                Label(row.x+116,row.y+73,500,20,lore.Cleared(i)?"回响已取回 · 首通奖励已领取":lore.Met(i)?"已结识居民 · E进入潮纹石门":"先结识岛上居民，听听当地的故事",13,lore.Cleared(i)?Mint:Faint);
                if(Button(new Rect(row.xMax-148,row.y+12,130,30),"阅读故事",c.Letter.CanRead,false,14))
                {_loreIsland=i;c.ToggleJournal();c.Letter.Show(FishingLore.ShrineName(i)+"航记",FishingLore.ResidentName(i),"回潮海档案：\n\n"+(lore.Cleared(i)?FishingLore.Echo(i):FishingLore.TrialHint(i))+"\n\n风、林、潮与星，仍在等待新的领航员。");}
                if(Button(new Rect(row.xMax-148,row.y+53,130,30),"设为航点",true,false,14)){c.ToggleJournal();c.SelectNavigation(c.IslandNavigationIndex(i));}
            }
            var side=new Rect(r.x+884,r.y+118,420,462);Rounded(side,InkRaised,10);
            Label(side.x+22,side.y+18,376,32,"回潮海 · 守望者航路",23,Gold,true);
            Label(side.x+22,side.y+64,376,110,"风、林、潮、星，曾将四座岛的灯火连成回家的路。风暴让秘境沉寂，新任领航员将取回四段潮汐回响。",17,Muted);
            Fill(side.x+22,side.y+186,376,1,Hairline);
            Label(side.x+22,side.y+204,376,92,lore.Cleared(_loreIsland)?FishingLore.Echo(_loreIsland):FishingLore.TrialHint(_loreIsland),17,lore.Cleared(_loreIsland)?Mint:Color.white);
            Label(side.x+22,side.y+322,376,28,lore.Finale?"航路重生 · 终章已完成":"回响收集  "+lore.ClearCount+" / 4",20,Gold,true);
            Bar(new Rect(side.x+22,side.y+366,376,6),lore.ClearCount/4f,Mint);
            Label(side.x+22,side.y+392,376,50,lore.ClearCount==4&&!lore.Finale?"回港找钓具店门前的林舟，汇报回响领取180金币。":"每处首通90金币；四处完成后，找港口林舟领取180金币。",15,Muted);
        }
        private void DrawRealmChart()
        {
            var c=Controller;var r=Layout.Chart;Card(r,Gold);
            Label(r.x+16,r.y+12,r.width-32,26,"秘境导览",18,Gold,true);
            var map=new Rect(r.x+36,r.y+51,176,177);Outline(map,Hairline,4);
            Vector2 Point(Vector3 world){var p=c.Realms.Room.InverseTransformPoint(world);return new Vector2(map.center.x+p.x*7.8f,map.center.y-p.z*5.5f);}
            for(int i=0;i<5;i++)
            {
                var device=c.Realms.Device(i);if(device==null)continue;var p=Point(device.position);
                Disc(p,i==3?7:5,i==4?Sky:i==3?Gold:Mint);
                Label(new Rect(p.x-22,p.y-28,44,20),i==4?"入口":i==3?"回响":(i+1).ToString(),12,Muted,false,TextAnchor.MiddleCenter);
            }
            Disc(Point(c.Shore.Position),4,Color.white);
            Label(r.x+14,r.yMax-39,r.width-28,26,"白点：当前位置 · 青点：机关",12,Muted);
        }
    }
}
