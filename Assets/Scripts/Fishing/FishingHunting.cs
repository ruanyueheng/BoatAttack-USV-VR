using System;
using System.Collections.Generic;
namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class HuntingData
    {
        public List<int> availableDays = new List<int>(new int[FishingBackpack.PickupCount]);
        public List<double> availableMinutes;
        public int harvests;
    }
    /// <summary>Stable spawn IDs and saved return dates prevent repeated meat after a reload.</summary>
    public sealed class FishingHunting
    {
        public const double RecoveryMinutes = 480; // Five active real minutes at the 15-minute day rate.
        private HuntingData _data = new HuntingData { availableMinutes = new List<double>(new double[FishingBackpack.PickupCount]) };
        private readonly HashSet<int> _liveCaptures=new HashSet<int>();
        public int Harvests => _data.harvests;
        public bool Available(int id, double now) => id >= 0 && id < _data.availableMinutes.Count && !_liveCaptures.Contains(id) && !double.IsNaN(now)
            && !double.IsInfinity(now) && now >= _data.availableMinutes[id];
        internal void SetLiveCapture(int id,bool captured)
        {if(captured)_liveCaptures.Add(id);else _liveCaptures.Remove(id);}
        public static bool Huntable(LandAnimalKind kind) => kind == LandAnimalKind.Rabbit || kind == LandAnimalKind.Fox;
        public bool Harvest(FishingBackpack bag, int id, LandAnimalKind kind, double now)
        {
            if (bag == null || !Huntable(kind) || now < 0 || now > FishingCalendar.MaxDay * 1440d || !Available(id, now)
                || bag.Equipped != FieldItem.HuntingSpear || !bag.TryAddBundle(FishingAnimalLoot.Drops(kind,true))) return false;
            _data.availableMinutes[id] = now + RecoveryMinutes;
            _data.availableDays[id] = (int)(now / 1440d) + 1; _data.harvests++; return true;
        }
        public HuntingData Snapshot() => new HuntingData { availableDays = new List<int>(_data.availableDays),
            availableMinutes = new List<double>(_data.availableMinutes), harvests = _data.harvests };
        public static bool TryRestore(HuntingData data, out FishingHunting hunting, double now = 0)
        {
            hunting = null;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0 || data == null || data.availableDays == null || data.availableDays.Count != FishingBackpack.PickupCount
                || data.harvests < 0 || data.harvests > 100000000) return false;
            foreach (int day in data.availableDays) if (day < 0 || day > FishingCalendar.MaxDay) return false;
            var minutes = data.availableMinutes;
            if (minutes == null)
            {
                minutes = new List<double>();
                foreach (int day in data.availableDays) minutes.Add(Math.Min(day * 1440d, now + RecoveryMinutes));
            }
            if (minutes.Count != FishingBackpack.PickupCount) return false;
            foreach (double due in minutes) if (double.IsNaN(due) || double.IsInfinity(due) || due < 0 || due > now + RecoveryMinutes) return false;
            hunting = new FishingHunting { _data = new HuntingData { availableDays = new List<int>(data.availableDays),
                availableMinutes = new List<double>(minutes), harvests = data.harvests } }; return true;
        }
    }
}
