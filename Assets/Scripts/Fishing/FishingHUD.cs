using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Desktop HUD. Active XR uses world instruments, never this screen-projected overlay.</summary>
    public sealed class FishingHUD : MonoBehaviour
    {
        public FishingPrototype Controller;
        private Font _font;
        private GUIStyle _label, _button;
        private bool _naturePage;
        private float _height, _scale, _offset;
        public static float LayoutScale(int width, int height) => Mathf.Max(0.01f, Mathf.Min(width / 1600f, height / 900f));
        private static readonly Color Ink = new Color(0.025f, 0.07f, 0.095f, 0.95f);
        private static readonly Color Muted = new Color(0.70f, 0.81f, 0.82f);
        private static readonly Color Mint = new Color(0.37f, 0.93f, 0.77f);
        private static readonly Color Gold = new Color(1f, 0.76f, 0.42f);
        private static readonly Color Danger = new Color(1f, 0.46f, 0.35f);
        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
        }
        private void OnGUI()
        {
            var c = Controller; if (c == null || c.Career == null || c.Player == null || XRSettings.isDeviceActive) return;
            _scale = LayoutScale(Screen.width, Screen.height); _height = Screen.height / _scale; _offset = (Screen.width - 1600 * _scale) * 0.5f;
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.TRS(new Vector3(_offset, 0, 0), Quaternion.identity, new Vector3(_scale, _scale, 1));
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { font = _font, wordWrap = true, padding = new RectOffset(0, 0, 0, 0) };
                _button = new GUIStyle(GUI.skin.button) { font = _font, fontSize = 19, normal = { textColor = Color.white } };
            }
            Panel(28, 24, 308, 91);
            Label(48, 33, 265, 43, "怪鱼航线", 30, Color.white, true);
            Label(49, 81, 266, 22, "TIDE & FIN   /   活海湾  0.3.1", 14, Mint);
            Panel(356, 24, 732, 91);
            DrawNavigation();
            Panel(1108, 24, 464, 91);
            Label(1130, 36, 230, 37, c.Career.Coins + "  金币", 27, Gold, true);
            Label(1372, 38, 190, 34, "鱼箱  " + c.Career.Count + " / 6", 23, Color.white, true);
            Label(1130, 82, 416, 22, "订单完成 " + c.Career.OrdersCompleted + "   ·   " + (c.Career.RodLevel == 1 ? "轻量鱼线 Lv.1" : "基础鱼线 · 港口可升级"), 14, Muted);
            if (c.VisibleMessage.Length > 0 || c.SaveWarning.Length > 0)
            {
                Panel(356, 128, 850, 58); Label(377, 139, 806, 44, c.SaveWarning.Length > 0 ? c.SaveWarning : c.VisibleMessage, 17, c.SaveWarning.Length > 0 ? Danger : Color.white);
            }
            Panel(28, 130, 308, 68);
            Label(47, 139, 280, 30, c.Anchored ? "已停船  /  捕捞工作站" : (c.Speed * 3.6f).ToString("0.0") + "  km/h  /  " + (c.ComfortMode ? "舒适航行" : "标准航行"), 21, Color.white, true);
            Label(48, 173, 265, 22, (c.FirstPerson ? "第一人称" : "第三人称") + "   C 切换  ·  右键环顾", 14, Muted);
            DrawClimate();
            if (c.ShowDetails)
            {
                DrawOrder();
                if (!c.Anchored) DrawMap();
                if (!c.Anchored)
                {
                    DrawWorldLabel(c.NavigationPosition + Vector3.up * 2.2f, c.NavigationName, Mint);
                }
            }
            DrawAction();
            if (c.ShowDetails)
            {
                Fill(0, _height - 35, 1600, 35, Ink);
                Label(30, _height - 29, 1540, 28, "W 前进  A/D 转向  S/Shift 刹车    E 停船/交易/抄鱼    左键 抛竿/提竿/收线    Q 起航    B 换饵    N 航点    J 图鉴    T 观察    U 升级    R 救援    H 简洁    Esc 设置", 14, Muted);
            }
            if (c.JournalOpen) DrawJournal();
            else if (c.Paused) DrawPause();
            GUI.matrix = matrix; GUI.color = Color.white;
        }
        private void DrawNavigation()
        {
            var c = Controller;
            Vector3 delta = c.NavigationPosition - c.Player.transform.position;
            float bearing = Mathf.DeltaAngle(c.Player.transform.eulerAngles.y, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg);
            string direction = Mathf.Abs(bearing) < 14 ? "前方" : bearing < 0 ? "左转 " + Mathf.Abs(bearing).ToString("0") + "°" : "右转 " + bearing.ToString("0") + "°";
            Label(378, 34, 665, 32, c.NavigationName + "   /   " + FishingPrototype.FlatDistance(c.Player.transform.position, c.NavigationPosition).ToString("0") + " m", 24, Color.white, true);
            Label(379, 80, 665, 23, direction + "     N 切换航点  ·  提示仅为方位，请沿可通行水道航行", 14, Mint);
        }
        private void DrawOrder()
        {
            var c = Controller; float x = 1268, y = 132;
            Panel(x, y, 304, 208);
            Label(x + 18, y + 15, 270, 29, "港口委托  /  " + FishingVoyage.OrderName(c.Career.OrdersCompleted), 19, Color.white, true);
            for (int i = 0; i < 3; i++)
            {
                int need = FishingVoyage.Required(c.Career.OrdersCompleted, (FishKind)i);
                string count = need == 0 ? "—" : Mathf.Min(need, c.Career.Owned((FishKind)i)) + " / " + need;
                Label(x + 20, y + 57 + i * 31, 245, 28, c.FishNames[i] + "                 " + count, 17, need > 0 && c.Career.Owned((FishKind)i) >= need ? Mint : Muted);
            }
            Label(x + 20, y + 157, 268, 44, c.Career.OrderReady ? "已集齐 · 返港 E 交付\n鱼价全额 + " + FishingVoyage.OrderBonus(c.Career.OrdersCompleted) + " 奖金" : "额外奖金 +" + FishingVoyage.OrderBonus(c.Career.OrdersCompleted) + "   ·   J 查看图鉴", 16, Gold);
        }
        private void DrawAction()
        {
            var c = Controller; var s = c.Session; float x = 386, y = _height - 214;
            Panel(x, y, 828, 164);
            string title, help;
            if (!c.Anchored)
            {
                title = c.AtHarbor ? (c.Career.OrderReady ? "订单已集齐  /  E 交付并领取奖金" : "潮汐渔港  /  E 出售鱼货") : c.SpotDistance <= 22 ? (c.Speed > 3 ? "进入渔区  /  先按住 Shift 减速" : "进入 " + c.SpotNames[c.Spot] + "  /  E 停船") : "探索海湾  /  驶向彩色航标";
                help = c.AtHarbor ? "U 升级鱼线（30 金币） · J 查看下一份订单与图鉴" : "一条 / 两条 / 三条横杠对应三处渔区；不必只靠颜色找路。";
            }
            else
            {
                switch (s.Phase)
                {
                    case CatchPhase.Waiting: title = "01  等待咬钩"; help = "留意浮标与声音；出现咬钩提示再点击左键。"; break;
                    case CatchPhase.Bite: title = "咬钩了！  /  点击左键提竿"; help = "剩余 " + s.Remaining.ToString("0.0") + " 秒 · 及时提竿进入拉锯"; break;
                    case CatchPhase.Fighting:
                        title = s.Tension > 0.78f ? "张力过高！  /  松开左键" : s.Beat == FishBeat.Rest ? "02  鱼在回气  /  按住左键收线" : s.Beat == FishBeat.Windup ? "02  鱼在蓄力  /  准备松开左键" : "02  鱼在冲刺  /  放线避开拉力";
                        help = "距离 " + s.Distance.ToString("0.0") + " m   ·   收线与放线交替；避免持续绷紧或松弛。"; break;
                    case CatchPhase.Landing: title = "03  抄鱼  /  指针进入中间区域时按 E"; help = "绿色中段就是有效区 · 剩余 " + s.Remaining.ToString("0.0") + " 秒"; break;
                    case CatchPhase.Caught: title = "捕获成功  /  已装入鱼箱与图鉴"; help = "点击左键继续 · B 换饵 · Q 起航返港"; break;
                    case CatchPhase.Escaped: title = "鱼逃走了  /  点击左键再试一次"; help = s.Failure; break;
                    default: title = c.SpotNames[c.Spot] + "  /  点击左键抛竿"; help = "B 切换鱼饵 · 咬钩后点击提竿 · Q 收竿起航"; break;
                }
            }
            Label(x + 22, y + 16, 784, 36, title, 25, s.Phase == CatchPhase.Bite ? Gold : Color.white, true);
            Label(x + 23, y + 59, 780, 34, help, 18, Muted);
            if (c.Anchored && s.Phase == CatchPhase.Fighting)
            {
                Label(x + 22, y + 105, 150, 26, "张力 " + (s.Tension * 100).ToString("0") + "%", 18, s.Tension > 0.78f ? Danger : Mint);
                Fill(x + 158, y + 115, 640, 12, new Color(0.17f, 0.27f, 0.3f)); Fill(x + 158, y + 115, 640 * s.Tension, 12, s.Tension > 0.78f ? Danger : Mint);
                Fill(x + 158 + 640 * 0.78f, y + 110, 2, 22, Color.white);
            }
            else if (c.Anchored && s.Phase == CatchPhase.Landing)
            {
                Fill(x + 23, y + 116, 778, 12, new Color(0.17f, 0.27f, 0.3f)); Fill(x + 23 + 778 * 0.25f, y + 116, 778 * 0.5f, 12, Mint);
                Fill(x + 23 + 778 * s.LandPointer - 3, y + 108, 6, 28, Color.white);
            }
            else Label(x + 23, y + 111, 780, 30, "B  " + FishingVoyage.BaitName(c.Bait) + "   /   " + FishingVoyage.BaitHelp(c.Bait), 17, Gold);
        }
        private void DrawMap()
        {
            var c = Controller; float y = _height - 264; Panel(28, y, 308, 214);
            Label(47, y + 14, 260, 30, "航海图  /  N 切换目标", 18, Color.white, true);
            Vector3 min = c.harbor, max = c.harbor; foreach (var p in c.fishingSpots) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); } min -= Vector3.one * 30; max += Vector3.one * 30;
            for (int i = 0; i < 4; i++) Fill(52 + i * 75, y + 58, 1, 133, new Color(0.11f, 0.23f, 0.26f));
            for (int i = 0; i < 3; i++) Fill(52, y + 58 + i * 60, 250, 1, new Color(0.11f, 0.23f, 0.26f));
            MapDot(c.harbor, min, max, y, "港", Mint);
            for (int i = 0; i < 3; i++) MapDot(c.fishingSpots[i], min, max, y, (i + 1).ToString(), c.NavigationTarget == i + 1 ? Mint : Gold);
            MapDot(c.Player.transform.position, min, max, y, "▲", Color.white);
        }
        private void MapDot(Vector3 p, Vector3 min, Vector3 max, float y, string text, Color color)
        {
            float x = 57 + Mathf.Clamp01((p.x - min.x) / (max.x - min.x)) * 238;
            float yy = y + 184 - Mathf.Clamp01((p.z - min.z) / (max.z - min.z)) * 113;
            Label(x - 8, yy - 12, 38, 30, text, 21, color, true);
        }
        private void DrawWorldLabel(Vector3 world, string text, Color color)
        {
            var s = Controller.viewCamera.WorldToScreenPoint(world); if (s.z <= 0) return;
            float x = (s.x - _offset) / _scale, y = (Screen.height - s.y) / _scale;
            if (x < 460 || x > 1140 || y < 215 || y > _height - 250) return;
            Panel(x - 100, y, 200, 35); Label(x - 87, y + 5, 176, 26, "◆ " + text, 17, color, true);
        }
        private void DrawJournal()
        {
            var c = Controller; float y = (_height - 624) / 2;
            Dim(); Panel(132, y, 1336, 624);
            Label(164, y + 26, 840, 46, "航海日志  /  鱼获与海湾伙伴", 30, Color.white, true);
            Label(165, y + 82, 850, 30, "已暂停 · 普通与珍珠变体分别收录，售出后不会丢失记录。", 18, Muted);
            if(GUI.Button(new Rect(1022,y+30,196,44),"鱼获图鉴",_button))_naturePage=false;
            if(GUI.Button(new Rect(1230,y+30,196,44),"自然观察",_button))_naturePage=true;
            if(_naturePage)
            {
                for(int i=0;i<5;i++)
                {
                    float row=y+138+i*76;bool seen=c.Career.Seen((MarineKind)i);
                    Fill(164,row,820,64,new Color(.055f,.13f,.16f));
                    Label(184,row+10,240,28,BayClimate.MarineName((MarineKind)i),22,seen?Mint:Color.white,true);
                    Label(422,row+9,535,27,BayClimate.MarineHint((MarineKind)i),17,Muted);
                    Label(184,row+39,700,24,seen?"已观察 · 记录永久保留":"未记录 · 28m 内看向它，按 T 观察",15,seen?Mint:Gold);
                }
            }
            else
            {
            for (int i = 0; i < 3; i++)
            {
                float row = y + 138 + i * 126; Fill(164, row, 820, 108, new Color(0.055f, 0.13f, 0.16f));
                Label(186, row + 17, 70, 65, "0" + (i + 1), 38, Gold, true);
                Label(264, row + 12, 700, 32, c.FishNames[i] + "   /   " + c.SpotNames[i], 22, Color.white, true);
                Label(264, row + 51, 700, 28, FishingVoyage.Habitat((FishKind)i), 17, Muted);
                Label(264, row + 80, 700, 25, (c.Career.Discovered((FishKind)i) ? "普通已收录" : "普通未发现") + "    ·    " + (c.Career.Discovered((FishKind)i, true) ? "珍珠已收录" : "珍珠未发现") + "    ·    鱼箱 " + c.Career.Owned((FishKind)i), 15, Mint);
            }
            }
            Label(1022, y + 137, 412, 34, FishingVoyage.OrderName(c.Career.OrdersCompleted), 27, Gold, true);
            Label(1022, y + 187, 410, 95, "集齐委托鱼货后返港按 E。\n优先交付普通鱼，保留多余鱼货。\n奖励 = 鱼货原价 + " + FishingVoyage.OrderBonus(c.Career.OrdersCompleted) + " 金币", 18, Color.white);
            Label(1022, y + 303, 410, 80, "累计捕获 " + c.Career.Catches + " 条\n完成委托 " + c.Career.OrdersCompleted + " 份", 23, Mint);
            Label(1022, y + 408, 410, 105, "珍珠饵更容易吸引稀有鱼，\n但需要更细心地控制张力。\n舒缓饵适合练习收放线。", 17, Muted);
            Label(165, y + 551, 800, 42, "自然观察不占鱼箱、不奖励金币；请保持距离，勿追赶野生伙伴。", 16, Muted);
            if (GUI.Button(new Rect(1174, y + 551, 252, 46), "J / Esc  返回海湾", _button)) c.ToggleJournal();
        }
        private void DrawPause()
        {
            var c=Controller;float y=(_height-710)/2;Dim();Panel(450,y,700,710);
            Label(484,y+25,630,44,"海湾设置 / 世界已暂停",29,Color.white,true);
            Label(484,y+77,630,44,"天气变化在继续航行后缓慢进行，不添加雷闪、镜头抖动或海浪推船。",17,Muted);
            if(GUI.Button(new Rect(484,y+133,632,46),"舒适航行："+(c.ComfortMode?"开启":"标准动力"),_button))c.ToggleComfort();
            if(GUI.Button(new Rect(484,y+188,632,46),"天气："+BayClimate.Name(c.Weather.Climate.Current)+"  /  点击切换下一种",_button))c.Weather.CyclePreview();
            if(GUI.Button(new Rect(484,y+243,632,46),"天气循环："+(c.Weather.Climate.Automatic?"自动":"固定")+"  /  点击切换",_button))
            {if(c.Weather.Climate.Automatic)c.Weather.Climate.Hold();else c.Weather.Climate.ResumeCycle();}
            if(GUI.Button(new Rect(484,y+298,632,46),"环境特效："+(c.Weather.ReducedEffects?"精简雨量":"标准雨量")+"（XR 自动精简）",_button))c.Weather.ToggleReducedEffects();
            if(GUI.Button(new Rect(484,y+353,632,46),"世界空间仪表预览："+(c.WorldInstruments?"开":"关"),_button))c.ToggleInstruments();
            if(GUI.Button(new Rect(484,y+408,632,46),"捕鱼 / 环境音："+(c.QuietMode?"静音":"开启"),_button))c.ToggleQuiet();
            Label(484,y+480,630,65,"天气对咬钩的加成在抛竿时锁定，不会中途改变已上钩的鱼。世界空间仪表是预览；PICO 仍须真机验收。",17,Muted);
            Label(484,y+558,630,48,"恢复时先松开鼠标。J 查看鱼获与自然观察记录。",17,Mint);
            if(GUI.Button(new Rect(484,y+634,300,46),"继续航行",_button))c.TogglePause();
            if(GUI.Button(new Rect(816,y+634,300,46),"退出游戏",_button))Application.Quit();
        }
        private void DrawClimate()
        {
            var c=Controller;
            if(!c.Paused)
            {
                if(GUI.Button(new Rect(28,210,95,40),"图鉴 J",_button))c.ToggleJournal();
                if(GUI.Button(new Rect(132,210,95,40),"设置",_button))c.TogglePause();
                if(GUI.Button(new Rect(236,210,100,40),"视角 C",_button))c.ToggleView();
            }
            if(!c.ShowDetails)return;
            var w=c.Weather.Climate;Panel(28,266,308,144);
            Label(46,279,272,30,BayClimate.Name(w.Current),22,Color.white,true);
            Label(46,318,272,28,c.FishNames[(int)BayClimate.Favored(w.Current)]+"活跃 · 咬钩更快 / 稀有 +4%",14,Mint);
            string forecast=w.Blend<1?"正在过渡 · "+(w.Blend*100).ToString("0")+"%":w.Automatic?"下一个："+BayClimate.Name(w.Forecast)+"  "+w.Remaining.ToString("0")+"s":"天气已固定 · 设置中恢复自动循环";
            Label(46,360,272,42,forecast,15,Muted);
            if(c.Ecology.Candidate.HasValue && !c.Paused)
            {
                if(GUI.Button(new Rect(28,422,308,44),"T 观察 · "+BayClimate.MarineName(c.Ecology.Candidate.Value),_button))c.Ecology.Observe();
            }
        }
        public static bool BlocksToolPointer(Vector2 screen,int width,int height)
        {
            float scale=LayoutScale(width,height);float offset=(width-1600*scale)*.5f;
            var p=new Vector2((screen.x-offset)/scale,(height-screen.y)/scale);
            return new Rect(28,210,308,256).Contains(p);
        }
        private void Dim() => Fill(-_offset / _scale, 0, Screen.width / _scale, _height, new Color(0.01f, 0.025f, 0.04f, 0.83f));
        private void Panel(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Ink, 0, 10);
            Fill(x + 18, y + 9, 28, 2, Mint);
        }
        private static void Fill(float x, float y, float width, float height, Color color)
        { GUI.color = color; GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture); GUI.color = Color.white; }
        private void Label(float x, float y, float width, float height, string text, int size, Color color, bool bold = false)
        { _label.fontSize = size; _label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; _label.normal.textColor = color; GUI.Label(new Rect(x, y, width, height), text, _label); }
        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
