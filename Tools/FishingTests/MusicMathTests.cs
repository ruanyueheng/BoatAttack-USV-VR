using System;
using BoatAttack.Fishing;

internal static class MusicMathTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(FishingMusicMath.EqualPowerIn(0) == 0 && FishingMusicMath.EqualPowerOut(0) == 1
            && FishingMusicMath.EqualPowerIn(1) == 1 && FishingMusicMath.EqualPowerOut(1) == 0,
            "Music: equal-power fades have exact silent and full-volume endpoints");
        bool power = true, monotonic = true; float previousIn = 0, previousOut = 1;
        for (int i = 0; i <= 500; i++)
        {
            float a = FishingMusicMath.EqualPowerIn(i / 500f), b = FishingMusicMath.EqualPowerOut(i / 500f);
            power &= Finite(a) && Finite(b) && Math.Abs(a * a + b * b - 1) < .000002f;
            monotonic &= a >= previousIn && b <= previousOut; previousIn = a; previousOut = b;
        }
        check(power, "Music: every sampled crossfade keeps the sum of squared channel gains at one");
        check(monotonic, "Music: fade-in and fade-out gains change continuously in their intended direction");
        check(FishingMusicMath.EqualPowerIn(-2) == 0 && FishingMusicMath.EqualPowerOut(-2) == 1
            && FishingMusicMath.EqualPowerIn(2) == 1 && FishingMusicMath.EqualPowerOut(2) == 0,
            "Music: finite fade phases clamp to their endpoints");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            check(FishingMusicMath.EqualPowerIn(invalid) == 0 && FishingMusicMath.EqualPowerOut(invalid) == 1,
                "Music: invalid fade phases preserve the source rather than jumping to the destination: " + invalid);

        check(FishingMusicMath.TryLoopWindow(60, 3, 4, out var normal) && normal.End == 57 && normal.StartLead == 53 && normal.Crossfade == 4,
            "Music loop: the next source starts before the deliberately cut-off unfinished tail");
        check(FishingMusicMath.TryLoopWindow(3, 1, 8, out var shortTrack) && Near(shortTrack.End, 2)
            && Near(shortTrack.Crossfade, .9f) && Near(shortTrack.StartLead, 1.1f) && shortTrack.StartLead > shortTrack.Crossfade,
            "Music loop: a short track reduces its crossfade and retains a positive middle hold instead of overlapping every fade");
        check(FishingMusicMath.TryLoopWindow(.25f, 0, .05f, out var tiny) && tiny.StartLead > 0,
            "Music loop: the exact minimum usable loop duration remains valid");
        foreach (var invalid in new[]
        {
            new[] { float.NaN, 0, 2f }, new[] { float.PositiveInfinity, 0, 2f }, new[] { -1f, 0, 2f }, new[] { 0f, 0, 2f },
            new[] { 10f, float.NaN, 2f }, new[] { 10f, float.PositiveInfinity, 2f }, new[] { 10f, -1f, 2f },
            new[] { 10f, 10f, 2f }, new[] { 10f, 11f, 2f }, new[] { 1f, .9f, 2f }, new[] { .1f, 0, 2f },
            new[] { 10f, 0, float.NaN }, new[] { 10f, 0, float.PositiveInfinity }, new[] { 10f, 0, 0f }, new[] { 10f, 0, -1f },
            new[] { float.MaxValue, 0, 1f }
        })
            check(!FishingMusicMath.TryLoopWindow(invalid[0], invalid[1], invalid[2], out var rejected)
                && rejected.End == 0 && rejected.Crossfade == 0 && rejected.StartLead == 0,
                "Music loop: invalid or unrepresentable windows are rejected with a safe empty result: " + invalid[0] + "/" + invalid[1] + "/" + invalid[2]);
        bool windows = true;
        for (int i = 1; i <= 100; i++)
        {
            float length = i * .5f;
            windows &= FishingMusicMath.TryLoopWindow(length, length * .25f, 200, out var window)
                && Finite(window.End) && Finite(window.StartLead) && Finite(window.Crossfade)
                && window.Crossfade > 0 && window.StartLead > window.Crossfade && window.StartLead < window.End && window.End <= length;
        }
        check(windows, "Music loop: short and long clips always have finite ordered crossfade windows");

        var gate = new FishingMusicZoneGate();
        check(gate.Current == 0 && gate.Candidate == 0 && gate.PendingSeconds == 0 && gate.HoldSeconds == 1.5f,
            "Music zone: initial region is active immediately without a synthetic candidate transition");
        bool early = false;
        for (int i = 0; i < 14; i++) early |= gate.Tick(1, .1f);
        check(!early && gate.Current == 0 && gate.Candidate == 1 && gate.PendingSeconds > 1.3f,
            "Music zone: a candidate is not committed before the continuous hold finishes");
        check(gate.Tick(1, .1f) && gate.Current == 1 && gate.PendingSeconds == 0 && !gate.Tick(1, .1f),
            "Music zone: a stable candidate commits exactly once after its hold");
        gate.Reset(0); bool jitter = false;
        for (int i = 0; i < 100; i++) jitter |= gate.Tick(i % 2 + 1, .1f);
        check(!jitter && gate.Current == 0, "Music zone: repeated border crossings cannot accumulate hold time across different candidates");
        for (int i = 0; i < 10; i++) gate.Tick(2, .1f);
        gate.Tick(0, .1f);
        for (int i = 0; i < 10; i++) jitter |= gate.Tick(2, .1f);
        check(!jitter && gate.Current == 0 && gate.Candidate == 2,
            "Music zone: returning to the current region clears a partially held different candidate");
        for (int i = 0; i < 5; i++) gate.Tick(2, .1f);
        check(gate.Current == 2, "Music zone: settling after repeated jitter still permits the next complete stable hold");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1, 0, .251f, 20 })
        {
            gate.Reset(0); for (int i = 0; i < 5; i++) gate.Tick(3, .1f);
            check(!gate.Tick(3, invalid) && gate.Current == 0 && gate.Candidate == 0 && gate.PendingSeconds == 0,
                "Music zone: invalid, paused or missing frame time cannot complete a stale region hold: " + invalid);
        }
        check(new FishingMusicZoneGate(8, float.NaN).HoldSeconds == 1.5f
            && new FishingMusicZoneGate(8, float.PositiveInfinity).HoldSeconds == 1.5f
            && new FishingMusicZoneGate(8, 0).HoldSeconds == 1.5f, "Music zone: invalid hold configuration falls back to a finite default");
        gate.Reset(-1);
        check(gate.Current == -1 && gate.Candidate == -1 && gate.PendingSeconds == 0,
            "Music zone: explicit reset can select a silence region without leaving an old pending candidate");
        var brief = new FishingMusicZoneGate(-1, .1f);
        check(brief.Tick(int.MaxValue, .1f) && brief.Current == int.MaxValue,
            "Music zone: caller-owned zone identifiers are not restricted to one scene catalog");

        var normalized = FishingMusicMath.NormalizePower(new FishingMusicGains(3, 4, 0, 0));
        check(Near(normalized.A, .6f) && Near(normalized.B, .8f) && Near(normalized.Power, 1),
            "Music gains: normalization uses squared power rather than a linear volume sum");
        normalized = FishingMusicMath.NormalizePower(new FishingMusicGains(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue));
        check(Finite(normalized.Power) && Near(normalized.A, .5f) && Near(normalized.D, .5f) && Near(normalized.Power, 1),
            "Music gains: very large finite input amplitudes normalize without overflow");
        normalized = FishingMusicMath.NormalizePower(new FishingMusicGains(1, float.NaN, -1, float.PositiveInfinity));
        check(normalized.A == 1 && normalized.B == 0 && normalized.C == 0 && normalized.D == 0
            && FishingMusicMath.NormalizePower(default).Power == 0,
            "Music gains: invalid amplitudes are silent and an all-silent vector does not divide by zero");
        var start = new FishingMusicGains(1, 0, 0, 0); bool fades = true;
        for (int i = 0; i <= 100; i++)
        {
            var result = FishingMusicMath.Blend(start, 1, i / 100f);
            fades &= Near(result.Power, 1) && Near(result.A, FishingMusicMath.EqualPowerOut(i / 100f))
                && Near(result.B, FishingMusicMath.EqualPowerIn(i / 100f)) && result.C == 0 && result.D == 0;
        }
        check(fades, "Music gains: a regular two-source transition follows the equal-power fade over its whole duration");
        var capture = FishingMusicMath.Blend(start, 1, .43f);
        check(Same(FishingMusicMath.Blend(capture, 2, 0), capture),
            "Music gains: changing destinations mid-fade begins at the exact captured channel mix");
        bool redirects = true;
        for (int i = 0; i < 100; i++)
        {
            int destination = i % 4;
            redirects &= Same(capture, FishingMusicMath.Blend(capture, destination, 0));
            capture = FishingMusicMath.Blend(capture, destination, .23f);
            redirects &= Finite(capture.Power) && Near(capture.Power, 1)
                && capture.A >= 0 && capture.B >= 0 && capture.C >= 0 && capture.D >= 0;
        }
        check(redirects, "Music gains: repeated four-source redirections preserve power and cannot reset an audible channel abruptly");
        check(Same(capture, FishingMusicMath.Blend(capture, 0, float.NaN)) && Same(capture, FishingMusicMath.Blend(capture, 8, .5f)),
            "Music gains: invalid phases or channel indexes preserve the current finite mix");
        var overlapping = FishingMusicMath.Blend(capture, 2, .5f);
        check(Near(overlapping.Power, 1), "Music gains: a destination already present in the mix cannot create a gain spike during redirection");
        var partial = new FishingMusicGains(.2f, .3f, 0, 0);
        check(Same(FishingMusicMath.Blend(partial, 3, 0), partial) && FishingMusicMath.Blend(partial, 3, 1).D == 1,
            "Music gains: redirecting a partially faded-in source preserves its quieter initial amplitude");
        check(Near(FishingMusicMath.Blend(partial, 3, .5f).Power, .565f),
            "Music gains: a quiet source approaches full power gradually without a normalization jump");
        var silent = default(FishingMusicGains);
        check(FishingMusicMath.Blend(silent, 2, 0).Power == 0 && FishingMusicMath.Blend(silent, 2, .0001f).C < .001f
            && Near(FishingMusicMath.Blend(silent, 2, .5f).Power, .5f),
            "Music gains: starting playback from silence fades in smoothly instead of instantly normalizing to full volume");
        check(Near(FishingMusicMath.Blend(capture, -1, .5f).Power, .5f) && FishingMusicMath.Blend(capture, -1, 1).Power == 0,
            "Music gains: a negative-one destination fades all existing channels to silence");
        check(FishingMusicMath.Blend(new FishingMusicGains(float.NaN, -1, float.PositiveInfinity, 0), 0, .5f).A > 0
            && normalized.At(-1) == 0 && normalized.At(4) == 0,
            "Music gains: malformed source gains and out-of-range channel queries remain safe");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Near(float a, float b) => Math.Abs(a - b) < .000003f;
    private static bool Same(FishingMusicGains a, FishingMusicGains b) => Near(a.A, b.A) && Near(a.B, b.B) && Near(a.C, b.C) && Near(a.D, b.D);
}
