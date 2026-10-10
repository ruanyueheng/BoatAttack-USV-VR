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
        HunyuanCatalog, HunyuanNightCatalog, CastFlight
    }

    /// <summary>Deterministic fixtures built through the production rules, not unchecked save JSON.</summary>
    public static class FishingAcceptanceCases
    {
        public const int Count = 30;
        private static readonly string[] Titles =
        {
            "自由实玩 / 继续验收进度", "咬钩提示", "高张力收线", "抄网时机", "捕获与后续反馈", "传说跃水",
            "三行委托与交易", "鱼箱已满", "三路线满级", "委托与传说详情", "图鉴日志", "暂停设置",
            "岸上第一人称同源手臂", "岸上第三人称人物", "实体钓具店柜台",
            "石镐实际握持", "铁镐实际握持", "望远镜双手携带", "望远镜光学观察",
            "站立低头视野", "蹲下低头视野", "成年女性店员", "第三人称举镜", "季节日历与天气预报", "海岸夜景", "夜间提灯照明", "猎矛握持",
            "混元鱼种图鉴", "夜鱼与礁区图鉴", "甩竿与浮标飞行"
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
        public static bool IsShopCase(FishingAcceptanceCase value)=>value==FishingAcceptanceCase.ShopCounter || value==FishingAcceptanceCase.ShopAttendant;
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
            var data = new FishingSaveData { calendar=new CalendarData{day=value==FishingAcceptanceCase.Calendar?34:0,minute=value==FishingAcceptanceCase.NightCoast || value==FishingAcceptanceCase.LanternCoast?1380:480}, backpack = new FishingBackpack().Snapshot(),  coins = 200, ordersCompleted = 2, rodLevel = max ? 2 : 1,
                reelLevel = max ? 2 : 1, holdLevel = value == FishingAcceptanceCase.MaxUpgrades ? 2 : 0 };
            if (!FishingCareer.TryRestore(data, out var career)) throw new InvalidOperationException("Invalid acceptance seed.");
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
            if (!session.Cast(legend ? FishKind.Sailfish : FishKind.Coral, false, career.LineLevel,
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
