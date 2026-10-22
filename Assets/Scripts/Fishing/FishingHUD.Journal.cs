using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private static readonly string[] JournalTabs={"鱼获图鉴","传说线索","自然观察","海岸调查","海湾日历","群岛航记","世界与秘境"};
        private static readonly string[] ChapterNotes={"海的收藏","夕照中的金帆","与万物相遇","沿着潮线走","听潮与候风","寄自远方的信","找回四段回响"};
        private int _catalogPage,_selectedFish,_natureSelection;
        private bool _knownFishOnly,_journalDrawn,_journalOrderOpen;
        private float _journalShownAt;
        private readonly List<int> _catalogEntries=new List<int>(20);
        public int CatalogPage { get=>_catalogPage;set{_catalogPage=Mathf.Clamp(value,0,(FishingSpecies.Count-1)/6);_selectedFish=_catalogPage*6;} }
        private void ChapterImage(Rect r,int tile)
        {
            if(_chapterArt==null)_chapterArt=Resources.Load<Texture2D>("FishingV031/Chapters");
            if(_chapterArt==null)return;
            float w=.25f,h=.5f;float aspect=(_chapterArt.width*w)/(_chapterArt.height*h),target=r.width/r.height;
            if(target>aspect)h*=aspect/target;else w*=target/aspect;
            float focus=tile==1 || tile==4 || tile==6?.74f:.5f;
            GUI.DrawTextureWithTexCoords(r,_chapterArt,new Rect(tile%4*.25f+(.25f-w)*.5f,(1-tile/4)*.5f+(.5f-h)*focus,w,h));
        }
        private void NavGlyph(Rect r,int index,Color tint)
        {
            var sprite=FishingUITheme.NavigationIcon(index);var tex=sprite.texture;var p=sprite.rect;
            var previous=GUI.color;GUI.color=Faded(tint);
            GUI.DrawTextureWithTexCoords(r,tex,new Rect(p.x/tex.width,p.y/tex.height,p.width/tex.width,p.height/tex.height));GUI.color=previous;
        }
        private void DrawJournal()
        {
            var c=Controller;var r=new Rect(72,Mathf.Max(14,(_height-756)*.5f),1456,756);
            if(!_journalDrawn){_journalDrawn=true;_journalShownAt=Time.unscaledTime;}
            float previousFade=_fade;_fade=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-_journalShownAt)/.28f));
            Dim();Rounded(r,Ink,12);Outline(r,Hairline,12);
            ThemedPaper(new Rect(r.x+254,r.y,r.width-254,r.height),_journalPage==0 || _journalPage==2?FishingPaperTheme.Herbarium:_journalPage==4?FishingPaperTheme.Workshop:FishingPaperTheme.Celestial);
            Rounded(new Rect(r.x,r.y,254,r.height),SeaInk,12);
            PaperImage(new Rect(r.x+96,r.y+604,62,50),FishingPaperDesign.Crest,Color.white);
            DisplayText(new Rect(r.x+27,r.y+29,205,26),"T I D E B O U N D",13,AmberInk);
            DisplayText(new Rect(r.x+26,r.y+64,220,54),c.ReadingFieldGuide?"自然手册":"航海手札",33,CreamInk,true);
            Fill(r.x+28,r.y+126,192,1,new Color(.86f,.72f,.48f,.4f));
            for(int i=0;i<JournalTabs.Length;i++)
            {
                var tab=new Rect(r.x+14,r.y+146+i*66,226,58);bool selected=_journalPage==i;
                if(selected)Rounded(tab,new Color(.18f,.29f,.33f),6);
                else if(tab.Contains(Event.current.mousePosition))Rounded(tab,new Color(.1f,.19f,.25f),6);
                var icon=new Rect(tab.x+12,tab.y+11,34,34);
                if(i==0)FishGlyph(icon,FishKind.Coral,false);
                else if(i==4)WeatherIcon(icon,Controller.Weather.Climate.Current);
                else if(i==6)ArtIllustration(icon,"StarPrism");
                else NavGlyph(icon,new[]{0,5,3,2,6,6,8}[i],selected?AmberInk:CreamInk);
                DisplayText(new Rect(tab.x+58,tab.y+16,166,30),JournalTabs[i],20,selected?AmberInk:CreamInk);
                if(GUI.Button(tab,GUIContent.none,GUIStyle.none)){_journalPage=i;_journalOrderOpen=false;}
            }
            DisplayText(new Rect(r.x+28,r.yMax-100,200,52),"每次发现，都有回响。\n记录随航程保存",15,CreamInk,true);
            if(SeaButton(new Rect(r.x+24,r.yMax-49,206,32),c.ReadingFieldGuide?"Esc  合上手册":"J / Esc  返回海湾",false,true,15))c.ToggleJournal();
            float x=r.x+290;
            Label(x,r.y+27,820,53,_journalOrderOpen?"港口委托":JournalTabs[_journalPage],36,InkText,true);
            Label(x+2,r.y+84,820,26,ChapterNotes[_journalPage]+"    /    回潮海自然档案",16,Muted);
            Label(new Rect(r.xMax-185,r.y+43,146,28),"0"+(_journalPage+1)+"  /  07",16,Muted,false,TextAnchor.UpperRight);
            if(Button(new Rect(r.xMax-250,r.y+79,210,31),_journalOrderOpen?"返回手札":"查看当前委托",true,false,14))_journalOrderOpen=!_journalOrderOpen;
            QuietRule(x,r.y+124,r.width-326);
            var page=new Rect(x,r.y+145,r.width-326,544);
            if(_journalOrderOpen)DrawJournalOrder(page);
            else switch(_journalPage)
            {
                case 0:DrawCatalogSpread(page);break;
                case 1:DrawLegendSpread(page);break;
                case 2:DrawNatureSpread(page);break;
                case 3:DrawShoreSpread(page);break;
                case 4:DrawCalendarPage(page);break;
                case 5:DrawIslandsPage(page);break;
                case 6:DrawLorePage(page);break;
            }
            Fill(x,r.yMax-49,r.width-326,1,Hairline);
            Label(x,r.yMax-33,r.width-326,22,c.HasPendingCatch?"请先收好或放生待处理的鱼，再阅读航海来信。":"点击卡片查看详情  ·  观察与收集进度永久保留  ·  阅读时海湾暂停",13,Muted);
            _fade=previousFade;
        }
        private bool KnownFish(int i)=>Controller.Career.Discovered((FishKind)i) || Controller.Career.Discovered((FishKind)i,true);
        private void DrawCatalogSpread(Rect r)
        {
            var career=Controller.Career;int known=0;for(int i=0;i<FishingSpecies.Count;i++)if(KnownFish(i))known++;
            Label(r.x,r.y,580,29,"海湾藏品   "+known.ToString("00")+" / "+FishingSpecies.Count,19,InkText,true);
            if(Tab(new Rect(r.x+330,r.y-4,110,32),"全部",!_knownFishOnly))_knownFishOnly=false;
            if(Tab(new Rect(r.x+444,r.y-4,134,32),"已发现",_knownFishOnly))_knownFishOnly=true;
            _catalogEntries.Clear();for(int i=0;i<FishingSpecies.Count;i++)if(!_knownFishOnly || KnownFish(i))_catalogEntries.Add(i);
            int pages=Mathf.Max(1,(_catalogEntries.Count+5)/6);_catalogPage=Mathf.Clamp(_catalogPage,0,pages-1);
            if(_knownFishOnly && _catalogEntries.Count>0 && !_catalogEntries.Contains(_selectedFish))_selectedFish=_catalogEntries[0];
            if(_catalogEntries.Count==0)Label(r.x+24,r.y+180,540,85,"还没有收录鱼种。\n带上鱼竿，让第一次相遇成为手札的起点。",23,Muted,true);
            for(int slot=0;slot<6;slot++)
            {
                int entry=_catalogPage*6+slot;if(entry>=_catalogEntries.Count)break;int index=_catalogEntries[entry];
                bool knownEntry=KnownFish(index),enabled=!_knownFishOnly || knownEntry;
                var tile=new Rect(r.x+(slot%2)*294,r.y+47+(slot/2)*138,282,126);bool selected=_selectedFish==index;
                Rounded(tile,selected?FishingUITheme.Selected:InkRaised,7);Outline(tile,selected?Gold:Hairline,7,selected?2:1);
                Label(tile.x+12,tile.y+8,66,20,(index+1).ToString("00"),13,Muted);
                bool hidden=FishingSpecies.Of((FishKind)index).Legendary && !knownEntry;
                if(!hidden)FishGlyph(new Rect(tile.x+62,tile.y+7,162,73),(FishKind)index,false,!knownEntry);
                else Label(new Rect(tile.x+80,tile.y+14,120,60),"？",38,Muted,true,TextAnchor.MiddleCenter);
                Label(new Rect(tile.x+12,tile.y+85,tile.width-24,31),hidden?"未解的金帆":FishingSpecies.Name((FishKind)index),20,enabled?InkText:Muted,true,TextAnchor.MiddleCenter);
                Disc(new Vector2(tile.xMax-17,tile.y+18),4,knownEntry?Mint:Track);
                if(!enabled)Rounded(tile,new Color(.1f,.17f,.17f,.16f),7);
                if(GUI.Button(tile,GUIContent.none,GUIStyle.none) && enabled)_selectedFish=index;
            }
            Label(r.x,r.y+450,576,25,FishingSurvey.Goal(career),14,Mint);
            if(Button(new Rect(r.x,r.y+480,115,36),"← 上一页",_catalogPage>0,false,14))CatalogPage--;
            Label(new Rect(r.x+125,r.y+487,338,24),(_catalogPage+1)+" / "+pages+"    ·    标本目录",14,Muted,false,TextAnchor.MiddleCenter);
            if(Button(new Rect(r.x+473,r.y+480,103,36),"下一页 →",_catalogPage<pages-1,false,14))CatalogPage++;
            var detail=new Rect(r.x+610,r.y,520,527);Rounded(detail,FishingUITheme.Light,4);Outline(detail,Hairline,4);
            ChapterImage(new Rect(detail.x,detail.y,520,130),0);
            PaperImage(new Rect(detail.xMax-67,detail.y-2,36,72),FishingPaperDesign.Bookmark,Color.white);
            var kind=(FishKind)_selectedFish;var species=FishingSpecies.Of(kind);bool knownFish=KnownFish(_selectedFish),secret=species.Legendary&&!knownFish;
            Rounded(new Rect(detail.x+57,detail.y+87,406,182),Ink,8);Outline(new Rect(detail.x+57,detail.y+87,406,182),Hairline,8);
            if(secret)Label(new Rect(detail.x+75,detail.y+112,370,128),"？",70,Gold,true,TextAnchor.MiddleCenter);
            else FishGlyph(new Rect(detail.x+90,detail.y+105,340,149),kind,false,!knownFish);
            Label(new Rect(detail.x+28,detail.y+285,464,48),secret?"传说仍在海风里":species.Name,32,InkText,true,TextAnchor.UpperCenter);
            Label(new Rect(detail.x+34,detail.y+338,452,56),secret?"收集传说线索，寻找海面上的金色帆影。":species.Habitat,17,Muted,false,TextAnchor.UpperCenter);
            Label(new Rect(detail.x+30,detail.y+389,460,21),secret?"传说条件见线索章":FishingText.Weight(species.MinGrams)+" – "+FishingText.Weight(species.MaxGrams),13,Muted,false,TextAnchor.MiddleCenter);
            QuietRule(detail.x+30,detail.y+416,460);
            Label(detail.x+30,detail.y+428,230,25,"最大纪录",14,Muted);
            Label(detail.x+30,detail.y+458,250,36,career.BestGrams(kind)>0?FishingText.Weight(career.BestGrams(kind)):"等待第一次相遇",24,Gold,true);
            Label(detail.x+300,detail.y+428,194,26,knownFish?"已收录":"尚未收录",16,knownFish?Mint:Muted,true);
            Label(detail.x+300,detail.y+461,194,42,species.Legendary?"测量后放生":(career.Discovered(kind,true)?"珍珠变体已发现":"珍珠变体待发现"),15,Muted);
        }
        private void DrawJournalOrder(Rect r)
        {
            var career=Controller.Career;var order=career.CurrentOrder;bool ready=FishingOrders.Select(order,career.Inventory,_picked,_perLine);
            Rounded(r,InkRaised,8);ChapterImage(new Rect(r.x,r.y,628,143),0);
            Label(r.x+24,r.y+161,580,30,order.Client,17,Muted);
            Label(r.x+24,r.y+200,580,43,order.Name,30,InkText,true);
            DrawOrderLines(order,r.x+24,r.y+263,580);
            Fill(r.x+648,r.y+25,1,477,Hairline);
            NavGlyph(new Rect(r.x+692,r.y+37,56,56),7,Gold);
            Label(r.x+772,r.y+46,330,43,"来自海湾的委托",27,InkText,true);
            Label(r.x+692,r.y+133,402,72,"+ "+order.Bonus+" 金币",43,Gold,true);
            Label(r.x+692,r.y+220,402,33,"奖金之外，渔货全额计价",20,InkText,true);
            Label(r.x+692,r.y+282,402,109,"普通出售会为委托留下所需鱼。\n集齐后返港按 E 交付，\n新的委托会接着送来。",19,Muted);
            Label(r.x+692,r.y+427,402,52,ready?"已集齐 · 等待返港":"带着这份约定，再去海上看看。",21,ready?Mint:Gold,true);
        }
        private void DrawLegendSpread(Rect r)
        {
            var career=Controller.Career;
            ChapterImage(new Rect(r.x,r.y,640,300),1);
            Rounded(new Rect(r.x,r.y+313,640,213),InkRaised,7);
            Label(r.x+23,r.y+329,594,44,career.LegendCatches>0?"金帆旗鱼":"海面上，传来金色的传闻",29,Gold,true);
            Label(r.x+23,r.y+385,594,68,"夕照染红水道，它追逐闪亮的饵，跃出浪尖。\n找到它，也请记得让它回到海里。",20,InkText,true);
            Label(r.x+23,r.y+473,594,28,career.LegendCatches>0?"已放生 "+career.LegendCatches+" 次  ·  纪录 "+FishingText.Weight(career.BestGrams(FishKind.Sailfish)):"首次测量放生：150–210 金币",16,Muted);
            for(int i=0;i<LegendRules.ClueCount;i++)
            {
                bool unlocked=LegendRules.ClueUnlocked(i,career);var card=new Rect(r.x+672,r.y+i*128,458,112);
                Rounded(card,InkRaised,8);NavGlyph(new Rect(card.x+17,card.y+23,56,56),new[]{1,6,5}[i],unlocked?Gold:Muted);
                Label(card.x+91,card.y+13,344,28,"线索 0"+(i+1)+(unlocked?"  / 已发现":"  / 待发现"),18,unlocked?Gold:Muted,true);
                Label(card.x+91,card.y+49,344,56,unlocked?LegendRules.Clue(i):LegendRules.ClueHint(i),16,InkText);
            }
            Label(r.x+682,r.y+400,430,65,"回气时收线，跃水时松开。\n让风与潮帮你完成这场相遇。",19,InkText,true);
            if(Button(new Rect(r.x+672,r.y+480,458,46),"设定图鉴调查航点",true,true))Controller.NavigateSurvey();
        }
        private static readonly string[] NatureNames={"海龟","鳐鱼","海豚","鱼群","海鸥","海岛野兔","赤狐","陆龟"};
        private static readonly string[] NatureArt={"Turtle","Ray","Dolphin","School","Seagull","Rabbit","Fox","Turtle"};
        private bool NatureSeen(int index)=>index<5?Controller.Career.Seen((MarineKind)index):Controller.Career.Backpack.Seen((LandAnimalKind)(index-5));
        private void NaturePortrait(Rect r,int index)
        {
            string path=index==7?"FishingV031/TortoisePortrait":index==0?"FishingV029/TurtleThumbnail":index>=4?"FishingV029/"+NatureArt[index]+"Thumbnail":index==1?"FishingFish/StingrayThumbnail":index==3?"FishingFish/SardineThumbnail":"FishingV031/DolphinPortrait";
            if(!_artIcons.TryGetValue(path,out var image))_artIcons[path]=image=Resources.Load<Texture2D>(path);
            if(image!=null)
            {
                if(index==3)for(int i=0;i<5;i++)GUI.DrawTexture(new Rect(r.x+(i%3)*r.width*.25f,r.y+(i/3)*r.height*.33f,r.width*.5f,r.height*.5f),image,ScaleMode.ScaleToFit,true);
                else GUI.DrawTexture(r,image,ScaleMode.ScaleToFit,true);
            }
            else {ChapterImage(r,2);}
        }
        private void DrawNatureSpread(Rect r)
        {
            int seen=0;for(int i=0;i<8;i++)if(NatureSeen(i))seen++;
            Label(r.x,r.y,580,30,"观察名录    "+seen+" / 8",19,InkText,true);
            for(int i=0;i<8;i++)
            {
                var card=new Rect(r.x+(i%2)*250,r.y+47+(i/2)*117,236,105);bool selected=_natureSelection==i;
                Rounded(card,selected?FishingUITheme.Selected:InkRaised,7);Outline(card,selected?Gold:Hairline,7,selected?2:1);
                NaturePortrait(new Rect(card.x+10,card.y+9,83,82),i);
                Label(card.x+105,card.y+21,124,29,NatureNames[i],19,InkText,true);
                Label(card.x+105,card.y+62,124,26,NatureSeen(i)?"已记录":"待观察",14,NatureSeen(i)?Mint:Muted);
                if(GUI.Button(card,GUIContent.none,GUIStyle.none))_natureSelection=i;
            }
            var d=new Rect(r.x+530,r.y,600,527);Rounded(d,InkRaised,8);ChapterImage(new Rect(d.x,d.y,600,175),2);
            Rounded(new Rect(d.x+157,d.y+95,286,213),Ink,8);NaturePortrait(new Rect(d.x+174,d.y+106,252,184),_natureSelection);
            Label(new Rect(d.x+30,d.y+324,540,45),NatureNames[_natureSelection],33,InkText,true,TextAnchor.MiddleCenter);
            string hint=_natureSelection<5?BayClimate.MarineHint((MarineKind)_natureSelection):_natureSelection==5?"草地与灌木间的轻巧访客。放慢脚步，留一点距离。":_natureSelection==6?"林缘与沙洲上的赤色身影。蹲下靠近，更容易观察。":"岩石和草地上的慢行者。请让它按自己的节奏生活。";
            Label(new Rect(d.x+38,d.y+382,524,65),hint,18,Muted,false,TextAnchor.UpperCenter);
            Fill(d.x+35,d.y+467,530,1,Hairline);
            Label(new Rect(d.x+35,d.y+487,530,28),NatureSeen(_natureSelection)?"观察已记录 · 与万物保持温柔的距离":"看向它按 T 记录 · 望远镜能看得更远",16,NatureSeen(_natureSelection)?Mint:Gold,false,TextAnchor.MiddleCenter);
        }
    }
}
