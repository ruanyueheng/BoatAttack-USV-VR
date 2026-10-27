using System;
using System.Collections.Generic;

namespace BoatAttack.Fishing
{
    public static class FishingFleet
    {
        public static float Power(int level) => 11f + 4f * Math.Max(0, Math.Min(2, level));
        public static float SpeedLimit(int level) => 11f + 3f * Math.Max(0, Math.Min(2, level));
        public static float Rudder(int level) => 1f + .22f * Math.Max(0, Math.Min(2, level));
    }

    public static class FishingSupplies
    {
        public static readonly FieldItem[] Items = { FieldItem.Binoculars, FieldItem.Lantern, FieldItem.StonePickaxe,
            FieldItem.IronPickaxe, FieldItem.HuntingSpear, FieldItem.Rope, FieldItem.Plank, FieldItem.Coal,
            FieldItem.IronIngot, FieldItem.Glass, FieldItem.Workbench, FieldItem.Furnace,
            FieldItem.CookedMeat, FieldItem.Fiber, FieldItem.Stick, FieldItem.Charcoal, FieldItem.Sand, FieldItem.Stone,
            FieldItem.Forge, FieldItem.IronPickHead, FieldItem.GunParts, FieldItem.Clay, FieldItem.IronOre, FieldItem.TurnipSeeds,
            FieldItem.FishFeed, FieldItem.CaptureNet, FieldItem.AnimalTrap, FieldItem.Aquarium, FieldItem.RabbitHutch, FieldItem.TurnipSeeds, FieldItem.Axe, FieldItem.Shovel, FieldItem.Rake, FieldItem.Driftwood, FieldItem.IronIngot, FieldItem.Fiber };
        private static readonly int[] Prices = { 55, 40, 30, 95, 35, 14, 18, 16, 35, 28, 65, 85, 16, 14, 12, 18, 12, 16, 200, 80, 160, 20, 45, 18, 12, 75, 60, 180, 160, 28, 85, 55, 70, 20, 35, 14 };
        private static readonly int[] Counts = { 1, 1, 1, 1, 1, 4, 6, 4, 3, 4, 1, 1, 3, 8, 8, 4, 8, 8, 1, 1, 1, 8, 6, 4, 8, 1, 1, 1, 1, 8, 1, 1, 1, 4, 3, 8 };
        public const int CategorySize = 6;
        public const int CategoryCount = 6;
        public static string CategoryName(int category) => category == 0 ? "探索工具" : category == 1 ? "制作补给" : category == 2 ? "远航物资" : category == 3 ? "建设物资" : category == 4 ? "家园生态" : category == 5 ? "农林工具" : "";
        public static bool Valid(int product) => product >= 0 && product < Items.Length;
        public static int Price(int product) => Valid(product) ? Prices[product] : 0;
        public static int Quantity(int product) => Valid(product) ? Counts[product] : 0;
    }

    [Serializable]
    public sealed class AdventureData
    {
        public int visited, treasures, beacons;
        public bool finale;
        public List<int> directions = new List<int>(new int[4]);
    }

