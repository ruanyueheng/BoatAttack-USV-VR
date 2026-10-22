using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>Real scene/DSP checks; portal approaches are isolated placements, not another route or puzzle acceptance suite.</summary>
    public static class FishingRegionalMusicValidation
    {
        public static void Run() => FishingV011Validation.Begin("REGIONAL-MUSIC");
        private static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            Debug.Log("[REGIONAL-MUSIC] CHECK: " + label);
        }

        public static IEnumerator Checks(FishingPrototype c)
        {
            string temporary = Path.GetFullPath(Application.temporaryCachePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            Check(c != null && c.Automated && c.HasIsolatedProfile
                && Path.GetFullPath(c.SavePath).StartsWith(temporary, StringComparison.OrdinalIgnoreCase),
                "Regional music checks use only the actual scene's temporary isolated profile");
            var original = c.Career.Snapshot();
            var music = c.RegionalMusic;
            Check(music != null && music.Ready, "The production scene initializes all nine regional music resources");
            float master = c.MasterVolume, volume = c.MusicVolume;
            bool quiet = c.QuietMode, enabled = music.enabled, firstPerson = c.FirstPerson, paused = c.Paused;
            int navigation = c.NavigationTarget;
            string productionPath = Path.Combine(Application.persistentDataPath, "boatattack-fishing-v13.json");
            byte[] production = Read(productionPath), backup = Read(productionPath + ".bak");
            try
            {
                ClosePanels(c);
                if (c.QuietMode) c.ToggleQuiet();
                c.SetSetting("Master", .2f); c.SetSetting("Music", 1);
                c.ReturnToHarbor();
                var fixture = new FishingCareer().Snapshot(); fixture.calendar.timeFixed = true; fixture.prologueSeen = true;
                c.RestoreShopTestProfile(fixture);
                ValidateTracks(music);
                Vector3 openSea = c.harbor + new Vector3(-75, .7f, -90);
                PlaceBoat(c, openSea);
                music.enabled = false; music.enabled = true; music.Step(0);
                Check(FishingRegionalMusic.ResolveZone(c) == 0 && !c.Ashore && !c.Anchored,
                    "An actual unanchored boat away from the harbor selects the sailing track");
                foreach (var frame in WaitZone(music, 0)) yield return frame;
                Check(AnyPlaying(music), "The regional sailing source really plays after its DSP fade-in");
                ValidateSettings(c, music);

                double[] sailingStarts = Starts(music);
                c.SelectNavigation(c.HomeNavigationIndex);
                Check(FishingRegionalMusic.ResolveZone(c) == 0,
                    "Selecting a distant navigation destination does not change the boat's actual music region");
                for (int edge = 0; edge < 8; edge++)
                {
                    PlaceBoat(c, edge % 2 == 0 ? c.harbor + Vector3.up * .7f : openSea);
                    foreach (var frame in WaitDsp(.12)) yield return frame;
                    Check(music.CurrentZone == 0 && music.DesiredZone == 0 && !music.Transitioning,
                        "Short real-position border visits do not commit a harbor candidate: " + edge);
                }
                PlaceBoat(c, openSea);
                Check(SameStarts(music, sailingStarts), "Boundary jitter does not restart the already playing sailing voices");

                PlaceBoat(c, c.harbor + Vector3.up * .7f);
                Check(c.AtHarbor && FishingRegionalMusic.ResolveZone(c) == 3,
                    "The actual low-speed boat at the harbor resolves the harbor/shop region");
                double deadline = EditorApplication.timeSinceStartup + 8;
                while (!music.Transitioning && EditorApplication.timeSinceStartup < deadline) yield return null;
                Check(music.Transitioning && music.DesiredZone == 3 && music.CurrentZone == 0,
                    "A continuously held harbor candidate starts a transition while retaining the audible source deck");
                double fadeBegan = AudioSettings.dspTime;
                var oldClip = music.Track(0).Clip; var newClip = music.Track(3).Clip;
                bool fadingOut = false, silentBoundary = false, fadingIn = false, noOverlap = true, stoppedBeforeNew = true;
                deadline = EditorApplication.timeSinceStartup + 16;
                while ((music.CurrentZone != 3 || music.Transitioning) && EditorApplication.timeSinceStartup < deadline)
                {
                    music.Step(0);
                    double oldPower = ClipPower(music, oldClip), newPower = ClipPower(music, newClip);
                    noOverlap &= !(oldPower > .000001 && newPower > .000001);
                    fadingOut |= oldPower > .02 && oldPower < .98 && newPower == 0;
                    if (Power(music) == 0 && !AnyPlaying(music)) silentBoundary = true;
                    if (newPower > 0)
                    {
                        for (int i = 0; i < 4; i++)
                            if (music.Voices[i].clip == oldClip) stoppedBeforeNew &= !music.Voices[i].isPlaying && double.IsNaN(music.ScheduledStart(i));
                        fadingIn |= newPower > .02 && newPower < .98 && oldPower == 0;
                    }
                    yield return null;
                }
                Check(music.CurrentZone == 3 && !music.Transitioning && music.DesiredZone == 3,
                    "The sequential transition settles the latest confirmed region");
                Check(fadingOut && silentBoundary && fadingIn && noOverlap && stoppedBeforeNew,
                    "Old music fades out, all old voices stop at zero, then new music fades in with no cross-track overlap");
                Check(AudioSettings.dspTime - fadeBegan >= 3.7,
                    "A regional transition includes both two-second fades, not simultaneous crossfading");

                var unchanged = Starts(music);
                c.TogglePause();
                foreach (var frame in WaitDsp(.3)) yield return frame;
                Check(c.Paused && music.CurrentZone == 3 && !music.Transitioning && SameStarts(music, unchanged) && AnyPlaying(music),
                    "Pausing in the same region preserves scheduled music playback without restarting it");
                c.TogglePause(); yield return null;
                c.ToggleInventory();
                foreach (var frame in WaitDsp(.3)) yield return frame;
                Check(c.InventoryOpen && music.CurrentZone == 3 && SameStarts(music, unchanged) && AnyPlaying(music),
                    "Opening the backpack preserves the current music source and its DSP schedule");
                c.ToggleInventory(); yield return null;

                // A third region confirmed during fade-out replaces the queued destination before any new track starts.
                PlaceBoat(c, openSea);
                deadline = EditorApplication.timeSinceStartup + 8;
                while (!music.Transitioning && EditorApplication.timeSinceStartup < deadline) yield return null;
                Check(music.Transitioning && music.CurrentZone == 3 && music.DesiredZone == 0,
                    "Leaving harbor starts only the outgoing harbor fade while sailing is queued");
                c.MoveToSpotForAutomation(0); c.Interact();
                Check(c.Anchored && FishingRegionalMusic.ResolveZone(c) == 1,
                    "A real fishing arrival replaces the intermediate sailing region during fade-out");
                bool intermediateScheduled = false, rapidOverlap = false;
                deadline = EditorApplication.timeSinceStartup + 16;
                while ((music.CurrentZone != 1 || music.Transitioning) && EditorApplication.timeSinceStartup < deadline)
                {
                    for (int i = 0; i < 4; i++)
                        if (music.Voices[i].clip == music.Track(0).Clip && !double.IsNaN(music.ScheduledStart(i))) intermediateScheduled = true;
                    rapidOverlap |= ClipPower(music, music.Track(3).Clip) > .000001 && ClipPower(music, music.Track(1).Clip) > .000001;
                    yield return null;
                }
                Check(music.CurrentZone == 1 && music.DesiredZone == 1 && !music.Transitioning
                    && !intermediateScheduled && !rapidOverlap,
                    "Rapid harbor-to-sailing-to-fishing arrival skips sailing entirely and never overlaps the old and latest tracks");

                c.ReturnToHarbor(); PlaceBoat(c, c.Home.Berth + Vector3.up * .7f);
                foreach (var frame in WaitDsp(.35)) yield return frame;
                Check(c.Shore.TryDisembark() && c.Shore.Relocate(c.Home.Origin + new Vector3(0, .2f, -3.5f), Quaternion.identity),
                    "A real home landing and supported indoor fixture place the character in the cottage");
                yield return null;
                Check(c.Home.Inside && FishingRegionalMusic.ResolveZone(c) == 2, "The cottage's actual Inside state selects the home/garden track");
                foreach (var frame in WaitZone(music, 2)) yield return frame;
                unchanged = Starts(music);
                Vector3 garden = c.Farm.Target(0).position + new Vector3(-.25f, 0, -1.1f);
                Check(c.Shore.Ground(garden, out var gardenGround) && c.Shore.Relocate(gardenGround, Quaternion.identity),
                    "The actual garden plot apron has safe character support");
                foreach (var frame in WaitDsp(.25)) yield return frame;
                Check(c.Farm.Nearby && !c.Home.Inside && FishingRegionalMusic.ResolveZone(c) == 2
                    && music.CurrentZone == 2 && SameStarts(music, unchanged),
                    "Cottage and nearby garden share one score without restarting music between their actual positions");

                c.ReturnToHarbor(); yield return null;
                c.MoveToSpotForAutomation(0); c.Interact();
                Check(c.Anchored && FishingRegionalMusic.ResolveZone(c) == 1,
                    "The production fishing-station interaction anchors the real boat and selects fishing music");
                foreach (var frame in WaitZone(music, 1)) yield return frame;

                c.ReturnToHarbor(); PlaceBoat(c, c.TackleShop.mooringPoint.position + Vector3.up * .7f);
                foreach (var frame in WaitDsp(.35)) yield return frame;
                Check(c.Shore.TryDisembark(), "The real shop mooring supports its actual shop landing");
                Check(c.Shore.Ground(c.TackleShop.purchasePoint.position, out var counterGround)
                    && c.Shore.Relocate(counterGround, c.TackleShop.transform.rotation),
                    "The shop customer point has actual ground support and capsule clearance");
                yield return null;
                Check(c.TackleShop.Inside && FishingRegionalMusic.ResolveZone(c) == 3,
                    "The physical shop interior shares the harbor score before a trading panel is opened");
                foreach (var frame in WaitZone(music, 3)) yield return frame;

                for (int island = 0; island < 4; island++)
                {
                    c.ReturnToHarbor(); yield return null;
                    PlaceBoat(c, FishingIslandWorld.Berth(island) + Vector3.up * .7f);
                    foreach (var frame in WaitDsp(.35)) yield return frame;
                    Check(c.Shore.TryDisembark(), "Real island landing for music-region checks: " + island);
                    yield return null;
                    Check(c.Ashore && FishingRegionalMusic.ResolveZone(c) == 4,
                        "Actual exterior island exploration uses the shared shore track: " + island);
                    if (island == 0) foreach (var frame in WaitZone(music, 4)) yield return frame;
                    Check(c.Shore.Relocate(c.Realms.PortalApproach(island), Quaternion.identity),
                        "Isolated character placement uses the actual supported portal approach: " + island);
                    yield return null; yield return null;
                    Check(c.Realms.TryTarget(out int target, out _) && target == island + 8 && c.Realms.Enter(island),
                        "Production portal range and entry begin the corresponding sanctuary: " + island);
                    Check(FishingRegionalMusic.ResolveZone(c) == FishingRegionalMusic.KeepZone,
                        "Portal travel keeps its current selection during the real fade and relocation: " + island);
                    deadline = EditorApplication.timeSinceStartup + 6;
                    while (c.Realms.Transitioning && EditorApplication.timeSinceStartup < deadline) yield return null;
                    Check(!c.Realms.Transitioning && c.Realms.Inside && c.Realms.Island == island
                        && FishingRegionalMusic.ResolveZone(c) == island + 5,
                        "The entered sanctuary state selects its distinct track: " + FishingLore.ShrineName(island));
                    foreach (var frame in WaitZone(music, island + 5)) yield return frame;
                    if (island == 0)
                    {
                        foreach (var frame in ObserveFirstRealmLoop(music, 5)) yield return frame;
                        Check(c.Prologue.Begin() && FishingRegionalMusic.ResolveZone(c) == FishingRegionalMusic.SilenceZone,
                            "The real prologue takes priority over an already playing sanctuary score");
                        double introFadeStart = AudioSettings.dspTime;
                        music.Step(0);
                        Check(music.Transitioning && Power(music) > .8,
                            "Prologue ownership starts from the captured audible gain instead of abruptly stopping its deck");
                        foreach (var frame in WaitDsp(.25)) yield return frame;
                        double sampledPower = Power(music), introElapsed = AudioSettings.dspTime - introFadeStart;
                        // EditorApplication.update can resume this coroutine before MonoBehaviour.Update.
                        // Read a freshly evaluated production envelope, not the previous frame's cached gain.
                        music.Step(0);
                        double expectedIntroPower = Math.Pow(FishingMusicMath.EqualPowerOut((float)(introElapsed / .75)), 2);
                        Check(Math.Abs(Power(music) - expectedIntroPower) < .08,
                            "The prologue handover follows its real DSP fade envelope; elapsed=" + introElapsed.ToString("0.000")
                            + ", pre-step power=" + sampledPower.ToString("0.000") + ", refreshed power=" + Power(music).ToString("0.000"));
                        foreach (var frame in WaitDsp(.65)) yield return frame;
                        Check(music.CurrentZone == FishingRegionalMusic.SilenceZone && Power(music) < .0001 && !AnyPlaying(music),
                            "Prologue takeover finishes with all four regional voices silent");
                        c.Prologue.Suspend(); yield return null;
                        foreach (var frame in WaitZone(music, 5)) yield return frame;
                    }
                }

                foreach (var frame in ValidateLegacyLease(c, music)) yield return frame;
                Debug.Log("[REGIONAL-MUSIC] SCOPE: Nine actual resolver mappings and scheduled tracks checked; wind sanctuary played through its first DSP loop overlap. Portal aprons use safe isolated Relocate fixtures; no complete island walking route or puzzle solution is claimed. Streaming clips were never read with GetData.");
            }
            finally
            {
                ClosePanels(c); c.ReturnToHarbor(); c.RestoreShopTestProfile(original);
                c.SetSetting("Master", master); c.SetSetting("Music", volume);
                if (c.QuietMode != quiet) c.ToggleQuiet();
                c.SelectNavigation(navigation);
                if (c.FirstPerson != firstPerson) c.ToggleView();
                music.enabled = enabled;
                if (paused && !c.Paused) c.TogglePause();
                Check(Same(production, Read(productionPath)) && Same(backup, Read(productionPath + ".bak")),
                    "Regional music validation leaves the player's persistent save and backup unchanged");
            }
        }

        private static void ValidateTracks(FishingRegionalMusic music)
        {
            string[] keys = { "Sailing", "Fishing", "Home", "HarborShop", "Exploration", "RealmWind", "RealmForest", "RealmWreck", "RealmStars" };
            var clips = new HashSet<AudioClip>();
            for (int zone = 0; zone < keys.Length; zone++)
            {
                var track = music.Track(zone);
                Check(track != null && track.Zone == zone && track.Key == keys[zone] && track.Clip != null && clips.Add(track.Clip),
                    "Distinct shipped track matches its manifest zone and resource key: " + zone + " / " + keys[zone]);
                var clip = track.Clip;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
                Check(importer != null && importer.defaultSampleSettings.loadType == AudioClipLoadType.Streaming
                    && !importer.forceToMono && clip.loadType == AudioClipLoadType.Streaming && clip.channels == 2 && clip.frequency == 44100,
                    "Music remains a streaming stereo clip without decoding its samples: " + keys[zone]);
                Check(track.Duration > 20 && Math.Abs(track.Duration - track.DeclaredDuration) < .08f
                    && Math.Abs(track.Duration - (double)clip.samples / clip.frequency) < .03,
                    "Imported clip duration matches the shipped manifest and sample-count metadata: " + keys[zone]);
                Check(FishingMusicMath.TryLoopWindow(track.Duration - track.LoopStart, track.TailTrim, track.LoopCrossfade, out var window)
                    && window.StartLead > window.Crossfade && window.End + track.LoopStart <= track.Duration,
                    "The shipped loop start, tail trim and crossfade form a positive safe playback window: " + keys[zone]);
                if (zone >= 5) Check(track.LoopCrossfade >= 6 && track.TailTrim > .5f,
                    "Unfinished sanctuary endings use a long crossfade and deliberate tail trim: " + keys[zone]);
            }
            Check(music.Voices.Count == 4, "The production controller owns exactly four sources across its two looping decks");
            for (int i = 0; i < music.Voices.Count; i++)
                Check(music.Voices[i] != null && !music.Voices[i].loop && !music.Voices[i].playOnAwake && music.Voices[i].spatialBlend == 0,
                    "A regional voice uses explicit nonspatial DSP scheduling instead of AudioSource.loop: " + i);
        }

        private static void ValidateSettings(FishingPrototype c, FishingRegionalMusic music)
        {
            var starts = Starts(music);
            c.SetSetting("Music", 0); music.Step(0);
            bool silent = true;
            for (int i = 0; i < 4; i++) silent &= music.Voices[i].volume == 0;
            Check(silent && SameStarts(music, starts), "Music volume zero silences all four voices without changing a scheduled start");
            c.SetSetting("Music", .4f); music.Step(0);
            bool scaled = true, audible = false;
            for (int i = 0; i < 4; i++)
            {
                float expected = FishingRegionalMusic.MusicBase * music.DuckGain * music.ChannelGain(i) * .4f;
                scaled &= Math.Abs(music.Voices[i].volume - expected) < .002f;
                audible |= music.Voices[i].volume > .001f;
            }
            Check(scaled && audible, "Each source's actual amplitude obeys the shared Music slider and runtime gain");
            c.ToggleQuiet(); music.Step(0);
            bool muted = true;
            for (int i = 0; i < 4; i++) muted &= music.Voices[i].mute;
            Check(muted && AudioListener.volume == 0 && SameStarts(music, starts), "Quiet mode mutes the real music voices without restarting playback");
            c.ToggleQuiet(); c.SetSetting("Music", 1); music.Step(0);
        }

        private static IEnumerable ObserveFirstRealmLoop(FishingRegionalMusic music, int zone)
        {
            var track = music.Track(zone); var channels = new List<int>();
            for (int i = 0; i < 4; i++) if (music.Voices[i].clip == track.Clip && Finite(music.ScheduledStart(i))) channels.Add(i);
            Check(channels.Count == 2, "The active sanctuary deck has both same-track loop voices scheduled ahead of its first tail");
            int first = music.ScheduledStart(channels[0]) < music.ScheduledStart(channels[1]) ? channels[0] : channels[1];
            int next = first == channels[0] ? channels[1] : channels[0];
            double firstStart = music.ScheduledStart(first), firstEnd = music.ScheduledEnd(first);
            double nextStart = music.ScheduledStart(next), nextEnd = music.ScheduledEnd(next);
            Check(nextStart > AudioSettings.dspTime + 1 && Math.Abs(firstEnd - nextStart - track.LoopCrossfade) < .03
                && Math.Abs(firstEnd - firstStart - (track.Duration - track.TailTrim)) < .03
                && Math.Abs(nextEnd - nextStart - (track.Duration - track.LoopStart - track.TailTrim)) < .03,
                "DSP scheduling overlaps the first sanctuary tail by its configured fade and skips only the declared loop intro on later voices");
            double deadline = EditorApplication.timeSinceStartup + Math.Max(10, firstEnd - AudioSettings.dspTime + 8);
            int samples = 0; bool gains = true, playing = true;
            while (AudioSettings.dspTime < firstEnd + .4 && EditorApplication.timeSinceStartup < deadline)
            {
                double now = AudioSettings.dspTime;
                if (now > nextStart + .3 && now < firstEnd - .3)
                {
                    float a = music.ChannelGain(first), b = music.ChannelGain(next);
                    gains &= a >= 0 && a <= 1.0001f && b >= 0 && b <= 1.0001f && Math.Abs(Power(music) - 1) < .06;
                    playing &= music.Voices[first].isPlaying && music.Voices[next].isPlaying;
                    samples++;
                }
                yield return null;
            }
            Check(AudioSettings.dspTime >= firstEnd + .35 && samples >= 10 && gains && playing,
                "Real sanctuary playback reaches its first tail with two playing voices and bounded squared gain sum near one; overlap samples=" + samples);
            Check(music.CurrentZone == zone && !music.Transitioning && music.ScheduledStart(first) > firstEnd
                && music.ScheduledStart(first) > AudioSettings.dspTime
                && Math.Abs(music.ScheduledStart(first) - (nextEnd - track.LoopCrossfade)) < .04,
                "After the first tail, the retired voice is already scheduled for the next future loop without restarting the active voice");
        }

        private static IEnumerable ValidateLegacyLease(FishingPrototype c, FishingRegionalMusic music)
        {
            var settings = AppSettings.Instance;
            if (settings == null)
            {
                Debug.Log("[REGIONAL-MUSIC] SCOPE: No AppSettings manager exists in this isolated run; legacy-manager lease playback could not be exercised.");
                yield break;
            }
            var prefab = Resources.Load<GameObject>("AppSettings");
            var template = prefab == null ? null : prefab.GetComponent<AudioSource>();
            Check(template != null && template.clip != null, "The old manager's exact MainTrack clip is available for a scoped lease fixture");
            AudioSource legacy = null, unrelated = null; GameObject child = null; AudioClip probe = null;
            bool wasEnabled = music.enabled;
            try
            {
                music.enabled = false; yield return null;
                legacy = settings.gameObject.AddComponent<AudioSource>(); legacy.playOnAwake = false; legacy.clip = template.clip;
                legacy.loop = true; legacy.volume = 0; legacy.Play();
                probe = AudioClip.Create("Regional music unrelated silent lease probe", 22050, 1, 22050, false);
                unrelated = settings.gameObject.AddComponent<AudioSource>(); unrelated.playOnAwake = false; unrelated.clip = probe;
                unrelated.loop = true; unrelated.volume = .013f; unrelated.pitch = .87f; unrelated.Play();
                child = new GameObject("Regional music excluded manager child probe"); child.transform.SetParent(settings.transform, false);
                var childSource = child.AddComponent<AudioSource>(); childSource.playOnAwake = false; childSource.clip = template.clip;
                childSource.loop = true; childSource.volume = 0; childSource.Play();
                foreach (var frame in WaitDsp(.12)) yield return frame;
                Check(legacy.isPlaying && unrelated.isPlaying && childSource.isPlaying, "All scoped legacy and unrelated probe sources begin real playback");
                music.enabled = true; yield return null;
                Check(music.BorrowingLegacyMusic && !legacy.isPlaying && unrelated.isPlaying && childSource.isPlaying
                    && legacy.clip == template.clip && legacy.loop && legacy.volume == 0
                    && unrelated.volume == .013f && unrelated.pitch == .87f,
                    "Only the exact MainTrack source on AppSettings itself is paused; a different clip and manager-child source remain untouched");
                int heldSample = legacy.timeSamples;
                music.enabled = false; yield return null;
                Check(legacy.isPlaying && legacy.timeSamples >= heldSample && unrelated.isPlaying && childSource.isPlaying
                    && unrelated.clip == probe && unrelated.volume == .013f && unrelated.pitch == .87f,
                    "Disabling regional music resumes the leased MainTrack in place without stopping or rewriting unrelated audio");
            }
            finally
            {
                music.enabled = false;
                if (legacy != null) { legacy.Stop(); Object.DestroyImmediate(legacy); }
                if (unrelated != null) { unrelated.Stop(); Object.DestroyImmediate(unrelated); }
                if (child != null) Object.DestroyImmediate(child);
                if (probe != null) Object.DestroyImmediate(probe);
                music.enabled = wasEnabled;
            }
        }

        private static IEnumerable WaitZone(FishingRegionalMusic music, int zone)
        {
            double deadline = EditorApplication.timeSinceStartup + 16;
            while ((music.CurrentZone != zone || music.Transitioning) && EditorApplication.timeSinceStartup < deadline) yield return null;
            Check(music.CurrentZone == zone && !music.Transitioning && music.DesiredZone == zone,
                "The production controller settles the actual region's audible deck: " + zone);
        }
        private static IEnumerable WaitDsp(double seconds)
        {
            double until = AudioSettings.dspTime + seconds, deadline = EditorApplication.timeSinceStartup + seconds * 3 + 5;
            while (AudioSettings.dspTime < until && EditorApplication.timeSinceStartup < deadline) yield return null;
            Check(AudioSettings.dspTime >= until, "The real audio DSP clock advances for " + seconds.ToString("0.00") + " seconds");
        }
        private static void ClosePanels(FishingPrototype c)
        {
            c.Prologue?.Suspend(); c.HandGames?.Cancel(); c.Letter?.Close(); c.Residents?.Close(); c.TackleShop?.Close();
            c.Home?.CancelSleep(); c.CloseHomeChart(); c.CloseSaveMenu();
            if (c.StartMenuOpen) c.FinishTitleVoyage();
            if (c.InventoryOpen) c.ToggleInventory();
            if (c.JournalOpen) c.ToggleJournal();
            if (c.Paused) c.TogglePause();
            c.CancelWorldAction();
        }
        private static void PlaceBoat(FishingPrototype c, Vector3 point)
        {
            var rb = c.Player.engine.RB; rb.velocity = rb.angularVelocity = Vector3.zero; rb.position = point; rb.rotation = Quaternion.identity;
            c.Player.transform.SetPositionAndRotation(point, Quaternion.identity); Physics.SyncTransforms();
        }
        private static double Power(FishingRegionalMusic music)
        { double total = 0; for (int i = 0; i < 4; i++) { double gain = music.ChannelGain(i); total += gain * gain; } return total; }
        private static double ClipPower(FishingRegionalMusic music, AudioClip clip)
        { double total = 0; for (int i = 0; i < 4; i++) if (music.Voices[i].clip == clip) { double gain = music.ChannelGain(i); total += gain * gain; } return total; }
        private static bool AnyPlaying(FishingRegionalMusic music)
        { for (int i = 0; i < 4; i++) if (music.Voices[i].isPlaying && music.ChannelGain(i) > .01f) return true; return false; }
        private static double[] Starts(FishingRegionalMusic music)
        { var starts = new double[4]; for (int i = 0; i < 4; i++) starts[i] = music.ScheduledStart(i); return starts; }
        private static bool SameStarts(FishingRegionalMusic music, double[] starts)
        {
            for (int i = 0; i < 4; i++)
            {
                double current = music.ScheduledStart(i);
                if (double.IsNaN(starts[i]) || double.IsNaN(current))
                { if (!(double.IsNaN(starts[i]) && double.IsNaN(current))) return false; }
                else if (Math.Abs(starts[i] - current) > .0001) return false;
            }
            return true;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static byte[] Read(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        private static bool Same(byte[] a, byte[] b)
        { if (a == null || b == null) return a == null && b == null; if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }
}
