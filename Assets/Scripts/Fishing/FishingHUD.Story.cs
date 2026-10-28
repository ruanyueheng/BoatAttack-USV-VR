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
        internal bool RegionTitleVisible=>Controller.Regions!=null && Controller.Regions.Alpha>.001f && !Controller.Realms.Transitioning && !CatchVisible
            && (Controller.Combat==null || Controller.Combat.Status.Length==0); // 战斗状态条优先，地名让位
        private void DrawRegionTitle()
        {
            var region=Controller.Regions;if(!RegionTitleVisible)return;
            float previous=_fade;_fade=region.Alpha;
            // V044 绘本式地名：无卡片，衬线大字 + 柔和暗晕 + 金线星芒，轻微上浮淡入。
            float y=150+(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(region.Age/1.1f)))*8;
            var previousColor=GUI.color;GUI.color=Faded(Color.white);
            GUI.DrawTexture(new Rect(520,y-34,560,170),FishingUITheme.TitleGlow,ScaleMode.StretchToFill,true);GUI.color=previousColor;
            WorldLabel(new Rect(500,y-4,600,56),region.Title,40,CreamInk,true,TextAnchor.MiddleCenter);
            GiltRule(650,y+60,300);
            WorldLabel(new Rect(500,y+70,600,26),region.Subtitle.Replace("，"," · ").TrimEnd('。'),16,CreamInk,false,TextAnchor.UpperCenter);
            _fade=previous;
        }
        private void DrawDialogue()
        {
            var c=Controller;var resident=c.Residents;int id=resident.ActiveId;
            Fill(-_offset/_scale,0,Screen.width/_scale,_height,new Color(.01f,.02f,.035f,.22f));
            // V044 绘本对话：底部半透明夜航框 + 细金边，左上名牌（宋体名 + 身份小字 + 金线）。
            var r=new Rect(240,_height-396,1120,352);
            Panel(new Rect(r.x-14,r.y-6,r.width+28,r.height+12),FishingUITheme.PanelVeil,ref _veilStyle,Color.white);
            if(id!=4)ArtIllustration(new Rect(r.x+28,r.y+26,74,88),"Resident"+id);
            else ArtIllustration(new Rect(r.x+30,r.y+25,70,80),"Lantern");
            WorldLabel(new Rect(r.x+120,r.y+20,510,44),Spaced(FishingLore.ResidentName(id)),30,AmberInk,true);
            GiltRule(r.x+120,r.y+70,300);
            WorldLabel(r.x+122,r.y+80,590,23,Memo(13,id*1000003L+(resident.TopicTitle?.GetHashCode()??0)*31L+(resident.Mood?.GetHashCode()??0),h=>{var res=h.Controller.Residents;return FishingLore.ResidentRole(res.ActiveId)+"  /  "+res.TopicTitle+(string.IsNullOrEmpty(res.Mood)?"":"  ·  "+res.Mood);}),14,Muted);
            Fill(r.x+760,r.y+24,1,r.height-48,AmberInk*new Color(1,1,1,.35f));
            int speechSize=21;_label.font=_font;_label.fontSize=speechSize;
            while(speechSize>15 && _label.CalcHeight(Content(resident.Speech),712)>176)_label.fontSize=--speechSize;
            WorldLabel(new Rect(r.x+34,r.y+130,712,176),resident.VisibleSpeech,speechSize,CreamInk);
            if(resident.Typing)// "正在说"：名牌旁三颗缓慢起伏的金点
                for(int k=0;k<3;k++)Disc(new Vector2(r.x+440+k*14,r.y+44-Mathf.Max(0,Mathf.Sin(Time.unscaledTime*4-k*.9f))*4),3,AmberInk);
            for(int i=0;i<resident.ChoiceCount;i++)
            {
                var choice=new Rect(r.x+778,r.y+24+i*53,310,47);
                if(resident.Asking && i<2 && AnswerCard(choice,resident.Choice(i)))resident.Choose(i);
                else if(!(resident.Asking && i<2) && SeaButton(choice,(i+1)+"  "+resident.Choice(i),false,true,17))resident.Choose(i);
            }
            if(id==4 && SeaButton(new Rect(r.x+778,r.y+240,310,34),"查看商品",true,true,17))resident.BrowseCashierGoods();
            if(SeaButton(new Rect(r.x+778,r.y+278,310,42),"Esc  结束交谈",false,true,17))resident.Close();
            if(resident.VisibleSpeech.Length<resident.Speech.Length)
            {if(SeaButton(new Rect(r.x+34,r.y+312,260,28),"E 显示完整对话",false,true,15))resident.Reveal();}
            else WorldLabel(r.x+34,r.y+318,640,22,(resident.ChoiceCount==4?"1 / 2 / 3 / 4":"1 / 2 / 3")+" 选择话题 · J 日志记录线索",14,Muted);
        }
        /// <summary>Answer to a resident's question: pale-gold card with a "?" medallion, distinct from topic buttons.</summary>
        private bool AnswerCard(Rect r,string text)
        {
            bool hover=r.Contains(Event.current.mousePosition);
            Rounded(r,new Color(.96f,.86f,.58f,hover?.34f:.22f),8);Outline(r,AmberInk*new Color(1,1,1,hover?1:.7f),8,hover?2:1.2f);
            Disc(new Vector2(r.x+24,r.center.y),13,AmberInk);
            DisplayText(new Rect(r.x+11,r.y,26,r.height),"?",17,SeaInk,false,TextAnchor.MiddleCenter);
            WorldLabel(new Rect(r.x+46,r.y,r.width-54,r.height),text,17,hover?AmberInk:CreamInk,false,TextAnchor.MiddleLeft);
            return GUI.Button(r,GUIContent.none,GUIStyle.none);
        }
        private void DrawExplorationStatus()
        {
            var c=Controller;if(!c.Ashore)return;
            var r=new Rect(1110,_height-171,165,74);
            WorldLabel(r.x+12,r.y+9,141,20,Memo(12,c.Shore.MovementName.GetHashCode(),h=>h.Controller.Shore.MovementName+" · 体力"),14,CreamInk);
            Bar(new Rect(r.x+12,r.y+40,141,5),c.Shore.Stamina,c.Shore.Stamina<.2f?Gold:Mint);
            if(!c.Realms.Inside)return;
            var goal=new Rect(1188,126,388,190);
            ReadingVeil(goal,true);
            WorldLabel(goal.x+18,goal.y+14,352,40,FishingLore.ShrineName(c.Realms.Island),27,AmberInk,true);
            GiltRule(goal.x+18,goal.y+54,240);
            WorldLabel(goal.x+22,goal.y+68,344,70,FishingLore.TrialObjective(c.Realms.Island).Replace("，","\n").TrimEnd('。'),18,CreamInk);
            WorldLabel(goal.x+18,goal.y+148,352,27,c.Career.Lore.Cleared(c.Realms.Island)?"回响已收录 · 可自由重访":c.Realms.Trial.Ready?"旅程已回应 · 前往回响所在":"循线索探索这片秘境",15,Mint);
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
                WorldLabel(new Rect(p.x-22,p.y-28,44,20),i==4?"出口":i==3?"回响":(i+1).ToString(),12,CreamInk,false,TextAnchor.MiddleCenter);
            }
            Disc(Point(c.Shore.Position),4,Color.white);
            WorldLabel(r.x+14,r.yMax-29,r.width-28,26,"白点：当前位置 · 青点：机关",12,CreamInk);
        }
    }
}
