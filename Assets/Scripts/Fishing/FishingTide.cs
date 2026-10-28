using System;
using System.Collections.Generic;

namespace BoatAttack.Fishing
{
    /// <summary>Tide timetable, the low-tide reef collection and the lamp keeper's late boats (Docs/LIGHTHOUSE_TIDE_V044.md).</summary>
    public static class FishingTide
    {
        /// <summary>The reef path is walkable below this tide level.</summary>
        public const float ExposedBelow = .30f;
        /// <summary>Game minutes of warning before the path floods.</summary>
        public const double WarnMinutes = 40;
        public static bool Exposed(FishingCalendar c) => c.TideLevel < ExposedBelow;
        /// <summary>Game minutes until the path floods, or +inf when it is not exposed and ebbing.</summary>
        public static double MinutesToFlood(FishingCalendar c)
        {
            if (!Exposed(c) || !c.RisingTide) return double.PositiveInfinity;
            // Level crosses ExposedBelow at phase acos(1-2L)/2pi of the period on the rising half.
            double flood = Math.Acos(1 - 2 * ExposedBelow) / (Math.PI * 2) * FishingCalendar.TidePeriod;
            return flood - c.TidePhase;
        }

        private static float Level(double total) => (float)(1 - Math.Cos(total % FishingCalendar.TidePeriod / FishingCalendar.TidePeriod * Math.PI * 2)) * .5f;
        private static string Clock(int minute) => (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
        /// <summary>Low waters and reef windows that start or fall inside the calendar day.</summary>
        public static string Today(FishingCalendar c) => Table(c.Day);
        public static string Table(int day)
        {
            var lows = new List<string>(); var windows = new List<string>();
            double start = day * 1440d; int open = -1;
            for (int m = 0; m <= 1440; m++)
            {
                double t = start + m; bool exposed = Level(t) < ExposedBelow;
                if (m < 1440 && Level(t) <= Level(t - 1) && Level(t) < Level(t + 1)) lows.Add(Clock(m));
                if (exposed && open < 0) open = m;
                if ((!exposed || m == 1440) && open >= 0) { windows.Add(Clock(open) + "–" + Clock(Math.Min(m, 1439))); open = -1; }
            }
            // A window running through midnight reads as one span ("22:33–02:18") at the end of the list.
            if (windows.Count > 1 && windows[0].StartsWith("00:00") && windows[windows.Count - 1].EndsWith("23:59"))
            {
                windows[windows.Count - 1] = windows[windows.Count - 1].Substring(0, 5) + "–" + windows[0].Substring(6);
                windows.RemoveAt(0);
            }
            return "低潮 " + string.Join(" · ", lows) + "  ／  礁石小路 " + string.Join("、", windows);
        }

        public const int ShellCount = 6, CrabCount = 3, SecretCount = 3, ItemCount = 12;
        public static readonly string[] Names =
        {
            "扇贝壳", "宝螺", "织纹螺", "月光蛤", "红海星", "海胆壳",
            "小沙蟹", "寄居蟹", "招潮蟹",
            "祖父的潮汐表残页", "礁石上的刻痕", "礁缝里的旧灯罩",
        };
        public static readonly string[] Lines =
        {
            "扇面上一圈圈纹路，像潮水一次次退去留下的线。",
            "背上有一圈金斑，村里孩子说握着它能听见远处的船。",
            "小小的尖螺，空壳里还夹着一粒沙。",
            "薄得透光的白蛤，退潮的月夜里最先露出来。",
            "五条腕少了一条，它会慢慢再长出来。",
            "刺已经掉光了，只剩一颗淡紫色的小灯笼。",
            "横着跑得飞快，钳子举得比个子还高。",
            "背着一只比自己大两号的螺壳，搬家搬了一半。",
            "一只大螯挥个不停，像在跟回港的船打招呼。",
            "被水泡皱的纸上是祖父的字：“潮比钟准。灯塔的人要先学会读潮。”下面是一整月的低潮时刻。",
            "礁石上刻着一排“正”字，最后一笔很新——有人退潮时来这里数过等人的夜。",
            "一只缺了口的玻璃灯罩，烟熏痕里刻着“风”字。四盏灯里，风灯的罩子换过一次。",
        };

        /// <summary>Lamp watch nights: every misty evening, otherwise about one night in three.</summary>
        public static bool WatchNight(int day)
        {
            if (FishingCalendar.Forecast(day, 3) == BayWeather.Mist) return true;
            uint seed = unchecked((uint)(day * 2654435761u + 977));
            return (seed ^ (seed >> 15)) % 3 == 0;
        }
        public static int BoatsTonight(int day, bool mist) => mist ? 3 : 1 + (int)(unchecked((uint)(day * 40503 + 11)) >> 7) % 2;

        public const int BoatCount = 6, ArrivedBit = 0, ReadBit = 6, GuideReward = 20, ParcelReward = 15, RepeatReward = 8;
        public static readonly string[] Boats = { "铃舌号", "红树号", "石头号", "观星号", "小满号", "老灯号" };
        public static readonly string[] Skippers = { "阿潮叔", "禾爸", "老石", "小北", "满哥", "老苇" };
        public static readonly string[] Letters =
        {
            "铃音那丫头天天念叨，说灯塔又有人守了，我还不信。今晚风大，我在湾口转了两圈找不着港，抬头就看见那道光。三十年前我也是这样跟着灯回来的。网里的鲷鱼给你留了两条，挂在门上了。",
            "青禾种的红树又往外长了一截，船进湾得绕开。我在雾里绕得心慌，是你的光把那排新树照亮了，我才知道港在后头。孩子说那片树是给以后的人走的，现在想想，灯也是。",
            "我小时候就是坐着老船长空出来的舱位回的家。从那以后我总想：海上的人最怕的不是浪，是回头看不见岸。今晚我回头看见了。码头上那袋东西是阿砾从沉船里捞的旧铜扣，他说该给守灯人。",
            "望星师父让我记星图，我贪看了一颗流星，把船漂过了头。找不到归航星的时候，灯塔就是归航星——师父说这是你祖父当年写在记录本第一页的话。谢谢你替他接着写。",
            "汐禾说她哥是个不着家的人。可每次看见灯，我都想快点回去。今晚的光比往年都亮，你是不是把镜片擦过了？店里新到的鱼线给你捎了一卷，就当船票。",
            "林舟在码头留灯留了三十年，我就在外头跑了三十年。今晚是第一回，看见塔上和码头上两盏灯一起亮着。老头子站在那儿没说话，我也没说。两个人都知道，有人在等，就够了。",
        };
        public const string GuidedPostscript = "\n\nPS：昨夜是你的灯把我们照回来的。";
        public const string LatePostscript = "\n\nPS：昨夜雾太大，我们天亮才摸回港。下回还请把灯点上。";
    }
}
