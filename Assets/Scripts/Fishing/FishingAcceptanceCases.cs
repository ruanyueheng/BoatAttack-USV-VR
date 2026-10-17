using System;

namespace BoatAttack.Fishing
{
    public enum FishingAcceptanceCase
    {
        FreePlay, Bite, Fighting, Landing, Catch, LegendLeap,
        HarborOrders, FullHold, MaxUpgrades, Details, Journal, Settings,
        ShoreFirstPerson, ShoreThirdPerson, ShopCounter,
        StonePickaxe, IronPickaxe, BinocularCarry, BinocularObserve,
        LookDownStanding, LookDownCrouched, ShopAttendant, BinocularThirdPerson, Calendar, NightCoast, LanternCoast, HuntingSpear,
        HunyuanCatalog, HunyuanNightCatalog, CastFlight, NightBoat, LuxuryBoat, IslandLanding, ShopSupplies, IslandJournal,
        ResidentDialogue, WindSanctuary, ForestSanctuary, WreckSanctuary, StarSanctuary, LoreJournal, ShopRations, RegionArrival, SwimmingView, ClimbingView,
        ForestIslandLanding, WreckIslandLanding, StarIslandLanding, HomeInterior, HomeWorkbench, HomeBarbecue, HomeGrilledFish, CashierDialogue,
        BootCatch, CanCatch, TreasureCatch, LetterEnvelope, LetterPaper, StartMenu, MenuControls, NatureJournal, LegendJournal, ShoreJournal, GuideOpening, HomeMoonlight
    }

    /// <summary>Deterministic fixtures built through the production rules, not unchecked save JSON.</summary>
    public static class FishingAcceptanceCases
    {
        public const int Count = 65;
        private static readonly string[] Titles =
        {
            "自由实玩 / 继续验收进度", "咬钩提示", "高张力收线", "抄网时机", "捕获与后续反馈", "传说跃水",
            "三行委托与交易", "鱼箱已满", "三路线满级", "委托与传说详情", "图鉴日志", "暂停设置",
            "岸上第一人称同源手臂", "岸上第三人称人物", "实体钓具店柜台",
            "石镐实际握持", "铁镐实际握持", "望远镜双手携带", "望远镜光学观察",
            "站立低头视野", "蹲下低头视野", "成年女性店员", "第三人称举镜", "季节日历与天气预报", "海岸夜景", "夜间提灯照明", "猎矛握持",
            "混元鱼种图鉴", "夜鱼与礁区图鉴", "甩竿与浮标飞行", "船上夜灯与厚壁船壳", "豪华钓艇与动力升级", "风铃岛实际登陆", "探索用品商店", "自由探索群岛航记",
            "居民剧情对话", "风之回廊", "森光庭院", "沉潮机库", "星轨圣所", "世界与秘境航记", "远航口粮与物资", "地区名称渐显", "海岸游泳与体力", "绳梯攀登与瞭望台",
            "潮林岛实际登陆", "沉船沙洲实际登陆", "星望遗迹岛实际登陆", "回潮渔屋生活与工坊", "家中工作台与图示配方", "屋旁烧烤与鱼箱选鱼", "炭火烤鱼与领取", "汐禾分支对话",
            "旧鞋幽默钓获卡", "罐头幽默钓获卡", "宝匣惊喜钓获卡", "海风邮戳信封", "牛皮纸手写来信", "夜海开始菜单", "控制与显示模式", "插画自然手册", "金帆传说线索", "沿岸调查卡片", "双手翻开自然手册", "家岛月色与环扫灯塔"
        };

        public static string Title(FishingAcceptanceCase value)
        {
            Validate(value);
            return Titles[(int)value];
        }

        public static FishingAcceptanceCase Cycle(FishingAcceptanceCase value, int direction)
        {
            Validate(value);
            return (FishingAcceptanceCase)(((int)value + direction % Count + Count) % Count);
        }

        public static bool IsShoreCase(FishingAcceptanceCase value)=>value>=FishingAcceptanceCase.ShoreFirstPerson && value<=FishingAcceptanceCase.HuntingSpear && value!=FishingAcceptanceCase.Calendar;
        public static bool IsFindCase(FishingAcceptanceCase value)=>value>=FishingAcceptanceCase.BootCatch && value<=FishingAcceptanceCase.TreasureCatch;
        public static bool IsShopCase(FishingAcceptanceCase value)=>value==FishingAcceptanceCase.ShopCounter || value==FishingAcceptanceCase.ShopAttendant || value==FishingAcceptanceCase.ShopSupplies || value==FishingAcceptanceCase.ShopRations || value==FishingAcceptanceCase.CashierDialogue;
        public static FieldItem ViewItem(FishingAcceptanceCase value)
        {
            if(value==FishingAcceptanceCase.StonePickaxe || value==FishingAcceptanceCase.LookDownStanding || value==FishingAcceptanceCase.LookDownCrouched)return FieldItem.StonePickaxe;
            if(value==FishingAcceptanceCase.NightCoast || value==FishingAcceptanceCase.LanternCoast)return FieldItem.Lantern;
            if(value==FishingAcceptanceCase.HuntingSpear)return FieldItem.HuntingSpear;
            if(value==FishingAcceptanceCase.IronPickaxe)return FieldItem.IronPickaxe;
            if(value==FishingAcceptanceCase.BinocularCarry || value==FishingAcceptanceCase.BinocularObserve || value==FishingAcceptanceCase.BinocularThirdPerson)return FieldItem.Binoculars;
            return FieldItem.FieldGuide;
        }
        public static bool IsCatchCase(FishingAcceptanceCase value)
            => value >= FishingAcceptanceCase.Bite && value <= FishingAcceptanceCase.LegendLeap;

