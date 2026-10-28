using System;

namespace BoatAttack.Fishing
{
    /// <summary>Seasonal festivals derived from the calendar day alone; nothing is saved.</summary>
    public static class FishingFestival
    {
        public const int Date = 14, SummerDays = 3;
        private static readonly string[] Names = { "风筝节", "夜光季", "收获祭", "灯会" };
        private static readonly string[] Blurbs = {
            "春风正好，渔港挂满彩旗，孩子们把风筝放到灯塔那么高。",
            "夏夜的海会发光。一连三晚，船尾都拖着青色的光痕。",
            "秋收的稻草和南瓜堆在广场，大家把一年的收成摆出来晒晒。",
            "冬夜最长，村里点起红纸灯笼，替还在海上的人照一照回家的路。" };
        /// <summary>-1 on ordinary days, otherwise the season index (0 spring … 3 winter).</summary>
        public static int On(int day)
        {
            if (day < 0) return -1;
            int season = day / FishingCalendar.DaysPerSeason % 4, date = day % FishingCalendar.DaysPerSeason + 1;
            return date >= Date && date < Date + (season == 1 ? SummerDays : 1) ? season : -1;
        }
        public static string Name(int festival) => festival >= 0 && festival < 4 ? Names[festival] : "";
        public static string Blurb(int festival) => festival >= 0 && festival < 4 ? Blurbs[festival] : "";
        /// <summary>First festival day at or after <paramref name="day"/>.</summary>
        public static int NextDay(int day)
        {
            day = Math.Max(0, day);
            for (int d = day; d < day + FishingCalendar.DaysPerSeason + 1; d++) if (On(d) >= 0) return d;
            return -1;
        }
    }

    /// <summary>One codex for the whole bay: "seen" reads the existing records, the camera only adds a photographed bit.</summary>
    public static class FishingPhotoCodex
    {
        public const int Whale = 0, Dolphin = 1, GlowSea = 2, RareFish = 3, Resident = 4, Realm = 10, Lighthouse = 14, Festival = 15, Count = 16;
        public const int FirstReward = 15;
        public static string Name(int entry) => entry == Whale ? "湾外座头鲸" : entry == Dolphin ? "湾流海豚" : entry == GlowSea ? "夜光海"
            : entry == RareFish ? "珍珠稀有鱼" : entry >= Resident && entry < Resident + 6 ? FishingLore.ResidentName(entry - Resident)
            : entry >= Realm && entry < Realm + 4 ? FishingLore.ShrineName(entry - Realm) : entry == Lighthouse ? "回潮渔屋灯塔"
            : entry == Festival ? "节日灯火" : "";
        public static string Hint(int entry) => entry == Whale ? "晴天出海，慢慢靠近浮出水面的鲸。" : entry == Dolphin ? "鲸附近常有海豚伴游船头。"
            : entry == GlowSea ? "夜光夜里低头拍海面。" : entry == RareFish ? "钓到珍珠个体时，先拍一张再收进鱼箱。"
            : entry < Realm ? "去岛上找到这位居民。" : entry < Lighthouse ? "登岛，拍下秘境入口。" : entry == Lighthouse ? "从海上回望渔屋灯塔。"
            : "每季第 14 日，村里会挂起节日装饰。";
        public static bool Photographed(FishingCareer c, int entry) => c != null && c.Lore.PhotoFlag(entry);
        public static bool Seen(FishingCareer c, int entry)
        {
            if (c == null || entry < 0 || entry >= Count) return false;
            if (Photographed(c, entry)) return true;
            if (entry == Whale) return c.Lore.SeaFlag(FishingSeaLetters.WhaleBit);
            if (entry == Dolphin) return c.Seen(MarineKind.Dolphin);
            if (entry == GlowSea) return c.Lore.SeaFlag((int)SeaEventKind.Glow);
            if (entry == RareFish) { for (int i = 0; i < FishingSpecies.Count; i++) if (c.Discovered((FishKind)i, true)) return true; return false; }
            if (entry < Realm) return c.Lore.Met(entry - Resident);
            if (entry == Festival) return c.Calendar.Day >= FishingFestival.Date - 1;
            return true;
        }
        /// <summary>Album file name carries the game day and the codex entry (-1 = none), so no index file is needed.</summary>
        public static string FileName(int sequence, int day, int entry) => "p" + sequence.ToString("000") + "_d" + day + "_e" + entry + ".jpg";
        public static bool TryParse(string fileName, out int sequence, out int day, out int entry)
        {
            sequence = day = 0; entry = -1;
            if (string.IsNullOrEmpty(fileName) || !fileName.StartsWith("p") || !fileName.EndsWith(".jpg")) return false;
            var parts = fileName.Substring(1, fileName.Length - 5).Split('_');
            return parts.Length == 3 && parts[1].StartsWith("d") && parts[2].StartsWith("e")
                && int.TryParse(parts[0], out sequence) && int.TryParse(parts[1].Substring(1), out day) && int.TryParse(parts[2].Substring(1), out entry)
                && sequence >= 0 && day >= 0 && entry >= -1 && entry < Count;
        }
    }
}
namespace BoatAttack.Fishing
{
    public sealed partial class FishingCareer
    {
        /// <summary>Coins paid for the first photo of a codex entry; 0 for repeats.</summary>
        public int RecordPhoto(int entry)
        {
            if (entry < 0 || entry >= FishingPhotoCodex.Count || !Lore.SetPhotoFlag(entry)) return 0;
            if (Coins > 100000000 - FishingPhotoCodex.FirstReward) return 0;
            Coins += FishingPhotoCodex.FirstReward; return FishingPhotoCodex.FirstReward;
        }
    }
}
