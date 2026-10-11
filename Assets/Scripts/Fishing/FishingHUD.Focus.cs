using System.Globalization;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void Glass(Rect r) { Rounded(r,new Color(.022f,.046f,.08f,.84f),7);Outline(r,Gold*new Color(1,1,1,.22f),7); }

        private void DrawCompactHeading()
        {
            var c = Controller;
            var r = Layout.Heading;
            Glass(r);
            WeatherIcon(new Rect(r.x + 12, r.y + 15, 32, 32), c.Weather.Climate.Current);
            string title = c.Ashore ? c.Shore.LocationName : c.Anchored ? FishingGrounds.Name(c.Spot) : c.NavigationName + "  " + FishingPrototype.FlatDistance(c.ExplorerPosition, c.NavigationPosition).ToString("0") + " m";
            Label(r.x + 56, r.y + 9, 262, 27, title, 19, Color.white, true);
            Label(r.x + 56, r.y + 36, 262, 20, c.Career.Calendar.SeasonName+" "+c.Career.Calendar.Date+"日  "+c.Career.Calendar.TimeLabel+" · "+BayClimate.Name(c.Weather.Climate.Current), 13, Muted);
            if (c.Ecology.Candidate.HasValue && !CatchVisible && Button(Layout.Observe, "T 观察 · " + BayClimate.MarineName(c.Ecology.Candidate.Value), true, false, 14)) c.Ecology.Observe();
        }

        private void DrawPocket()
        {
            var c = Controller.Career;
            var r = Layout.Pocket;
            Glass(r);
            Label(new Rect(r.x + 14, r.y, 132, r.height), c.Coins.ToString("N0", CultureInfo.InvariantCulture) + " 金币", 16, Gold, true, TextAnchor.MiddleLeft);
            Fill(r.x + 148, r.y + 12, 1, 16, Hairline);
            Label(new Rect(r.x + 165, r.y, 128, r.height), "鱼箱  " + c.Count + " / " + c.Capacity, 16, c.Count == c.Capacity ? Danger : Color.white, false, TextAnchor.MiddleLeft);
        }

        private void DrawCompactToolbar()
        {
            var r = Layout.Toolbar;
            Glass(r);
            if (TextButton(new Rect(r.x, r.y, 76, r.height), "J 日志")) Controller.ToggleJournal();
            if (TextButton(new Rect(r.x + 86, r.y, 76, r.height), "C 视角")) Controller.ToggleView();
            if (TextButton(new Rect(r.x + 172, r.y, 76, r.height), Controller.ShowDetails ? "H 收起" : "H 详情")) Controller.ToggleDetails();
            if (TextButton(new Rect(r.x + 258, r.y, 78, r.height), "Esc 设置")) Controller.TogglePause();
        }
        private bool TextButton(Rect r,string text)
        {
            Label(r,text,14,r.Contains(Event.current.mousePosition)?Mint:Muted,false,TextAnchor.MiddleCenter);
            return GUI.Button(r,GUIContent.none,GUIStyle.none);
        }

        private void DrawFocusedAction()
        {
            var c = Controller;
            var s = c.Session;
            var r = Layout.Action;
            Glass(r);
            string title, help;
            Color accent = Mint;
            if(c.HasPendingCatch)
            {
                title="待处理鱼获 · "+FishingSpecies.Name(c.Career.PendingCatch.fish.kind)+" "+FishingText.Weight(c.Career.PendingCatch.fish.grams);
                help=c.CatchRig.ReleaseConfirmation?"再次按 X 确认放生；普通鱼不增加金币":"E 拿起/放回 · F 入箱 · X 放生确认 · R 安全返港";accent=Gold;
            }
            else if(c.Ashore)
            {
                title=(c.Expedition.Aligning?c.Expedition.ActionTitle:c.WorldInteractionTitle) ?? c.Shore.ActionTitle;
                help=c.Shore.Swimming?"Shift加速 · 停步恢复体力 · 空格上低岸 · R救援":c.Shore.Climbing?"W / S上下攀登 · 空格离开 · 停步恢复体力":c.Realms.Inside?"E操作机关 / 回响台 · 入口E返回岛屿 · J世界航记":c.Expedition.Aligning?c.Expedition.AlignmentHint:c.Islands.NearbyHint!=null?"E 读信 / 开箱 / 转盘 · F 点灯 · J 群岛航记 · G 上船":"空格跳跃 · Ctrl蹲下 · E交谈 / 采集 · T观察 · I背包 · R救援";
            }
            else if (!c.Anchored)
            {
                if (c.AtHarbor)
                {
                    Label(r.x + 18, r.y + 10, r.width - 175, 25, "潮汐渔港 · E 快速交付 / 出售", 18, Color.white, true);
                    Label(r.x + 18, r.y + 42, r.width - 175, 22, "G 靠岸下船· Tab 升级 · E 提交调查", 14, Muted);
                    if (Button(Layout.HarborButton, c.HarborDeskOpen ? "Tab 收起" : "Tab 交易台", true, c.HarborDeskOpen, 15)) c.ToggleHarborDesk();
                    return;
                }
                bool near = c.SpotDistance <= 22f;
                title = near ? (c.Speed > 3f ? "按住 Shift 减速，再按 E 停船" : "进入 " + FishingGrounds.Name(c.Spot) + " · E 停船") : "驶向彩色航标，靠近后按 E 停船";
                if(near && !FishingGrounds.Available(c.Spot,c.Career.Expedition))title="北岛信标渔场 · 修复旧港信标后开放";
                if(c.Shore.BerthDistance<18 || c.NavigationTarget>c.fishingSpots.Length)
                {title=c.Expedition.ActionTitle ?? c.Shore.ActionTitle;help="Shift 刹车至 5.4 km/h 以下 · S 倒航 · N 航点 · M 地图";}
                else help = c.Shore.ActionTitle;
            }
            else
            {
                switch (s.Phase)
                {
                    case CatchPhase.Waiting: title = c.CastMotion.Casting?"浮标飞向海面":"等待咬钩…"; help = c.CastMotion.Casting?"鱼线正在放出 · 落水后开始等待":"留意浮标与声音，出现咬钩提示再点击左键"; break;
                    case CatchPhase.Bite: title = (s.Species.Legendary ? "金帆旗鱼！" : "咬钩了！") + "点击左键提竿"; help = "剩余 " + s.Remaining.ToString("0.0") + " 秒"; accent = Gold; break;
                    case CatchPhase.Fighting: title = FightTitle(s); help = ""; accent = s.Beat == FishBeat.Leap || s.Tension > .78f ? Danger : Mint; break;
                    case CatchPhase.Landing: title = "E 辅助抄网 · 套住鱼后轻抬"; help = "XR：左握键拿网；网口覆盖鱼体，保持片刻再轻抬"; break;
                    case CatchPhase.Caught: title = "捕获成功 · 点击左键再抛一竿"; help = "B 换饵 · Q 起航 · J 查看图鉴与纪录"; break;
                    case CatchPhase.Escaped: title = "鱼逃走了 · 点击左键重试"; help = "B 换饵 · Q 起航"; break;
                    default: title = c.CastMotion.Preparing?"松开左键甩竿 · 蓄力 "+(c.CastMotion.Charge*100).ToString("0")+"%":"按住左键后摆，松开甩竿"; help = "轻点近抛 · 按住远抛 · B 换饵 · Q 起航"; break;
                }
            }
            if(c.CastMotion.Preparing)
            {title="松开左键甩竿 · 蓄力 "+(c.CastMotion.Charge*100).ToString("0")+"%";help="轻点近抛 · 按住远抛 · B 换饵 · Q 起航";}
            Rounded(new Rect(r.x + 8, r.y + 15, 3, r.height - 30), accent, 1.5f);
            Label(r.x + 20, r.y + 12, r.width - 40, 28, title, 20, accent, true);
            if(c.CastMotion.Preparing)Bar(new Rect(r.x+20,r.yMax-5,r.width-40,2),c.CastMotion.Charge,Mint);
            if (c.Anchored && s.Phase == CatchPhase.Fighting) DrawCompactFight(r, s, accent);
            else if (c.Anchored && s.Phase == CatchPhase.Landing)
            {
                Label(r.x + 20, r.y + 43, r.width - 40, 20, help, 14, Muted);
                Label(r.x+20,r.y+77,r.width-40,20,"捕获后不会自动入箱：可拿起观察、入箱或放生。",13,Faint);
            }
            else
            {
                Label(r.x + 20, r.y + 44, r.width - 40, 22, help, 14, Muted);
                if (c.Anchored && s.Phase == CatchPhase.Bite) Bar(new Rect(r.x + 20, r.yMax - 7, r.width - 40, 3), s.Remaining / 2.5f, Gold);
            }
        }

        private void DrawCompactFight(Rect r, CatchSession s, Color accent)
        {
            Label(r.x + 20, r.y + 47, 300, 22, FishingSpecies.Name(s.Kind) + " · 张力 " + (s.Tension * 100).ToString("0") + "%", 15, Color.white);
            var bar = new Rect(r.x + 20, r.y + 80, 344, 9);
            Bar(bar, s.Tension, accent);
            Fill(bar.x + bar.width * .78f, bar.y - 4, 1, 17, Muted);
            Label(new Rect(r.x + 390, r.y + 47, 218, 22), "余线 " + s.Distance.ToString("0.0") + " m", 15, Muted, false, TextAnchor.UpperRight);
            Bar(new Rect(r.x + 394, r.y + 80, 214, 9), 1 - s.Distance / Mathf.Max(.01f, s.MaxDistance), Sky);
        }

        private void DrawCatchToast()
        {
            if (!CatchVisible) return;
            var card = Controller.LastCatch;
            float age = CatchAge;
            _fade = Mathf.Min(Mathf.Clamp01(age / .2f), Mathf.Clamp01((CardSeconds - age) / .5f));
            var r = Layout.Catch;
            var fish = card.Fish;
            Glass(r);
            FishGlyph(new Rect(r.x + 12, r.y + 26, 76, 48), fish.kind, fish.rare);
            Label(r.x + 102, r.y + 12, 236, 27, FishingSpecies.Name(fish.kind) + "  " + FishingText.Weight(fish.grams), 18, Color.white, true);
            string result = FishingSpecies.Of(fish.kind).Legendary ? "已放生 · 奖励 +" + card.Bounty : "已入箱 · 价值 " + fish.value + " 金币";
            Label(r.x + 102, r.y + 43, 236, 23, result, 14, Gold);
            Label(r.x + 102, r.y + 69, 236, 20, card.Discovery ? "图鉴新发现" : card.PersonalBest ? "刷新个人纪录" : fish.rare ? "珍珠变体" : "J 查看鱼获图鉴", 13, Mint);
            _fade = 1f;
        }

        // Suppress only the duplicate catch copy; later actions must still get feedback.
        internal string NoticeText => Controller.SaveWarning.Length > 0 ? Controller.SaveWarning
            : CatchVisible && Controller.MessageIsCatch ? string.Empty : Controller.VisibleMessage;

        private void DrawCompactNotice()
        {
            var c = Controller;
            bool warning = c.SaveWarning.Length > 0;
            string text = NoticeText;
            if (text.Length == 0) return;
            var r = Layout.Notice;
            if(c.Acceptance!=null)r.y=102;
            Glass(r);
            Label(new Rect(r.x + 14, r.y + 3, r.width - 28, r.height - 6), text, 14, warning ? Danger : Muted, false, TextAnchor.MiddleCenter);
        }
    }
}
