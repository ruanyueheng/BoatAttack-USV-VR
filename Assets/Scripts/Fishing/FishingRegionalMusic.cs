using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingRegionalMusic RegionalMusic { get; private set; }
    }

    /// <summary>Two independently looping decks. Zone transitions never recycle a still-audible deck.</summary>
    [DefaultExecutionOrder(600)]
    public sealed class FishingRegionalMusic : MonoBehaviour
    {
        public const int SilenceZone = -1, KeepZone = -2;
        public const float MusicBase = .18f, RegionFadeSeconds = 4f;
        private const double ScheduleLead = .18;
        private static readonly string[] Keys = { "Sailing", "Fishing", "Home", "HarborShop", "Exploration",
            "RealmWind", "RealmForest", "RealmWreck", "RealmStars" };

        [Serializable]
        private sealed class Manifest { public ManifestTrack[] tracks = Array.Empty<ManifestTrack>(); }
        [Serializable]
        private sealed class ManifestTrack
        {
            public int zone = -1;
            public string key = "", title = "";
            public float duration = 0, loopStart = 0, tailTrim = 0, loopCrossfade = 0;
        }

        public sealed class TrackInfo
        {
            public int Zone { get; private set; }
            public string Key { get; private set; }
            public string Title { get; private set; }
            public float Duration { get; private set; }
            public float DeclaredDuration { get; private set; }
            public float LoopStart { get; private set; }
            public float TailTrim { get; private set; }
            public float LoopCrossfade { get; private set; }
            public AudioClip Clip { get; private set; }
            internal float ClipEnd { get; private set; }
            internal FishingMusicLoopWindow Window { get; private set; }
            private TrackInfo() { }
            internal static TrackInfo Load(int zone, string key, string title, float declaredDuration,
                float loopStart, float tailTrim, float crossfade)
            {
                var clip = Resources.Load<AudioClip>("FishingAudio/Music/" + key);
                if (clip == null || !Finite(loopStart) || loopStart < 0 || loopStart >= clip.length ||
                    !FishingMusicMath.TryLoopWindow(clip.length - loopStart, tailTrim, crossfade, out var window)) return null;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                return new TrackInfo { Zone = zone, Key = key, Title = title ?? key, Clip = clip,
                    Duration = clip.length, DeclaredDuration = declaredDuration, LoopStart = loopStart,
                    TailTrim = tailTrim, LoopCrossfade = window.Crossfade, Window = window, ClipEnd = loopStart + window.End };
            }
        }

        private sealed class Voice
        {
            public AudioSource Source;
            public bool Scheduled, First;
            public double Start, End;
        }
        private sealed class Deck
        {
            public readonly Voice[] Voices = new Voice[2];
            public TrackInfo Track;
            public int Recycle;
            public double LastEnd;
            public bool Active => Track != null;
        }
        private sealed class LegacyLease
        {
            public AudioSource Source;
            public AudioClip Clip;
            public bool PausedByUs;
        }

        private FishingPrototype _c;
        private readonly TrackInfo[] _tracks = new TrackInfo[9];
        private readonly Deck[] _decks = { new Deck(), new Deck() };
        private readonly AudioSource[] _voices = new AudioSource[4];
        private IReadOnlyList<AudioSource> _voiceView;
        private readonly List<LegacyLease> _legacy = new List<LegacyLease>();
        private readonly FishingMusicZoneGate _gate = new FishingMusicZoneGate(SilenceZone, 1.35f);
        private FishingMusicGains _deckGains, _fadeFrom;
        private bool _built, _running, _zoneInitialized, _prologue, _fading;
        private int _currentDeck = -1, _fadeTarget = -1;
        private double _fadeStart, _legacyCheck;
        private float _fadeDuration, _duck = 1;

        public bool Ready { get; private set; }
        public int CurrentZone => _currentDeck < 0 || !_decks[_currentDeck].Active ? SilenceZone : _decks[_currentDeck].Track.Zone;
        public int DesiredZone { get; private set; } = SilenceZone;
        public bool Transitioning => _fading;
        public float DuckGain => _duck;
        public bool BorrowingLegacyMusic
        {
            get { foreach (var lease in _legacy) if (lease.PausedByUs && lease.Source != null) return true; return false; }
        }
        public IReadOnlyList<AudioSource> Voices => _voiceView ?? (_voiceView = Array.AsReadOnly(_voices));
        public TrackInfo Track(int zone) => zone >= 0 && zone < _tracks.Length ? _tracks[zone] : null;
        public double ScheduledStart(int channel) => Slot(channel)?.Scheduled == true ? Slot(channel).Start : double.NaN;
        public double ScheduledEnd(int channel) => Slot(channel)?.Scheduled == true ? Slot(channel).End : double.NaN;
        public float ChannelGain(int channel)
        {
            var slot = Slot(channel);
            return slot == null ? 0 : _deckGains.At(channel / 2) * LoopGain(slot, _decks[channel / 2].Track, AudioSettings.dspTime);
        }
        private Voice Slot(int channel) => channel >= 0 && channel < 4 ? _decks[channel / 2].Voices[channel % 2] : null;

        public void Build(FishingPrototype controller)
        {
            if (_built || controller == null) return;
            _c = controller; _built = true;
            for (int i = 0; i < _voices.Length; i++)
            {
                var go = new GameObject("Regional music deck " + (i / 2 + 1) + " voice " + (i % 2 + 1));
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
                source.dopplerLevel = 0; source.priority = 96; source.volume = 0;
                _voices[i] = source; _decks[i / 2].Voices[i % 2] = new Voice { Source = source };
                _c.RegisterAudioSource(source, FishingAudioCategory.Music, 0);
            }
            Ready = LoadManifest();
            if (Ready && isActiveAndEnabled && _c.isActiveAndEnabled) BeginSession();
        }

        private bool LoadManifest()
        {
            var text = Resources.Load<TextAsset>("FishingAudio/Music/Manifest");
            if (text == null) { Debug.LogWarning("[REGIONAL MUSIC] Music manifest is missing; existing music was left alone."); return false; }
            Manifest manifest;
            try { manifest = JsonUtility.FromJson<Manifest>(text.text); }
            catch (ArgumentException) { Debug.LogWarning("[REGIONAL MUSIC] Music manifest is invalid; existing music was left alone."); return false; }
            if (manifest?.tracks == null || manifest.tracks.Length != _tracks.Length) return InvalidManifest();
            foreach (var item in manifest.tracks)
            {
                if (item == null || item.zone < 0 || item.zone >= _tracks.Length || _tracks[item.zone] != null ||
                    item.key != Keys[item.zone] || !Finite(item.duration) || item.duration <= 0) return InvalidManifest();
                var info = TrackInfo.Load(item.zone, item.key, item.title, item.duration, item.loopStart, item.tailTrim, item.loopCrossfade);
                if (info == null) return InvalidManifest();
                _tracks[item.zone] = info;
            }
            return true;
        }
        private static bool InvalidManifest()
        { Debug.LogWarning("[REGIONAL MUSIC] All nine valid region tracks are required; existing music was left alone."); return false; }

        private void Update() => Step(Time.unscaledDeltaTime);

        /// <summary>Production clock boundary; DSP time, actual scene state and the same residence gate are always used.</summary>
        public void Step(float unscaledDeltaTime)
        {
            if (!_built || !Ready || _c == null) return;
            if (!isActiveAndEnabled || !_c.isActiveAndEnabled) { EndSession(); return; }
            if (!_running) BeginSession();
            double now = AudioSettings.dspTime;
            if (double.IsNaN(now) || double.IsInfinity(now)) return;
            MaintainLegacyLease(now);
            UpdateFade(now);
            int resolved = ResolveZone(_c);
            if (resolved == SilenceZone)
            {
                if (!_prologue)
                {
                    _prologue = true; _zoneInitialized = true; DesiredZone = SilenceZone; _gate.Reset(SilenceZone);
                    // Intro ownership can fade both in-flight decks together; ordinary region changes queue instead.
                    BeginFade(-1, now, .75f);
                }
            }
            else
            {
                if (_prologue) { _prologue = false; _zoneInitialized = false; }
                if (resolved == KeepZone) _gate.Tick(_gate.Current, 0);
                else if (!_zoneInitialized)
                {
                    _zoneInitialized = true; _gate.Reset(resolved); DesiredZone = resolved;
                }
                else if (_c.Paused && !_c.StartMenuOpen) _gate.Tick(_gate.Current, 0);
                else if (_gate.Tick(resolved, unscaledDeltaTime)) DesiredZone = _gate.Current;
            }
            if (!_fading && DesiredZone != CurrentZone) BeginRegion(DesiredZone, now);
            for (int i = 0; i < _decks.Length; i++) AdvanceDeck(_decks[i], now);
            float dt = Finite(unscaledDeltaTime) && unscaledDeltaTime > 0 ? Mathf.Min(unscaledDeltaTime, .1f) : 0;
            float targetDuck = _c.DialogueOpen ? .55f : _c.Anchored || _c.HasPendingCatch ? .70f : 1;
            _duck = Mathf.MoveTowards(_duck, targetDuck, dt / .6f);
            ApplyGains(now);
        }

        /// <summary>KeepZone freezes selection during a portal's fade/relocation. SilenceZone grants intro ownership.</summary>
        public static int ResolveZone(FishingPrototype c)
        {
            if (c == null) return SilenceZone;
            if (c.Prologue != null && c.Prologue.Open) return SilenceZone;
            if (c.StartMenuOpen) return 2;
            if (c.Realms != null && c.Realms.Transitioning) return KeepZone;
            if (c.Realms != null && c.Realms.Inside) return 5 + Mathf.Clamp(c.Realms.Island, 0, 3);
            if (c.Home != null && c.Home.Inside) return 2;
            if (c.Farm != null && c.Farm.Nearby) return 2;
            if (c.Ashore && c.Home != null && FishingPrototype.FlatDistance(c.ExplorerPosition, c.Home.Origin) < 55) return 2;
            if ((c.TackleShop != null && (c.TackleShop.Inside || c.ShopOpen)) ||
                (c.Residents != null && (c.Residents.ActiveId == 4 || c.Residents.ActiveId == 5))) return 3;
            if (c.Anchored || (!c.Ashore && c.HasPendingCatch)) return 1;
            if (c.AtHarbor) return 3;
            float harborRadius = c.RegionalMusic != null && c.RegionalMusic.CurrentZone == 3 ? 44 : 34;
            if (FishingPrototype.FlatDistance(c.ExplorerPosition, c.harbor) < harborRadius) return 3;
            return c.Ashore ? 4 : 0;
        }

        private void BeginRegion(int zone, double now)
        {
            if (zone < 0)
            {
                if (_currentDeck >= 0 || _decks[0].Active || _decks[1].Active) BeginFade(-1, now, .75f);
                return;
            }
            var track = Track(zone); if (track == null) return;
            int incoming = _currentDeck == 0 ? 1 : 0;
            StartDeck(_decks[incoming], track, now + ScheduleLead);
            BeginFade(incoming, now + ScheduleLead, RegionFadeSeconds);
        }
        private void BeginFade(int targetDeck, double start, float seconds)
        {
            _fadeFrom = _deckGains; _fadeTarget = targetDeck; _fadeStart = start;
            _fadeDuration = seconds; _fading = true;
        }
        private void UpdateFade(double now)
        {
            if (!_fading) return;
            float phase = Mathf.Clamp01((float)((now - _fadeStart) / _fadeDuration));
            _deckGains = FishingMusicMath.Blend(_fadeFrom, _fadeTarget, phase);
            if (phase < 1) return;
            int target = _fadeTarget;
            for (int i = 0; i < _decks.Length; i++) if (i != target) StopDeck(_decks[i]);
            _currentDeck = target; _fading = false; _fadeTarget = -1;
        }

        private void StartDeck(Deck deck, TrackInfo track, double start)
        {
            StopDeck(deck); deck.Track = track;
            Schedule(deck.Voices[0], track, start, true);
            Schedule(deck.Voices[1], track, deck.Voices[0].End - track.LoopCrossfade, false);
            deck.LastEnd = deck.Voices[1].End; deck.Recycle = 0;
        }
        private void Schedule(Voice voice, TrackInfo track, double start, bool first)
        {
            var source = voice.Source;
            _c.SetAudioBaseVolume(source, 0); source.Stop();
            source.clip = track.Clip; source.loop = false; source.pitch = 1;
            float offset = first ? 0 : track.LoopStart;
            source.timeSamples = Mathf.Clamp(Mathf.RoundToInt(offset * track.Clip.frequency), 0, track.Clip.samples - 1);
            voice.Start = start; voice.End = start + (first ? track.ClipEnd : track.Window.End);
            voice.First = first; voice.Scheduled = true;
            source.PlayScheduled(voice.Start); source.SetScheduledEndTime(voice.End);
        }
        private void AdvanceDeck(Deck deck, double now)
        {
            if (!deck.Active) return;
            // The just-finished source has a positive silent hold before its next scheduled use.
            // Schedule both on recovery from a long application stall, never issue a PlayScheduled in the past.
            for (int i = 0; i < 2; i++)
            {
                var voice = deck.Voices[deck.Recycle];
                if (!voice.Scheduled || now < voice.End + .01) break;
                double start = Math.Max(deck.LastEnd - deck.Track.LoopCrossfade, now + ScheduleLead);
                Schedule(voice, deck.Track, start, false);
                deck.LastEnd = voice.End; deck.Recycle = 1 - deck.Recycle;
            }
        }
        private static float LoopGain(Voice voice, TrackInfo track, double now)
        {
            if (voice == null || track == null || !voice.Scheduled || now < voice.Start || now >= voice.End) return 0;
            float fade = track.LoopCrossfade;
            float incoming = voice.First ? 1 : FishingMusicMath.EqualPowerIn((float)((now - voice.Start) / fade));
            float outgoing = FishingMusicMath.EqualPowerOut((float)((now - (voice.End - fade)) / fade));
            return incoming * outgoing;
        }
        private void ApplyGains(double now)
        {
            for (int deck = 0; deck < _decks.Length; deck++)
                for (int i = 0; i < 2; i++)
                {
                    var slot = _decks[deck].Voices[i];
                    float gain = MusicBase * _duck * _deckGains.At(deck) * LoopGain(slot, _decks[deck].Track, now);
                    _c.SetAudioBaseVolume(slot.Source, gain);
                }
        }
        private void StopDeck(Deck deck)
        {
            foreach (var voice in deck.Voices)
            {
                if (voice == null) continue;
                if (_c != null) _c.SetAudioBaseVolume(voice.Source, 0);
                if (voice.Source != null) voice.Source.Stop();
                voice.Scheduled = false; voice.Start = voice.End = 0;
            }
            deck.Track = null; deck.LastEnd = 0; deck.Recycle = 0;
        }

        private void BeginSession()
        {
            _running = true; _zoneInitialized = _prologue = _fading = false;
            _currentDeck = _fadeTarget = -1; _deckGains = _fadeFrom = default;
            DesiredZone = SilenceZone; _gate.Reset(SilenceZone); _duck = 1; _legacyCheck = 0;
            MaintainLegacyLease(AudioSettings.dspTime);
        }
        private void EndSession()
        {
            if (!_built) return;
            foreach (var deck in _decks) StopDeck(deck);
            RestoreLegacyMusic();
            _running = _fading = _zoneInitialized = false; _currentDeck = _fadeTarget = -1;
            _deckGains = _fadeFrom = default; DesiredZone = SilenceZone;
        }
        private void OnEnable()
        { if (_built && Ready && _c != null && _c.isActiveAndEnabled) BeginSession(); }
        private void OnDisable() => EndSession();
        private void OnDestroy() => EndSession();

        private void MaintainLegacyLease(double now)
        {
            if (now >= _legacyCheck)
            {
                _legacyCheck = now + .5;
                var settings = AppSettings.Instance;
                if (settings != null)
                {
                    var prefab = Resources.Load<GameObject>("AppSettings");
                    var template = prefab == null ? null : prefab.GetComponent<AudioSource>();
                    var expected = template == null ? null : template.clip;
                    foreach (var source in settings.GetComponents<AudioSource>())
                    {
                        if (source.gameObject != settings.gameObject || source.clip == null ||
                            (source.clip != expected && !string.Equals(source.clip.name, "MainTrack", StringComparison.Ordinal))) continue;
                        bool known = false;
                        foreach (var lease in _legacy) if (lease.Source == source) { known = true; break; }
                        if (!known) _legacy.Add(new LegacyLease { Source = source, Clip = source.clip });
                    }
                }
            }
            foreach (var lease in _legacy)
            {
                // Never affect a changed clip, another manager, its children or any other persistent audio.
                if (lease.Source == null || lease.Source.clip != lease.Clip || !lease.Source.isPlaying) continue;
                lease.PausedByUs = true; lease.Source.Pause();
            }
        }
        private void RestoreLegacyMusic()
        {
            foreach (var lease in _legacy)
                if (lease.PausedByUs && lease.Source != null && lease.Source.clip == lease.Clip) lease.Source.UnPause();
            _legacy.Clear();
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
