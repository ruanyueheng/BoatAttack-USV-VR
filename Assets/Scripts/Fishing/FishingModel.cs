using System;
using System.Collections.Generic;

namespace BoatAttack.Fishing
{
    public enum CatchPhase { Idle, Waiting, Bite, Fighting, Landing, Caught, Escaped }
    public enum FishKind { Coral, Dart, Puffer, Sailfish, Mackerel, SeaBream, Sardine, Grouper, Butterfly, Tuna, Lanternfish, Lionfish, SeaTurtle, Octopus, Crab, Lobster, Seahorse, Stingray, Jellyfish, Cuttlefish }

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
        public FishBeat Beat => FishingVoyage.Beat(Kind, _beatTime);
        public float BeatProgress => _beatTime / Species.BeatSeconds;
        public float FightTime { get; private set; }
        public float FishDirection => Phase == CatchPhase.Fighting
            ? FishingFightControl.Direction(FightTime, Species.BeatSeconds, _directionSeed) : 0;
        public float ControlQuality { get; private set; }
        public float Stamina { get; private set; } = 1;
        public float BitePreview => Phase == CatchPhase.Waiting ? FishingFightControl.BitePreview(_timer, _deadline)
            : Phase == CatchPhase.Bite ? 1 : 0;
        public float StrikeQuality { get; private set; }
        public bool PerfectStrike => StrikeQuality >= .85f;
        public float Struggle => (Beat == FishBeat.Leap ? LeapEffort : Beat == FishBeat.Surge ? Species.SurgeEffort : Beat == FishBeat.Windup ? 1f : .75f) * _surgeScale;
        /// <summary>0-1 across the current leap and 0 outside one; drives the airborne arc.</summary>
        public float LeapProgress => Beat == FishBeat.Leap
            ? (BeatProgress - FishingVoyage.LeapStart) / (FishingVoyage.LeapEnd - FishingVoyage.LeapStart) : 0f;
        public float Distance { get; private set; }
        public float MaxDistance { get; private set; }
        public float LandPointer => (float)(0.5 + 0.5 * Math.Sin(_timer * (Species.Legendary ? 3.6 : 3.1)));
        // Half-width of the scoop window around the centre of the landing gauge.
        public float LandZone => Species.Legendary ? 0.15f : 0.25f;
        public bool CanLand => Phase == CatchPhase.Landing && Math.Abs(LandPointer - 0.5f) <= LandZone;
        public bool CanNet => Phase == CatchPhase.Landing && Tension < .85f;
        public CatchFailure Failure { get; private set; }
        public float Remaining => Math.Max(0f, _deadline - _timer);
        private string _id;
        private float _timer, _deadline, _overload, _slack, _sizeTension = 1f;
        private int _lineLevel, _reelLevel, _misses;
        private bool _claimed;
        private uint _rhythmSeed, _directionSeed;
        private float _beatTime, _rhythmRate = 1f, _surgeScale = 1f;

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
            _rhythmSeed = 2166136261;
            foreach (char letter in id) _rhythmSeed = unchecked((_rhythmSeed ^ letter) * 16777619);
            // Rhythm advances once per beat; lateral direction keeps this immutable cast seed.
            _directionSeed = _rhythmSeed;
            _beatTime = FightTime = 0;
            ControlQuality = StrikeQuality = 0;
            Stamina = 1;
            NextRhythm();
            _misses = 0;
            _overload = _slack = 0f;
            Tension = 0.3f;
            Failure = CatchFailure.None;
            Enter(CatchPhase.Waiting, species.BiteSeconds * BayClimate.BiteScale(weather, kind));
            return true;
        }

        public void Tick(float deltaTime, bool reel) => Tick(deltaTime, reel, 0);

        public void Tick(float deltaTime, bool reel, float rodSide)
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
                    FightTime += dt;
                    _beatTime += dt * _rhythmRate;
                    if (_beatTime >= Species.BeatSeconds) { _beatTime -= Species.BeatSeconds; NextRhythm(); }
                    ControlQuality = FishingFightControl.Quality(rodSide, FishDirection);
                    Fight(dt, reel);
                    break;
                case CatchPhase.Landing:
                    // The drag keeps the line taut while the left hand moves from reel to net.
                    Tension = Clamp(Tension + (reel ? Species.TensionGain * 1.8f : -.48f) * dt, .2f, 1f);
                    _overload = Tension >= .98f ? _overload + dt : 0f;
                    if (_overload >= .7f) Escape(CatchFailure.LineSnapped);
                    else if (_timer >= _deadline) Escape(CatchFailure.LandingTimeout);
                    break;
            }
        }

        private void NextRhythm()
        {
            _rhythmSeed = unchecked(_rhythmSeed * 1664525 + 1013904223);
            _rhythmRate = .85f + (_rhythmSeed & 255) / 255f * .3f;
            _surgeScale = .9f + ((_rhythmSeed >> 8) & 255) / 255f * .2f;
            // Later legendary cycles shorten the apparent rest before another warned lunge.
            if (Species.Legendary && FightTime >= 10f) _rhythmRate *= 1.35f;
        }

        private void Fight(float dt, bool reel)
        {
            var species = Species;
            var beat = Beat;
            float effort = Struggle;
            float gain = species.TensionGain * effort * FishingVoyage.TensionScale(Bait) * LineFactor(_lineLevel) * _sizeTension;
            float retrieve = beat == FishBeat.Rest ? 2.65f : beat == FishBeat.Windup ? 1.8f : beat == FishBeat.Leap ? 0f : 0.8f;
            retrieve *= FishingVoyage.ReelScale(Bait) * ReelFactor(_reelLevel);
            // Neutral input preserves the original arithmetic exactly. A good lean is assistance,
            // not permission to reel through a legendary leap or ignore excess line tension.
            if (ControlQuality != 0 && beat != FishBeat.Leap)
            {
                gain *= FishingFightControl.TensionMultiplier(ControlQuality);
                retrieve *= FishingFightControl.RetrieveMultiplier(ControlQuality);
            }
            // An airborne legend takes line quickly when it is not being fought.
            float run = beat == FishBeat.Leap ? 1.1f : 0.35f;
            Tension = Clamp(Tension + (reel ? gain : -0.48f) * dt, 0f, 1f);
            Distance = Clamp(Distance + (reel ? -retrieve : run) * dt, 0f, MaxDistance);
            _overload = Tension >= 0.98f ? _overload + dt : 0f;
            _slack = Tension <= 0.02f ? _slack + dt : 0f;
            if (_overload >= 0.7f) Escape(beat == FishBeat.Leap ? CatchFailure.LeapSnapped : CatchFailure.LineSnapped);
            else if (_slack >= 4f) Escape(CatchFailure.SlackLine);
            else if (_timer > _deadline) Escape(CatchFailure.SwamAway);
            else if (Distance <= 0f)
            {
                if (species.Legendary && FightTime < 20f) Distance = .7f;
                else { Tension = Math.Min(Tension, .74f); _overload = 0; Enter(CatchPhase.Landing, 10f); }
            }
            // Fatigue is feedback, not another landing gate. A fish can take line without regaining stamina.
            if (Phase == CatchPhase.Fighting)
                Stamina = Math.Max(0, Math.Min(Stamina, Distance / MaxDistance)
                    - Math.Max(0, ControlQuality) * dt * (reel ? .012f : .004f));
        }

        public bool Hook()
        {
            if (Phase != CatchPhase.Bite) return false;
            StrikeQuality = FishingFightControl.StrikeQuality(_timer);
            Enter(CatchPhase.Fighting, Species.FightSeconds);
            return true;
        }

        public bool Land()
        {
            if (Phase != CatchPhase.Landing) return false;
            if (CanLand && CanNet) { Enter(CatchPhase.Caught, 0f); return true; }
            if (++_misses >= 3) Escape(CatchFailure.NetMissed);
            return false;
        }

        public bool LandFromNet(){if(!CanNet)return false;Enter(CatchPhase.Caught,0f);return true;}

        public CatchRecord Claim()
        {
            if (Phase != CatchPhase.Caught || _claimed) return null;
            _claimed = true;
            return new CatchRecord { id = _id, kind = Kind, rare = Rare, grams = Grams, value = FishingSpecies.Value(Kind, Rare, Grams) };
        }

        public void Cancel()
        {
            Phase = CatchPhase.Idle; Failure = CatchFailure.None;
            ControlQuality = StrikeQuality = FightTime = _beatTime = 0; Stamina = 1;
        }
        private void Escape(CatchFailure reason) { Failure = reason; Enter(CatchPhase.Escaped, 0f); }
        private void Enter(CatchPhase phase, float deadline)
        {
            Phase = phase; _timer = 0f; _deadline = deadline;
            if (phase != CatchPhase.Fighting) ControlQuality = 0;
            if (phase == CatchPhase.Landing || phase == CatchPhase.Caught) Stamina = 0;
        }
        private static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(max, x));
    }

    [Serializable]
    public sealed class FishingSaveData
    {
        public List<HuntedAnimalData> huntedAnimals=new List<HuntedAnimalData>();
        public const int CurrentVersion = 20;
        public int version = CurrentVersion;
        public List<FieldStack> homeStorage;
        public int[] homeFruitDays;
        public FarmData farm = new FishingFarm().Snapshot();
        public HomeEcologyData homeEcology = new HomeEcologyData();
        public CombatData combat = new CombatData();
        public FishingBarbecueData barbecue = new FishingBarbecueData();
        public bool prologueSeen;

        public LoreData lore = new LoreData();
        public int engineLevel, rudderLevel, cabinLevel;
        public AdventureData adventure = new AdventureData();
        public CalendarData calendar = new CalendarData();
        public HuntingData hunting = new HuntingData();
        public WorkshopData workshop = new FishingWorkshop().Snapshot();
        public PendingCatchData pendingCatch;
        public bool hasPendingCatch;
        public int releasedCatches;
        public bool orderMarked;
        public List<FieldStack> backpack;
        public int selectedSlot, collectedPickupMask, landAnimalMask;
        public int expeditionFacts;
        public int shoreTaskMask;
        public int shoreRewardMask;
        public long discoveryMask;
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

    public sealed partial class FishingCareer
    {
        public FishingCalendar Calendar { get; private set; } = new FishingCalendar();
        public FishingHunting Hunting { get; private set; } = new FishingHunting();
        public FishingWorkshop Workshop { get; private set; } = new FishingWorkshop();
        public FishingBackpack Backpack { get; private set; } = new FishingBackpack();
        public const int BaseCapacity = 6;
        public const int MaxLevel = 2;
        // Enough history to reject any replayed catch while keeping the save file bounded.
        public const int RecentIdLimit = 256;
        private static readonly int[,] UpgradeCosts = { { 30, 90 }, { 40, 100 }, { 50, 120 }, { 80, 180 }, { 65, 150 }, { 90, 220 } };

        public int Coins { get; private set; }
        // Session-only purchasing policy; never replaces or serializes the real wallet.
        public bool CreativeMode { get; private set; }
        public string CoinDisplay => CreativeMode ? "∞" : Coins.ToString();
        public void SetCreativeMode(bool enabled) => CreativeMode=enabled;
        public bool CanAfford(int amount) => amount>=0 && (CreativeMode || Coins>=amount);
        public bool SpendCoins(int amount)
        {
            if(!CanAfford(amount))return false;
            if(!CreativeMode)Coins-=amount;
            return true;
        }
        public FishingExpedition Expedition { get; private set; } = new FishingExpedition();
        public int ShoreTaskMask { get; private set; }
        public int ShoreRewardMask { get; private set; }
        public long DiscoveryMask { get; private set; }
        public bool PrologueSeen { get; private set; }
        public void FinishPrologue() => PrologueSeen = true;
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
            => FishingSpecies.IsDefined(kind) && (DiscoveryMask & (1L << FishingSpecies.DiscoveryBit(kind, rare))) != 0;

        public int BestGrams(FishKind kind) => FishingSpecies.IsDefined(kind) ? _bestGrams[(int)kind] : 0;

        public int Owned(FishKind kind)
        {
            int count = 0;
            foreach (var fish in _inventory) if (fish.kind == kind) count++;
            return count;
        }

        public int Level(UpgradeTrack track)
        {
            switch (track) { case UpgradeTrack.Line: return LineLevel; case UpgradeTrack.Reel: return ReelLevel;
                case UpgradeTrack.Hold: return HoldLevel; case UpgradeTrack.Engine: return EngineLevel;
                case UpgradeTrack.Rudder: return RudderLevel; case UpgradeTrack.Cabin: return CabinLevel; default: return -1; }
        }

        /// <summary>Price of the next level, or 0 when the track is complete.</summary>
        public static int UpgradeCost(UpgradeTrack track, int level)
            => level < 0 || level >= MaxLevel || !Enum.IsDefined(typeof(UpgradeTrack), track) ? 0 : UpgradeCosts[(int)track, level];

        public int NextUpgradeCost(UpgradeTrack track) => UpgradeCost(track, Level(track));
        public string UpgradePurchaseLabel(UpgradeTrack track)
        {
            if(SpecializationBlocked(track))return "专精互斥";
            int cost=NextUpgradeCost(track);
            return cost<=0?"已满级":CreativeMode?"创造模式免费":cost+" 金币";
        }

        public bool SpecializationBlocked(UpgradeTrack track) => Level(track) == 1
            && ((track == UpgradeTrack.Hold && (EngineLevel == 2 || RudderLevel == 2))
                || (track == UpgradeTrack.Engine && (HoldLevel == 2 || RudderLevel == 2))
                || (track == UpgradeTrack.Rudder && (HoldLevel == 2 || EngineLevel == 2)));

        public bool Upgrade(UpgradeTrack track)
        {
            int cost = NextUpgradeCost(track);
            if (cost <= 0 || !CanAfford(cost) || SpecializationBlocked(track)) return false;
            SpendCoins(cost);
            if (track == UpgradeTrack.Line) LineLevel++;
            else if (track == UpgradeTrack.Reel) ReelLevel++;
            else if (track == UpgradeTrack.Hold) HoldLevel++;
            else if (track == UpgradeTrack.Engine) EngineLevel++;
            else if (track == UpgradeTrack.Rudder) RudderLevel++;
            else CabinLevel++;
            return true;
        }

        public bool Store(CatchRecord fish)
        {
            if ((_pending!=null && !_resolving && fish!=null && fish.id==_pending.fish.id) || !Valid(fish) || FishingSpecies.Of(fish.kind).Legendary || Count >= Capacity || !Remember(fish.id)) return false;
            _inventory.Add(fish.Copy());
            Record(fish);
            return true;
        }

        /// <summary>The legend is measured and released: a bounty instead of hold space. Returns 0 if rejected.</summary>
        public int RecordLegend(CatchRecord legend)
        {
            if ((_pending!=null && !_resolving && legend!=null && legend.id==_pending.fish.id) || !Valid(legend) || !FishingSpecies.Of(legend.kind).Legendary || !Remember(legend.id)) return 0;
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
            OrdersCompleted++;OrderMarked=false;
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
                prologueSeen = PrologueSeen, barbecue = _barbecue.Copy(), homeEcology = _ecology.Copy(), combat = _combat.Copy(),
                calendar = Calendar.Snapshot(), hunting = Hunting.Snapshot(), lore = Lore.Snapshot(), farm=Farm.Snapshot(),
                engineLevel = EngineLevel, rudderLevel = RudderLevel, cabinLevel = CabinLevel, adventure = Adventure.Snapshot(),
                hasPendingCatch = HasPendingCatch, pendingCatch = _pending?.Copy(), releasedCatches = ReleasedCatches, orderMarked = OrderMarked,
                workshop = Workshop.Snapshot(), backpack = Backpack.Snapshot(), selectedSlot = Backpack.Selected, collectedPickupMask = Backpack.CollectedMask, landAnimalMask = Backpack.AnimalMask,
                homeStorage=HomeStorage.Snapshot(),homeFruitDays=(int[])_homeFruitDays.Clone(),
                expeditionFacts = Expedition.Facts,
                shoreTaskMask = ShoreTaskMask, shoreRewardMask = ShoreRewardMask,
                coins = Coins, rodLevel = LineLevel, reelLevel = ReelLevel, holdLevel = HoldLevel,
                discoveryMask = DiscoveryMask, wildlifeMask = WildlifeMask, catches = Catches,
                ordersCompleted = OrdersCompleted, legendCatches = LegendCatches,
                bestGrams = new List<int>(_bestGrams), recordedIds = new List<string>(_recentIds)
            };
            foreach (var item in _inventory) result.inventory.Add(item.Copy());
            foreach(var animal in _huntedAnimals)result.huntedAnimals.Add(animal.Copy());
            return result;
        }

        public static bool TryRestore(FishingSaveData data, out FishingCareer career)
        {
            career = null;
            if (data == null || data.version < 1 || data.version > FishingSaveData.CurrentVersion || (data.version < 12 && data.prologueSeen)) return false;
            if(data.version<19 && data.collectedPickupMask>255)return false;
            if(data.version<14 && !FishingFarm.Pristine(data.farm))return false;
            if(data.version<16 && !FishingLore.QuestPristine(data.lore))return false;
            if (data.version < 11 && data.lore != null && (data.lore.residents != 0 || data.lore.shrines != 0 || data.lore.finale)) return false;
            if (data.engineLevel < 0 || data.engineLevel > MaxLevel || data.rudderLevel < 0 || data.rudderLevel > MaxLevel
                || data.cabinLevel < 0 || data.cabinLevel > MaxLevel
                || (data.version < 10 && (data.engineLevel != 0 || data.rudderLevel != 0 || data.cabinLevel != 0))) return false;
            if(data.version<10 && data.adventure!=null && (data.adventure.visited!=0 || data.adventure.treasures!=0 || data.adventure.beacons!=0 || data.adventure.finale))return false;
            if (!FishingExpedition.TryRestore(data.expeditionFacts, out var expedition)
                || (data.version < 5 && data.expeditionFacts != 0)) return false;
            bool v3 = data.version >= 3;
            if (!FishingExploration.Valid(data.shoreTaskMask, data.shoreRewardMask)
                || (data.version < 4 && (data.shoreTaskMask != 0 || data.shoreRewardMask != 0))) return false;
            if (data.coins < 0 || data.coins > 100000000 || data.rodLevel < 0 || data.rodLevel > (v3 ? MaxLevel : 1)
                || data.reelLevel < 0 || data.reelLevel > (v3 ? MaxLevel : 0) || data.holdLevel < 0 || data.holdLevel > (v3 ? MaxLevel : 0)
                || data.wildlifeMask < 0 || data.wildlifeMask > 31
                || data.discoveryMask < 0 || data.discoveryMask > (data.version >= 12 ? FishingSpecies.DiscoveryMaskLimit : v3 ? (1L << 23) - 1 : 63)
                || data.catches < 0 || data.catches > 100000000 || data.ordersCompleted < 0 || data.ordersCompleted > 1000000
                || data.legendCatches < 0 || data.legendCatches > 1000000 || (!v3 && data.legendCatches != 0)
                || data.inventory == null || data.recordedIds == null
                || (data.bestGrams != null && data.bestGrams.Count > FishingSpecies.Count)) return false;
            var result = new FishingCareer { HoldLevel = data.holdLevel };
            if (data.inventory.Count > result.Capacity) return false;
            foreach (var item in data.inventory) if (item == null || (data.version < 12 && (int)item.kind >= 12) || !result.Store(item)) return false;
            foreach (var id in data.recordedIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return false;
                result.Remember(id);
            }
            if (data.bestGrams != null)
                for (int i = 0; i < data.bestGrams.Count; i++)
                {
                    int grams = data.bestGrams[i];
                    if (data.version < 12 && i >= 12 && grams != 0) return false;
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
            if(data.version>=6)
            {
                if(!FishingBackpack.TryRestore(FishingBackpack.MigrateLocalV8(data.backpack,data.version),data.selectedSlot,data.collectedPickupMask,data.landAnimalMask,out var bag))return false;
                result.Backpack=bag;
            }
            else if((data.backpack!=null && data.backpack.Count!=0) || data.selectedSlot!=0 || data.collectedPickupMask!=0 || data.landAnimalMask!=0)return false;
            result.Expedition = expedition;
            result.ShoreTaskMask = data.shoreTaskMask;
            result.ShoreRewardMask = data.shoreRewardMask;
            result.Coins = data.coins;
            result.LineLevel = data.rodLevel;
            result.ReelLevel = data.reelLevel;
            if(data.version>=7)
            {
                if(data.releasedCatches<0 || data.releasedCatches>data.catches-data.legendCatches)return false;
                if(data.hasPendingCatch)
                {
                    if(!ValidPending(data.pendingCatch) || (data.version < 12 && (int)data.pendingCatch.fish.kind >= 12) || result._recorded.Contains(data.pendingCatch.fish.id))return false;
                    result._pending=data.pendingCatch.Copy();
                }
                else if(!EmptyPending(data.pendingCatch))return false;
                result.ReleasedCatches=data.releasedCatches;result.OrderMarked=data.orderMarked;
            }
            else if(data.hasPendingCatch || !EmptyPending(data.pendingCatch) || data.releasedCatches!=0 || data.orderMarked)return false;
            if(data.version>=8)
            {
                if(!FishingWorkshop.TryRestore(data.workshop,out var workshop))return false;
                result.Workshop=workshop;
            }
            if (data.version >= 9)
            {
                if (!FishingCalendar.TryRestore(data.calendar, out var calendar) || !FishingHunting.TryRestore(data.hunting, out var hunting, calendar.TotalMinutes)) return false;
                foreach (int day in data.hunting.availableDays) if (day > calendar.Day + 1) return false;
                result.Calendar = calendar; result.Hunting = hunting;
            }
            if (data.version >= 10)
            {
                if (!FishingAdventure.TryRestore(data.adventure, out var adventure)) return false;
                result.Adventure = adventure; result.EngineLevel = data.engineLevel;
                result.RudderLevel = data.rudderLevel; result.CabinLevel = data.cabinLevel;
            }
            if (data.version >= 11)
            {
                if (!FishingLore.TryRestore(data.lore, out var lore, data.version)) return false;
                result.Lore = lore;
            }
            if(data.huntedAnimals!=null)
            {
                if(data.huntedAnimals.Count>FishingAnimalLoot.TargetCount || (data.version<8 && data.huntedAnimals.Count>0))return false;
                var ids=new HashSet<int>();
                foreach(var animal in data.huntedAnimals)
                {
                    if(!ValidHuntedAnimal(animal) || !ids.Add(animal.id))return false;
                    var receipt=animal.Copy();
                    // Pre-timer local saves still owe a full active recovery after their collected kill.
                    if(receipt.collected && !receipt.activeRecovery && receipt.respawnAt==0)
                    {receipt.activeRecovery=true;receipt.respawnSeconds=FishingAnimalLoot.RespawnSeconds;}
                    result._huntedAnimals.Add(receipt);
                }
            }
            if(data.version>=14)
            {
                if(!FishingFarm.TryRestore(data.farm,result.Calendar.Day,out var farm))return false;
                result.Farm=farm;
            }
            else result.Farm=new FishingFarm(result.Calendar.Day);
            if(!result.RestoreBarbecue(data.barbecue,data.version))return false;
            if(!result.RestoreHomeStorage(data))return false;
            if(!result.RestoreHomeEcology(data))return false;
            if(!result.RestoreCombat(data))return false;
            result.PrologueSeen = data.prologueSeen;
            if(!result.RestoreVoyageQuest(data.version))return false;

            career = result;
            return true;
        }

        private void Record(CatchRecord fish)
        {
            DiscoveryMask |= 1L << FishingSpecies.DiscoveryBit(fish.kind, fish.rare);
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
            if(AquariumContains(id))return true;
            if(_barbecue.fish!=null && _barbecue.fish.id==id)return true;
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
