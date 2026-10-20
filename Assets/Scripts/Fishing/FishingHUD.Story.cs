using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private readonly Dictionary<string,Texture2D> _artIcons=new Dictionary<string,Texture2D>();
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
            ReadingVeil(new Rect(542,y-20,516,154),true);
            WorldLabel(new Rect(600,y-12,400,62),region.Title,40,CreamInk,true,TextAnchor.MiddleCenter,onPaper:true);
            GiltRule(700,y+46,200);
            WorldLabel(new Rect(580,y+60,440,60),region.Subtitle.Replace("，","\n").TrimEnd('。'),16,CreamInk,false,TextAnchor.UpperCenter,onPaper:true);
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
            var r=new Rect(1110,_height-171,165,74);
            WorldLabel(r.x+12,r.y+9,141,20,c.Shore.MovementName+" · 体力",14,CreamInk);
            Bar(new Rect(r.x+12,r.y+40,141,5),c.Shore.Stamina,c.Shore.Stamina<.2f?Gold:Mint);
            if(!c.Realms.Inside)return;
            var goal=new Rect(1188,126,388,190);
            ReadingVeil(goal,true);
            WorldLabel(goal.x+18,goal.y+14,352,40,FishingLore.ShrineName(c.Realms.Island),27,AmberInk,true,onPaper:true);
            GiltRule(goal.x+18,goal.y+54,240);
            WorldLabel(goal.x+22,goal.y+68,344,70,FishingLore.TrialObjective(c.Realms.Island).Replace("，","\n").TrimEnd('。'),18,CreamInk,onPaper:true);
            WorldLabel(goal.x+18,goal.y+148,352,27,c.Career.Lore.Cleared(c.Realms.Island)?"回响已收录 · 可自由重访":c.Realms.Trial.Ready?"旅程已回应 · 前往回响所在":"循线索探索这片秘境",15,Mint,onPaper:true);
        }
        private void DrawLorePage(Rect r)=>DrawIslandSpread(r,true);
        private void DrawRealmChart()
        {
            var c=Controller;var r=Layout.Chart;
            WorldLabel(r.x+16,r.y+12,r.width-32,26,"秘境导览",18,AmberInk,true);
            var map=new Rect(r.x+36,r.y+51,176,177);
            Outline(new Rect(map.center.x-100,map.center.y-100,200,200),AmberInk*new Color(1,1,1,.5f),100);
            var bounds=new Bounds(c.Realms.Room.InverseTransformPoint(c.Shore.Position),Vector3.one);
            for(int i=0;i<5;i++){var device=c.Realms.Device(i);if(device!=null)bounds.Encapsulate(c.Realms.Room.InverseTransformPoint(device.position));}
            float scale=150/Mathf.Max(1,bounds.size.x,bounds.size.z);
            Vector2 Point(Vector3 world){var p=c.Realms.Room.InverseTransformPoint(world)-bounds.center;return map.center+new Vector2(p.x,-p.z)*scale;}
            for(int i=0;i<5;i++)
            {
                var device=c.Realms.Device(i);if(device==null)continue;var p=Point(device.position);
                Disc(p,i==3?7:5,i==4?Sky:i==3?Gold:Mint);
                WorldLabel(new Rect(p.x-22,p.y-28,44,20),i==4?"入口":i==3?"回响":(i+1).ToString(),12,CreamInk,false,TextAnchor.MiddleCenter);
            }
            Disc(Point(c.Shore.Position),4,Color.white);
            WorldLabel(r.x+14,r.yMax-29,r.width-28,26,"白点：当前位置 · 青点：机关",12,CreamInk);
        }
    }
}
