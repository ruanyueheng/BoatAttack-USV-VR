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
        public static string TrialHint(int island) => island == 0 ? "按『星 → 风 → 潮』敲响三座风铃。错误会重新开始。"
            : island == 1 ? "让两侧的灯照亮幼苗，中央灯保持熄灭。到尽头的回响台确认。"
            : island == 2 ? "将货箱推进两格，压住铜色承重台，再转动水闸。"
            : "转动三个星盘，让朝向依次为『东 → 西 → 南』，再到回响台确认。";
        public static string TrialObjective(int island) => island == 0 ? "按归航铭文的顺序敲响风铃，让三种声音共鸣。"
            : island == 1 ? "为幼苗安排光与阴影，接通庭院中的光桥。"
            : island == 2 ? "将货箱送上承重台，再恢复旧机库的水闸。"
            : "解读日月星辰的铭文，让三个星盘指向归航之路。";
        public static string Inscription(int island) => island == 0 ? "星指引方向\n风扬起船帆\n潮迎你归港"
            : island == 1 ? "晨光与夕光，唤醒幼苗\n正午林荫，让万物歇息"
            : island == 2 ? "两格货程，落箱压台\n一次开闸，听潮归来"
            : "一望日出\n二送落日\n三寻南星";
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
        public FishingShrineTrial(int island)
        {
            if (island < 0 || island >= 4) throw new ArgumentOutOfRangeException(nameof(island));
            Island = island;
        }
        public int State(int device) => device >= 0 && device < 3 ? _states[device] : 0;
        public bool Ready => Island == 0 ? Progress == 3 : Island == 1 ? _states[0] == 1 && _states[1] == 0 && _states[2] == 1
            : Island == 2 ? _states[0] == 2 && _states[1] == 1 : _states[0] == 1 && _states[1] == 3 && _states[2] == 2;
        public bool Use(int device)
        {
            if (device < 0 || device >= (Island == 2 ? 2 : 3)) return false;
            if (Island == 0)
            {
                if (Ready) return false;
                int expected = Progress == 0 ? 2 : Progress == 1 ? 0 : 1;
                if (device != expected) { Progress = 0; Array.Clear(_states, 0, 3); return false; }
                _states[device] = 1; Progress++; return true;
            }
            if (Island == 2 && device == 0) _states[0] = (_states[0] + 1) % 3;
            else _states[device] = (_states[device] + 1) % (Island == 3 ? 4 : 2);
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
