using System;
using System.Collections.Generic;
using System.Globalization;

namespace BoatAttack.Fishing
{
    public struct OrderLine
    {
        public FishKind Kind;
        public int Count;
        public int MinGrams;
        public bool Rare;

        public OrderLine(FishKind kind, int count, int minGrams = 0, bool rare = false)
        {
            Kind = kind;
            Count = count;
            MinGrams = minGrams;
            Rare = rare;
        }

        public bool Accepts(CatchRecord fish)
            => fish != null && fish.kind == Kind && (!Rare || fish.rare) && (MinGrams == 0 || fish.grams >= MinGrams);

        public string Describe()
        {
            string text = FishingSpecies.Name(Kind) + " ×" + Count;
            if (Rare) text += " · 珍珠";
            if (MinGrams > 0) text += " · ≥" + (MinGrams / 1000m).ToString("0.00#", CultureInfo.InvariantCulture) + " kg";
            return text;
        }
    }

    public sealed class FishingOrder
    {
        private readonly OrderLine[] _lines;

        public FishingOrder(int index, string name, string client, OrderLine[] lines, int bonus)
        {
            Index = index;
            Name = name;
            Client = client;
            _lines = lines;
            Bonus = bonus;
        }

        public int Index { get; }
        public string Name { get; }
        public string Client { get; }
        public int Bonus { get; }
        public int LineCount => _lines.Length;
        public OrderLine Line(int i) => _lines[i];

        public int FishCount
        {
            get
            {
                int total = 0;
                foreach (var line in _lines) total += line.Count;
                return total;
            }
        }

        public int Required(FishKind kind)
        {
            int total = 0;
            foreach (var line in _lines) if (line.Kind == kind) total += line.Count;
            return total;
        }
    }

    /// <summary>
    /// Deterministic harbor requests. The first three keep the v0.3 tutorial; later ones come from an
    /// integer hash of the order number, so every runtime and the pure tests see the same sequence.
    /// </summary>
    public static class FishingOrders
    {
        private const string Noodles = "码头面馆 · 陈伯";
        private const string Market = "渔市掌柜 · 阿珊";
        private const string Keeper = "灯塔看守 · 老林";
        private const string Scholar = "海洋学者 · 周博士";

        private static readonly FishingOrder[] Tutorial =
        {
            new FishingOrder(0, "海湾早餐", Noodles, new[] { new OrderLine(FishKind.Coral, 1), new OrderLine(FishKind.Dart, 1) }, 24),
            new FishingOrder(1, "远航补给", Market, new[] { new OrderLine(FishKind.Dart, 1), new OrderLine(FishKind.Puffer, 1) }, 32),
            new FishingOrder(2, "海岛盛宴", Keeper, new[] { new OrderLine(FishKind.Coral, 1), new OrderLine(FishKind.Dart, 1), new OrderLine(FishKind.Puffer, 1) }, 50),
        };

        private static readonly string[] TrophyNames = { "灯塔大鱼", "渔市招牌鱼", "码头称重赛" };
        private static readonly string[] PairNames = { "远航补给", "码头午餐", "渔船便当" };
        private static readonly string[] FeastNames = { "海岛盛宴", "节日烤鱼", "三鲜海湾锅" };
        private static readonly string[] PearlNames = { "珍珠标本", "学者的委托", "珍珠鱼汤" };

        public const int TrophyPermille = 650;

        public static FishingOrder For(int completed)
        {
            if (completed < 0) completed = 0;
            return completed < Tutorial.Length ? Tutorial[completed] : Generate(completed);
        }

        /// <summary>Threshold for "big fish" requests: 65% of the species range.</summary>
        public static int TrophyGrams(FishKind kind)
        {
            var species = FishingSpecies.Of(kind);
            return species.MinGrams + (species.MaxGrams - species.MinGrams) * TrophyPermille / 1000;
        }

        private static FishingOrder Generate(int index)
        {
            uint h = Mix((uint)index);
            uint h2 = Mix(h ^ 0x5bd1e995u);
            var a = (FishKind)(h2 % 3);
            var b = (FishKind)(((uint)a + 1 + (h2 >> 8) % 2) % 3);
            int growth = Math.Min(24, index - Tutorial.Length);
            int variant = (int)((h >> 12) % 3);
            switch (h % 4)
            {
                case 0:
                    return new FishingOrder(index, TrophyNames[variant], variant == 2 ? Noodles : Keeper,
                        new[] { new OrderLine(a, 1, TrophyGrams(a)) }, 28 + 6 * (int)a + growth);
                case 1:
                    int first = 1 + (int)((h >> 20) % 2);
                    return new FishingOrder(index, PairNames[variant], variant == 1 ? Noodles : Market,
                        new[] { new OrderLine(a, first), new OrderLine(b, 1) }, 16 + 10 * (first + 1) + growth);
                case 2:
                    return new FishingOrder(index, FeastNames[variant], Market,
                        new[] { new OrderLine(FishKind.Coral, 1), new OrderLine(FishKind.Dart, 1), new OrderLine(FishKind.Puffer, 1) }, 44 + 4 * variant + growth);
                default:
                    return new FishingOrder(index, PearlNames[variant], Scholar,
                        new[] { new OrderLine(a, 1, 0, true) }, 55 + 8 * (int)a + growth);
            }
        }

        /// <summary>
        /// Picks the cheapest eligible fish for each line (ties by ID) so valuable cargo stays aboard.
        /// Fills a partial pick when a line is short; perLine, if given, receives each line's count.
        /// </summary>
        public static bool Select(FishingOrder order, IReadOnlyList<CatchRecord> hold, List<int> picked, int[] perLine = null)
        {
            picked.Clear();
            bool complete = true;
            // Holds contain at most ten fish. Scan for each cheapest match instead of allocating
            // a candidate list and sort closure on every HUD query (including partial orders).
            for (int l = 0; l < order.LineCount; l++)
            {
                var line = order.Line(l);
                int take = 0;
                for (; take < line.Count; take++)
                {
                    int best = -1;
                    for (int i = 0; i < hold.Count; i++)
                    {
                        if (!line.Accepts(hold[i]) || picked.Contains(i)) continue;
                        if (best < 0 || hold[i].value < hold[best].value
                            || (hold[i].value == hold[best].value && string.CompareOrdinal(hold[i].id, hold[best].id) < 0))
                            best = i;
                    }
                    if (best < 0) break;
                    picked.Add(best);
                }
                if (perLine != null && l < perLine.Length) perLine[l] = take;
                if (take < line.Count) complete = false;
            }
            return complete;
        }

        // SplitMix-style integer hash: identical on Mono, IL2CPP and .NET.
        private static uint Mix(uint x)
        {
            unchecked
            {
                x += 0x9E3779B9u;
                x = (x ^ (x >> 16)) * 0x85EBCA6Bu;
                x = (x ^ (x >> 13)) * 0xC2B2AE35u;
                return x ^ (x >> 16);
            }
        }
    }
}
