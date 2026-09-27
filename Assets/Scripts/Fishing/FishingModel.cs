using System;
using System.Collections.Generic;

namespace BoatAttack.Fishing
{
    public enum CatchPhase { Idle, Waiting, Bite, Fighting, Landing, Caught, Escaped }
    public enum FishKind { Coral, Dart, Puffer }

    [Serializable]
    public sealed class CatchRecord
    {
        public string id;
        public FishKind kind;
        public bool rare;
        public int value;
        public CatchRecord Copy() => new CatchRecord { id = id, kind = kind, rare = rare, value = value };
    }

    /// <summary>No Unity dependencies: mouse/XR adapters can drive the same catch rules.</summary>
    public sealed class CatchSession
    {
        public CatchPhase Phase { get; private set; }
        public FishKind Kind { get; private set; }
        public bool Rare { get; private set; }
        public float Tension { get; private set; }
        public BaitKind Bait { get; private set; }
        public BayWeather WeatherAtCast { get; private set; }
        public FishBeat Beat => FishingVoyage.Beat(Kind, _timer);
        public float BeatProgress => _timer % FishingVoyage.BeatDuration(Kind) / FishingVoyage.BeatDuration(Kind);
        public float Distance { get; private set; }
        public float MaxDistance { get; private set; }
        public float LandPointer => (float)(0.5 + 0.5 * Math.Sin(_timer * 3.1));
        public bool CanLand => Phase == CatchPhase.Landing && LandPointer >= 0.25f && LandPointer <= 0.75f;
        public string Failure { get; private set; }
        public float Remaining => Math.Max(0f, _deadline - _timer);
        private string _id;
        private float _timer, _deadline, _overload, _slack;
        private int _rodLevel, _misses;
        private bool _claimed;

        public bool Cast(FishKind kind, bool rare, int rodLevel, string id, BaitKind bait = BaitKind.Balanced, BayWeather weather = BayWeather.Clear)
        {
            if (Phase != CatchPhase.Idle && Phase != CatchPhase.Caught && Phase != CatchPhase.Escaped) return false;
            if (!Enum.IsDefined(typeof(BayWeather), weather) || !Enum.IsDefined(typeof(BaitKind), bait)) return false;
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || !Enum.IsDefined(typeof(FishKind), kind)) return false;
            WeatherAtCast = weather; Bait = bait; Kind = kind; Rare = rare; _id = id; _rodLevel = Math.Max(0, Math.Min(1, rodLevel));
            _claimed = false; _misses = 0; _overload = _slack = 0f; Tension = 0.3f;
            MaxDistance = Distance = 10f + (int)kind * 2f;
            Failure = string.Empty;
            Enter(CatchPhase.Waiting, (2.2f + (int)kind * 0.35f) * BayClimate.BiteScale(weather, kind));
            return true;
        }

