using System;

namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class LoreData
    {
        public int residents, shrines;
        public bool finale;
    }

    public sealed class FishingLore
    {
        public const int ResidentCount = 6;
        private LoreData _data = new LoreData();
        public static string ShrineName(int island) => island == 0 ? "风之回廊" : island == 1 ? "森光庭院" : island == 2 ? "沉潮机库" : "星轨圣所";
        public static string Subtitle(int island) => island == 0 ? "听风辨路 · 黄铜与海盐的故乡" : island == 1 ? "林海共生 · 守护潮林的微光"
            : island == 2 ? "潮起潮落 · 旧航船长眠于此" : "循星归航 · 守望者最后的灯火";
        public static string ResidentName(int id) => id == 0 ? "铃音" : id == 1 ? "青禾" : id == 2 ? "阿砾" : id == 3 ? "望星" : id == 4 ? "汐禾" : "老舵手·林舟";
        public static string ResidentRole(int id) => id == 0 ? "风铃修缮师" : id == 1 ? "潮林守护人" : id == 2 ? "沉船打捞员" : id == 3 ? "星象记录者" : id == 4 ? "潮汐钓具店店员" : "渔港领航员";
        public static string Echo(int island) => island == 0 ? "风之回响：航路不是画在海上的线，而是人们愿意等待彼此的约定。"
            : island == 1 ? "林之回响：守望者留出一片阴影，让幼苗和归来的渔人都有地方歇脚。"
            : island == 2 ? "潮之回响：风暴之夜，旧船长放下货物，把最后的舱位留给岛上的孩子。"
            : "星之回响：四灯从未为了寻找财宝而建。它们只为回答一句话：回家的人，走哪条路？";
        public static string TrialHint(int island) => island == 0 ? "听清星、风、潮的示范，依次走入对应的共鸣台。风桥会回应脚步；每段可停留30秒。"
            : island == 1 ? "沿栈道转动反光镜，把日井的光一段段接到种池。保持照亮5秒，活根便会跨过水隙。"
            : island == 2 ? "先沿侧舷开阀排水，再下船腹按住E推货并随货箱前进。箱到铜台后返回注水，浮桥将接上船长室。"
            : "登上螺旋台阶操作望远镜，移动鼠标或方向键寻找星图轮廓，按住左键对焦。完成海鸥、船锚与归航星三次观测。";
        public static string TrialObjective(int island) => island == 0 ? "循声踩响共鸣台，跨越风桥，登上巨钟瞭望台。"
            : island == 1 ? "接通折线光路，让种池长出通往秘林的活根桥。"
            : island == 2 ? "排水、下舱推货、返回注水，经浮桥进入船长室。"
            : "在望远镜中寻找三幅星图，并保持对焦完成观测。";
        public static string Inscription(int island) => island == 0 ? "星落脚下，风送前路\n循潮而上，巨钟归航"
            : island == 1 ? "日井入镜，折光穿林\n五息不散，活根成桥"
            : island == 2 ? "先退潮，再下舱\n推货上铜台，回阀迎归潮"
            : "拾级望星\n认清海鸥、船锚与归航星";
        public bool Met(int id) => id >= 0 && id < ResidentCount && (_data.residents & (1 << id)) != 0;
        public bool Cleared(int island) => island >= 0 && island < 4 && (_data.shrines & (1 << island)) != 0;
        public int ClearCount { get { int n = 0; for (int i = 0; i < 4; i++) if (Cleared(i)) n++; return n; } }
        public bool Finale => _data.finale;
        public bool Meet(int id)
        {
            if (id < 0 || id >= ResidentCount || Met(id)) return false;
            _data.residents |= 1 << id; return true;
        }
        public int Clear(int island)
        {
            if (island < 0 || island >= 4 || Cleared(island)) return 0;
            _data.shrines |= 1 << island; return 90;
        }
        public int Finish()
        {
            if (_data.shrines != 15 || _data.finale) return 0;
            _data.finale = true; return 180;
        }
        public LoreData Snapshot() => new LoreData { residents = _data.residents, shrines = _data.shrines, finale = _data.finale };
        public static bool TryRestore(LoreData data, out FishingLore lore)
        {
            lore = null;
            if (data == null || data.residents < 0 || data.residents > 63 || data.shrines < 0 || data.shrines > 15 || (data.finale && data.shrines != 15)) return false;
            lore = new FishingLore { _data = new LoreData { residents = data.residents, shrines = data.shrines, finale = data.finale } }; return true;
        }
    }

    /// <summary>Session puzzle rules; unfinished rooms reset on exit, paid completion lives in FishingLore.</summary>
    public sealed class FishingShrineTrial
    {
        public readonly int Island;
        private readonly int[] _states = new int[3];
        public int Progress { get; private set; }
        private float _clock,_lastBell=-100;
        private float _cargo,_starFocus;
        private int _starStage;
        public float Growth { get; private set; }
        public float WaterLevel { get; private set; }=1;
        public float CargoProgress=>_cargo;
        public int StarStage=>_starStage;
        public float StarFocus=>_starFocus;
        public float StarYaw { get; private set; }=-30;
        public float StarPitch { get; private set; }=25;
        public static float StarTargetYaw(int stage)=>stage==0?-50:stage==1?12:47;
        public static float StarTargetPitch(int stage)=>stage==0?27:stage==1?52:34;
        public bool LightAligned=>_states[0]==1 && _states[1]==2 && _states[2]==3;
        public void Tick(float delta)
        {
            if(float.IsNaN(delta) || float.IsInfinity(delta) || delta<=0)return;
            _clock+=delta;
            if(Island==0 && Progress>0 && !Ready && _clock-_lastBell>30){Progress=0;Array.Clear(_states,0,3);}
            if(Island==1)Growth=Math.Max(0,Math.Min(1,Growth+(LightAligned?delta/5:-delta/3)));
            if(Island==2 && _states[1]!=0)WaterLevel=Math.Max(0,Math.Min(1,WaterLevel+(_states[1]==1?-delta:delta)/4));
        }
        public FishingShrineTrial(int island)
        {
            if (island < 0 || island >= 4) throw new ArgumentOutOfRangeException(nameof(island));
            Island = island;
        }
        public int State(int device) => device >= 0 && device < 3 ? _states[device] : 0;
        public bool Ready => Island == 0 ? Progress == 3 : Island == 1 ? Growth>=1
            : Island == 2 ? _cargo>=1 && WaterLevel>=.95f : _starStage>=3;
        public bool Use(int device)
        {
            if (device < 0 || device >= (Island == 2 ? 2 : 3)) return false;
            if (Island == 0)
            {
                if (Ready) return false;
                int expected = Progress == 0 ? 2 : Progress == 1 ? 0 : 1;
                if (device != expected || (Progress>0 && (_clock-_lastBell<.4f || _clock-_lastBell>30))) { Progress = 0; Array.Clear(_states, 0, 3); return false; }
                _states[device] = 1; Progress++;_lastBell=_clock;return true;
            }
            if(Island==2)
            {
                if(device==0)return WaterLevel<=.15f && _cargo<1;
                else _states[1]=(_states[1]+1)%3;
            }
            else if(Island==1)
            {
                _states[device]=(_states[device]+1)%4;
            }
            else return false;
            return true;
        }
        public bool PushCargo(float seconds)
        {
            if(Island!=2 || WaterLevel>.15f || _cargo>=1 || !Finite(seconds) || seconds<=0 || seconds>.15f)return false;
            _cargo=Math.Min(1,_cargo+seconds/7f);_states[0]=Math.Min(2,(int)(_cargo*2+.0001f));return true;
        }
        public bool Observe(float yaw,float pitch,float seconds,bool focusing)
        {
            if(Island!=3 || _starStage>=3)return false;
            if(!Finite(yaw) || !Finite(pitch) || !Finite(seconds) || seconds<=0 || seconds>.15f){_starFocus=0;return false;}
            StarYaw=Math.Max(-75,Math.Min(75,yaw));StarPitch=Math.Max(10,Math.Min(70,pitch));
            float dx=StarYaw-StarTargetYaw(_starStage),dy=StarPitch-StarTargetPitch(_starStage);
            _starFocus=focusing && dx*dx+dy*dy<9?Math.Min(1,_starFocus+seconds/1.4f):0;
            if(_starFocus<1)return false;
            _starStage++;_starFocus=0;return true;
        }
        public void ReleaseFocus()=>_starFocus=0;
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed partial class FishingCareer
    {
        public FishingLore Lore { get; private set; } = new FishingLore();
        public int CompleteShrine(int island)
        {
            if (Coins > 100000000 - 90) return 0;
            int pay = Lore.Clear(island); Coins += pay; return pay;
        }
        public int CompleteEchoVoyage()
        {
            if (Coins > 100000000 - 180) return 0;
            int pay = Lore.Finish(); Coins += pay; return pay;
        }
    }
}
