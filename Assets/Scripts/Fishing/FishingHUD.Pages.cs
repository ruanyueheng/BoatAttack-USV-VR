using System.Globalization;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Modal pages: the voyage journal (catches, legend, wildlife) and the pause settings.</summary>
    public sealed partial class FishingHUD
    {
        private static readonly string[] JournalTabs = { "鱼获图鉴", "传说线索", "自然观察", "海岸调查", "海湾日历", "群岛航记", "世界与秘境" };

        private void Dim() => Fill(-_offset / _scale, 0, Screen.width / _scale, _height, new Color(0.01f, 0.025f, 0.04f, 0.55f));

        private void DrawJournal()
        {
            var c = Controller;
            float h = 700f;
            var r = new Rect(132, Mathf.Max(16f, (_height - h) * 0.5f), 1336, h);
            Dim();
            Card(r, Gold);
            Label(r.x + 32, r.y + 24, 600, 44, "航海日志", 30, Color.white, true);
            Label(new Rect(r.x+730,r.y+32,570,24),"回潮海档案  /  发现与纪录永久保留",15,Muted,false,TextAnchor.UpperRight);
            Fill(r.x+32,r.y+105,r.width-64,1,Hairline);
            for (int i = 0; i < JournalTabs.Length; i++)
                if (Tab(new Rect(r.x + 32 + i * 182, r.y + 69, 174, 34), JournalTabs[i], _journalPage == i)) _journalPage = i;
            if (_journalPage == 1) DrawLegendPage(r);
            else if (_journalPage == 2) DrawNaturePage(r);
            else if (_journalPage == 3) DrawShorePage(r);
            else if (_journalPage == 4) DrawCalendarPage(r);
            else if (_journalPage == 5) DrawIslandsPage(r);
            else if (_journalPage == 6) DrawLorePage(r);
            else { DrawCatalogPage(r); DrawCatalogPager(r); }
            if (_journalPage != 4 && _journalPage != 5 && _journalPage != 6) DrawJournalSide(r);
            Label(r.x + 34, r.yMax - 52, 820, 24, c.HasPendingCatch?"请先收好或放生待处理的鱼，再阅读来信与航记。":_journalPage==6?"E交谈 / 进入石门 · 四岛自由探索 · 每处首通90；回港林舟领取终章180金币。":_journalPage==3?"岸上调查不占鱼箱 · 每区两项调查返港领 25 金币 · 四区全部交付额外 60 金币。":_journalPage==5?"四岛顺序自由 · 探索奖励各领一次 · 点亮四灯后返港 E 交付 +150 金币。":"自然观察不奖励金币；请保持距离。传说旗鱼测量后放生。", 14, Faint);
            if (Button(new Rect(r.xMax - 286, r.yMax - 64, 254, 44), "J / Esc  返回海湾")) c.ToggleJournal();
        }

        private void DrawShorePage(Rect r)
        {
            var c=Controller;
            for(int i=0;i<FishingExploration.SiteCount;i++)
            {
                var row=new Rect(r.x+32,r.y+118+i*110,820,98);
                Rounded(row,InkRaised,12);
                bool done=FishingExploration.SiteDone(c.Career.ShoreTaskMask,i);
                bool paid=(c.Career.ShoreRewardMask&(1<<i))!=0;
                Label(row.x+18,row.y+8,780,27,((char)('A'+i))+" · "+FishingExploration.SiteName(i)+"  /  "+(paid?"已提交":done?"回港 E 提交 +25":"待调查"),20,done?Mint:Gold,true);
                if(i==0 && c.Career.Expedition.Has(FishingExpedition.RecordRead) && Button(new Rect(row.xMax-180,row.y+8,160,27),"重读维修记录",c.Letter.CanRead,false,14))c.Expedition.ReadRecordFromJournal();
                for(int t=0;t<2;t++)
                {
                    int id=i*2+t;
                    Label(row.x+18,row.y+40+t*24,780,24,(FishingExploration.Done(c.Career.ShoreTaskMask,id)?"已记录  ":"未记录  ")+FishingExploration.TaskName(id),15,Muted);
                }
            }
        }
        private int _catalogPage;
        public int CatalogPage { get=>_catalogPage; set=>_catalogPage=Mathf.Clamp(value,0,2); }
        private void DrawCatalogPage(Rect r)
        {
            var career = Controller.Career;
            for (int i = _catalogPage * 4; i < Mathf.Min(FishingSpecies.Count, (_catalogPage + 1) * 4); i++)
            {
                var kind = (FishKind)i;
                var species = FishingSpecies.Of(kind);
                var row = new Rect(r.x + 32, r.y + 118 + (i % 4) * 110, 820, 98);
                Fill(row.x,row.yMax,row.width,1,Hairline);
                bool known = career.Discovered(kind) || career.Discovered(kind, true);
                if (species.Legendary && !known) Label(new Rect(row.x + 14, row.y + 12, 120, 74), "？", 40, Faint, true, TextAnchor.MiddleCenter);
                else FishGlyph(new Rect(row.x + 22, row.y + 20, 104, 58), kind, false);
                Label(row.x + 150, row.y + 10, 40, 24, (i + 1).ToString("00"), 15, Gold, true);
                string title = species.Legendary
                    ? (known ? species.Name + " · 航标水道" : "？？？ · 传说怪鱼")
                    : species.Name;
                Label(row.x + 190, row.y + 8, 620, 30, title, 21, species.Legendary ? Gold : Color.white, true);
                Label(row.x + 150, row.y + 40, 660, 22, species.Legendary && !known ? "日志“传说线索”页记录着关于它的传闻。" : species.Habitat, 14, Muted);
                float x = row.x + 150;
                float y = row.y + 66;
                if (species.Legendary)
                {
                    x += Chip(x, y, known ? "已放生 " + career.LegendCatches + " 次" : "尚未遇见", known ? Gold : Faint, known) + 8;
                }
                else
                {
                    x += Chip(x, y, career.Discovered(kind) ? "普通 √" : "普通 未发现", career.Discovered(kind) ? Mint : Faint) + 8;
                    x += Chip(x, y, career.Discovered(kind, true) ? "珍珠 √" : "珍珠 未发现", career.Discovered(kind, true) ? Sky : Faint) + 8;
                    x += Chip(x, y, "鱼箱 " + career.Owned(kind), Muted) + 8;
                }
                int best = career.BestGrams(kind);
                Chip(x, y, best > 0 ? "纪录 " + FishingText.Weight(best) : "纪录 —", best > 0 ? Gold : Faint);
                Label(new Rect(row.xMax - 230, row.y + 10, 214, 22), FishingText.Weight(species.MinGrams) + " – " + FishingText.Weight(species.MaxGrams), 13, Faint, false, TextAnchor.UpperRight);
            }
        }

        private void DrawCatalogPager(Rect r)
        {
            if(Button(new Rect(r.x+32,r.y+566,140,36),"上一页",_catalogPage>0,false,15))_catalogPage--;
            Label(r.x+192,r.y+572,490,26,"12 种鱼 · 8 种混元模型   /   "+(_catalogPage+1)+" / 3",15,Muted);
            if(Button(new Rect(r.x+712,r.y+566,140,36),"下一页",_catalogPage<2,false,15))_catalogPage++;
        }
        private void DrawLegendPage(Rect r)
        {
            var career = Controller.Career;
            bool known = career.LegendCatches > 0;
            var panel = new Rect(r.x + 32, r.y + 118, 820, 446);
            Rounded(panel, InkRaised, 12);
            Rounded(new Rect(panel.x + 20, panel.y + 20, 260, 170), Ink, 12);
            if (known) FishGlyph(new Rect(panel.x + 36, panel.y + 58, 228, 110), FishKind.Sailfish, false);
            else Label(new Rect(panel.x + 20, panel.y + 20, 260, 170), "？", 64, Faint, true, TextAnchor.MiddleCenter);
            Label(panel.x + 304, panel.y + 22, 500, 40, known ? "金帆旗鱼" : "传说怪鱼 · ？？？", 30, Gold, true);
            Label(panel.x + 304, panel.y + 68, 496, 72, "传说它在夕照染红海面时浮上航标水道，追逐闪光的珍珠饵，一次次跃出水面。跃水时硬拉，鱼线必断。", 16, Muted);
            float x = panel.x + 304;
            x += Chip(x, panel.y + 146, known ? "已放生 " + career.LegendCatches + " 次" : "尚未遇见", known ? Gold : Faint, known) + 8;
            x += Chip(x, panel.y + 146, career.BestGrams(FishKind.Sailfish) > 0 ? "纪录 " + FishingText.Weight(career.BestGrams(FishKind.Sailfish)) : "纪录 —", Gold) + 8;
            Chip(x, panel.y + 146, "首次放生奖励 150 – 210 金币", Muted);
            Section(panel.x + 20, panel.y + 212, 780, "线索", "解锁后，港口的传说卡会亮起对应条件");
            for (int i = 0; i < LegendRules.ClueCount; i++)
            {
                bool open = LegendRules.ClueUnlocked(i, career);
                float y = panel.y + 252 + i * 50;
                Chip(panel.x + 20, y, open ? "线索 " + (i + 1) : "未解锁", open ? Mint : Faint, open);
                Label(panel.x + 120, y + 1, 680, 24, open ? LegendRules.Clue(i) : LegendRules.ClueHint(i), 16, open ? Color.white : Faint);
            }
            Label(panel.x + 20, panel.y + 404, 780, 40, "对决要诀：回气时收线，蓄力时准备，跃水时立即松开，冲刺时放线。捕获后测量并放生，灯塔顶会升起金帆风向标。", 14, Gold);
        }

        private void DrawNaturePage(Rect r)
        {
            var career = Controller.Career;
            for (int i = 0; i < 5; i++)
            {
                var kind = (MarineKind)i;
                bool seen = career.Seen(kind);
                var row = new Rect(r.x + 32, r.y + 118 + i * 88, 820, 76);
                Rounded(row, InkRaised, 12);
                Label(row.x + 22, row.y + 10, 240, 30, BayClimate.MarineName(kind), 22, seen ? Mint : Color.white, true);
                Label(row.x + 260, row.y + 13, 540, 24, BayClimate.MarineHint(kind), 16, Muted);
                Chip(row.x + 22, row.y + 44, seen ? "已观察 · 记录永久保留" : "未记录 · 28m 内看向它，按 T 观察", seen ? Mint : Gold, seen);
            }
        }

        private void DrawJournalSide(Rect r)
        {
            var c = Controller;
            var career = c.Career;
            var order = career.CurrentOrder;
            FishingOrders.Select(order, career.Inventory, _picked, _perLine);
            var side = new Rect(r.x + 884, r.y + 118, 420, Mathf.Max(250f, 142f + order.LineCount * OrderLineHeight));
            Rounded(side, InkRaised, 12);
            Label(side.x + 20, side.y + 16, 380, 20, order.Client, 13, Muted);
            Label(side.x + 20, side.y + 36, 380, 30, "当前委托 · " + order.Name, 21, Gold, true);
            DrawOrderLines(order, side.x + 18, side.y + 76, 384);
            Label(side.x + 20, side.y + 82 + order.LineCount * OrderLineHeight, 380, 44, "集齐后返港按 E 交付：鱼价全额 + " + order.Bonus + " 奖金。\n普通出售会自动保留委托所需的鱼。", 14, Muted);
            var stats = new Rect(r.x + 884, side.yMax + 14, 420, 182);
            Rounded(stats, InkRaised, 12);
            if(_journalPage==3)
            {
                Label(stats.x+20,stats.y+10,380,22,"重燃旧港信标",16,Gold,true);
                Label(stats.x+20,stats.y+34,380,24,c.Career.Expedition.NextObjective,15,Color.white,true);
                Label(stats.x+20,stats.y+62,380,66,c.Expedition.Clue,14,Mint);
                if(Button(new Rect(stats.x+20,stats.y+132,380,34),c.Expedition.PrecisionGuide?"关闭精确指引（地图 ★）":"需要帮助：开启地图精确指引",true,false,14))c.Expedition.ToggleGuide();
                return;
            }
            if(_journalPage==0 || _journalPage==1)
            {
                Label(stats.x+20,stats.y+12,380,44,FishingSurvey.Goal(career),16,Gold,true);
                Label(stats.x+20,stats.y+60,380,26,FishingSurvey.Checklist(career),14,Mint);
                Label(stats.x+20,stats.y+90,380,40,FishingSpecies.Of(FishingSurvey.NextFish(career)).Habitat,14,Muted);
                int destination=FishingSurvey.Stage(career)==0?1:2;
                if(Button(new Rect(stats.x+20,stats.y+136,380,34),"设定调查航点 · "+FishingGrounds.Name(destination-1),true,false,14))c.NavigateSurvey();
                return;
            }
            Label(stats.x + 20, stats.y + 16, 380, 26, "航海统计", 17, Color.white, true);
            Label(stats.x + 20, stats.y + 50, 380, 110,
                "累计捕获   " + career.Catches + " 条\n完成委托   " + career.OrdersCompleted + " 份\n放生传说   " + career.LegendCatches + " 次\n持有金币   " + career.Coins.ToString("N0", CultureInfo.InvariantCulture), 17, Mint);
            Label(stats.x + 220, stats.y + 50, 190, 120, "珍珠饵更易引来稀有鱼，但张力更大；舒缓饵适合练习收放线。", 13, Faint);
        }

        private void DrawPause()
        {
            var c = Controller;
            float h = 700f;
            var r = new Rect(450, Mathf.Max(16f, (_height - h) * 0.5f), 700, h);
            Dim();
            Card(r, Mint);
            float x = r.x + 34, w = r.width - 68;
            Label(x, r.y + 24, w, 44, "海湾设置 · 世界已暂停", 29, Color.white, true);
            Label(x, r.y + 74, w, 44, "天气在继续航行后缓慢变化；不添加雷闪、镜头抖动或海浪推船。", 16, Muted);
            float y = r.y + 128;
            if (Button(new Rect(x, y, w, 46), "舒适航行：" + (c.ComfortMode ? "开启（降速、平滑视角）" : "标准动力"))) c.ToggleComfort();
            y += 56;
            if (Button(new Rect(x, y, w, 46), "天气：" + BayClimate.Name(c.Weather.Climate.Current) + "  ·  点击切换下一种")) c.Weather.CyclePreview();
            y += 56;
            if (Button(new Rect(x, y, w, 46), "天气循环：" + (c.Weather.Climate.Automatic ? "自动" : "固定") + "  ·  点击切换"))
            {
                if (c.Weather.Climate.Automatic) c.Weather.Climate.Hold();
                else c.Weather.Climate.ResumeCycle();
            }
            y += 56;
            if (Button(new Rect(x, y, w, 46), "环境特效：" + (c.Weather.ReducedEffects ? "精简雨量" : "标准雨量") + "（XR 自动精简）")) c.Weather.ToggleReducedEffects();
            y += 56;
            if (Button(new Rect(x, y, w, 46), "世界空间仪表预览：" + (c.WorldInstruments ? "开" : "关"))) c.ToggleInstruments();
            y += 56;
            if (Button(new Rect(x, y, w, 46), "捕鱼 / 环境音：" + (c.QuietMode ? "静音" : "开启"))) c.ToggleQuiet();
            y += 66;
            Label(x, y, w, 48, "天气对咬钩的加成在抛竿时锁定；想等金色夕照，可在这里固定天气。世界空间仪表只是预览，PICO 仍须真机验收。", 15, Muted);
            y += 56;
            Label(x, y, w, 24, "恢复前先松开鼠标 · J 查看图鉴、委托与传说线索", 15, Mint);
            if (Button(new Rect(x, r.yMax - 70, w * 0.5f - 8, 46), "继续航行", true, true)) c.TogglePause();
            if (Button(new Rect(x + w * 0.5f + 8, r.yMax - 70, w * 0.5f - 8, 46), "退出游戏")) Application.Quit();
        }
    }
}
