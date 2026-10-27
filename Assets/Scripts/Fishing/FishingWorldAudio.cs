using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingWorldAudio WorldAudio { get; private set; }
    }

    /// <summary>Scene-local recorded action and ambience. Audio observation never changes gameplay or physics.</summary>
    [DefaultExecutionOrder(700)]
    public sealed partial class FishingWorldAudio : MonoBehaviour
    {
        private static readonly HashSet<string> CueKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "ImpactWood", "ImpactStone", "ImpactMetal", "BodyLand", "WaterEntry", "WaterStroke", "ClimbGrip",
            "FishBite", "FishHook", "LineSnap", "FishSplash", "FishStore", "AnimalHit", "LootBag", "CanHandle",
            "WellBucket", "ForgeStrike", "WorkbenchAssemble", "CraftFinish", "StorageMove", "LanternSwitch",
            "FruitShake", "UIConfirm", "UIBack", "UIReject", "TVSwitch", "CushionCloth", "PortalPass",
            "MechanismClick", "HandcraftRub", "SeagullCall", "AnimalRustle", "BoatSlap", "WaterFinish",
            "BoatScrapeLoop", "FurnaceLoop", "CookSizzleLoop", "RainLoop", "ForestLoop", "WreckLoop",
            "WindCorridorLoop", "StarsLoop", "WellRopeLoop", "WaterPourLoop", "WaterSprinkleLoop", "TVLoop"
        };
        private static readonly HashSet<string> LoopKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "BoatScrapeLoop", "FurnaceLoop", "CookSizzleLoop", "RainLoop", "ForestLoop", "WreckLoop",
            "WindCorridorLoop", "StarsLoop", "WellRopeLoop", "WaterPourLoop", "WaterSprinkleLoop", "TVLoop"
        };
        private sealed class Shot
        {
            public AudioSource Source;
            public bool BedContact;
        }
        private sealed class LoopVoice
        {
            public AudioSource Source;
            public string Id, Cue;
            public FishingAudioCategory Category;
            public int MaintainedFrame = -1;
            public float Gain, Target;
        }

        private FishingPrototype _c;
        private bool _built, _disposed, _focused = true, _applicationPaused;
        private readonly Shot[] _effects = new Shot[10], _ui = new Shot[4], _ambientShots = new Shot[4];
        private readonly LoopVoice[] _loops = new LoopVoice[12];
        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _plays = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _starts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _lastPlay = new Dictionary<string, double>(StringComparer.Ordinal);
        public IReadOnlyList<AudioSource> Sources { get; private set; }

        partial void BuildMotion();
        partial void TickMotion(float dt);
        partial void ResetMotion();
        partial void DisposeMotion();
        partial void BuildGameplay();
        partial void TickGameplay(float dt);
        partial void ResetGameplay();
        partial void BuildEnvironment();
        partial void TickEnvironment(float dt);
        partial void ResetEnvironment();
        partial void ApplyEnvironmentShelter();

        private bool CommonContext => _built && isActiveAndEnabled && _c != null && _c.isActiveAndEnabled
            && _c.Career != null && !_c.QuietMode && _c.MasterVolume > 0 && !_applicationPaused
            && (_c.Automated || (_focused && Application.isFocused));
        private bool WorldContext => CommonContext && !_c.Paused && !_c.StartMenuOpen
            && (_c.Prologue == null || !_c.Prologue.Open) && (_c.Home == null || !_c.Home.Sleeping);
        private bool BedContactContext => CommonContext && !_c.Paused && !_c.StartMenuOpen
            && (_c.Prologue == null || !_c.Prologue.Open);

        public void Build(FishingPrototype controller)
        {
            if (_built || controller == null) return;
            _c = controller; _focused = Application.isFocused;
            BuildShots(_effects, "World action", FishingAudioCategory.Effects, .17f, false);
            BuildShots(_ui, "Successful interface action", FishingAudioCategory.Effects, .11f, true);
            BuildShots(_ambientShots, "Local environmental event", FishingAudioCategory.Ambient, .13f, false);
            for (int i = 0; i < _loops.Length; i++)
                _loops[i] = new LoopVoice { Source = Source("Local ambience " + (i + 1), FishingAudioCategory.Ambient, 0, false, true), Category = FishingAudioCategory.Ambient };
            Sources = _sources.AsReadOnly();
            _built = true;
            BuildMotion(); BuildGameplay(); BuildEnvironment();
        }

        private void BuildShots(Shot[] pool, string label, FishingAudioCategory category, float volume, bool ui)
        {
            for (int i = 0; i < pool.Length; i++)
                pool[i] = new Shot { Source = Source(label + " " + (i + 1), category, volume, ui, false) };
        }
        private AudioSource Source(string label, FishingAudioCategory category, float volume, bool ui, bool loop)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = ui ? 0 : 1;
            source.dopplerLevel = 0; source.minDistance = 1.5f;
            source.maxDistance = category == FishingAudioCategory.Ambient && !loop ? 60 : 24;
            source.rolloffMode = AudioRolloffMode.Linear; source.priority = ui ? 100 : loop ? 180 : 140;
            _sources.Add(source);
            _c.RegisterAudioSource(source, category, volume, ui);
            return source;
        }

        public static AudioClip Clip(string key) => key != null && CueKeys.Contains(key)
            ? Resources.Load<AudioClip>("FishingAudio/World/" + key) : null;
        private AudioClip LoadedClip(string key)
        {
            if (key == null || !CueKeys.Contains(key)) return null;
            if (!_clips.TryGetValue(key, out var clip)) _clips.Add(key, clip = Clip(key));
            return clip;
        }
        public bool HasCue(string key) => LoadedClip(key) != null;
        public int PlayCount(string key) => key != null && _plays.TryGetValue(key, out int count) ? count : 0;
        public int LoopStarts(string key) => key != null && _starts.TryGetValue(key, out int count) ? count : 0;
        public AudioSource LoopSource(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var voice in _loops) if (voice != null && voice.Id == id) return voice.Source;
            return null;
        }
        private static bool UiCue(string key) => key == "UIConfirm" || key == "UIBack" || key == "UIReject"
            || key == "StorageMove" || key == "CraftFinish" || key == "ForgeStrike" || key == "WorkbenchAssemble"
            || key == "LanternSwitch" || key == "TVSwitch";
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        /// <summary>One recorded cue, once per successful edge. Pool and cooldown bound collision/UI spam.</summary>
        public bool Play(string cue, Vector3 at, float gain = 1, FishingAudioCategory category = FishingAudioCategory.Effects)
            => PlayRecorded(cue, at, gain, category, false);

        /// <summary>Only the actual sleep-motion contacts may sound while the rest of the world is asleep.</summary>
        public bool NotifyBedContact(Vector3 at)
            => _c != null && _c.Home != null && _c.Home.Sleeping
                && PlayRecorded("CushionCloth", at, .4f, FishingAudioCategory.Effects, true);

        private bool PlayRecorded(string cue, Vector3 at, float gain, FishingAudioCategory category, bool bedContact)
        {
            bool ui = category == FishingAudioCategory.Effects && UiCue(cue);
            if (!(bedContact ? BedContactContext : ui ? CommonContext : WorldContext) || !Finite(at) || !Finite(gain) || gain <= 0
                || (category != FishingAudioCategory.Effects && category != FishingAudioCategory.Ambient)
                || (category == FishingAudioCategory.Effects ? _c.EffectsVolume : _c.AmbientVolume) <= 0
                || cue == null || LoopKeys.Contains(cue)) return false;
            var clip = LoadedClip(cue); if (clip == null) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            float cooldown = ui ? .08f : cue.StartsWith("Impact", StringComparison.Ordinal) ? .28f : .18f;
            if (_lastPlay.TryGetValue(cue, out double last) && now - last < cooldown) return false;
            var pool = ui ? _ui : category == FishingAudioCategory.Ambient ? _ambientShots : _effects;
            Shot voice = null;
            foreach (var candidate in pool)
                if (candidate.Source != null && !candidate.Source.isPlaying) { voice = candidate; break; }
            // Do not steal an audible voice: a crowded instant may omit a quiet detail, never stack without a bound.
            if (voice == null) return false;
            voice.BedContact = bedContact;
            voice.Source.transform.position = at; voice.Source.clip = clip;
            voice.Source.pitch = 1;
            _c.SetAudioBaseVolume(voice.Source, Mathf.Clamp01(gain) * (ui ? .11f : category == FishingAudioCategory.Ambient ? .13f : .17f));
            voice.Source.Play(); _lastPlay[cue] = now; _plays[cue] = PlayCount(cue) + 1;
            return true;
        }

        /// <summary>Lease a local loop for this frame. Action loops follow Effects; unspecified loops remain Ambient.</summary>
        public void MaintainLoop(string id, string cue, Vector3 at, float volume, bool spatial = true, float range = 24,
            FishingAudioCategory category = FishingAudioCategory.Ambient)
        {
            if (!WorldContext || (category != FishingAudioCategory.Effects && category != FishingAudioCategory.Ambient)
                || (category == FishingAudioCategory.Effects ? _c.EffectsVolume : _c.AmbientVolume) <= 0
                || string.IsNullOrEmpty(id) || id.Length > 80
                || !LoopKeys.Contains(cue ?? "") || !Finite(at) || !Finite(volume) || volume <= 0 || !Finite(range)) return;
            var clip = LoadedClip(cue); if (clip == null) return;
            LoopVoice voice = null;
            foreach (var candidate in _loops) if (candidate.Id == id) { voice = candidate; break; }
            if (voice == null)
                foreach (var candidate in _loops) if (candidate.Id == null) { voice = candidate; break; }
            if (voice == null || voice.Source == null) return;
            if (voice.Id == null || voice.Cue != cue || voice.Category != category)
            {
                voice.Source.Stop(); voice.Source.clip = clip; voice.Gain = 0; voice.Cue = cue; voice.Id = id; voice.Category = category;
                // A pooled source may previously have belonged to the other channel.
                // Rebind only at a lease/cue/category boundary, never once per maintained frame.
                _c.RegisterAudioSource(voice.Source, category, 0);
                voice.Source.priority = category == FishingAudioCategory.Effects ? 140 : 180;
                voice.Source.Play(); _starts[cue] = LoopStarts(cue) + 1;
            }
            voice.Source.spatialBlend = spatial ? 1 : 0; voice.Source.transform.position = at;
            voice.Source.maxDistance = Mathf.Clamp(range, 3, 90);
            voice.Target = Mathf.Clamp(volume, 0, .15f); voice.MaintainedFrame = Time.frameCount;
        }

        private void TickLoops(float dt)
        {
            foreach (var voice in _loops)
            {
                if (voice.Id == null) continue;
                if (voice.Source == null) { ReleaseLoop(voice); continue; }
                float target = voice.MaintainedFrame == Time.frameCount ? voice.Target : 0;
                // A watering stream is visible for only about 1.2 seconds. It must
                // reach audible gain promptly instead of spending most of that time
                // in the intentionally slow ambience fade. Volumes remain raw base
                // gains: channel/master settings are applied once by the registry.
                float speed = voice.Category == FishingAudioCategory.Effects
                    ? Mathf.Max(.0001f, voice.Target) / (target > voice.Gain ? .08f : .12f)
                    : target > voice.Gain ? .12f : .18f;
                voice.Gain = Mathf.MoveTowards(voice.Gain, target, dt * speed);
                _c.SetAudioBaseVolume(voice.Source, voice.Gain);
                if (target == 0 && voice.Gain <= .0001f) ReleaseLoop(voice);
            }
        }
        private void ReleaseLoop(LoopVoice voice)
        {
            if (voice.Source != null)
            {
                voice.Source.Stop(); voice.Source.clip = null;
                if (_c != null) _c.SetAudioBaseVolume(voice.Source, 0);
            }
            voice.Id = voice.Cue = null; voice.Target = voice.Gain = 0; voice.MaintainedFrame = -1;
        }
        private void StopWorldVoices(bool preserveBedContact = false)
        {
            foreach (var voice in _effects)
                if (voice != null && voice.Source != null && !(preserveBedContact && voice.BedContact && BedContactContext)) voice.Source.Stop();
            foreach (var voice in _ambientShots) if (voice != null && voice.Source != null) voice.Source.Stop();
            foreach (var voice in _loops) if (voice != null && voice.Id != null) ReleaseLoop(voice);
        }
        private void StopAllVoices()
        {
            StopWorldVoices();
            foreach (var voice in _ui) if (voice != null && voice.Source != null) voice.Source.Stop();
        }
        private void LateUpdate()
        {
            if (!_built || _c == null || _c.Career == null) return;
            float dt = Time.unscaledDeltaTime;
            if (!Finite(dt) || dt < 0) dt = 0;
            dt = Mathf.Min(dt, .1f);
            if (WorldContext)
            {
                TickMotion(Time.deltaTime); TickEnvironment(dt);
            }
            else
            {
                StopWorldVoices(true); ResetMotion(); ResetEnvironment();
            }
            // Observe menu edges and gameplay baselines even while the world is paused, preventing resume replays.
            TickGameplay(dt);
            if (!CommonContext) StopAllVoices();
            TickLoops(dt);
            _c.ApplyAudioSettings();
            ApplyEnvironmentShelter();
        }
        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused && _built && _c != null && !_c.Automated) { StopAllVoices(); ResetMotion(); ResetEnvironment(); ResetGameplay(); }
        }
        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (paused && _built) { StopAllVoices(); ResetMotion(); ResetEnvironment(); ResetGameplay(); }
        }
        private void OnEnable()
        {
            if (_built) { ResetMotion(); ResetGameplay(); ResetEnvironment(); }
        }
        private void OnDisable()
        {
            if (!_built) return;
            StopAllVoices(); ResetMotion(); ResetGameplay(); ResetEnvironment();
        }
        private void OnDestroy()
        {
            if (!_built || _disposed) return;
            _disposed = true; StopAllVoices(); ResetEnvironment(); DisposeMotion();
            foreach (var source in _sources) if (source != null) Destroy(source.gameObject);
        }
    }
}