        public void Tick(float deltaTime, bool reel)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            // Do not skip entire reaction windows on an application hitch.
            float dt = Math.Min(deltaTime, 0.1f);
            _timer += dt;
            switch (Phase)
            {
                case CatchPhase.Waiting:
                    if (_timer >= _deadline) Enter(CatchPhase.Bite, 2.5f);
                    break;
                case CatchPhase.Bite:
                    if (_timer >= _deadline) Escape("错过咬钩时机：看到提示后点击左键。");
                    break;
                case CatchPhase.Fighting:
                    float effort = Beat == FishBeat.Surge ? (Kind == FishKind.Puffer ? 1.8f : 1.55f) : Beat == FishBeat.Rest ? 0.75f : 1f;
                    float gain = (0.36f + 0.09f * (int)Kind) * effort * FishingVoyage.TensionScale(Bait) * (_rodLevel == 1 ? 0.72f : 1f);
                    float retrieve = (Beat == FishBeat.Rest ? 2.65f : Beat == FishBeat.Windup ? 1.8f : 0.8f) * FishingVoyage.ReelScale(Bait);
                    Tension = Clamp(Tension + (reel ? gain : -0.48f) * dt, 0f, 1f);
                    Distance = Clamp(Distance + (reel ? -retrieve : 0.35f) * dt, 0f, MaxDistance);
                    _overload = Tension >= 0.98f ? _overload + dt : 0f;
                    _slack = Tension <= 0.02f ? _slack + dt : 0f;
                    if (_overload >= 0.7f) Escape("鱼线绷断：红色张力过高时松开鼠标。");
                    else if (_slack >= 4f) Escape("鱼挣脱了：不要连续放线太久。");
                    else if (_timer > 75f) Escape("鱼游远了：收线与放线交替进行。");
                    else if (Distance <= 0f) Enter(CatchPhase.Landing, 10f);
                    break;
                case CatchPhase.Landing:
                    if (_timer >= _deadline) Escape("没有及时抄鱼：指针进入绿色区时按 E。");
                    break;
            }
        }

        public bool Hook()
        {
            if (Phase != CatchPhase.Bite) return false;
            Enter(CatchPhase.Fighting, 75f);
            return true;
        }

        public bool Land()
        {
            if (Phase != CatchPhase.Landing) return false;
            if (CanLand) { Enter(CatchPhase.Caught, 0f); return true; }
            if (++_misses >= 3) Escape("抄网落空三次：看准绿色区域再按 E。");
            return false;
        }

        public CatchRecord Claim()
        {
            if (Phase != CatchPhase.Caught || _claimed) return null;
            _claimed = true;
            return new CatchRecord { id = _id, kind = Kind, rare = Rare, value = (10 + (int)Kind * 6) * (Rare ? 3 : 1) };
        }

        public void Cancel() { Phase = CatchPhase.Idle; Failure = string.Empty; }
        private void Escape(string reason) { Failure = reason; Enter(CatchPhase.Escaped, 0f); }
        private void Enter(CatchPhase phase, float deadline) { Phase = phase; _timer = 0f; _deadline = deadline; }
        private static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(max, x));
    }

    [Serializable]
    public sealed class FishingSaveData
    {
        public int version = 2;
        public int discoveryMask;
        public int wildlifeMask;
        public int catches;
        public int ordersCompleted;
        public int coins;
        public int rodLevel;
        public List<CatchRecord> inventory = new List<CatchRecord>();
        public List<string> recordedIds = new List<string>();
    }

    public sealed class FishingCareer
    {
        public const int Capacity = 6;
        public const int UpgradeCost = 30;
        public int Coins { get; private set; }
        public int DiscoveryMask { get; private set; }
        public int WildlifeMask { get; private set; }
        public bool Seen(MarineKind kind) => Enum.IsDefined(typeof(MarineKind), kind) && (WildlifeMask & (1 << (int)kind)) != 0;
        public bool Observe(MarineKind kind)
        {
            if (!Enum.IsDefined(typeof(MarineKind), kind) || Seen(kind)) return false;
            WildlifeMask |= 1 << (int)kind; return true;
        }
        public int Catches { get; private set; }
        public int OrdersCompleted { get; private set; }
        public bool Discovered(FishKind kind, bool rare = false) => (DiscoveryMask & (1 << ((int)kind + (rare ? 3 : 0)))) != 0;
        public int Owned(FishKind kind) { int count = 0; foreach (var fish in _inventory) if (fish.kind == kind) count++; return count; }
        public bool OrderReady { get { for (int i = 0; i < 3; i++) if (Owned((FishKind)i) < FishingVoyage.Required(OrdersCompleted, (FishKind)i)) return false; return true; } }
        public int RodLevel { get; private set; }
        public IReadOnlyList<CatchRecord> Inventory => _inventory.AsReadOnly();
        public int Count => _inventory.Count;
        public int HoldValue { get { int total = 0; foreach (var item in _inventory) total += item.value; return total; } }
        private readonly List<CatchRecord> _inventory = new List<CatchRecord>();
        private readonly HashSet<string> _recorded = new HashSet<string>();

        public bool Store(CatchRecord fish)
        {
            if (!Valid(fish) || Count >= Capacity || !_recorded.Add(fish.id)) return false;
            _inventory.Add(fish.Copy());
            DiscoveryMask |= 1 << ((int)fish.kind + (fish.rare ? 3 : 0));
            Catches++;
            return true;
        }
        public int Sell()
        {
            int value = HoldValue;
            Coins += value;
            _inventory.Clear();
            return value;
        }
        // A delivery consumes only the requested fish, normal variants before rare ones.
        // Base fish value is paid in full, plus the displayed bonus; unused cargo stays aboard.
        public int DeliverOrder()
        {
            if (!OrderReady || OrdersCompleted >= 1000000) return 0;
            int payout = FishingVoyage.OrderBonus(OrdersCompleted);
            for (int kind = 0; kind < 3; kind++)
            {
                int need = FishingVoyage.Required(OrdersCompleted, (FishKind)kind);
                for (int rare = 0; rare < 2 && need > 0; rare++)
                    for (int i = _inventory.Count - 1; i >= 0 && need > 0; i--)
                        if ((int)_inventory[i].kind == kind && _inventory[i].rare == (rare == 1))
                        { payout += _inventory[i].value; _inventory.RemoveAt(i); need--; }
            }
            Coins += payout; OrdersCompleted++; return payout;
        }
        public bool Upgrade()
        {
            if (RodLevel != 0 || Coins < UpgradeCost) return false;
            Coins -= UpgradeCost; RodLevel = 1; return true;
        }
        public FishingSaveData Snapshot()
        {
            var result = new FishingSaveData { coins = Coins, rodLevel = RodLevel, discoveryMask = DiscoveryMask, wildlifeMask = WildlifeMask, catches = Catches, ordersCompleted = OrdersCompleted, recordedIds = new List<string>(_recorded) };
            foreach (var item in _inventory) result.inventory.Add(item.Copy());
            return result;
        }
        public static bool TryRestore(FishingSaveData data, out FishingCareer career)
        {
            career = null;
            if (data == null || (data.version != 1 && data.version != 2) || data.coins < 0 || data.coins > 100000000 || data.rodLevel < 0 || data.rodLevel > 1
                || data.wildlifeMask < 0 || data.wildlifeMask > 31 || data.discoveryMask < 0 || data.discoveryMask > 63 || data.catches < 0 || data.catches > 100000000 || data.ordersCompleted < 0 || data.ordersCompleted > 1000000
                || data.inventory == null || data.inventory.Count > Capacity || data.recordedIds == null) return false;
            var result = new FishingCareer();
            foreach (var item in data.inventory) if (!result.Store(item)) return false;
            foreach (var id in data.recordedIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return false;
                result._recorded.Add(id);
            }
            // v1 can recover only fish still in the hold; sold species were never recorded.
            if (data.version == 2)
            {
                if ((data.discoveryMask & result.DiscoveryMask) != result.DiscoveryMask || data.catches < result.Count) return false;
                result.WildlifeMask = data.wildlifeMask; result.DiscoveryMask = data.discoveryMask; result.Catches = data.catches; result.OrdersCompleted = data.ordersCompleted;
            }
            result.Coins = data.coins; result.RodLevel = data.rodLevel;
            career = result; return true;
        }
        private static bool Valid(CatchRecord record) => record != null && !string.IsNullOrWhiteSpace(record.id)
            && record.id.Length <= 64 && Enum.IsDefined(typeof(FishKind), record.kind)
            && record.value == (10 + (int)record.kind * 6) * (record.rare ? 3 : 1);
    }
}
