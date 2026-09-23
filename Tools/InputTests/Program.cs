using System;
using BoatAttack.VR;

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
            Console.WriteLine("TOTAL: " + _passed + " passed. Pure input logic only; not Unity/device validation.");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
