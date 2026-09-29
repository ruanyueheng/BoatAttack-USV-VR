using System;
using System.Collections.Generic;

namespace BoatAttack.Fishing
{
    public enum CatchPhase { Idle, Waiting, Bite, Fighting, Landing, Caught, Escaped }
    public enum FishKind { Coral, Dart, Puffer, Sailfish }

    [Serializable]
    public sealed class CatchRecord
    {
        public string id;
        public FishKind kind;
        public bool rare;
        public int value;
        // Whole grams. Zero marks a record saved before v0.4 introduced sizes.
        public int grams;
        public CatchRecord Copy() => new CatchRecord { id = id, kind = kind, rare = rare, value = value, grams = grams };
    }

    /// <summary>No Unity dependencies: mouse/XR adapters can drive the same catch rules.</summary>
    public sealed class CatchSession
    {
        // Reeling while a legend is airborne multiplies the pull; releasing is the intended answer.
        public const float LeapEffort = 3.2f;
        public CatchPhase Phase { get; private set; }
        public FishKind Kind { get; private set; }
        public FishSpecies Species => FishingSpecies.Of(Kind);
        public bool Rare { get; private set; }
        public int Grams { get; private set; }
        public float Tension { get; private set; }
        public BaitKind Bait { get; private set; }
        public BayWeather WeatherAtCast { get; private set; }
        public FishBeat Beat => FishingVoyage.Beat(Kind, _timer);
        public float BeatProgress => _timer % Species.BeatSeconds / Species.BeatSeconds;
        /// <summary>0-1 across the current leap and 0 outside one; drives the airborne arc.</summary>
        public float LeapProgress => Beat == FishBeat.Leap
            ? (BeatProgress - FishingVoyage.LeapStart) / (FishingVoyage.LeapEnd - FishingVoyage.LeapStart) : 0f;
        public float Distance { get; private set; }
        public float MaxDistance { get; private set; }
        public float LandPointer => (float)(0.5 + 0.5 * Math.Sin(_timer * (Species.Legendary ? 3.6 : 3.1)));
        // Half-width of the scoop window around the centre of the landing gauge.
        public float LandZone => Species.Legendary ? 0.15f : 0.25f;
        public bool CanLand => Phase == CatchPhase.Landing && Math.Abs(LandPointer - 0.5f) <= LandZone;
        public CatchFailure Failure { get; private set; }
        public float Remaining => Math.Max(0f, _deadline - _timer);
        private string _id;
        private float _timer, _deadline, _overload, _slack, _sizeTension = 1f;
        private int _lineLevel, _reelLevel, _misses;
        private bool _claimed;

        public static float LineFactor(int level) => level >= 2 ? 0.58f : level == 1 ? 0.72f : 1f;
        public static float ReelFactor(int level) => 1f + 0.2f * Math.Max(0, Math.Min(2, level));

