using System;
using System.Collections.Generic;
using BoatAttack.Fishing;

internal static class ReviewTests
{
    internal static void Run(Action<bool, string> check)
    {
        var data=new FishingCareer().Snapshot();data.ordersCompleted=3;
        FishingCareer.TryRestore(data, out var career);
        var firstOrder = career.CurrentOrder;
        var firstView = career.Inventory;
        for (int i = 0; i < 100; i++) ReadHud(career);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) ReadHud(career);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine("REVIEW HUD: " + allocated + " bytes / 1000 warmed-up query sets");
        check(allocated == 0, "Review HUD order/inventory queries allocate no memory after warmup");
        check(ReferenceEquals(firstOrder, career.CurrentOrder), "Review generated order is cached until delivery");
        check(ReferenceEquals(firstView, career.Inventory), "Review inventory reuses its read-only view");
        check(firstView is System.Collections.ObjectModel.ReadOnlyCollection<CatchRecord>, "Review inventory remains read-only");
        var tutorial = new FishingCareer();
        var oldOrder = tutorial.CurrentOrder;
        var liveView = tutorial.Inventory;
        tutorial.Store(Fish("coral", FishKind.Coral, 1000));
        tutorial.Store(Fish("dart", FishKind.Dart, 1500));
        check(liveView.Count == 2, "Review cached inventory view reflects catches");
        for (int i = 0; i < 100; i++) ReadHud(tutorial);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) ReadHud(tutorial);
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        check(allocated == 0, "Review populated order queries allocate no memory after warmup");
        check(tutorial.DeliverOrder() > 0 && tutorial.CurrentOrder.Index == 1
            && !ReferenceEquals(oldOrder, tutorial.CurrentOrder), "Review delivery invalidates the cached order");
        check(liveView.Count == 0, "Review cached inventory view reflects delivery");
        check(FishingCareer.TryRestore(tutorial.Snapshot(), out var restored)
            && restored.CurrentOrder.Index == 1, "Review restored career uses the saved order index");
        SelectionParity(check);
        var line = new OrderLine(FishKind.Dart, 1, 1745);
        check(line.Describe().Contains("1.745"), "Review order threshold displays the exact accepted grams");
        check(FishingText.Weight(1744) == "1.744 kg" && FishingText.Weight(1745) == "1.745 kg", "Review borderline catches cannot display the same weight");
    }

    private static CatchRecord Fish(string id, FishKind kind, int grams, bool rare = false)
        => new CatchRecord { id = id, kind = kind, grams = grams, rare = rare, value = FishingSpecies.Value(kind, rare, grams) };

    private static void SelectionParity(Action<bool, string> check)
    {
        var hold = new List<CatchRecord>();
        var actual = new List<int>();
        var expected = new List<int>();
        var actualCounts = new int[3];
        var expectedCounts = new int[3];
        bool matches = true;
        for (int count = 0; count <= 10; count++)
        {
            if (count > 0)
            {
                var kind = (FishKind)((count - 1) % 3);
                var species = FishingSpecies.Of(kind);
                hold.Add(Fish("fish-" + (10 - count), kind, count % 2 == 0 ? species.MinGrams : species.MaxGrams, count % 3 == 0));
            }
            for (int index = 0; index < 256; index++)
            {
                var order = FishingOrders.For(index);
                bool ready = FishingOrders.Select(order, hold, actual, actualCounts);
                bool expectedReady = ReferenceSelect(order, hold, expected, expectedCounts);
                matches &= ready == expectedReady && actual.Count == expected.Count;
                for (int i = 0; i < actual.Count && i < expected.Count; i++) matches &= actual[i] == expected[i];
                for (int i = 0; i < order.LineCount; i++) matches &= actualCounts[i] == expectedCounts[i];
            }
        }
        check(matches, "Review selection preserves 2816 complete/partial order and cargo combinations");
        var tied = new List<CatchRecord> { Fish("b", FishKind.Coral, 1000), Fish("a", FishKind.Coral, 1000) };
        FishingOrders.Select(FishingOrders.For(0), tied, actual);
        check(actual.Count == 1 && actual[0] == 1, "Review equal-price fish retain ordinal ID tie-breaking");
    }

    // Deliberately independent reference for the old sort-based selection contract.
    private static bool ReferenceSelect(FishingOrder order, IReadOnlyList<CatchRecord> hold, List<int> picked, int[] perLine)
    {
        picked.Clear();
        bool complete = true;
        for (int l = 0; l < order.LineCount; l++)
        {
            var line = order.Line(l);
            var candidates = new List<int>();
            for (int i = 0; i < hold.Count; i++) if (line.Accepts(hold[i]) && !picked.Contains(i)) candidates.Add(i);
            candidates.Sort((a, b) => hold[a].value != hold[b].value ? hold[a].value.CompareTo(hold[b].value) : string.CompareOrdinal(hold[a].id, hold[b].id));
            int take = Math.Min(line.Count, candidates.Count);
            for (int i = 0; i < take; i++) picked.Add(candidates[i]);
            perLine[l] = take;
            complete &= take == line.Count;
        }
        return complete;
    }

    private static void ReadHud(FishingCareer career)
    {
        _ = career.CurrentOrder;
        _ = career.Inventory;
        _ = career.OrderReady;
        _ = career.ReservedCount;
        _ = career.SaleValue;
    }
}
