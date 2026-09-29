using System;
using System.IO;

namespace BoatAttack.Fishing
{
    /// <summary>Explicit opt-in only. Session identifiers are names, never filesystem paths.</summary>
    public sealed class FishingAcceptanceOptions
    {
        public const string EnableFlag = "-fishing-acceptance";
        public string SessionId { get; private set; } = "manual-v043";
        public FishingAcceptanceCase InitialCase { get; private set; } = FishingAcceptanceCase.FreePlay;
        public string OutputDirectory { get; private set; }
        public bool CaptureAll { get; private set; }
        public bool Smoke { get; private set; }
        public bool Automatic => CaptureAll || Smoke;

        public static FishingAcceptanceOptions Parse(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            bool enabled = Array.IndexOf(args, EnableFlag) >= 0;
            foreach (string arg in args)
                if (arg != null && arg.StartsWith(EnableFlag + "-", StringComparison.Ordinal) && !enabled)
                    throw new ArgumentException("Acceptance options require " + EnableFlag + ".");
            if (!enabled) return null;
            if (Array.IndexOf(args, "-fishing-autoplay") >= 0 || Array.IndexOf(args, "-fishing-capture") >= 0)
                throw new ArgumentException("Do not combine acceptance with the legacy autoplay/capture driver.");
            var result = new FishingAcceptanceOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "-fishing-acceptance-session": result.SessionId = Value(args, ref i); break;
                    case "-fishing-acceptance-case":
                        string name = Value(args, ref i);
                        if (!Enum.TryParse(name, true, out FishingAcceptanceCase selected)
                            || !string.Equals(Enum.GetName(typeof(FishingAcceptanceCase), selected), name, StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("Unknown acceptance case: " + name);
                        result.InitialCase = selected;
                        break;
                    case "-fishing-acceptance-output": result.OutputDirectory = Value(args, ref i); break;
                    case "-fishing-acceptance-capture": result.CaptureAll = true; break;
                    case "-fishing-acceptance-smoke": result.Smoke = true; break;
                    default:
                        if (arg != null && arg.StartsWith(EnableFlag + "-", StringComparison.Ordinal))
                            throw new ArgumentException("Unknown acceptance option: " + arg);
                        break;
                }
            }
            ValidateSession(result.SessionId);
            if (result.CaptureAll && result.Smoke) throw new ArgumentException("Capture and headless smoke are separate runs.");
            if (result.OutputDirectory != null && !Path.IsPathRooted(result.OutputDirectory))
                throw new ArgumentException("Acceptance output must be an absolute directory.");
            return result;
        }

        public string ProfilePath(string temporaryRoot, bool fixture)
        {
            ValidateSession(SessionId);
            if (string.IsNullOrEmpty(temporaryRoot) || !Path.IsPathRooted(temporaryRoot))
                throw new ArgumentException("A rooted temporary directory is required.");
            return Path.Combine(temporaryRoot, "FishingAcceptance", SessionId, fixture ? "fixture.json" : "playthrough.json");
        }

        private static string Value(string[] args, ref int index)
        {
            if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException("Acceptance option is missing its value.");
            return args[index];
        }

        private static void ValidateSession(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 48) throw new ArgumentException("Session name must contain 1-48 safe characters.");
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                    throw new ArgumentException("Session name may contain only ASCII letters, numbers, '-' and '_'.");
            string upper = value.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL"
                || (upper.Length == 4 && (upper.StartsWith("COM", StringComparison.Ordinal) || upper.StartsWith("LPT", StringComparison.Ordinal))
                    && upper[3] >= '1' && upper[3] <= '9'))
                throw new ArgumentException("Session name cannot be a Windows device name.");
        }
    }
}
