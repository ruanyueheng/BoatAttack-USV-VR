using System;
using BoatAttack.Fishing;

internal static class FightControlTests
{
    public static void Run(Action<bool, string> check)
    {
        Neutral(check);
        Direction(check);
        Strike(check);
        Recovery(check);
        Strategy(check);
    }

    private static CatchSession Hooked(FishKind kind, string id, int level = 0, float hookDelay = 0)
    {
        var s = new CatchSession();
        if (!s.Cast(kind, false, level, id, BaitKind.Balanced, BayWeather.Clear, .5f, level))
            throw new Exception("Invalid fight-control fixture");
        for (int i = 0; i < 200 && s.Phase == CatchPhase.Waiting; i++) s.Tick(.02f, false);
        for (float elapsed = 0; elapsed < hookDelay; elapsed += .02f) s.Tick(.02f, false);
        if (!s.Hook()) throw new Exception("Fight-control fixture missed bite");
        return s;
    }

    private static bool Same(CatchSession a, CatchSession b)
        => a.Phase == b.Phase && a.Tension == b.Tension && a.Distance == b.Distance
            && a.Remaining == b.Remaining && a.FightTime == b.FightTime && a.Failure == b.Failure;

    private static void Neutral(Action<bool, string> check)
    {
        foreach (FishKind kind in Enum.GetValues(typeof(FishKind)))
        {
            var oldApi = Hooked(kind, "neutral-" + kind, 1);
            var newApi = Hooked(kind, "neutral-" + kind, 1);
            bool exact = true;
            for (int i = 0; i < 6500 && oldApi.Phase == CatchPhase.Fighting; i++)
            {
                bool reel = oldApi.Tension < .55f && oldApi.Beat != FishBeat.Leap;
                oldApi.Tick(.02f, reel);
                newApi.Tick(.02f, reel, 0);
                exact &= Same(oldApi, newApi) && newApi.ControlQuality == 0;
            }
            check(exact && oldApi.Phase == CatchPhase.Landing,
                "Fight control: neutral new API is frame-exact with old API for " + kind);
        }
        // Independent original first-cycle formula: the neutral branch must not change the base tuning.
        var s = Hooked(FishKind.Puffer, "original-first-cycle");
        uint seed = 2166136261;
        foreach (char letter in "original-first-cycle") seed = unchecked((seed ^ letter) * 16777619);
        seed = unchecked(seed * 1664525 + 1013904223);
        float surgeScale = .9f + ((seed >> 8) & 255) / 255f * .2f;
        float tension = .3f, distance = s.MaxDistance;
        for (int i = 0; i < 40; i++)
        {
            float gain = s.Species.TensionGain * (.75f * surgeScale)
                * FishingVoyage.TensionScale(BaitKind.Balanced) * CatchSession.LineFactor(0) * 1f;
            tension = Math.Max(0, Math.Min(1, tension + gain * .02f));
            distance = Math.Max(0, Math.Min(s.MaxDistance, distance + -2.65f * .02f));
            s.Tick(.02f, true, 0);
        }
        check(s.Tension == tension && s.Distance == distance,
            "Fight control: neutral tension and retrieval preserve independent original formula exactly");
    }

    private static void Direction(Action<bool, string> check)
    {
        var good = Hooked(FishKind.Puffer, "lateral-run");
        var bad = Hooked(FishKind.Puffer, "lateral-run");
        var neutral = Hooked(FishKind.Puffer, "lateral-run");
        float previous = 0;
        bool smooth = true, bounds = true;
        for (int i = 0; i < 60; i++)
        {
            float direction = good.FishDirection;
            good.Tick(.02f, true, -Math.Sign(direction));
            bad.Tick(.02f, true, Math.Sign(direction));
            neutral.Tick(.02f, true);
            smooth &= Math.Abs(good.FishDirection - previous) < .05f;
            bounds &= good.FishDirection >= -1 && good.FishDirection <= 1
                && good.ControlQuality >= -1 && good.ControlQuality <= 1
                && good.Stamina >= 0 && good.Stamina <= 1;
            previous = good.FishDirection;
        }
        check(smooth && bounds && good.FishDirection == bad.FishDirection,
            "Fight control: ID-seeded lateral lean is smooth, bounded and input-independent");
        check(good.Tension < neutral.Tension && neutral.Tension < bad.Tension
            && good.Distance < neutral.Distance && neutral.Distance < bad.Distance,
            "Fight control: counter-steering reduces tension and retrieves faster than following the fish");
        check(good.Stamina < neutral.Stamina && neutral.Stamina < 1,
            "Fight control: visible fatigue follows recovery and rewards good control without a new landing gate");
        check(FishingFightControl.Quality(-1, 1) == 1 && FishingFightControl.Quality(1, 1) == -1
            && FishingFightControl.Quality(.1f, -1) == 0 && FishingFightControl.Quality(0, 1) == 0,
            "Fight control: opposite rod direction is helpful and small neutral drift has no penalty");
        check(FishingFightControl.TensionMultiplier(1) == .75f
            && FishingFightControl.TensionMultiplier(-1) == 1.3f
            && FishingFightControl.RetrieveMultiplier(0) == 1,
            "Fight control: assistance and wrong-side penalty stay at the declared tuning");
    }