    /// <summary>Four independent island stories. Stable bits and paid facts prevent repeat rewards.</summary>
    public sealed class FishingAdventure
    {
        public const int Count = 4;
        private AdventureData _data = new AdventureData();
        public static string Name(int island) => island == 0 ? "月牙风铃岛" : island == 1 ? "翡翠潮林岛" : island == 2 ? "沉船沙洲" : "星望遗迹岛";
        public static string Clue(int island) => island == 0 ? "风铃随落日而鸣，灯头应朝西；财宝藏在双岩拱后。"
            : island == 1 ? "北风穿过潮林，灯头应朝北；财宝藏在老树根旁。"
            : island == 2 ? "船长追逐日出，灯头应朝东；船首残骸下藏着旧货箱。"
            : "南方星光引船回家，灯头应朝南；财宝守在残柱之间。";
        public static string DirectionName(int direction) => direction == 0 ? "北" : direction == 1 ? "东" : direction == 2 ? "南" : "西";
        private static int Target(int island) => island == 0 ? 3 : island == 1 ? 0 : island == 2 ? 1 : 2;
        private static bool ValidId(int island) => island >= 0 && island < Count;
        public bool Visited(int island) => ValidId(island) && (_data.visited & (1 << island)) != 0;
        public bool TreasureFound(int island) => ValidId(island) && (_data.treasures & (1 << island)) != 0;
        public bool Restored(int island) => ValidId(island) && (_data.beacons & (1 << island)) != 0;
        public int Direction(int island) => ValidId(island) ? _data.directions[island] : 0;
        public bool Finale => _data.finale;
        public bool ReadyForFinale => _data.beacons == 15 && !_data.finale;
        public int Discover(int island)
        {
            if (!ValidId(island) || Visited(island)) return 0;
            _data.visited |= 1 << island; return 10;
        }
        public int OpenTreasure(int island)
        {
            if (!Visited(island) || TreasureFound(island)) return 0;
            _data.treasures |= 1 << island; return 40;
        }
        public bool Rotate(int island)
        {
            if (!Visited(island) || Restored(island)) return false;
            _data.directions[island] = (Direction(island) + 1) % 4; return true;
        }
        public int LightBeacon(int island)
        {
            if (!Visited(island) || Restored(island) || Direction(island) != Target(island)) return 0;
            _data.beacons |= 1 << island; return 75;
        }
        public int CompleteVoyage()
        {
            if (!ReadyForFinale) return 0;
            _data.finale = true; return 150;
        }
        public AdventureData Snapshot() => new AdventureData { visited = _data.visited, treasures = _data.treasures,
            beacons = _data.beacons, finale = _data.finale, directions = new List<int>(_data.directions) };
        public static bool TryRestore(AdventureData data, out FishingAdventure adventure)
        {
            adventure = null;
            if (data == null || data.visited < 0 || data.visited > 15 || data.treasures < 0 || data.treasures > 15
                || data.beacons < 0 || data.beacons > 15 || (data.treasures & ~data.visited) != 0
                || (data.beacons & ~data.visited) != 0 || (data.finale && data.beacons != 15)
                || data.directions == null || data.directions.Count != Count) return false;
            for (int i = 0; i < Count; i++)
                if (data.directions[i] < 0 || data.directions[i] > 3
                    || ((data.beacons & (1 << i)) != 0 && data.directions[i] != Target(i))) return false;
            adventure = new FishingAdventure { _data = new AdventureData { visited = data.visited, treasures = data.treasures,
                beacons = data.beacons, finale = data.finale, directions = new List<int>(data.directions) } }; return true;
        }
    }

    public sealed partial class FishingCareer
    {
        public FishingAdventure Adventure { get; private set; } = new FishingAdventure();
        public int EngineLevel { get; private set; }
        public int RudderLevel { get; private set; }
        public int CabinLevel { get; private set; }
        public bool BuySupply(int product)
        {
            int cost = FishingSupplies.Price(product);
            if (cost <= 0 || Coins < cost || !Backpack.TryAdd(FishingSupplies.Items[product], FishingSupplies.Quantity(product))) return false;
            Coins -= cost; return true;
        }
        public int DiscoverIsland(int island) { if(Coins>100000000-10)return 0;int pay = Adventure.Discover(island); Coins += pay; return pay; }
        public int OpenIslandTreasure(int island) { if(Coins>100000000-40)return 0;int pay = Adventure.OpenTreasure(island); Coins += pay; return pay; }
        public int RestoreIslandBeacon(int island) { if(Coins>100000000-75)return 0;int pay = Adventure.LightBeacon(island); Coins += pay; return pay; }
        public int CompleteIslandVoyage() { if(Coins>100000000-150)return 0;int pay = Adventure.CompleteVoyage(); Coins += pay; return pay; }
    }
}
