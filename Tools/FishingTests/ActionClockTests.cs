using System;
using BoatAttack.Fishing;

internal static class ActionClockTests
{
    public static void Run(Action<bool, string> check)
    {
        var clock = new FishingActionClock();
        check(!clock.Active && !clock.ContactPending && clock.ActionId == 0 &&
            clock.TargetId == string.Empty && clock.Kind == FishingActionKind.None && clock.Progress == 0f,
            "Action clock starts empty");
        check(!clock.TryConsumeContact(0) && !clock.TryConsumeContact(-1) && !clock.TryConsumeContact(1),
            "Empty clock grants no contact");
        clock.Tick(float.MaxValue);
        check(!clock.Active && !clock.ContactPending && clock.Progress == 0f, "Idle ticks do not invent an action");
        InvalidBegins(clock, check, "idle");

        // 多帧到达接触：重复按键不能换掉目标，消费不代表动画立即结束。
        check(clock.TryBegin(FishingActionKind.Harvest, "node:original", 2f, .75f), "Begin timed harvest");
        long firstId = clock.ActionId;
        check(firstId > 0 && clock.Active && !clock.ContactPending && clock.Progress == 0f,
            "Begin allocates a positive ID without early contact");
        for (int i = 0; i < 4; i++)
            check(!clock.TryBegin(FishingActionKind.Pickup, "node:replacement", 1f, 0f), "Rapid press rejected " + i);
        check(clock.ActionId == firstId && clock.TargetId == "node:original" && clock.Kind == FishingActionKind.Harvest,
            "Repeated presses preserve action ID, kind and target");
        check(!clock.TryConsumeContact(firstId), "Cannot consume before contact time");
        clock.Tick(.25f);
        clock.Tick(.25f);
        check(clock.Active && !clock.ContactPending && clock.Progress == .25f, "Multiple ticks accumulate before contact");
        InvalidBegins(clock, check, "active");
        clock.Tick(.25f);
        check(clock.Active && clock.ContactPending && clock.Progress == .375f, "Exact contact threshold grants permission");
        check(!clock.TryConsumeContact(firstId + 1) && clock.ContactPending, "Wrong ID cannot discard pending contact");
        check(clock.TryConsumeContact(firstId) && !clock.TryConsumeContact(firstId) && !clock.ContactPending,
            "Contact consumed exactly once");
        check(!clock.TryBegin(FishingActionKind.Place, "other", 1f, 0f), "Consumed contact cannot interrupt active recovery");
        clock.Tick(.5f);
        check(clock.Active && !clock.ContactPending && clock.Progress == .625f, "Later ticks never rearm consumed contact");
        clock.Tick(.75f);
        check(!clock.Active && !clock.ContactPending && clock.Progress == 1f && clock.ActionId == firstId &&
            clock.TargetId == "node:original" && clock.Kind == FishingActionKind.Harvest,
            "Completion preserves metadata and completed progress");
        clock.Tick(float.MaxValue);
        check(!clock.TryConsumeContact(firstId) && clock.Progress == 1f, "Finished consumed contact stays consumed");
        InvalidBegins(clock, check, "completed and consumed");

        // 一帧跨过接触和结束，必须先留住许可，后续空闲帧不能吞掉它。
        check(clock.TryBegin(FishingActionKind.Store, "fish:low-fps", 1f, .375f) && clock.ActionId > firstId,
            "Completed action permits a new monotonic ID");
        long lowFpsId = clock.ActionId;
        clock.Tick(float.MaxValue);
        check(!clock.Active && clock.ContactPending && clock.Progress == 1f && clock.ActionId == lowFpsId &&
            clock.TargetId == "fish:low-fps" && clock.Kind == FishingActionKind.Store,
            "One large frame crosses contact and end without losing permission or metadata");
        clock.Tick(1f);
        clock.Tick(float.MaxValue);
        check(clock.ContactPending && !clock.TryBegin(FishingActionKind.Release, "other-fish", 1f, 0f) &&
            clock.ActionId == lowFpsId && clock.TargetId == "fish:low-fps", "Unconsumed finished contact blocks replacement");
        InvalidBegins(clock, check, "completed and pending");
        check(!clock.TryConsumeContact(firstId) && clock.ContactPending, "Earlier completed ID cannot consume a newer contact");
        check(clock.TryConsumeContact(lowFpsId) && !clock.TryConsumeContact(lowFpsId), "Late contact still consumes exactly once");

        // 边界接触：零秒在开始时可用，结束秒在结束帧仍可用。
        check(clock.TryBegin(FishingActionKind.Inspect, "fish:zero", 1f, 0f), "Zero-second contact accepted");
        long zeroId = clock.ActionId;
        check(clock.Active && clock.ContactPending && clock.Progress == 0f, "Zero contact available immediately without ticking");
        clock.Tick(0f);
        clock.Tick(float.NaN);
        clock.Tick(10f, true);
        check(clock.Active && clock.ContactPending && clock.Progress == 0f, "Ignored ticks preserve existing zero-time contact");
        check(clock.TryConsumeContact(zeroId), "Zero contact grants one permission");
        clock.Tick(1f);
        check(!clock.Active && !clock.ContactPending && !clock.TryConsumeContact(zeroId), "Zero contact does not reappear on completion");
        check(clock.TryBegin(FishingActionKind.Board, "boat", 1f, 1f), "End-time contact accepted");
        long endId = clock.ActionId;
        clock.Tick(.5f);
        check(clock.Active && !clock.ContactPending && !clock.TryConsumeContact(endId), "End contact not granted at halfway");
        clock.Tick(.5f);
        check(!clock.Active && clock.ContactPending && clock.Progress == 1f && clock.TryConsumeContact(endId),
            "Contact equal to duration remains consumable at exact end");
        check(clock.TryBegin(FishingActionKind.Board, "boat:overshoot", 1f, 1f), "End-time contact can restart after consumption");
        endId = clock.ActionId;
        clock.Tick(5f);
        check(!clock.Active && clock.ContactPending && clock.TryConsumeContact(endId), "Overshooting end-time contact preserves permission");

        // 暂停及无效增量必须冻结时序，也不能清除已到达的许可。
        check(clock.TryBegin(FishingActionKind.Repair, "boat:repair", 2f, 1f), "Begin pause test");
        long pauseId = clock.ActionId;
        clock.Tick(.5f);
        foreach (float delta in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f })
        {
            clock.Tick(delta);
            check(clock.Active && !clock.ContactPending && clock.Progress == .25f && clock.ActionId == pauseId,
                "Invalid or zero delta freezes pre-contact action: " + delta);
        }
        clock.Tick(float.MaxValue, true);
        clock.Tick(.5f, true);
        check(clock.Active && !clock.ContactPending && clock.Progress == .25f && !clock.TryConsumeContact(pauseId),
            "Paused frames never advance or grant contact");
        clock.Tick(.5f, false);
        check(clock.Active && clock.ContactPending && clock.Progress == .5f, "Resume continues original elapsed time");
        clock.Tick(float.MaxValue, true);
        clock.Tick(-1f);
        clock.Tick(float.PositiveInfinity);
        check(clock.Active && clock.ContactPending && clock.Progress == .5f && clock.TargetId == "boat:repair",
            "Paused or invalid frames preserve pending permission and target");
        check(clock.TryConsumeContact(pauseId), "Pending permission remains consumable after frozen ticks");
        clock.Tick(1f);
        check(!clock.Active && !clock.ContactPending && clock.Progress == 1f, "Resumed action completes normally");