    private static void Strike(Action<bool, string> check)
    {
        var waiting = new CatchSession();
        waiting.Cast(FishKind.Coral, false, 0, "preview");
        bool previewed = false, safe = true;
        for (int i = 0; i < 200 && waiting.Phase == CatchPhase.Waiting; i++)
        {
            previewed |= waiting.BitePreview > 0;
            safe &= waiting.BitePreview >= 0 && waiting.BitePreview <= 1 && !waiting.Hook();
            waiting.Tick(.02f, false);
        }
        check(previewed && safe && waiting.Phase == CatchPhase.Bite && waiting.BitePreview == 1,
            "Fight control: light float pecks precede the real bite but never permit early hooking");
        var prompt = Hooked(FishKind.Coral, "prompt", hookDelay: .3f);
        var late = Hooked(FishKind.Coral, "late", hookDelay: 2.1f);
        check(prompt.PerfectStrike && prompt.StrikeQuality == 1 && !late.PerfectStrike
            && late.StrikeQuality < prompt.StrikeQuality && prompt.Claim() == null && late.Claim() == null,
            "Fight control: timely strike earns feedback, not an early fish or reward");
    }

    private static void Recovery(Action<bool, string> check)
    {
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var invalidSide = Hooked(FishKind.Dart, "invalid-input");
            var neutral = Hooked(FishKind.Dart, "invalid-input");
            for (int i = 0; i < 40; i++)
            {
                invalidSide.Tick(.02f, true, invalid);
                neutral.Tick(.02f, true, 0);
            }
            check(Same(invalidSide, neutral) && invalidSide.ControlQuality == 0,
                "Fight control: nonfinite rod input safely becomes neutral " + invalid);
        }
        var s = Hooked(FishKind.Dart, "paused-direction");
        s.Tick(.02f, true, 1);
        float direction = s.FishDirection, quality = s.ControlQuality, stamina = s.Stamina, time = s.FightTime;
        s.Tick(0, true, -1); s.Tick(float.NaN, true, -1); s.Tick(float.PositiveInfinity, true, -1);
        check(s.FishDirection == direction && s.ControlQuality == quality && s.Stamina == stamina && s.FightTime == time,
            "Fight control: pause and invalid time cannot rotate the fish or advance fatigue");
        s.Cancel();
        check(s.Phase == CatchPhase.Idle && s.FishDirection == 0 && s.ControlQuality == 0
            && s.FightTime == 0 && s.Stamina == 1 && s.StrikeQuality == 0,
            "Fight control: cancellation clears lateral, strike and fatigue feedback");
        check(s.Cast(FishKind.Dart, false, 0, "paused-direction") && s.FishDirection == 0
            && s.ControlQuality == 0 && s.Stamina == 1 && !s.PerfectStrike,
            "Fight control: recasting starts fresh with no previous rod-side residue");
        for (int i = 0; i < 300; i++) s.Tick(.02f, false);
        check(s.Phase == CatchPhase.Escaped && s.FishDirection == 0 && s.ControlQuality == 0,
            "Fight control: missed bite exposes no stale fight-direction instruction");
        check(FishingFightControl.Direction(float.NaN, 5, 1) == 0
            && FishingFightControl.Direction(1, 0, 1) == 0
            && FishingFightControl.BitePreview(float.NaN, 2) == 0
            && FishingFightControl.StrikeQuality(float.NaN) == 0,
            "Fight control: standalone presentation helpers reject nonfinite clocks");
    }

    private static void Strategy(Action<bool, string> check)
    {
        foreach (FishKind kind in Enum.GetValues(typeof(FishKind)))
        for (int level = 0; level < 3; level++)
        for (int seed = 0; seed < 3; seed++)
        {
            var s = Hooked(kind, "controlled-" + seed, level);
            float previousStamina = 1;
            bool fatigueBounded = true;
            for (int i = 0; i < 6500 && s.Phase == CatchPhase.Fighting; i++)
            {
                s.Tick(.02f, s.Tension < .55f && s.Beat != FishBeat.Leap, -Math.Sign(s.FishDirection));
                fatigueBounded &= s.Stamina <= previousStamina && s.Stamina >= 0;
                previousStamina = s.Stamina;
            }
            check(s.Phase == CatchPhase.Landing && s.Stamina == 0 && fatigueBounded
                && s.FishDirection == 0 && s.ControlQuality == 0 && (!s.Species.Legendary || s.FightTime >= 20),
                "Fight control: follow hints, release on leaps, then net " + kind + "/" + level + "/" + seed);
        }
        var hard = Hooked(FishKind.Sailfish, "steering-cannot-brute-force-legend", 2);
        for (int i = 0; i < 6500 && hard.Phase == CatchPhase.Fighting; i++)
            hard.Tick(.02f, true, -Math.Sign(hard.FishDirection));
        check(hard.Phase == CatchPhase.Escaped && (hard.Failure == CatchFailure.LineSnapped
            || hard.Failure == CatchFailure.LeapSnapped) && hard.Claim() == null,
            "Fight control: perfect rod lean still cannot brute-force legendary leaps");
        var leap = Hooked(FishKind.Sailfish, "leap-remains-dangerous", 2);
        for (int i = 0; i < 600 && leap.Beat != FishBeat.Leap; i++) leap.Tick(.02f, leap.Tension < .55f);
        var leapNeutral = Hooked(FishKind.Sailfish, "leap-remains-dangerous", 2);
        for (int i = 0; i < 600 && leapNeutral.Beat != FishBeat.Leap; i++) leapNeutral.Tick(.02f, leapNeutral.Tension < .55f);
        leap.Tick(.02f, true, -Math.Sign(leap.FishDirection));
        leapNeutral.Tick(.02f, true);
        check(leap.Beat == FishBeat.Leap && leap.Tension == leapNeutral.Tension && leap.Distance == leapNeutral.Distance,
            "Fight control: counter-steer provides no airborne line-snap exemption");
    }
}
