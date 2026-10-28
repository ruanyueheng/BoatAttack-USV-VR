using System;
namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class CalendarData
    {
        public int day;
        public double minute = 480;
        public bool timeFixed;
    }
    /// <summary>Fifteen active real minutes per day; menus and tracking/focus loss never consume time.</summary>
    public sealed class FishingCalendar
    {
        public const int DaysPerSeason = 28, DaysPerYear = 112, MaxDay = 111887;
        public const double MinutesPerSecond = 1.6;
        public int Day { get; private set; }
        public double Minute { get; private set; } = 480;
        public bool Automatic { get; private set; } = true;
        public void ToggleAutomatic() => Automatic = !Automatic;
        public bool AdvanceToHour(float hour)
        {
            if(float.IsNaN(hour) || float.IsInfinity(hour) || hour<0 || hour>=24)return false;
            double minutes=hour*60-Minute;if(minutes<0)minutes+=1440;
            if(Day==MaxDay && Minute+minutes>=1440)return false;
            Advance(minutes);return true;
        }
        public double TotalMinutes => Day * 1440d + Minute;
        public int Year => Day / DaysPerYear + 1;
        public int Season => Day / DaysPerSeason % 4;
        public int Date => Day % DaysPerSeason + 1;
        public int Weekday => Day % 7;
        public float Hour => (float)(Minute / 60);
        public bool Night => Hour < 6 || Hour >= 19;
        /// <summary>One tide every 12h25m of game time, so low water comes about 50 minutes later each day.</summary>
        public const double TidePeriod = 745;
        public double TidePhase => TotalMinutes % TidePeriod;
        public bool RisingTide => TidePhase < TidePeriod / 2;
        /// <summary>0 at low water, 1 at high water.</summary>
        public float TideLevel => (float)(1 - Math.Cos(TidePhase / TidePeriod * Math.PI * 2)) * .5f;
        public string TideName => RisingTide ? "涨潮" : "落潮";
        private static readonly string[] Seasons={"春","夏","秋","冬"};
        public string SeasonName => Seasons[Season];
        public string DateLabel => "第 " + Year + " 年 · " + SeasonName + " " + Date + " 日";
        public string TimeLabel => ((int)Minute / 60).ToString("00") + ":" + ((int)Minute % 60).ToString("00");
        public float Daylight => Smooth((Hour - 5) / 2) * (1 - Smooth((Hour - 17) / 2));
        private static float Smooth(float v) { v = Math.Max(0, Math.Min(1, v)); return v * v * (3 - 2 * v); }
        public void Tick(float seconds, bool paused)
        {
            if (!Automatic || paused || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
            Advance(Math.Min(seconds, .25f) * MinutesPerSecond);
        }
        private void Advance(double minutes)
        {
            double total = Minute + minutes;
            Day = Math.Min(MaxDay, Day + (int)(total / 1440));
            Minute = total % 1440;
        }
        public void RestUntilMorning() => Advance(Minute < 360 ? 360 - Minute : 1800 - Minute);
        public CalendarData Snapshot() => new CalendarData { day = Day, minute = Minute, timeFixed = !Automatic };
        public static bool TryRestore(CalendarData data, out FishingCalendar calendar)
        {
            calendar = null;
            if (data == null || data.day < 0 || data.day > MaxDay || double.IsNaN(data.minute)
                || double.IsInfinity(data.minute) || data.minute < 0 || data.minute >= 1440) return false;
            calendar = new FishingCalendar { Day = data.day, Minute = data.minute, Automatic = !data.timeFixed }; return true;
        }
        public static BayWeather Forecast(int day, int period = 1)
        {
            uint seed = unchecked((uint)(day * 1103515245 + period * 12345 + 731));
            int roll = (int)((seed ^ (seed >> 13)) % 100);
            int season = day / DaysPerSeason % 4;
            return roll < (season == 1 ? 35 : 22) ? BayWeather.Rain : roll < 45 ? BayWeather.Cloudy
                : roll < 60 ? BayWeather.Mist : BayWeather.Clear;
        }
        public BayWeather Weather => Hour >= 17 && Hour < 19 && Forecast(Day, (int)Minute / 360) != BayWeather.Rain ? BayWeather.Sunset : Forecast(Day, (int)Minute / 360);
    }
}
