using System;
using System.Collections.Generic;
namespace BoatAttack.Fishing
{
    [Serializable]
    public sealed class HuntingData
    {
        public List<int> availableDays = new List<int>(new int[FishingBackpack.PickupCount]);
        public int harvests;
    }
    /// <summary>Stable spawn IDs and saved return dates prevent repeated meat after a reload.</summary>
    public sealed class FishingHunting
    {
        private HuntingData _data = new HuntingData();
        public int Harvests => _data.harvests;
        public bool Available(int id, int day) => id >= 0 && id < _data.availableDays.Count && day >= _data.availableDays[id];
        public static bool Huntable(LandAnimalKind kind) => kind == LandAnimalKind.Rabbit || kind == LandAnimalKind.Fox;
        public bool Harvest(FishingBackpack bag, int id, LandAnimalKind kind, int day)
        {
            if (bag == null || !Huntable(kind) || day < 0 || day >= FishingCalendar.MaxDay || !Available(id, day)
                || bag.Equipped != FieldItem.HuntingSpear || !bag.TryAdd(FieldItem.RawMeat, kind == LandAnimalKind.Rabbit ? 2 : 3)) return false;
            _data.availableDays[id] = day + 1; _data.harvests++; return true;
        }
        public HuntingData Snapshot() => new HuntingData { availableDays = new List<int>(_data.availableDays), harvests = _data.harvests };
        public static bool TryRestore(HuntingData data, out FishingHunting hunting)
        {
            hunting = null;
            if (data == null || data.availableDays == null || data.availableDays.Count != FishingBackpack.PickupCount
                || data.harvests < 0 || data.harvests > 100000000) return false;
            foreach (int day in data.availableDays) if (day < 0 || day > FishingCalendar.MaxDay) return false;
            hunting = new FishingHunting { _data = data }; hunting._data = hunting.Snapshot(); return true;
        }
    }
}
