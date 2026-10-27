using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private int _islandSelection,_shoreSelection;
        private void DrawIslandsPage(Rect r)=>DrawIslandSpread(r,false);
        private void DrawIslandSpread(Rect r,bool lorePage)
        {
            var c=Controller;var a=c.Career.Adventure;var lore=c.Career.Lore;
            for(int i=0;i<4;i++)
            {
                var tile=new Rect(r.x+(i%2)*284,r.y+(i/2)*245,270,229);bool selected=_islandSelection==i;
                Rounded(tile,selected?FishingUITheme.Selected:InkRaised,8);ChapterImage(new Rect(tile.x+1,tile.y+1,268,137),4+i);
                Label(tile.x+16,tile.y+149,238,34,FishingAdventure.Name(i),23,InkText,true);
                Label(tile.x+16,tile.y+190,238,24,lorePage?(lore.Cleared(i)?"回响已取回":lore.Met(i)?"已结识守望者":"守望者等待来访"):(a.Restored(i)?"灯塔已重新点亮":a.Visited(i)?"来信已读 · 继续探索":"循着海图，发现这座岛"),14,Muted);
                Outline(tile,selected?Gold:Hairline,8,selected?2:1);
                if(GUI.Button(tile,GUIContent.none,GUIStyle.none))_islandSelection=i;
            }
            int id=_islandSelection;var d=new Rect(r.x+590,r.y,540,527);Rounded(d,InkRaised,8);
            Label(d.x+26,d.y+22,488,46,lorePage?FishingLore.ShrineName(id):FishingAdventure.Name(id),31,Gold,true);
            if(lorePage)
            {
                Label(d.x+26,d.y+78,488,90,c.Career.PlayerTitle+" · "+c.Career.MainObjective,17,Gold,true);
                ArtIllustration(new Rect(d.x+26,d.y+174,72,72),"Resident"+id);
                Label(d.x+116,d.y+180,394,28,FishingLore.ResidentName(id),22,InkText,true);
                Label(d.x+116,d.y+214,394,24,FishingLore.ResidentRole(id),15,Muted);
                Label(d.x+26,d.y+258,488,90,lore.Cleared(id)?FishingLore.Echo(id):FishingLore.TrialHint(id),17,InkText,true);
                var reward=FishingCareer.ShrineReward(id);
                Label(d.x+26,d.y+355,488,68,(lore.RewardClaimed(id)?"已领取 · ":lore.Cleared(id)?"待领取 · ":"秘境收获 · ")+FishingBackpack.Name(reward[0].item)+" ×1、"+FishingBackpack.Name(reward[1].item)+" ×"+reward[1].count+"\n永久蓝图："+FishingLore.BlueprintName(id)+"\n"+FishingLore.BlueprintEffect(id),16,lore.RewardClaimed(id)?Mint:Gold,true);
                Bar(new Rect(d.x+26,d.y+435,488,6),lore.ClearCount/4f,Mint);
            }
            else
            {
                Label(d.x+26,d.y+87,488,96,a.Visited(id)?FishingAdventure.Clue(id):"码头旁有一封航海来信。岛上的风、树林与旧物，都藏着重燃灯塔的线索。",20,InkText,true);
                string[] names={"发现岛屿","地标寻宝","重燃信标"};bool[] done={a.Visited(id),a.TreasureFound(id),a.Restored(id)};
                for(int i=0;i<3;i++)
                {
                    NavGlyph(new Rect(d.x+26,d.y+215+i*61,38,38),new[]{1,7,5}[i],done[i]?Mint:Gold);
                    Label(d.x+83,d.y+221+i*61,394,30,names[i]+"  /  "+(done[i]?"已完成":new[]{"+10 金币","+40 金币","+75 金币"}[i]),19,InkText);
                }
                Label(d.x+26,d.y+413,488,30,a.Finale?"四岛航记已完成":a.ReadyForFinale?"四灯已亮 · 返港领取150金币":"四岛顺序自由，循着自己的航线出发",15,Muted);
            }
            bool treasureRoute=lorePage && lore.RewardedShrines==15 && !c.Career.PirateKing && c.Career.Backpack.Count(FieldItem.VoyageCompass)>0;
            if(Button(new Rect(d.x+26,d.y+465,234,42),treasureRoute?"白帆拱湾宝藏航线":"设为航点",true,true,16)){c.ToggleJournal();c.SelectNavigation(treasureRoute?c.HomeNavigationIndex+1:c.IslandNavigationIndex(id));}
            if(lorePage)
            {
                if(Button(new Rect(d.x+280,d.y+465,234,42),"阅读故事",c.Letter.CanRead,false,16))
                {c.ToggleJournal();c.Letter.Show(FishingLore.ShrineName(id)+"航记",FishingLore.ResidentName(id),(lore.Cleared(id)?FishingLore.Echo(id):FishingLore.TrialHint(id))+"\n\n"+c.Career.MainObjective);}
            }
            else if(Button(new Rect(d.x+280,d.y+465,234,42),"重读来信",a.Visited(id)&&c.Letter.CanRead,false,16)){c.ToggleJournal();c.Letter.ShowIsland(id);}
        }
        private void DrawShoreSpread(Rect r)
        {
            var c=Controller;
            for(int i=0;i<4;i++)
            {
                var tile=new Rect(r.x+(i%2)*284,r.y+(i/2)*245,270,229);bool selected=_shoreSelection==i;
                Rounded(tile,selected?FishingUITheme.Selected:InkRaised,8);ChapterImage(new Rect(tile.x+1,tile.y+1,268,122),i==0?3:3+i);
                Label(tile.x+15,tile.y+134,240,32,FishingExploration.SiteName(i),23,InkText,true);
                for(int t=0;t<2;t++)
                {
                    bool done=FishingExploration.Done(c.Career.ShoreTaskMask,i*2+t);
                    Disc(new Vector2(tile.x+20,tile.y+183+t*23),4,done?Mint:Track);
                    Label(tile.x+34,tile.y+170+t*23,221,24,FishingExploration.TaskName(i*2+t),14,Muted);
                }
                Outline(tile,selected?Gold:Hairline,8,selected?2:1);if(GUI.Button(tile,GUIContent.none,GUIStyle.none))_shoreSelection=i;
            }
            var d=new Rect(r.x+590,r.y,540,527);Rounded(d,InkRaised,8);
            NavGlyph(new Rect(d.x+29,d.y+23,59,59),2,Gold);
            Label(d.x+110,d.y+29,400,45,"沿岸调查",31,InkText,true);
            Label(d.x+28,d.y+108,484,42,FishingExploration.SiteName(_shoreSelection),25,Gold,true);
            Label(d.x+28,d.y+163,484,66,"走近勘测台按 E 记录。\n每区两项调查，不占用鱼箱空间。",19,InkText,true);
            Label(d.x+28,d.y+251,484,32,"已记录  "+FishingExploration.CompletedTasks(c.Career.ShoreTaskMask)+" / 8",23,Mint,true);
            Bar(new Rect(d.x+28,d.y+299,484,6),FishingExploration.CompletedTasks(c.Career.ShoreTaskMask)/8f,Mint);
            Label(d.x+28,d.y+330,484,62,"每区回港提交 +25 金币\n完成四区，额外奖励 +60 金币",18,Muted);
            Label(d.x+28,d.y+414,484,30,c.Career.Expedition.NextObjective,17,Gold,true);
            if(Button(new Rect(d.x+28,d.y+465,234,42),"前往调查点",true,true,16))
            {c.ToggleJournal();c.SelectNavigation(1+c.fishingSpots.Length+_shoreSelection);}
            if(Button(new Rect(d.x+282,d.y+465,230,42),c.Expedition.PrecisionGuide?"关闭信标指引":"开启信标指引",true,false,15))c.Expedition.ToggleGuide();
        }
    }
}
