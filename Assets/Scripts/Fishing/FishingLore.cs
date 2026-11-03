using System;

namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class LoreData
    {
        public int residents, shrines;
        public bool finale;
        public bool mayorAccepted, chartFound, grandTreasureFound;
        public int rewardedShrines;
        // v20: 2-bit stage checkpoint per realm, 12 echo-shell bits, 2-bit (variant+1) per realm.
        public int realmStages, realmShells, realmVariants;
        // Optional four-bit masks keep replay checkpoints separate from historical completion and payment.
        public int realmReplay, realmShellRewards;
        // v20 (V044 sea events): bit layout in FishingSeaLetters.
        public int seaLog;
        // v20: tideLog bit0-11 low-tide finds; keeperLog bit0-5 boats home, bit6-11 letters read, bit12-21 nights kept.
        public int tideLog, keeperLog;
        // v20: photo codex bits 0-23 (layout in FishingPhotoCodex).
        public int photoLog;
        // v20: seaPost bit0-11 souvenirs/replies, bit12-14 pending note+1; seaPostDay = release day while pending.
        public int seaPost, seaPostDay;
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
        public static string TrialHint(int island) => island == 0 ? "钟声在石台间来回，第二段回声比第一段更长。"
            : island == 1 ? "林中有两处渴光的印记，旧铜闸后还藏着另一条光路。"
            : island == 2 ? "船腹的旧吃水线下，铜台压痕与浮桥缆绳遥相呼应。"
            : "残页上的轮廓，仍在夜空中等待归航人。";
        /// <summary>Echo-shell keepsakes: one small moment from each realm's old lamp keeper (index island*3+shell).</summary>
        private static readonly string[] ShellLines = {
            "阿铃的铃舌上缠着红线，是女儿出海前系上的。",
            "栏杆上刻满“正”字，每一笔都是一个等人回港的夜。",
            "半张乐谱上写：“摇慢一点，远处的人才听得清。”",
            "树根边埋着陶罐，装着第一年落下的红树种子。",
            "苔石上有个歪掌印，旁边写：“明年再来量量你。”",
            "灯光一照，树影里浮出字：“我看不到你长大，但替你挡过风。”",
            "老船长的烟斗卡在舱壁缝里，烟丝早就潮了。",
            "货单最后一行改成：“孩子们 · 七人 · 都在。”",
            "铜台边有颗小纽扣，是那夜某个孩子上船时掉的。",
            "星图角落画着戴歪帽的小人，旁边写着“你”。",
            "望远镜罩里夹着一张没用过的船票：回潮村→远方。",
            "夜里星更亮。贝壳背面写：“灯要有人守，我就留下了。”" };
        public static string ShellLine(int island, int shell) => island >= 0 && island < 4 && shell >= 0 && shell < 3 ? ShellLines[island * 3 + shell] : "";
        public static string TrialObjective(int island) => island == 0 ? "听巨钟留下的两段回声。"
            : island == 1 ? "寻找林中失落的两束光。"
            : island == 2 ? "唤醒沉船里不再升降的通路。"
            : "找回守望者记录的三幅星图。";
        public static string Inscription(int island) => island == 0 ? "星落脚下，风送前路\n循潮而上，巨钟归航"
            : island == 1 ? "日井入镜，折光穿林\n五息不散，活根成桥"
            : island == 2 ? "先退潮，再下舱\n推货上铜台，回阀迎归潮"
            : "拾级望星\n认清海鸥、船锚与归航星";
        public bool Met(int id) => id >= 0 && id < ResidentCount && (_data.residents & (1 << id)) != 0;
        public bool Replaying(int island) => island >= 0 && island < 4 && (_data.realmReplay & (1 << island)) != 0;
        public bool Cleared(int island) => island >= 0 && island < 4 && (_data.shrines & (1 << island)) != 0 && !Replaying(island);
        public int ClearCount { get { int n = 0; for (int i = 0; i < 4; i++) if (Cleared(i)) n++; return n; } }
        public bool Finale => _data.finale;
        public bool MayorAccepted => _data.mayorAccepted;
        public bool ChartFound => _data.chartFound;
        public bool GrandTreasureFound => _data.grandTreasureFound;
        public int RewardedShrines => _data.rewardedShrines;
        public bool CanOpenGrandTreasure => ChartFound && _data.shrines == 15 && RewardedShrines == 15 && !GrandTreasureFound;
        public static FieldItem Relic(int island) => island >= 0 && island < 4 ? (FieldItem)((int)FieldItem.WindRelic + island) : FieldItem.Empty;
        public static string BlueprintName(int island) => island == 0 ? "精纺工艺蓝图" : island == 1 ? "兔舍蓝图" : island == 2 ? "水族箱蓝图" : island == 3 ? "水族饲育蓝图" : "";
        public static string BlueprintEffect(int island) => island == 0 ? "绕绳每轮纤维×3 → 绳索×2" : island == 1 ? "住所可扩建至8格，需250币" : island == 2 ? "水族箱可扩建至8格，需300币" : island == 3 ? "展示鱼成熟改为2日与2次喂养" : "";
        public bool RewardClaimed(int island) => island >= 0 && island < 4 && (RewardedShrines & (1 << island)) != 0;
        public bool BlueprintUnlocked(int island) => RewardClaimed(island);
        public bool AcceptMayorship()
        {
            if (MayorAccepted) return false;
            _data.mayorAccepted = true; return true;
        }
        public bool DiscoverChart()
        {
            if (!MayorAccepted || ChartFound) return false;
            _data.chartFound = true; return true;
        }
        public static int MaxStage(int island) => island == 3 ? 3 : island == 2 ? 1 : 2;
        public int RealmStage(int island) => Cleared(island) ? MaxStage(island) : (_data.realmStages >> (island * 2)) & 3;
        public bool SaveRealmStage(int island, int stage)
        {
            if (island < 0 || island >= 4 || stage <= RealmStage(island) || stage > MaxStage(island)) return false;
            _data.realmStages = (_data.realmStages & ~(3 << (island * 2))) | (stage << (island * 2)); return true;
        }
        public bool ShellFound(int island, int shell) => (_data.realmShells & (1 << (island * 3 + shell))) != 0;
        public int ShellCount(int island) { int n = 0; for (int i = 0; i < 3; i++) if (ShellFound(island, i)) n++; return n; }
        public bool FindShell(int island, int shell)
        {
            if (island < 0 || island >= 4 || shell < 0 || shell >= 3 || ShellFound(island, shell)) return false;
            _data.realmShells |= 1 << (island * 3 + shell); return true;
        }
        public bool TryClaimShellReward(int island)
        {
            if(island<0 || island>=4 || ShellCount(island)!=3 || (_data.realmShellRewards & (1<<island))!=0)return false;
            _data.realmShellRewards|=1<<island;return true;
        }
        public bool ResetRealm(int island)
        {
            if(island<0 || island>=4)return false;
            // Older saves have no reward flag; a complete shell set was already settled before this reset.
            if(ShellCount(island)==3)_data.realmShellRewards|=1<<island;
            _data.realmStages&=~(3<<(island*2));_data.realmShells&=~(7<<(island*3));_data.realmVariants&=~(3<<(island*2));
            if((_data.shrines&(1<<island))!=0)_data.realmReplay|=1<<island;
            return true;
        }
        /// <summary>Per-save puzzle variant, chosen once on first entry so revisits stay consistent.</summary>
        public int RealmVariant(int island, Func<int> choose)
        {
            int stored = (_data.realmVariants >> (island * 2)) & 3;
            if (stored == 0) { stored = Math.Max(0, Math.Min(2, choose())) + 1; _data.realmVariants |= stored << (island * 2); }
            return stored - 1;
        }
        public bool TideFlag(int bit) => bit >= 0 && bit < 12 && (_data.tideLog & (1 << bit)) != 0;
        public bool SetTideFlag(int bit)
        {
            if (bit < 0 || bit >= 12 || TideFlag(bit)) return false;
            _data.tideLog |= 1 << bit; return true;
        }
        public bool KeeperFlag(int bit) => bit >= 0 && bit < 12 && (_data.keeperLog & (1 << bit)) != 0;
        public bool SetKeeperFlag(int bit)
        {
            if (bit < 0 || bit >= 12 || KeeperFlag(bit)) return false;
            _data.keeperLog |= 1 << bit; return true;
        }
        public int KeeperNights => (_data.keeperLog >> 12) & 1023;
        public void AddKeeperNight() { if (KeeperNights < 1023) _data.keeperLog += 1 << 12; }
        public bool PhotoFlag(int bit) => bit >= 0 && bit < 24 && (_data.photoLog & (1 << bit)) != 0;
        public bool SetPhotoFlag(int bit)
        {
            if (bit < 0 || bit >= 24 || PhotoFlag(bit)) return false;
            _data.photoLog |= 1 << bit; return true;
        }
        public int SeaPost => _data.seaPost;
        public int SeaPostDay => _data.seaPostDay;
        public bool SetSeaPost(int post, int day)
        {
            if (!ValidSeaPost(post, day)) return false;
            _data.seaPost = post; _data.seaPostDay = day; return true;
        }
        private static bool ValidSeaPost(int post, int day) => post >= 0 && post <= 32767 && ((post >> 12) & 7) <= 4
            && day >= 0 && day <= FishingCalendar.MaxDay && (((post >> 12) & 7) != 0 || day == 0);
        public bool SeaFlag(int bit) => bit >= 0 && bit < 21 && (_data.seaLog & (1 << bit)) != 0;
        public bool SetSeaFlag(int bit)
        {
            if (bit < 0 || bit >= 21 || SeaFlag(bit)) return false;
            _data.seaLog |= 1 << bit; return true;
        }
        internal void MarkRewardClaimed(int island) => _data.rewardedShrines |= 1 << island;
        internal void OpenGrandTreasure() => _data.grandTreasureFound = true;
        internal static bool QuestPristine(LoreData data) => data == null || (!data.mayorAccepted && !data.chartFound && !data.grandTreasureFound && data.rewardedShrines == 0);
        public bool Meet(int id)
        {
            if (id < 0 || id >= ResidentCount || Met(id)) return false;
            _data.residents |= 1 << id; return true;
        }
        public int Clear(int island)
        {
            if (island < 0 || island >= 4 || Cleared(island)) return 0;
            if(Replaying(island)){_data.realmReplay&=~(1<<island);return 0;}
            _data.shrines |= 1 << island; return 90;
        }
        public int Finish()
        {
            if (_data.shrines != 15 || _data.finale) return 0;
            _data.finale = true; return 180;
        }
        public LoreData Snapshot() => new LoreData { residents = _data.residents, shrines = _data.shrines, finale = _data.finale,
            mayorAccepted = MayorAccepted, chartFound = ChartFound, grandTreasureFound = GrandTreasureFound, rewardedShrines = RewardedShrines,
            realmStages = _data.realmStages, realmShells = _data.realmShells, realmVariants = _data.realmVariants, realmReplay = _data.realmReplay, realmShellRewards = _data.realmShellRewards, seaLog = _data.seaLog, tideLog = _data.tideLog, keeperLog = _data.keeperLog, photoLog = _data.photoLog, seaPost = _data.seaPost, seaPostDay = _data.seaPostDay };
        public static bool TryRestore(LoreData data, out FishingLore lore, int version = FishingSaveData.CurrentVersion)
        {
            lore = null;
            if (data == null || data.residents < 0 || data.residents > 63 || data.shrines < 0 || data.shrines > 15 || (data.finale && data.shrines != 15)
                || data.rewardedShrines < 0 || data.rewardedShrines > 15 || (data.rewardedShrines & ~data.shrines) != 0
                || (data.chartFound && !data.mayorAccepted) || (data.rewardedShrines != 0 && !data.chartFound)
                || (data.grandTreasureFound && (!data.chartFound || data.shrines != 15 || data.rewardedShrines != 15))
                || (version < 16 && !QuestPristine(data))
                || data.realmStages < 0 || data.realmStages > 255 || data.realmShells < 0 || data.realmShells > 4095 || data.realmVariants < 0 || data.realmVariants > 255
                || data.realmReplay<0 || data.realmReplay>15 || (data.realmReplay & ~data.shrines)!=0 || data.realmShellRewards<0 || data.realmShellRewards>15
                || (version < 20 && (data.realmStages != 0 || data.realmShells != 0 || data.realmVariants != 0 || data.realmReplay != 0 || data.realmShellRewards != 0))
                || data.seaLog < 0 || data.seaLog >= FishingSeaLetters.LogLimit || (version < 20 && data.seaLog != 0)
                || data.tideLog < 0 || data.tideLog >= 1 << 12 || data.keeperLog < 0 || data.keeperLog >= 1 << 22 || (version < 20 && (data.tideLog != 0 || data.keeperLog != 0))
                || data.photoLog < 0 || data.photoLog >= 1 << 24 || (version < 20 && data.photoLog != 0)
                || !ValidSeaPost(data.seaPost, data.seaPostDay) || (version < 20 && (data.seaPost != 0 || data.seaPostDay != 0))) return false;
            for (int i = 0; i < 4; i++) if (((data.realmStages >> (i * 2)) & 3) > MaxStage(i)) return false;
            lore = new FishingLore { _data = new LoreData { residents = data.residents, shrines = data.shrines, finale = data.finale,
                mayorAccepted = data.mayorAccepted, chartFound = data.chartFound, grandTreasureFound = data.grandTreasureFound, rewardedShrines = data.rewardedShrines,
                realmStages = data.realmStages, realmShells = data.realmShells, realmVariants = data.realmVariants, realmReplay = data.realmReplay, realmShellRewards = data.realmShellRewards, seaLog = data.seaLog, tideLog = data.tideLog, keeperLog = data.keeperLog, photoLog = data.photoLog, seaPost = data.seaPost, seaPostDay = data.seaPostDay } }; return true;
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
        private int _windRound,_lightStage,_shutter;
        public int WindRound=>_windRound;
        public int WindLength=>_windRound==0?3:5;
        public int LightStage=>_lightStage;
        public bool ShutterOpen=>_shutter==1;
        public static int WindNote(int round,int step)=>round==0?(step==0?2:step==1?0:1):(step==0?2:step==1?0:step==2?1:step==3?0:1);
        public readonly int Variant;
        // Variant 0 keeps the authored v0.35 answers; 1 and 2 rotate them without ever making a mirror start aligned.
        public int MirrorTarget(int device)=>(((_lightStage==0?device+1:(device==0?3:device==1?1:2))-1+Variant)%3)+1;
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
        public float TargetYaw(int stage)=>Variant==1?-StarTargetYaw(stage):StarTargetYaw(stage)-(Variant==2?15:0);
        public float TargetPitch(int stage)=>Variant==2?StarTargetPitch((stage+2)%3):StarTargetPitch(stage);
        /// <summary>Seconds of steady focus; the world shortens it at night when the sky is clear of glare.</summary>
        public float FocusSeconds=1.4f;
        /// <summary>Seconds the drained hold stays dry before the tide stalls the pump; infinite disables the hazard.</summary>
        public float LeakLimit=float.PositiveInfinity;
        public float DrySeconds { get; private set; }
        public bool Flooding { get; private set; }
        public int Checkpoint=>Island==0?(Ready?2:_windRound):Island==1?Math.Min(2,_lightStage):Island==2?(_cargo>=1?1:0):Math.Min(3,_starStage);
        public bool LightAligned=>_states[0]==MirrorTarget(0) && _states[1]==MirrorTarget(1) && _states[2]==MirrorTarget(2) && (_lightStage==0 || ShutterOpen);
        public void Tick(float delta)
        {
            if(float.IsNaN(delta) || float.IsInfinity(delta) || delta<=0)return;
            _clock+=delta;
            if(Island==0 && Progress>0 && !Ready && _clock-_lastBell>30){Progress=0;Array.Clear(_states,0,3);}
            if(Island==1 && _lightStage<2)
            {
                Growth=Math.Max(0,Math.Min(1,Growth+(LightAligned?delta/5:-delta/3)));
                if(Growth>=1){_lightStage++;if(_lightStage==1)Growth=0;}
            }
            if(Island==2 && _states[1]!=0)WaterLevel=Math.Max(0,Math.Min(1,WaterLevel+(_states[1]==1?-delta:delta)/4));
            if(Island==2 && _cargo<1)
            {
                if(Flooding)WaterLevel=Math.Min(1,WaterLevel+delta/12);
                else if(WaterLevel<=.15f && (DrySeconds+=delta)>LeakLimit)
                {
                    // The pump stalls; cargo past the half-way chock stays there instead of sliding home.
                    Flooding=true;_states[1]=0;_cargo=_cargo>=.5f?.5f:0;_states[0]=(int)(_cargo*2);
                }
            }
        }
        public FishingShrineTrial(int island,int variant=0,int checkpoint=0)
        {
            if (island < 0 || island >= 4) throw new ArgumentOutOfRangeException(nameof(island));
            Island = island;Variant=Math.Max(0,Math.Min(2,variant));
            if(checkpoint<=0)return;
            if(island==0){_windRound=1;if(checkpoint>=2)Progress=5;}
            else if(island==1)_lightStage=Math.Min(2,checkpoint);
            else if(island==2){_cargo=1;_states[0]=2;}
            else _starStage=Math.Min(3,checkpoint);
        }
        public int State(int device) => device >= 0 && device < 3 ? _states[device] : 0;
        public bool Ready => Island == 0 ? _windRound==1 && Progress==5 : Island == 1 ? _lightStage>=2
            : Island == 2 ? _cargo>=1 && WaterLevel>=.95f : _starStage>=3;
        public bool Use(int device)
        {
            if (device < 0 || device >= (Island == 2 ? 2 : Island==1?4:3)) return false;
            if (Island == 0)
            {
                if (Ready) return false;
                int expected = WindNote(_windRound,Progress);
                if (device != expected || (Progress>0 && (_clock-_lastBell<.4f || _clock-_lastBell>30))) { Progress = 0; Array.Clear(_states, 0, 3); return false; }
                _states[device] = 1; Progress++;_lastBell=_clock;
                if(_windRound==0 && Progress==3){_windRound=1;Progress=0;Array.Clear(_states,0,3);}
                return true;
            }
            if(Island==2)
            {
                if(device==0)return WaterLevel<=.15f && _cargo<1;
                else {_states[1]=(_states[1]+1)%3;Flooding=false;DrySeconds=0;}
            }
            else if(Island==1)
            {
                if(Ready || (device==3 && _lightStage==0))return false;
                if(device==3){_shutter=1-_shutter;return true;}
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
            float dx=StarYaw-TargetYaw(_starStage),dy=StarPitch-TargetPitch(_starStage);
            _starFocus=focusing && dx*dx+dy*dy<9?Math.Min(1,_starFocus+seconds/Math.Max(.2f,FocusSeconds)):0;
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
            if (!Lore.Replaying(island) && Coins > 100000000 - 90) return 0;
            int pay = Lore.Clear(island); Coins += pay; return pay;
        }
        /// <summary>-1 if already found; otherwise coins paid (30 when the realm's third shell completes its set).</summary>
        public int CollectRealmShell(int island, int shell)
        {
            if (!Lore.FindShell(island, shell)) return -1;
            if (Coins > 100000000 - 30 || !Lore.TryClaimShellReward(island)) return 0;
            Coins += 30; return 30;
        }
        public int CompleteEchoVoyage()
        {
            if (Coins > 100000000 - 180) return 0;
            int pay = Lore.Finish(); Coins += pay; return pay;
        }
    }
}
