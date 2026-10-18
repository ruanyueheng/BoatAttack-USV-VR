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
        public static string TrialHint(int island) => island == 0 ? "听取星、风、潮的示范，再以留有间隔的节奏敲响。连按或停顿超过8秒会重新开始。"
            : island == 1 ? "转动三面反光镜，使光束分别指向东、南、西。保持照亮幼苗5秒，光桥才会生长。"
            : island == 2 ? "先开闸排水，水位降到底后将货箱推进两格；再切到注水，让浮桥升起。"
            : "每个星盘会同时带动下一个。使三盘朝向东、西、南；从全北开始可转一盘一次、二盘两次。";
        public static string TrialObjective(int island) => island == 0 ? "听取风铃示范，以星、风、潮的顺序和间隔回应。"
            : island == 1 ? "转动反光镜引导光束，持续照亮幼苗，使光桥生长。"
            : island == 2 ? "先排水推动货箱，再注水抬升浮桥。"
            : "解开相邻星盘的联动，使三盘指向东、西、南。";
        public static string Inscription(int island) => island == 0 ? "听星，听风，再听潮\n留一拍，等风回应\n八息之后，重奏归航"
            : island == 1 ? "东光入林，南光养叶，西光归根\n五息不散，幼苗成桥"
            : island == 2 ? "退潮推货，箱落铜台\n再引海水，浮桥自来"
            : "三环相牵，转此亦转彼\n一望日出，二送落日，三寻南星";
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
        public float Growth { get; private set; }
        public float WaterLevel { get; private set; }=1;
        public bool LightAligned=>_states[0]==1 && _states[1]==2 && _states[2]==3;
        public void Tick(float delta)
        {
            if(float.IsNaN(delta) || float.IsInfinity(delta) || delta<=0)return;
            _clock+=delta;
            if(Island==0 && Progress>0 && !Ready && _clock-_lastBell>8){Progress=0;Array.Clear(_states,0,3);}
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
            : Island == 2 ? _states[0] == 2 && _states[1] == 2 && WaterLevel>=.95f : _states[0] == 1 && _states[1] == 3 && _states[2] == 2;
        public bool Use(int device)
        {
            if (device < 0 || device >= (Island == 2 ? 2 : 3)) return false;
            if (Island == 0)
            {
                if (Ready) return false;
                int expected = Progress == 0 ? 2 : Progress == 1 ? 0 : 1;
                if (device != expected || (Progress>0 && (_clock-_lastBell<.4f || _clock-_lastBell>8))) { Progress = 0; Array.Clear(_states, 0, 3); return false; }
                _states[device] = 1; Progress++;_lastBell=_clock;return true;
            }
            if(Island==2)
            {
                if(device==0){if(WaterLevel>.15f || _states[0]>=2)return false;_states[0]++;}
                else _states[1]=(_states[1]+1)%3;
            }
            else
            {
                _states[device]=(_states[device]+1)%4;
                if(Island==3)_states[(device+1)%3]=(_states[(device+1)%3]+1)%4;
            }
            return true;
        }
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
