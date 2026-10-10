using System;
using System.IO;
using BoatAttack.Fishing;

internal static class AcceptanceTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(FishingAcceptanceOptions.Parse(new[] { "game.exe" }) == null, "Acceptance is absent in a normal launch");
        var manual = FishingAcceptanceOptions.Parse(new[] { "game.exe", "-fishing-acceptance" });
        check(!manual.Automatic && manual.InitialCase == FishingAcceptanceCase.FreePlay, "Acceptance defaults to real-input free play");
        string root = Path.GetFullPath(Path.GetTempPath());
        string play = manual.ProfilePath(root, false), fixture = manual.ProfilePath(root, true);
        check(play != fixture && Path.GetFileName(play) == "playthrough.json" && Path.GetFileName(fixture) == "fixture.json", "Acceptance fixtures cannot overwrite playthrough progress");
        check(play.StartsWith(Path.Combine(root, "FishingAcceptance"), StringComparison.Ordinal)
            && !play.Contains("boatattack-fishing-v1.json"), "Acceptance profiles stay separate from the normal player filename");
        var second = FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-acceptance-session", "second_run" });
        check(second.ProfilePath(root, false) != play, "Acceptance session identifiers isolate separate runs");
        var capture = FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-acceptance-capture", "-fishing-acceptance-case", "Landing" });
        check(capture.Automatic && capture.CaptureAll && capture.InitialCase == FishingAcceptanceCase.Landing, "Acceptance capture is separately opted in");
        foreach (string bad in new[] { "../player", "..", "a/b", "a\\b", "C:player", "CON", "nul", "COM1", "lpt9", "space name", new string('a', 49) })
            check(Rejects(() => FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-acceptance-session", bad })), "Acceptance rejects unsafe session name " + bad);
        check(Rejects(() => FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance-case", "Catch" })), "Acceptance child flags cannot silently enable normal play");
        check(Rejects(() => FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-autoplay" })), "Acceptance rejects competing autoplay drivers");
        check(Rejects(() => FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-capture", root })), "Acceptance rejects competing legacy capture");
        check(Rejects(() => FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-acceptance-session" })), "Acceptance rejects a missing session value");
        check(Rejects(() => FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-acceptance-typo" })), "Acceptance rejects misspelled flags");
        check(Rejects(() => FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-acceptance-case", "99" })), "Acceptance rejects numeric/unknown case names");
        check(Rejects(() => FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-acceptance-output", "relative" })), "Acceptance output must be explicit and rooted");
        check(Rejects(() => FishingAcceptanceOptions.Parse(new[] { "-fishing-acceptance", "-fishing-acceptance-smoke", "-fishing-acceptance-capture" })), "Acceptance smoke and screenshot runs are mutually exclusive");
        check(FishingAcceptanceCases.Count == Enum.GetValues(typeof(FishingAcceptanceCase)).Length, "Acceptance catalog covers every case");
        check(FishingAcceptanceCases.Cycle(FishingAcceptanceCase.FreePlay, -1) == FishingAcceptanceCase.CastFlight
            && FishingAcceptanceCases.Cycle(FishingAcceptanceCase.CastFlight, 1) == FishingAcceptanceCase.FreePlay, "Acceptance case cycling wraps safely");
        check(FishingAcceptanceCases.Cycle(FishingAcceptanceCase.Settings, 1) == FishingAcceptanceCase.ShoreFirstPerson, "Acceptance includes actual v11 shore and shop views");
        check(Rejects(() => FishingAcceptanceCases.CreateCareer(FishingAcceptanceCase.FreePlay)), "Free play can never be replaced by a synthetic fixture");
        foreach (FishingAcceptanceCase value in Enum.GetValues(typeof(FishingAcceptanceCase)))
        {
            check(FishingAcceptanceCases.Title(value).Length > 0, "Acceptance case has an explicit label: " + value);
            if (value == FishingAcceptanceCase.FreePlay) continue;
            var career = FishingAcceptanceCases.CreateCareer(value);
            check(FishingCareer.TryRestore(career.Snapshot(), out _), "Acceptance fixture uses a valid production save: " + value);
            if (value == FishingAcceptanceCase.FullHold) check(career.Count == career.Capacity, "Full-hold fixture really fills the inventory");
            if (value == FishingAcceptanceCase.HarborOrders) check(career.CurrentOrder.LineCount == 3 && career.OrderReady, "Harbor fixture contains a deliverable three-line order");
            if (value == FishingAcceptanceCase.MaxUpgrades) check(career.LineLevel == 2 && career.ReelLevel == 2 && career.HoldLevel == 2, "Upgrade fixture maxes all three tracks");
            if (!FishingAcceptanceCases.IsCatchCase(value)) continue;
            var session = new CatchSession();
            FishingAcceptanceCases.PrepareSession(session, value, career);
            if (value == FishingAcceptanceCase.Bite) check(session.Phase == CatchPhase.Bite && session.Remaining > 2f, "Bite fixture preserves the real reaction window");
            else if (value == FishingAcceptanceCase.Fighting) check(session.Phase == CatchPhase.Fighting && session.Tension >= .78f, "Fight fixture exercises the high-tension warning");
            else if (value == FishingAcceptanceCase.LegendLeap) check(session.Kind == FishKind.Sailfish && session.Beat == FishBeat.Leap, "Legend fixture uses the real airborne beat");
            else check(session.Phase == CatchPhase.Landing && session.CanLand, "Landing/catch fixture can use the real scoop: " + value);
        }
    }

    private static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (ArgumentException) { return true; }
    }
}
