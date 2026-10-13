using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawIslandsPage(Rect r)
        {
            var c=Controller;var a=c.Career.Adventure;
            for(int i=0;i<4;i++)
            {
                var row=new Rect(r.x+32,r.y+118+i*110,820,98);Rounded(row,InkRaised,12);
                Label(row.x+18,row.y+9,610,26,((char)('E'+i))+" · "+FishingAdventure.Name(i),21,Gold,true);
                Label(row.x+18,row.y+41,780,25,a.Visited(i)?FishingAdventure.Clue(i):"寻找码头旁的航海来信，读信后获得地标寻宝与灯头方向线索。",14,Muted);
                Label(row.x+18,row.y+72,760,21,(a.Visited(i)?"来信已读":"发现 +10")+"   /   "+(a.TreasureFound(i)?"宝箱已开":"寻宝 +40")+"   /   "+(a.Restored(i)?"信标已亮":"信标 +75"),14,Mint);
                if(Button(new Rect(row.xMax-160,row.y+7,140,32),"设为航点",true,false,14)){c.ToggleJournal();c.SelectNavigation(c.IslandNavigationIndex(i));}
                if(a.Visited(i) && Button(new Rect(row.xMax-160,row.y+58,140,28),"重读来信",true,false,14)){c.ToggleJournal();c.Letter.ShowIsland(i);}
            }
            var side=new Rect(r.x+884,r.y+118,420,438);Rounded(side,InkRaised,12);
            Label(side.x+20,side.y+17,380,30,"群岛来信 · 重连海湾",22,Gold,true);
            Label(side.x+20,side.y+62,380,150,"一场风暴让四座岛失去航标。\n旧船长把灯头方向写进岛上来信，\n也留下了地标寻宝的线索。\n\n岛屿顺序自由，可随时捕鱼、制作，\n赚取金币后回店升级船只。",16,Muted);
            Label(side.x+20,side.y+231,380,80,"E 读信 / 开箱 / 转动航向盘\nF 确认灯头方向；失败不扣金币\n点亮四灯后返港 E 领取 150 金币",16,Mint);
            Label(side.x+20,side.y+333,380,60,a.Finale?"航记已完成 · 海湾重新亮起":"航记："+(a.ReadyForFinale?"四灯已亮，返港交付":"探索四岛，点亮每座信标"),17,Gold,true);
        }
    }
}