        // 取消时清空当前状态但保留编号序列，迟到回调不能消费新动作。
        check(clock.TryBegin(FishingActionKind.Cast, "water:cancel", 2f, 1f), "Begin cancellation before contact");
        long cancelledId = clock.ActionId;
        clock.Tick(.5f);
        clock.Cancel();
        clock.Tick(float.MaxValue);
        check(!clock.Active && !clock.ContactPending && clock.ActionId == 0 && clock.Progress == 0f &&
            clock.TargetId == string.Empty && clock.Kind == FishingActionKind.None && !clock.TryConsumeContact(cancelledId),
            "Cancel before contact clears state and invalidates permission");
        clock.Cancel();
        check(clock.TryBegin(FishingActionKind.Scoop, "fish:new", 1f, 0f) && clock.ActionId > cancelledId,
            "Repeated cancel does not recycle IDs");
        long afterCancelId = clock.ActionId;
        check(!clock.TryConsumeContact(cancelledId) && !clock.TryConsumeContact(0) && clock.ContactPending,
            "Stale or cleared ID cannot consume new immediate contact");
        clock.Cancel();
        check(!clock.TryConsumeContact(afterCancelId) && !clock.ContactPending && !clock.Active, "Cancel after contact revokes unconsumed permission");
        check(clock.TryBegin(FishingActionKind.UseTool, "tool", 1f, 0f) && clock.TryConsumeContact(clock.ActionId),
            "New action after revoked contact can be consumed");
        long consumedId = clock.ActionId;
        clock.Cancel();
        clock.Tick(1f);
        check(!clock.TryConsumeContact(consumedId) && !clock.ContactPending, "Cancel after consumption cannot grant permission again");
        check(clock.TryBegin(FishingActionKind.Place, "place", 1f, .5f), "Begin finished-contact cancellation");
        long finishedId = clock.ActionId;
        clock.Tick(2f);
        clock.Cancel();
        check(!clock.TryConsumeContact(finishedId) && clock.TryBegin(FishingActionKind.Pickup, "pickup", 1f, 0f) &&
            clock.ActionId > finishedId, "Explicit cancel clears finished pending contact and permits a fresh action");

