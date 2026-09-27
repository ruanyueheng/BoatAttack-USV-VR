using System;
namespace BoatAttack.Fishing
{
    public enum BayWeather { Clear, Cloudy, Rain, Mist, Sunset }
    public enum MarineKind { Turtle, Ray, Dolphin, School, Gull }

    /// <summary>Deterministic, pause-aware climate. No wall clock, random global state or Unity dependency.</summary>
    public sealed class BayClimate
    {
        public const float TransitionSeconds = 12f;
        public BayWeather Current { get; private set; }
        public BayWeather Previous { get; private set; }
        public BayWeather Forecast => (BayWeather)(((int)Current + 1) % 5);
        public bool Automatic { get; private set; } = true;
        public float Age { get; private set; } = TransitionSeconds;
        public float Blend => Math.Min(1f, Age / TransitionSeconds);
        public float Remaining => Math.Max(0f, Duration(Current) - Age);
        public void Tick(float dt, bool paused)
        {
            if (paused || float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
            Age = Math.Min(Duration(Current), Age + Math.Min(dt, 0.25f));
            if (Automatic && Age >= Duration(Current)) Select(Forecast, true);
        }
        public bool Select(BayWeather weather, bool automatic = false)
        {
            if (!Enum.IsDefined(typeof(BayWeather), weather)) return false;
            // Finish the current blend before another change; avoids brightness jumps on repeated clicks.
            if (Blend < 1f) return false;
            Previous = Current; Current = weather; Automatic = automatic;
            Age = Current == Previous ? TransitionSeconds : 0f; return true;
        }
        public void Hold() { Automatic = false; }
        public void ResumeCycle() { Automatic = true; }
        public static float Duration(BayWeather weather) => weather == BayWeather.Rain ? 95 : weather == BayWeather.Mist ? 110 : 150;
        public static string Name(BayWeather weather)
        {
            switch (weather) { case BayWeather.Cloudy: return "海风多云"; case BayWeather.Rain: return "轻雨潮汐"; case BayWeather.Mist: return "薄雾海湾"; case BayWeather.Sunset: return "金色夕照"; default: return "晴朗海湾"; }
        }
        public static FishKind Favored(BayWeather weather) => weather == BayWeather.Rain || weather == BayWeather.Cloudy ? FishKind.Dart : weather == BayWeather.Mist ? FishKind.Puffer : FishKind.Coral;
        public static float BiteScale(BayWeather weather, FishKind fish) => fish == Favored(weather) ? 0.75f : 1f;
        public static float RareBonus(BayWeather weather, FishKind fish) => fish == Favored(weather) ? 0.04f : 0f;
        public static string MarineName(MarineKind kind)
        {
            switch (kind) { case MarineKind.Turtle: return "绿海龟"; case MarineKind.Ray: return "蓝斑鳐"; case MarineKind.Dolphin: return "湾流海豚"; case MarineKind.School: return "银鳞鱼群"; default: return "海湾鸥"; }
        }
        public static string MarineHint(MarineKind kind)
        {
            switch (kind) { case MarineKind.Turtle: return "浅湾 · 慢游浮息"; case MarineKind.Ray: return "礁区 · 舒展胸鳍滑行"; case MarineKind.Dolphin: return "水道 · 晴天与夕照跃出"; case MarineKind.School: return "渔区 · 雨天游动更活跃"; default: return "渔港 · 环绕灯塔盘旋"; }
        }
        public static float Activity(MarineKind kind, BayWeather weather) => kind == MarineKind.Dolphin ? (weather == BayWeather.Clear || weather == BayWeather.Sunset ? 1.2f : 0.65f) : kind == MarineKind.School && weather == BayWeather.Rain ? 1.35f : 1f;
        public static bool CanObserve(float distance, float facingDot, bool unobstructed) => unobstructed && distance >= 0 && distance <= 28 && facingDot >= 0.82f;
    }
}