        public static FishingCareer CreateCareer(FishingAcceptanceCase value)
        {
            Validate(value);
            if (value == FishingAcceptanceCase.FreePlay)
                throw new ArgumentException("Free play must load its own saved progress, never a fixture.");
            bool max = value == FishingAcceptanceCase.MaxUpgrades || value == FishingAcceptanceCase.LegendLeap;
            var data = new FishingSaveData { calendar=new CalendarData{day=value==FishingAcceptanceCase.Calendar?34:0,minute=value==FishingAcceptanceCase.NightCoast || value==FishingAcceptanceCase.LanternCoast || value==FishingAcceptanceCase.NightBoat?1380:480}, backpack = new FishingBackpack().Snapshot(),  coins = 200, ordersCompleted = 2, rodLevel = max ? 2 : 1,
                reelLevel = max ? 2 : 1, holdLevel = value == FishingAcceptanceCase.MaxUpgrades ? 2 : 0 };
            if (!FishingCareer.TryRestore(data, out var career)) throw new InvalidOperationException("Invalid acceptance seed.");
            if(value==FishingAcceptanceCase.LuxuryBoat){data.engineLevel=data.rudderLevel=data.cabinLevel=2;if(!FishingCareer.TryRestore(data,out career))throw new InvalidOperationException("Invalid fleet fixture.");}
            if(value==FishingAcceptanceCase.IslandJournal)career.DiscoverIsland(0);
            int cargo = value == FishingAcceptanceCase.FullHold ? career.Capacity : IsCatchCase(value) ? 0 : 3;
            for (int i = 0; i < cargo; i++)
            {
                var kind = (FishKind)(i % 3);
                int grams = FishingSpecies.Grams(kind, .7f);
                var fish = new CatchRecord { id = "acceptance-" + value + "-" + i, kind = kind, grams = grams,
                    value = FishingSpecies.Value(kind, false, grams) };
                if (!career.Store(fish)) throw new InvalidOperationException("Acceptance cargo was rejected.");
            }
            return career;
        }

        // Catch stops at a valid landing window so the controller's real Scoop commits the result.
        public static void PrepareSession(CatchSession session, FishingAcceptanceCase value, FishingCareer career)
        {
            Validate(value);
            if (session == null || career == null || !IsCatchCase(value)) throw new ArgumentException("A catch fixture is required.");
            bool legend = value == FishingAcceptanceCase.LegendLeap;
            session.Cancel();
            if (!session.Cast(legend ? FishKind.Sailfish : value == FishingAcceptanceCase.Fighting ? FishKind.Puffer : FishKind.Coral, false, career.LineLevel,
                "acceptance-cast-" + value, legend ? BaitKind.Pearl : BaitKind.Balanced,
                legend ? BayWeather.Sunset : BayWeather.Clear, .65f, career.ReelLevel))
                throw new InvalidOperationException("Acceptance cast was rejected.");
            Advance(session, () => session.Phase == CatchPhase.Bite);
            if (value == FishingAcceptanceCase.Bite) return;
            if (!session.Hook()) throw new InvalidOperationException("Acceptance hook was rejected.");
            if (value == FishingAcceptanceCase.Fighting)
            {
                for (int i = 0; i < 2000 && session.Tension < .82f && session.Phase == CatchPhase.Fighting; i++) session.Tick(.02f, true);
                if (session.Phase != CatchPhase.Fighting || session.Tension < .78f) throw new InvalidOperationException("High-tension fixture failed.");
                return;
            }
            if (legend) Advance(session, () => session.Beat == FishBeat.Leap && session.LeapProgress >= .35f);
            else Advance(session, () => session.Phase == CatchPhase.Landing);
        }

        private static void Advance(CatchSession session, Func<bool> ready)
        {
            for (int i = 0; i < 6000 && !ready(); i++)
            {
                session.Tick(.05f, session.Tension < .6f && session.Beat != FishBeat.Leap);
                if (session.Phase == CatchPhase.Escaped) throw new InvalidOperationException("Acceptance fixture escaped: " + session.Failure);
            }
            if (!ready()) throw new InvalidOperationException("Acceptance fixture exceeded its deterministic step budget.");
        }

        private static void Validate(FishingAcceptanceCase value)
        {
            if ((int)value < 0 || (int)value >= Count) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