        // 许可不是库存事务；即便提交失败，也只能尝试一次该动作。
        long failedTransactionId = clock.ActionId;
        int attempts = 0;
        for (int i = 0; i < 3; i++)
        {
            if (clock.TryConsumeContact(failedTransactionId)) attempts++;
            clock.Tick(.5f);
        }
        check(attempts == 1 && !clock.ContactPending, "Failed transaction cannot request a second permission from the same action");

        // 两个实例各自计时、取消、消费，不通过静态状态相互影响。
        var left = new FishingActionClock();
        var right = new FishingActionClock();
        check(left.TryBegin(FishingActionKind.Harvest, "left", 1f, .5f) &&
            right.TryBegin(FishingActionKind.Craft, "right", 2f, 1f), "Independent clocks can both begin");
        left.Tick(.5f);
        check(left.ContactPending && right.Active && !right.ContactPending && right.Progress == 0f,
            "Ticking one instance does not advance the other");
        long rightId = right.ActionId;
        left.Cancel();
        right.Tick(1f);
        check(!left.Active && !left.ContactPending && right.ContactPending && right.ActionId == rightId &&
            right.TargetId == "right" && right.TryConsumeContact(rightId), "Cancellation and consumption are instance-local");

        // 浮点极值仍按秒比较；累计采用双精度，避免大数相加溢出。
        var edge = new FishingActionClock();
        check(edge.TryBegin(FishingActionKind.Release, "tiny", float.Epsilon * 2f, float.Epsilon), "Subnormal duration accepted");
        long tinyId = edge.ActionId;
        edge.Tick(0f);
        check(edge.Progress == 0f && !edge.ContactPending, "Zero delta cannot advance a subnormal action");
        edge.Tick(float.Epsilon);
        check(edge.Active && edge.Progress == .5f && edge.ContactPending && edge.TryConsumeContact(tinyId),
            "Smallest positive delta reaches tiny contact exactly");
        edge.Tick(float.Epsilon);
        check(!edge.Active && edge.Progress == 1f && !edge.ContactPending, "Tiny duration finishes without repeating contact");
        check(edge.TryBegin(FishingActionKind.Harvest, "huge", float.MaxValue, float.MaxValue / 2f), "Largest finite duration accepted");
        long hugeId = edge.ActionId;
        edge.Tick(float.MaxValue / 2f);
        check(edge.Active && edge.Progress == .5f && edge.ContactPending, "Large finite delta reaches halfway contact");
        edge.Tick(float.MaxValue);
        check(!edge.Active && edge.Progress == 1f && !float.IsNaN(edge.Progress) && !float.IsInfinity(edge.Progress) &&
            edge.TryConsumeContact(hugeId), "Huge accumulated delta clamps without overflow or lost contact");
        check(edge.TryBegin(FishingActionKind.Inspect, "small-delta", 1f, .5f), "Begin ordinary duration with tiny delta");
        edge.Tick(float.Epsilon);
        check(edge.Active && edge.Progress > 0f && !edge.ContactPending, "Tiny delta on ordinary duration advances without early contact");

        // 每个有效动作类型使用同一契约，目标标识不做隐式修剪或改写。
        foreach (FishingActionKind kind in Enum.GetValues(typeof(FishingActionKind)))
        {
            if (kind == FishingActionKind.None) continue;
            edge.Cancel();
            check(edge.TryBegin(kind, " target:" + kind + " ", 1f, 0f) && edge.Kind == kind &&
                edge.TargetId == " target:" + kind + " " && edge.TryConsumeContact(edge.ActionId),
                "Shared timing contract supports action kind " + kind);
        }
    }

    private static void InvalidBegins(FishingActionClock clock, Action<bool, string> check, string stage)
    {
        long id = clock.ActionId;
        string target = clock.TargetId;
        FishingActionKind kind = clock.Kind;
        float progress = clock.Progress;
        bool active = clock.Active, pending = clock.ContactPending;
        void Reject(FishingActionKind proposedKind, string proposedTarget, float duration, float contact, string label)
        {
            check(!clock.TryBegin(proposedKind, proposedTarget, duration, contact), "Reject " + label + " while " + stage);
            check(clock.ActionId == id && clock.TargetId == target && clock.Kind == kind && clock.Progress == progress &&
                clock.Active == active && clock.ContactPending == pending, "Rejected " + label + " preserves all state while " + stage);
        }

        foreach (float duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Reject(FishingActionKind.Craft, "invalid", duration, 0f, "duration " + duration);
        foreach (float contact in new[] { -1f, 2f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Reject(FishingActionKind.Craft, "invalid", 1f, contact, "contact " + contact);
        foreach (FishingActionKind invalidKind in new[] { FishingActionKind.None, (FishingActionKind)(-1), (FishingActionKind)99 })
            Reject(invalidKind, "invalid", 1f, 0f, "kind " + invalidKind);
        foreach (string invalidTarget in new[] { null, string.Empty, " \t " })
            Reject(FishingActionKind.Craft, invalidTarget, 1f, 0f, "empty target");
    }
}
