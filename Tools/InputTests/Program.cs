using System;
using BoatAttack.VR;
using BoatAttack;

internal static class Program
{
    private static int _passed;
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name); _passed++;
    }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.00001f;
    private static USVInputState All(bool held) => new USVInputState {reset=held, pause=held, interact=held, map=held};
    private static bool NoPress(USVInputState s) => !s.reset && !s.pause && !s.interact && !s.map;
    public static int Main()
    {
        try
        {
            Check("Desktop works without HMD", USVInputProcessor.Resolve(USVInputMode.Desktop,false,true)==USVInputSourceKind.Desktop);
            Check("Desktop ignores connected HMD", USVInputProcessor.Resolve(USVInputMode.Desktop,true,true)==USVInputSourceKind.Desktop);
            Check("Auto chooses desktop without loader", USVInputProcessor.Resolve(USVInputMode.Auto,false,true)==USVInputSourceKind.Desktop);
            Check("Auto chooses XR with active loader", USVInputProcessor.Resolve(USVInputMode.Auto,true,true)==USVInputSourceKind.XR);
            Check("Forced XR without loader stays neutral", USVInputProcessor.Resolve(USVInputMode.XR,false,true)==USVInputSourceKind.None);
            Check("Lost focus always stops input", USVInputProcessor.Resolve(USVInputMode.Desktop,true,false)==USVInputSourceKind.None);
            var p=new USVInputProcessor();
            p.Process(default(USVInputState),USVInputSourceKind.Desktop,0.12f);
            var s=p.Process(All(true),USVInputSourceKind.Desktop,0.12f);
            Check("All command press edges emitted", s.reset && s.pause && s.interact && s.map);
            Check("Held commands do not repeat", NoPress(p.Process(All(true),USVInputSourceKind.Desktop,0.12f)));
            Check("Releases are not presses", NoPress(p.Process(All(false),USVInputSourceKind.Desktop,0.12f)));
            s=p.Process(All(true),USVInputSourceKind.Desktop,0.12f);
            Check("Second press unpauses without physics ticks", s.pause);
            Check("Source switch suppresses held commands", NoPress(p.Process(All(true),USVInputSourceKind.XR,0.12f)));
            s=p.Process(new USVInputState {throttle=0.1f,steering=-0.1f},USVInputSourceKind.XR,0.12f);
            Check("Deadzone removes joystick drift", s.throttle==0f && s.steering==0f);
            s=p.Process(new USVInputState {throttle=0.56f,steering=-0.56f},USVInputSourceKind.XR,0.12f);
            Check("Axes rescale outside deadzone", Near(s.throttle,0.5f) && Near(s.steering,-0.5f));
            s=p.Process(new USVInputState {throttle=2f,steering=-3f,brake=5f},USVInputSourceKind.XR,0.12f);
            Check("Out-of-range values clamp", s.throttle==1f && s.steering==-1f && s.brake==1f);
            s=p.Process(new USVInputState {throttle=float.NaN,steering=float.PositiveInfinity,brake=float.NaN},USVInputSourceKind.XR,0.12f);
            Check("Invalid values cannot enter physics", s.throttle==0f && s.steering==0f && s.brake==0f);
            s=p.Process(new USVInputState {throttle=1f,steering=1f,brake=1f,pause=true},USVInputSourceKind.None,0.12f);
            Check("Lost runtime clears every control", s.throttle==0f && s.steering==0f && s.brake==0f && NoPress(s));
            Check("Reconnect held buttons suppressed", NoPress(p.Process(All(true),USVInputSourceKind.XR,0.12f)));
            p.Reset();
            Check("Explicit reset clears history", NoPress(p.Process(All(true),USVInputSourceKind.Desktop,0.12f)));
            CheckRoutingMatrix();
            CheckAxisBoundaries();
            CheckIndependentCommands();
            CheckAxisInvariants();
            CheckFrameBudgets();
            Console.WriteLine("TOTAL: " + _passed + " passed. Pure input/render policy logic only; not Unity/device validation.");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void CheckRoutingMatrix()
    {
        foreach (USVInputMode mode in Enum.GetValues(typeof(USVInputMode)))
        {
            foreach (bool xrActive in new[] { false, true })
            {
                Check(mode + " clears input when unfocused (XR=" + xrActive + ")",
                    USVInputProcessor.Resolve(mode, xrActive, false) == USVInputSourceKind.None);
                var expected = mode == USVInputMode.Desktop ? USVInputSourceKind.Desktop
                    : xrActive ? USVInputSourceKind.XR
                    : mode == USVInputMode.Auto ? USVInputSourceKind.Desktop : USVInputSourceKind.None;
                Check(mode + " resolves focused input (XR=" + xrActive + ")",
                    USVInputProcessor.Resolve(mode, xrActive, true) == expected);
            }
        }
    }

    private static void CheckAxisBoundaries()
    {
        var p = new USVInputProcessor();
        var s = p.Process(new USVInputState { throttle = 0.12f, steering = -0.12f, brake = -1f },
            USVInputSourceKind.Desktop, 0.12f);
        Check("Exact deadzone boundary and negative brake are zero", s.throttle == 0f && s.steering == 0f && s.brake == 0f);
        s = p.Process(new USVInputState { throttle = 1f, steering = -1f, brake = 1f }, USVInputSourceKind.XR, 0.95f);
        Check("Full axis range remains available with maximum deadzone", s.throttle == 1f && s.steering == -1f && s.brake == 1f);
        s = p.Process(new USVInputState { throttle = 0.5f, steering = -0.5f }, USVInputSourceKind.XR, -5f);
        Check("Negative deadzone clamps to zero", s.throttle == 0.5f && s.steering == -0.5f);
        s = p.Process(new USVInputState { throttle = 0.5f, steering = -0.5f }, USVInputSourceKind.XR, 8f);
        Check("Oversized deadzone clamps below one", s.throttle == 0f && s.steering == 0f);
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            s = p.Process(new USVInputState { throttle = invalid, steering = invalid, brake = invalid }, USVInputSourceKind.XR, 0.12f);
            Check("Non-finite axes and brake are neutral: " + invalid, s.throttle == 0f && s.steering == 0f && s.brake == 0f);
            s = p.Process(new USVInputState { throttle = 0.5f, steering = -0.5f }, USVInputSourceKind.XR, invalid);
            Check("Non-finite deadzone safely falls back to zero: " + invalid, s.throttle == 0.5f && s.steering == -0.5f);
        }
    }

    private static void CheckIndependentCommands()
    {
        var p = new USVInputProcessor();
        p.Process(default(USVInputState), USVInputSourceKind.Desktop, 0f);
        var s = p.Process(new USVInputState { interact = true }, USVInputSourceKind.Desktop, 0f);
        Check("Interact emits independently", s.interact && !s.pause && !s.reset && !s.map);
        s = p.Process(new USVInputState { interact = true, pause = true }, USVInputSourceKind.Desktop, 0f);
        Check("Pause emits while interact is held", s.pause && !s.interact && !s.reset && !s.map);
        p.Process(All(true), USVInputSourceKind.None, 0f);
        Check("Focus recovery suppresses every held command", NoPress(p.Process(All(true), USVInputSourceKind.Desktop, 0f)));
        p.Process(All(false), USVInputSourceKind.Desktop, 0f);
        s = p.Process(All(true), USVInputSourceKind.Desktop, 0f);
        Check("Commands work after recovery and release", s.reset && s.pause && s.interact && s.map);
    }

    private static void CheckAxisInvariants()
    {
        var random = new Random(20260924);
        var p = new USVInputProcessor();
        for (int i = 0; i < 2000; i++)
        {
            float axis = (float)(random.NextDouble() * 6d - 3d);
            float brake = (float)(random.NextDouble() * 6d - 3d);
            float deadzone = (float)(random.NextDouble() * 3d - 1d);
            var s = p.Process(new USVInputState { throttle = axis, steering = -axis, brake = brake }, USVInputSourceKind.Desktop, deadzone);
            if (float.IsNaN(s.throttle) || float.IsInfinity(s.throttle) || Math.Abs(s.throttle) > 1f
                || !Near(s.throttle, -s.steering) || s.brake < 0f || s.brake > 1f)
                throw new Exception("Axis invariant failed at deterministic sample " + i);
        }
        Check("2000 deterministic axis samples stay finite, bounded and symmetric", true);
    }

    private static void CheckFrameBudgets()
    {
        foreach (int fps in new[] { 30, 60, 120 })
        {
            Check("Slow frame reduces scale at " + fps + " FPS", DynamicResolutionPolicy.GetAdjustment(2f / fps, fps, 0.1f) == -0.1f);
            Check("Fast frame increases scale at " + fps + " FPS", DynamicResolutionPolicy.GetAdjustment(0.5f / fps, fps, 0.1f) == 0.1f);
            Check("Exact frame budget holds scale at " + fps + " FPS", DynamicResolutionPolicy.GetAdjustment(1f / fps, fps, 0.1f) == 0f);
        }
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Check("Invalid frame duration holds scale: " + invalid, DynamicResolutionPolicy.GetAdjustment(invalid, 60, 0.1f) == 0f);
        Check("Invalid target FPS holds scale", DynamicResolutionPolicy.GetAdjustment(0.02f, 0, 0.1f) == 0f);
        Check("Invalid step holds scale", DynamicResolutionPolicy.GetAdjustment(0.02f, 60, float.NaN) == 0f);
        Check("A 20ms frame exceeds a 60 FPS budget", DynamicResolutionPolicy.GetAdjustment(0.020f, 60, 0.1f) < 0f);
    }
}
