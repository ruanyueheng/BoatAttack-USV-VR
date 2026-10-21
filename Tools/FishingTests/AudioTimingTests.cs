using System;
using BoatAttack.Fishing;

internal static class AudioTimingTests
{
    internal static void Run(Action<bool, string> check)
    {
        var feet = new FishingFootstepClock();
        int sounds = 0;
        for (int i = 0; i < 200; i++) if (feet.Tick(0, 0, .02f, true, true, false)) sounds++;
        check(sounds == 0, "Footsteps: idle and walking against a wall produce no displacement-based events");
        check(!feet.Tick(.1f, 0, .04f, true, true, false), "Footsteps: starting movement does not immediately play a step");
        for (int i = 0; i < 4; i++) if (feet.Tick(.1f, 0, .04f, true, true, false)) sounds++;
        check(sounds == 0 && feet.Tick(.1f, 0, .04f, true, true, false), "Footsteps: first step waits for roughly half a normal stride");
        sounds = 0;
        for (int i = 0; i < 10; i++) if (feet.Tick(.1f, 0, .04f, true, true, false)) sounds++;
        check(sounds == 0 && feet.Tick(.1f, 0, .04f, true, true, false), "Footsteps: later steps require a complete normal stride");
        int walking = DistanceSteps(2.7f, false, 10.8f), running = DistanceSteps(4.4f, false, 10.8f), crouching = DistanceSteps(1.25f, true, 10.8f);
        check(walking == 10 && running == 8 && crouching == 15,
            "Footsteps: equal real distances use distinct walking, running and crouching stride lengths");
        check(DistanceSteps(3.3f, false, 10.8f) == 10 && DistanceSteps(3.31f, false, 10.8f) == 8,
            "Footsteps: the longer running stride begins only above the actual 3.3m/s threshold");
        feet.Reset(); float last = -1; bool spaced = true; sounds = 0;
        for (int i = 1; i <= 40; i++)
        {
            if (!feet.Tick(.4f, 0, .05f, true, true, true)) continue;
            if (last >= 0 && i * .05f - last < .1699f) spaced = false;
            last = i * .05f; sounds++;
        }
        check(spaced && sounds > 0 && sounds <= 11, "Footsteps: fast displacement still respects the minimum 0.17-second interval");

        feet.Reset(); feet.Tick(.2f, 0, .1f, true, true, false); feet.Tick(.2f, 0, .1f, true, true, false);
        check(!feet.Tick(0, 0, .02f, false, true, false) && !feet.Tick(.2f, 0, .1f, true, true, false),
            "Footsteps: pause or a modal disable clears accumulated partial strides");
        feet.Reset(); feet.Tick(.2f, 0, .1f, true, true, false); feet.Tick(.2f, 0, .1f, true, true, false);
        check(!feet.Tick(.2f, 0, .1f, true, false, false) && !feet.Tick(.2f, 0, .1f, true, true, false),
            "Footsteps: airborne motion clears the old ground cadence before landing");
        feet.Reset(); feet.Tick(.2f, 0, .1f, true, true, false); feet.Tick(.2f, 0, .1f, true, true, false);
        for (int i = 0; i < 3; i++) feet.Tick(0, 0, .08f, true, true, false);
        check(!feet.Tick(.2f, 0, .1f, true, true, false), "Footsteps: standing still for over 0.2 seconds clears leftover stride distance");
        feet.Reset(); feet.Tick(.2f, 0, .1f, true, true, false); feet.Tick(.2f, 0, .1f, true, true, false);
        feet.Tick(0, 0, .1f, true, true, false);
        check(feet.Tick(.2f, 0, .1f, true, true, false), "Footsteps: a brief stop does not force a new half-stride on every frame");

        foreach (var sample in new[]
        {
            new[] { float.NaN, 0, .1f }, new[] { float.PositiveInfinity, 0, .1f }, new[] { -.1f, 0, .1f },
            new[] { .1f, float.NaN, .1f }, new[] { .1f, float.NegativeInfinity, .1f },
            new[] { .1f, 0, float.NaN }, new[] { .1f, 0, float.PositiveInfinity },
            new[] { .1f, 0, -.01f }, new[] { .1f, 0, 0 }, new[] { .1f, 0, .251f },
            new[] { 1.51f, 0, .25f }, new[] { .81f, 0, .1f }, new[] { .1f, .501f, .1f }, new[] { .1f, -.501f, .1f }
        })
        {
            feet.Reset(); feet.Tick(.2f, 0, .1f, true, true, false); feet.Tick(.2f, 0, .1f, true, true, false);
            check(!feet.Tick(sample[0], sample[1], sample[2], true, true, false)
                && !feet.Tick(.2f, 0, .1f, true, true, false),
                "Footsteps: invalid samples, teleports and large jumps clear residual cadence: " + sample[0] + "/" + sample[1] + "/" + sample[2]);
        }
        feet.Reset();
        check(!feet.Tick(.27f, .10f, .1f, true, true, false) && feet.Tick(.27f, -.10f, .1f, true, true, false),
            "Footsteps: ordinary grounded slope motion still supports a first step");
        feet.Reset();
        check(feet.Tick(1.5f, .5f, .25f, true, true, false), "Footsteps: documented distance, vertical and frame-time boundary values remain valid");
        feet.Reset();
        check(!feet.Tick(.1f, 0, .05f, true, true, false), "Footsteps: explicit reset starts with a quiet fresh partial stride");

        var fish = new FishingFishFlapClock();
        check(!fish.Tick("fish-a", 0, true) && Advance(fish, "fish-a", 0, .6f, true) == 0,
            "Fish flap: the early 0.196-second lift peak is silent before inspection settles");
        check(Advance(fish, "fish-a", .6f, 1.1f, true) == 1, "Fish flap: the first audible inspection peak occurs at approximately 0.982 seconds");
        bool repeated = false;
        for (int i = 0; i < 20; i++) repeated |= fish.Tick("fish-a", 1.1f, true);
        check(!repeated, "Fish flap: presenting the same fish and animation timestamp repeatedly cannot duplicate a beat");
        check(Advance(fish, "fish-a", 1.1f, 2.6f, true) == 2, "Fish flap: later peaks occur once per 2-pi-over-8-second animation cycle");
        check(!fish.Tick("fish-b", 2.6f, true) && !fish.Tick("fish-b", 2.61f, true),
            "Fish flap: a newly restored or replaced fish never replays a peak from earlier in its current cycle");
        check(Advance(fish, "fish-b", 2.61f, 3.4f, true) == 1, "Fish flap: a new fish can play its next future peak normally");

        fish.Reset(); fish.Tick("paused-fish", 0, true); Advance(fish, "paused-fish", 0, .8f, true);
        check(!fish.Tick("paused-fish", 1, false) && !fish.Tick("paused-fish", 1.8f, false)
            && !fish.Tick("paused-fish", 2.6f, false) && !fish.Tick("paused-fish", 2.65f, true),
            "Fish flap: suppressed animation cycles are consumed and resuming does not replay them");
        check(Advance(fish, "paused-fish", 2.65f, 3.4f, true) == 1, "Fish flap: normal future cadence survives a long suppression without resetting every frame");
        fish.Reset(); fish.Tick("frozen-fish", 0, true); Advance(fish, "frozen-fish", 0, .96f, true);
        fish.Tick("frozen-fish", .96f, false);
        check(!fish.Tick("frozen-fish", .96f, true) && Advance(fish, "frozen-fish", .96f, 1.05f, true) == 1,
            "Fish flap: resuming a genuinely frozen clock preserves the next not-yet-crossed peak");
        fish.Reset(); fish.Tick("resume-fish", 0, true); Advance(fish, "resume-fish", 0, .96f, true);
        fish.Tick("resume-fish", .96f, false);
        check(!fish.Tick("resume-fish", 1.03f, true) && !fish.Tick("resume-fish", 1.04f, true),
            "Fish flap: a peak crossed while resuming is synchronized without a delayed slap");
        fish.Reset(); fish.Tick("hitch-fish", 0, true); Advance(fish, "hitch-fish", 0, .8f, true);
        check(!fish.Tick("hitch-fish", 5, true) && !fish.Tick("hitch-fish", 5, true)
            && Advance(fish, "hitch-fish", 5, 5.75f, true) == 1,
            "Fish flap: a long skipped interval cannot catch up obsolete beats, but its next real peak still plays");
        fish.Reset(); fish.Tick("rewound-fish", 0, true); Advance(fish, "rewound-fish", 0, 1.1f, true);
        check(!fish.Tick("rewound-fish", 0, true) && Advance(fish, "rewound-fish", 0, .6f, true) == 0
            && Advance(fish, "rewound-fish", .6f, 1.1f, true) == 1,
            "Fish flap: clock rollback restarts quiet inspection rather than replaying the previous cycle immediately");
        fish.Reset();
        check(!fish.Tick("loaded-fish", 15, true) && !fish.Tick("loaded-fish", 15.01f, true),
            "Fish flap: first sampling a loaded inspection halfway through its animation produces no overdue sound");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1 })
            check(!fish.Tick("invalid-fish", invalid, true) && !fish.Tick("invalid-fish", 1.1f, true),
                "Fish flap: invalid animation time resets safely without an immediate recovery sound: " + invalid);
        check(!fish.Tick(null, 1, true) && !fish.Tick("", 1, true) && !fish.Tick(" ", 1, true),
            "Fish flap: missing catch identity cannot produce a sound");
        check(!fish.Tick("large-fish", float.MaxValue, true) && !fish.Tick("large-fish", float.MaxValue, true),
            "Fish flap: even extremely large finite animation timestamps do not overflow a cycle counter");
    }

    private static int DistanceSteps(float speed, bool crouched, float total)
    {
        var clock = new FishingFootstepClock(); int count = 0;
        float distance = speed * .02f;
        for (int i = 0; i < (int)Math.Round(total / distance); i++) if (clock.Tick(distance, 0, .02f, true, true, crouched)) count++;
        return count;
    }
    private static int Advance(FishingFishFlapClock clock, string id, float from, float to, bool playing)
    {
        int count = 0; float time = from;
        while (time < to)
        {
            time = Math.Min(to, time + .02f);
            if (clock.Tick(id, time, playing)) count++;
        }
        return count;
    }
}