        public bool Cast(FishKind kind, bool rare, int lineLevel, string id, BaitKind bait = BaitKind.Balanced,
            BayWeather weather = BayWeather.Clear, float size = 0.5f, int reelLevel = 0)
        {
            if (Phase != CatchPhase.Idle && Phase != CatchPhase.Caught && Phase != CatchPhase.Escaped) return false;
            if (!Enum.IsDefined(typeof(BayWeather), weather) || !Enum.IsDefined(typeof(BaitKind), bait)) return false;
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || !FishingSpecies.IsDefined(kind)) return false;
            if (float.IsNaN(size) || size < 0f || size > 1f) return false;
            var species = FishingSpecies.Of(kind);
            if (rare && species.Legendary) return false;
            WeatherAtCast = weather;
            Bait = bait;
            Kind = kind;
            Rare = rare;
            _id = id;
            _lineLevel = Math.Max(0, Math.Min(2, lineLevel));
            _reelLevel = Math.Max(0, Math.Min(2, reelLevel));
            Grams = FishingSpecies.Grams(kind, size);
            // Bigger fish pull a little harder and start a little farther away.
            _sizeTension = 0.92f + 0.16f * size;
            MaxDistance = Distance = species.LineMeters * (0.9f + 0.2f * size);
            _claimed = false;
            _misses = 0;
            _overload = _slack = 0f;
            Tension = 0.3f;
            Failure = CatchFailure.None;
            Enter(CatchPhase.Waiting, species.BiteSeconds * BayClimate.BiteScale(weather, kind));
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
                    if (_timer >= _deadline) Escape(CatchFailure.MissedBite);
                    break;
                case CatchPhase.Fighting:
                    Fight(dt, reel);
                    break;
                case CatchPhase.Landing:
                    if (_timer >= _deadline) Escape(CatchFailure.LandingTimeout);
                    break;
            }
        }

        private void Fight(float dt, bool reel)
        {
            var species = Species;
            var beat = Beat;
            float effort = beat == FishBeat.Surge ? species.SurgeEffort : beat == FishBeat.Rest ? 0.75f : beat == FishBeat.Leap ? LeapEffort : 1f;
            float gain = species.TensionGain * effort * FishingVoyage.TensionScale(Bait) * LineFactor(_lineLevel) * _sizeTension;
            float retrieve = beat == FishBeat.Rest ? 2.65f : beat == FishBeat.Windup ? 1.8f : beat == FishBeat.Leap ? 0f : 0.8f;
            retrieve *= FishingVoyage.ReelScale(Bait) * ReelFactor(_reelLevel);
            // An airborne legend takes line quickly when it is not being fought.
            float run = beat == FishBeat.Leap ? 1.1f : 0.35f;
            Tension = Clamp(Tension + (reel ? gain : -0.48f) * dt, 0f, 1f);
            Distance = Clamp(Distance + (reel ? -retrieve : run) * dt, 0f, MaxDistance);
            _overload = Tension >= 0.98f ? _overload + dt : 0f;
            _slack = Tension <= 0.02f ? _slack + dt : 0f;
            if (_overload >= 0.7f) Escape(beat == FishBeat.Leap ? CatchFailure.LeapSnapped : CatchFailure.LineSnapped);
            else if (_slack >= 4f) Escape(CatchFailure.SlackLine);
            else if (_timer > _deadline) Escape(CatchFailure.SwamAway);
            else if (Distance <= 0f) Enter(CatchPhase.Landing, species.Legendary ? 8f : 10f);
        }

        public bool Hook()
        {
            if (Phase != CatchPhase.Bite) return false;
            Enter(CatchPhase.Fighting, Species.FightSeconds);
            return true;
        }

        public bool Land()
        {
            if (Phase != CatchPhase.Landing) return false;
            if (CanLand) { Enter(CatchPhase.Caught, 0f); return true; }
            if (++_misses >= 3) Escape(CatchFailure.NetMissed);
            return false;
        }

        public CatchRecord Claim()
        {
            if (Phase != CatchPhase.Caught || _claimed) return null;
            _claimed = true;
            return new CatchRecord { id = _id, kind = Kind, rare = Rare, grams = Grams, value = FishingSpecies.Value(Kind, Rare, Grams) };
        }

        public void Cancel() { Phase = CatchPhase.Idle; Failure = CatchFailure.None; }
        private void Escape(CatchFailure reason) { Failure = reason; Enter(CatchPhase.Escaped, 0f); }
        private void Enter(CatchPhase phase, float deadline) { Phase = phase; _timer = 0f; _deadline = deadline; }
        private static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(max, x));
    }

    [Serializable]
    public sealed class FishingSaveData
    {
        public int version = 5;
        public int expeditionFacts;
        public int shoreTaskMask;
        public int shoreRewardMask;
        public int discoveryMask;
        public int wildlifeMask;
        public int catches;
        public int ordersCompleted;
        public int coins;
        // Line upgrade level. The v1/v2 name is kept so older saves load without migration code.
        public int rodLevel;
        public int reelLevel;
        public int holdLevel;
        public int legendCatches;
        public List<int> bestGrams = new List<int>();
        public List<CatchRecord> inventory = new List<CatchRecord>();
        public List<string> recordedIds = new List<string>();
    }

    public sealed class FishingCareer
    {
        public const int BaseCapacity = 6;
        public const int MaxLevel = 2;
        // Enough history to reject any replayed catch while keeping the save file bounded.
        public const int RecentIdLimit = 256;
        private static readonly int[,] UpgradeCosts = { { 30, 90 }, { 40, 100 }, { 50, 120 } };

        public int Coins { get; private set; }
        public FishingExpedition Expedition { get; private set; } = new FishingExpedition();
        public int ShoreTaskMask { get; private set; }
        public int ShoreRewardMask { get; private set; }
        public int DiscoveryMask { get; private set; }
        public int WildlifeMask { get; private set; }
        public int Catches { get; private set; }
        public int OrdersCompleted { get; private set; }
        public int LegendCatches { get; private set; }
        public int LineLevel { get; private set; }
        public int ReelLevel { get; private set; }
        public int HoldLevel { get; private set; }
        public int Capacity => BaseCapacity + 2 * HoldLevel;
        public IReadOnlyList<CatchRecord> Inventory => _inventoryView ?? (_inventoryView = _inventory.AsReadOnly());
        public int Count => _inventory.Count;
        public FishingOrder CurrentOrder
        {
            get
            {
                if (_currentOrder == null || _currentOrder.Index != OrdersCompleted)
                    _currentOrder = FishingOrders.For(OrdersCompleted);
                return _currentOrder;
            }
        }
        public bool OrderReady => FishingOrders.Select(CurrentOrder, _inventory, _picked);

        /// <summary>Fish currently earmarked for the open order; a normal sale keeps them aboard.</summary>
        public int ReservedCount
        {
            get
            {
                FishingOrders.Select(CurrentOrder, _inventory, _picked);
                return _picked.Count;
            }
        }

        public int HoldValue
        {
            get
            {
                int total = 0;
                foreach (var item in _inventory) total += item.value;
                return total;
            }
        }

        /// <summary>What a normal sale pays now: the hold value minus fish reserved for the open order.</summary>
        public int SaleValue
        {
            get
            {
                FishingOrders.Select(CurrentOrder, _inventory, _picked);
                int total = 0;
                for (int i = 0; i < _inventory.Count; i++) if (!_picked.Contains(i)) total += _inventory[i].value;
                return total;
            }
        }

        private readonly List<CatchRecord> _inventory = new List<CatchRecord>();
        private IReadOnlyList<CatchRecord> _inventoryView;
        private FishingOrder _currentOrder;
        private readonly HashSet<string> _recorded = new HashSet<string>();
        private readonly List<string> _recentIds = new List<string>();
        private readonly List<int> _picked = new List<int>();
        private readonly int[] _bestGrams = new int[FishingSpecies.Count];

        public bool Seen(MarineKind kind) => Enum.IsDefined(typeof(MarineKind), kind) && (WildlifeMask & (1 << (int)kind)) != 0;

        public bool Observe(MarineKind kind)
        {
            if (!Enum.IsDefined(typeof(MarineKind), kind) || Seen(kind)) return false;
            WildlifeMask |= 1 << (int)kind;
            return true;
        }

        public bool Discovered(FishKind kind, bool rare = false)
            => FishingSpecies.IsDefined(kind) && (DiscoveryMask & (1 << FishingSpecies.DiscoveryBit(kind, rare))) != 0;

        public int BestGrams(FishKind kind) => FishingSpecies.IsDefined(kind) ? _bestGrams[(int)kind] : 0;

        public int Owned(FishKind kind)
        {
            int count = 0;
            foreach (var fish in _inventory) if (fish.kind == kind) count++;
            return count;
        }

        public int Level(UpgradeTrack track) => track == UpgradeTrack.Line ? LineLevel : track == UpgradeTrack.Reel ? ReelLevel : HoldLevel;

        /// <summary>Price of the next level, or 0 when the track is complete.</summary>
        public static int UpgradeCost(UpgradeTrack track, int level)
            => level < 0 || level >= MaxLevel || !Enum.IsDefined(typeof(UpgradeTrack), track) ? 0 : UpgradeCosts[(int)track, level];

        public int NextUpgradeCost(UpgradeTrack track) => UpgradeCost(track, Level(track));

        public bool Upgrade(UpgradeTrack track)
        {
            int cost = NextUpgradeCost(track);
            if (cost <= 0 || Coins < cost) return false;
            Coins -= cost;
            if (track == UpgradeTrack.Line) LineLevel++;
            else if (track == UpgradeTrack.Reel) ReelLevel++;
            else HoldLevel++;
            return true;
        }

        public bool Store(CatchRecord fish)
        {
            if (!Valid(fish) || FishingSpecies.Of(fish.kind).Legendary || Count >= Capacity || !Remember(fish.id)) return false;
            _inventory.Add(fish.Copy());
            Record(fish);
            return true;
        }

        /// <summary>The legend is measured and released: a bounty instead of hold space. Returns 0 if rejected.</summary>
        public int RecordLegend(CatchRecord legend)
        {
            if (!Valid(legend) || !FishingSpecies.Of(legend.kind).Legendary || !Remember(legend.id)) return 0;
            int bounty = LegendRules.Bounty(LegendCatches == 0, legend.grams);
            LegendCatches++;
            Coins += bounty;
            Record(legend);
            return bounty;
        }

        /// <summary>Sells everything except fish reserved for the open order.</summary>
        public int Sell()
        {
            FishingOrders.Select(CurrentOrder, _inventory, _picked);
            int value = 0;
            for (int i = _inventory.Count - 1; i >= 0; i--)
            {
                if (_picked.Contains(i)) continue;
                value += _inventory[i].value;
                _inventory.RemoveAt(i);
            }
            Coins += value;
            return value;
        }

        public int SellAll()
        {
            int value = HoldValue;
            Coins += value;
            _inventory.Clear();
            return value;
        }

        /// <summary>Atomic: pays the full value of the cheapest eligible fish plus the bonus, or nothing.</summary>
        public int DeliverOrder()
        {
            var order = CurrentOrder;
            if (OrdersCompleted >= 1000000 || !FishingOrders.Select(order, _inventory, _picked)) return 0;
            int payout = order.Bonus;
            _picked.Sort();
            for (int i = _picked.Count - 1; i >= 0; i--)
            {
                payout += _inventory[_picked[i]].value;
                _inventory.RemoveAt(_picked[i]);
            }
            Coins += payout;
            OrdersCompleted++;
            return payout;
        }

        public bool CompleteShoreTask(int task)
        {
            if (task < 0 || task >= 8 || FishingExploration.Done(ShoreTaskMask, task)) return false;
            ShoreTaskMask |= 1 << task;
            return true;
        }

        public int ClaimShoreRewards()
        {
            int payout = 0;
            int before = ShoreRewardMask;
            for (int i = 0; i < FishingExploration.SiteCount; i++)
                if (FishingExploration.SiteDone(ShoreTaskMask, i) && (ShoreRewardMask & (1 << i)) == 0)
                { ShoreRewardMask |= 1 << i; payout += 25; }
            if (before != FishingExploration.RewardMask && ShoreRewardMask == FishingExploration.RewardMask) payout += 60;
            Coins += payout;
            return payout;
        }

        public FishingSaveData Snapshot()
        {
            var result = new FishingSaveData
            {
                expeditionFacts = Expedition.Facts,
                shoreTaskMask = ShoreTaskMask, shoreRewardMask = ShoreRewardMask,
                coins = Coins, rodLevel = LineLevel, reelLevel = ReelLevel, holdLevel = HoldLevel,
                discoveryMask = DiscoveryMask, wildlifeMask = WildlifeMask, catches = Catches,
                ordersCompleted = OrdersCompleted, legendCatches = LegendCatches,
                bestGrams = new List<int>(_bestGrams), recordedIds = new List<string>(_recentIds)
            };
            foreach (var item in _inventory) result.inventory.Add(item.Copy());
            return result;
        }

        public static bool TryRestore(FishingSaveData data, out FishingCareer career)
        {
            career = null;
            if (data == null || data.version < 1 || data.version > 5) return false;
            if (!FishingExpedition.TryRestore(data.expeditionFacts, out var expedition)
                || (data.version < 5 && data.expeditionFacts != 0)) return false;
            bool v3 = data.version >= 3;
            if (!FishingExploration.Valid(data.shoreTaskMask, data.shoreRewardMask)
                || (data.version < 4 && (data.shoreTaskMask != 0 || data.shoreRewardMask != 0))) return false;
            if (data.coins < 0 || data.coins > 100000000 || data.rodLevel < 0 || data.rodLevel > (v3 ? MaxLevel : 1)
                || data.reelLevel < 0 || data.reelLevel > (v3 ? MaxLevel : 0) || data.holdLevel < 0 || data.holdLevel > (v3 ? MaxLevel : 0)
                || data.wildlifeMask < 0 || data.wildlifeMask > 31
                || data.discoveryMask < 0 || data.discoveryMask > (v3 ? FishingSpecies.DiscoveryMaskLimit : 63)
                || data.catches < 0 || data.catches > 100000000 || data.ordersCompleted < 0 || data.ordersCompleted > 1000000
                || data.legendCatches < 0 || data.legendCatches > 1000000 || (!v3 && data.legendCatches != 0)
                || data.inventory == null || data.recordedIds == null
                || (data.bestGrams != null && data.bestGrams.Count > FishingSpecies.Count)) return false;
            var result = new FishingCareer { HoldLevel = data.holdLevel };
            if (data.inventory.Count > result.Capacity) return false;
            foreach (var item in data.inventory) if (!result.Store(item)) return false;
            foreach (var id in data.recordedIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return false;
                result.Remember(id);
            }
            if (data.bestGrams != null)
                for (int i = 0; i < data.bestGrams.Count; i++)
                {
                    int grams = data.bestGrams[i];
                    if (grams != 0 && !FishingSpecies.ValidGrams((FishKind)i, grams)) return false;
                    result._bestGrams[i] = Math.Max(result._bestGrams[i], grams);
                }
            // v1 can recover only fish still in the hold; sold species were never recorded.
            if (data.version >= 2)
            {
                bool legendSeen = (data.discoveryMask & (1 << FishingSpecies.LegendDiscoveryBit)) != 0;
                if ((data.discoveryMask & result.DiscoveryMask) != result.DiscoveryMask
                    || data.catches < result.Count + data.legendCatches || legendSeen != data.legendCatches > 0) return false;
                result.WildlifeMask = data.wildlifeMask;
                result.DiscoveryMask = data.discoveryMask;
                result.Catches = data.catches;
                result.OrdersCompleted = data.ordersCompleted;
                result.LegendCatches = data.legendCatches;
            }
            result.Expedition = expedition;
            result.ShoreTaskMask = data.shoreTaskMask;
            result.ShoreRewardMask = data.shoreRewardMask;
            result.Coins = data.coins;
            result.LineLevel = data.rodLevel;
            result.ReelLevel = data.reelLevel;
            career = result;
            return true;
        }

        private void Record(CatchRecord fish)
        {
            DiscoveryMask |= 1 << FishingSpecies.DiscoveryBit(fish.kind, fish.rare);
            _bestGrams[(int)fish.kind] = Math.Max(_bestGrams[(int)fish.kind], fish.grams);
            Catches++;
        }

        /// <summary>Adds an ID to the bounded replay history. False when it was already processed.</summary>
        private bool Remember(string id)
        {
            if (!_recorded.Add(id)) return false;
            _recentIds.Add(id);
            for (int i = 0; _recentIds.Count > RecentIdLimit && i < _recentIds.Count; )
            {
                // IDs of fish still aboard must stay known, or the same catch could be stored twice.
                if (IsAboard(_recentIds[i])) { i++; continue; }
                _recorded.Remove(_recentIds[i]);
                _recentIds.RemoveAt(i);
            }
            return true;
        }

        private bool IsAboard(string id)
        {
            foreach (var fish in _inventory) if (fish.id == id) return true;
            return false;
        }

        private static bool Valid(CatchRecord record) => record != null && !string.IsNullOrWhiteSpace(record.id)
            && record.id.Length <= 64 && FishingSpecies.IsDefined(record.kind)
            && !(record.rare && FishingSpecies.Of(record.kind).Legendary)
            && FishingSpecies.ValidGrams(record.kind, record.grams)
            && record.value == FishingSpecies.Value(record.kind, record.rare, record.grams);
    }
}
