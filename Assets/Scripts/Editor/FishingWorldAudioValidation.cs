using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using WaterSystem;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>Explicit real-scene acceptance. Never saves the source scene or changes production audio guards.</summary>
    public static class FishingWorldAudioValidation
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] Keys = {
            "ImpactWood", "ImpactStone", "ImpactMetal", "BodyLand", "WaterEntry", "WaterStroke", "ClimbGrip",
            "FishBite", "FishHook", "LineSnap", "FishSplash", "FishStore", "AnimalHit", "LootBag", "CanHandle",
            "WellBucket", "ForgeStrike", "WorkbenchAssemble", "CraftFinish", "StorageMove", "LanternSwitch",
            "FruitShake", "UIConfirm", "UIBack", "UIReject", "TVSwitch", "CushionCloth", "PortalPass",
            "MechanismClick", "HandcraftRub", "SeagullCall", "AnimalRustle", "BoatSlap", "WaterFinish",
            "BoatScrapeLoop", "FurnaceLoop", "CookSizzleLoop", "RainLoop", "ForestLoop", "WreckLoop",
            "WindCorridorLoop", "StarsLoop", "WellRopeLoop", "WaterPourLoop", "TVLoop"
        };
        public static void Run() => FishingV011Validation.Begin("WORLD-AUDIO");
        private static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            Debug.Log("[WORLD-AUDIO] CHECK: " + label);
        }
        private static object Field(object owner, string name)
        {
            var field = owner.GetType().GetField(name, Fields);
            if (field == null) throw new InvalidOperationException("Missing inspection field: " + name);
            return field.GetValue(owner);
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;

        public static IEnumerator Checks(FishingPrototype c)
        {
            string temporary = Path.GetFullPath(Application.temporaryCachePath).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            Check(c != null && c.Automated && c.HasIsolatedProfile
                && Path.GetFullPath(c.SavePath).StartsWith(temporary, StringComparison.OrdinalIgnoreCase),
                "World audio checks use the real scene and only its temporary isolated career");
            var original = c.Career.Snapshot();
            float master = c.MasterVolume, music = c.MusicVolume, effects = c.EffectsVolume, ambient = c.AmbientVolume;
            bool quiet = c.QuietMode, paused = c.Paused, firstPerson = c.FirstPerson;
            string playerPath = Path.Combine(Application.persistentDataPath, "boatattack-fishing-v13.json");
            byte[] player = Read(playerPath), backup = Read(playerPath + ".bak");
            var prefs = CapturePreferences();
            try
            {
                ClosePanels(c); c.ReturnToHarbor();
                var fixture = new FishingCareer().Snapshot(); fixture.prologueSeen = true; fixture.calendar.timeFixed = true;
                c.RestoreShopTestProfile(fixture);
                if (c.QuietMode) c.ToggleQuiet();
                c.SetSetting("Master", .3f); c.SetSetting("Music", .6f); c.SetSetting("Effects", .7f); c.SetSetting("Ambient", .5f);
                yield return null; yield return null;
                Check(c.WorldAudio != null && c.WorldAudio.enabled && c.RegionalMusic != null && c.RegionalMusic.Ready,
                    "Actual scene initializes the world-audio observer beside the ready regional music director");
                ValidateResources();
                ValidateWaterSamplerLifetime();
                foreach (var step in ValidateRequests(c)) yield return step;
                foreach (var step in ValidateStorageAndCrafting(c)) yield return step;
                foreach (var step in ValidateEnvironment(c)) yield return step;
                foreach (var step in ValidateFarmWater(c)) yield return step;
                Debug.Log("[WORLD-AUDIO] SCOPE: Real decoded clips, bounded scene sources, successful paused-panel transactions, actual can/refill/pour animations, and indoor/quiet routing were checked. No physical speaker/headset listening, every collision geometry, complete animal route, or subjective loudness approval is claimed.");
            }
            finally
            {
                ClosePanels(c); c.ReturnToHarbor(); c.RestoreShopTestProfile(original);
                if (c.WorldAudio != null) { c.WorldAudio.enabled = false; c.WorldAudio.enabled = true; }
                c.SetSetting("Master", master); c.SetSetting("Music", music); c.SetSetting("Effects", effects); c.SetSetting("Ambient", ambient);
                if (c.QuietMode != quiet) c.ToggleQuiet();
                if (c.FirstPerson != firstPerson) c.ToggleView();
                if (paused && !c.Paused) c.TogglePause();
                Check(PreferencesUnchanged(prefs) && Same(player, Read(playerPath)) && Same(backup, Read(playerPath + ".bak")),
                    "Persistent preferences, player save and backup remain byte-for-byte unchanged after cleanup");
            }
        }

        private static void ValidateResources()
        {
            var distinct = new HashSet<AudioClip>();
            foreach (string key in Keys)
            {
                var clip = FishingWorldAudio.Clip(key);
                Check(clip != null && distinct.Add(clip) && clip.channels == 1 && clip.samples > 256
                    && clip.length >= .1f && clip.length <= 40 && clip.frequency >= 22050,
                    "Distinct real mono resource exists with a bounded duration: " + key);
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
                Check(importer != null && importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad
                    && clip.loadType == AudioClipLoadType.DecompressOnLoad,
                    "Short world recording is decoded for contact playback rather than a music stream: " + key);
                var samples = new float[clip.samples * clip.channels];
                Check(clip.GetData(samples, 0), "Unity decodes the actual shipped samples: " + key);
                double energy = 0; float peak = 0;
                foreach (float sample in samples)
                {
                    if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new InvalidDataException("Nonfinite audio sample: " + key);
                    energy += (double)sample * sample; peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                Check(peak > .001f && peak < .99f && Math.Sqrt(energy / samples.Length) > .0001,
                    "Decoded recording contains finite non-silent sound with headroom: " + key);
            }
            Check(distinct.Count == 45 && FishingWorldAudio.Clip("../Music/Home") == null && FishingWorldAudio.Clip(null) == null,
                "All 44 generated originals plus the edited water-finish clip exist; unknown paths cannot load arbitrary resources");
        }

        private sealed class WaterSampleSnapshot
        {
            public int2 Range;
            public float3[] Input, Positions, Normals;
        }
        private static Dictionary<int, int2> WaterRegistry => (Dictionary<int, int2>)typeof(GerstnerWavesJobs)
            .GetField("Registry", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static int WaterSampleCount => (int)typeof(GerstnerWavesJobs)
            .GetField("_positionCount", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static NativeArray<float3> WaterInput => (NativeArray<float3>)typeof(GerstnerWavesJobs)
            .GetField("_positions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static void ValidateWaterSamplerLifetime()
        {
            // A synchronous fixture only appends its own registrations; no frame, boat transform,
            // original sample coordinates or pre-existing registration is changed by the test.
            Check(GerstnerWavesJobs.Initialized && WaterInput.IsCreated, "The production water sampler is initialized before lifecycle validation");
            int before = WaterSampleCount;
            var originalRanges = new Dictionary<int, int2>(WaterRegistry);
            Check(originalRanges.Count > 0 && before + 6 < WaterInput.Length,
                "Real scene samplers already occupy valid ranges with room for six isolated temporary points");
            int first = int.MinValue + 1000000;
            while (WaterRegistry.ContainsKey(first) || WaterRegistry.ContainsKey(first + 1) || WaterRegistry.ContainsKey(first + 2)) first += 3;
            int middle = first + 1, last = first + 2;
            var a = new NativeArray<float3>(2, Allocator.TempJob);
            var b = new NativeArray<float3>(3, Allocator.TempJob);
            var tail = new NativeArray<float3>(1, Allocator.TempJob);
            Dictionary<int, WaterSampleSnapshot> original = null;
            try
            {
                a[0] = new float3(17, 0, 43); a[1] = new float3(24, 0, 48);
                b[0] = new float3(-21, 0, 71); b[1] = new float3(-28, 0, 79); b[2] = new float3(-33, 0, 87);
                tail[0] = new float3(93, 0, -41);
                GerstnerWavesJobs.UpdateSamplePoints(ref a, first);
                GerstnerWavesJobs.UpdateSamplePoints(ref b, middle);
                GerstnerWavesJobs.UpdateSamplePoints(ref tail, last);
                // The existing API reserves a new range on its first update and fills it on the next.
                GerstnerWavesJobs.UpdateSamplePoints(ref a, first);
                GerstnerWavesJobs.UpdateSamplePoints(ref b, middle);
                GerstnerWavesJobs.UpdateSamplePoints(ref tail, last);
                Check(WaterSampleCount == before + 6 && WaterRegistry.Count == originalRanges.Count + 3
                    && math.all(WaterRegistry[first] == new int2(before, before + 2))
                    && math.all(WaterRegistry[middle] == new int2(before + 2, before + 5))
                    && math.all(WaterRegistry[last] == new int2(before + 5, before + 6)),
                    "Three isolated registrations occupy successive distinct ranges after the actual existing samplers");
                GerstnerWavesJobs.UpdateHeights();
                // Public UpdateSamplePoints completes the scheduled height job before any result inspection.
                GerstnerWavesJobs.UpdateSamplePoints(ref a, first);
                original = CaptureWaterSamples(originalRanges);
                var aResult = CaptureWaterSample(first);
                var bResult = CaptureWaterSample(middle);
                var tailResult = CaptureWaterSample(last);
                Check(Equal(aResult.Input, new[] { a[0], a[1] }) && Equal(bResult.Input, new[] { b[0], b[1], b[2] })
                    && Equal(tailResult.Input, new[] { tail[0] }) && ValidWaterResult(aResult) && ValidWaterResult(bResult) && ValidWaterResult(tailResult),
                    "Public sampling updates store the supplied points and GetData returns finite completed height and normal results");
                GerstnerWavesJobs.RemoveSamplePoints(middle);
                Check(WaterSampleCount == before + 3 && !WaterRegistry.ContainsKey(middle)
                    && math.all(WaterRegistry[first] == aResult.Range)
                    && math.all(WaterRegistry[last] == new int2(before + 2, before + 3)),
                    "Removing the middle registration reclaims exactly its three slots and moves the later sampler's offsets");
                Check(SameWaterSample(first, aResult) && SameWaterSample(last, tailResult, false) && OriginalWaterSamplesUnchanged(original),
                    "Middle-range compaction retains both neighbour input/result/normal data and every pre-existing sampler's offsets and data");
                GerstnerWavesJobs.RemoveSamplePoints(middle);
                Check(WaterSampleCount == before + 3 && OriginalWaterSamplesUnchanged(original),
                    "Repeated removal is idempotent and cannot decrement or rewrite another sampler");
                GerstnerWavesJobs.RemoveSamplePoints(first);
                Check(WaterSampleCount == before + 1 && math.all(WaterRegistry[last] == new int2(before, before + 1))
                    && SameWaterSample(last, tailResult, false) && OriginalWaterSamplesUnchanged(original),
                    "Removing the remaining leading neighbour compacts the last sampler again without losing its completed results");
                var missingPositions = new[] { new float3(123, 456, 789) };
                var missingNormals = new[] { new float3(987, 654, 321) };
                GerstnerWavesJobs.GetData(middle, ref missingPositions, ref missingNormals);
                Check(math.all(missingPositions[0] == new float3(123, 456, 789))
                    && math.all(missingNormals[0] == new float3(987, 654, 321)),
                    "GetData for an unloaded registration leaves the caller's unrelated output buffers untouched");
            }
            finally
            {
                // Only remove IDs allocated by this fixture, even when an assertion or job fails.
                GerstnerWavesJobs.RemoveSamplePoints(first);
                GerstnerWavesJobs.RemoveSamplePoints(middle);
                GerstnerWavesJobs.RemoveSamplePoints(last);
                if (a.IsCreated) a.Dispose(); if (b.IsCreated) b.Dispose(); if (tail.IsCreated) tail.Dispose();
                Check(WaterSampleCount == before && WaterRegistry.Count == originalRanges.Count
                    && WaterRangesUnchanged(originalRanges) && (original == null || OriginalWaterSamplesUnchanged(original)),
                    "Final sampler unload returns _positionCount to its initial value and preserves every original registration and completed sample");
            }
        }
        private static Dictionary<int, WaterSampleSnapshot> CaptureWaterSamples(Dictionary<int, int2> ranges)
        {
            var result = new Dictionary<int, WaterSampleSnapshot>();
            foreach (var pair in ranges) result.Add(pair.Key, CaptureWaterSample(pair.Key));
            return result;
        }
        private static WaterSampleSnapshot CaptureWaterSample(int guid)
        {
            int2 range = WaterRegistry[guid]; int count = range.y - range.x;
            var result = new WaterSampleSnapshot { Range = range, Input = new float3[count], Positions = new float3[count], Normals = new float3[count] };
            var input = WaterInput;
            for (int i = 0; i < count; i++) result.Input[i] = input[range.x + i];
            GerstnerWavesJobs.GetData(guid, ref result.Positions, ref result.Normals);
            return result;
        }
        private static bool SameWaterSample(int guid, WaterSampleSnapshot previous, bool sameRange = true)
        {
            if (!WaterRegistry.ContainsKey(guid)) return false;
            var current = CaptureWaterSample(guid);
            return (!sameRange || math.all(current.Range == previous.Range)) && Equal(current.Input, previous.Input)
                && Equal(current.Positions, previous.Positions) && Equal(current.Normals, previous.Normals);
        }
        private static bool OriginalWaterSamplesUnchanged(Dictionary<int, WaterSampleSnapshot> original)
        { foreach (var pair in original) if (!SameWaterSample(pair.Key, pair.Value)) return false; return true; }
        private static bool WaterRangesUnchanged(Dictionary<int, int2> original)
        { foreach (var pair in original) if (!WaterRegistry.TryGetValue(pair.Key, out var range) || !math.all(range == pair.Value)) return false; return true; }
        private static bool ValidWaterResult(WaterSampleSnapshot sample)
        {
            for (int i = 0; i < sample.Positions.Length; i++)
                if (!math.all(math.isfinite(sample.Positions[i])) || !math.all(math.isfinite(sample.Normals[i])) || math.lengthsq(sample.Normals[i]) < .5f) return false;
            return true;
        }
        private static bool Equal(float3[] a, float3[] b)
        { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (!math.all(a[i] == b[i])) return false; return true; }

        private static IEnumerable ValidateRequests(FishingPrototype c)
        {
            var a = c.WorldAudio; var position = c.ExplorerPosition + Vector3.up;
            a.enabled = false; a.enabled = true; yield return null;
            int sourceCount = a.Sources.Count;
            var unique = new HashSet<AudioSource>(a.Sources);
            Check(sourceCount > 8 && sourceCount <= 32 && unique.Count == sourceCount,
                "The observer creates one finite reusable set of unique voices");
            foreach (var source in a.Sources)
                Check(source != null && !source.playOnAwake && source.dopplerLevel == 0 && !source.ignoreListenerVolume,
                    "Owned voice has no autoplay, velocity pitch shift or master bypass: " + source.name);
            int hits = a.PlayCount("ImpactStone");
            Check(a.Play("ImpactStone", position, .5f) && a.PlayCount("ImpactStone") == hits + 1,
                "A valid world contact request plays its recorded clip exactly once");
            bool deduplicated = true;
            for (int i = 0; i < 80; i++) deduplicated &= !a.Play("ImpactStone", position, .5f);
            Check(deduplicated && a.PlayCount("ImpactStone") == hits + 1 && a.Sources.Count == sourceCount,
                "Collision input repetition neither stacks sounds nor creates extra voices");
            Check(!a.Play("unknown", position) && !a.Play(null, position) && !a.Play("RainLoop", position)
                && !a.Play("ImpactWood", new Vector3(float.NaN, 0, 0))
                && !a.Play("ImpactWood", new Vector3(0, float.PositiveInfinity, 0))
                && !a.Play("ImpactWood", position, float.NaN) && !a.Play("ImpactWood", position, 0)
                && !a.Play("ImpactWood", position, 1, FishingAudioCategory.Music),
                "Unknown, loop-as-shot, nonfinite, zero-gain and Music-route effect requests are rejected");

            string[] contacts = { "ImpactWood", "ImpactMetal", "BodyLand", "WaterEntry", "WaterStroke", "ClimbGrip", "FishBite", "FishHook", "LineSnap", "FishSplash", "AnimalHit", "LootBag", "CanHandle", "WellBucket" };
            int accepted = 1;
            foreach (string key in contacts) if (a.Play(key, position, .2f)) accepted++;
            Check(accepted > 2 && accepted <= 10 && a.Sources.Count == sourceCount,
                "Simultaneous distinct physical cues fill but cannot overgrow the bounded effects pool; accepted=" + accepted);
            var registry = (IDictionary)Field(c, "_settingAudio");
            foreach (var source in a.Sources)
            {
                Check(registry.Contains(source), "Every world voice participates in existing channel controls: " + source.name);
                if (!source.isPlaying || source.clip == null || source.loop) continue;
                Check(source.spatialBlend == 1 && !source.mute && source.volume > 0 && source.volume <= .17f * c.EffectsVolume + .001f,
                    "A physical cue is an audible bounded spatial Effects voice");
            }
            for (int i = 0; i < 64; i++) a.MaintainLoop("validation-pool-" + i, "WaterPourLoop", position, .04f);
            int leased = 0;
            for (int i = 0; i < 64; i++) if (a.LoopSource("validation-pool-" + i) != null) leased++;
            Check(leased > 0 && leased <= 12 && a.Sources.Count == sourceCount,
                "Distinct loop leases share a finite pool and reject overflow without allocating more AudioSources");
            a.enabled = false; a.enabled = true; yield return null;
            Check(a.LoopSource("validation-pool-0") == null, "Disabling releases prior loop leases instead of preserving stale emitters");
            c.SetSetting("Effects", 0);
            Check(!a.Play("PortalPass", position) && !a.Play("UIReject", position), "Effects zero rejects both world and interface playback");
            c.SetSetting("Effects", .7f); c.SetSetting("Ambient", 0);
            int loops = a.LoopStarts("FurnaceLoop"), birds = a.PlayCount("SeagullCall");
            a.MaintainLoop("validation-muted", "FurnaceLoop", position, .04f);
            Check(a.LoopStarts("FurnaceLoop") == loops && a.LoopSource("validation-muted") == null
                && !a.Play("SeagullCall", position, .3f, FishingAudioCategory.Ambient) && a.PlayCount("SeagullCall") == birds,
                "Ambient zero rejects new ambience requests and loop leases");
            // EditorApplication.update can run after the player's LateUpdate.
            // Drive the real gain step explicitly for the isolated routing probe;
            // the separate farm test below observes normal production timing.
            a.MaintainLoop("validation-action", "WaterPourLoop", position, .10f, category: FishingAudioCategory.Effects);
            var actionLoop = a.LoopSource("validation-action");
            typeof(FishingWorldAudio).GetMethod("TickLoops", Fields).Invoke(a, new object[] { .08f });
            Check(actionLoop != null && actionLoop.isPlaying && !actionLoop.mute && actionLoop.volume >= .10f * c.EffectsVolume * .8f
                && (FishingAudioCategory)Field(registry[actionLoop], "Category") == FishingAudioCategory.Effects,
                "Recorded water action reaches audible gain promptly through Effects even with Ambient at zero");
            c.SetSetting("Effects", 0);
            Check(actionLoop.volume == 0, "Effects zero immediately silences an already playing water action");
            c.SetSetting("Ambient", .5f);
            a.MaintainLoop("validation-action", "WaterPourLoop", position, .04f, category: FishingAudioCategory.Ambient);
            Check(a.LoopSource("validation-action") == actionLoop
                && (FishingAudioCategory)Field(registry[actionLoop], "Category") == FishingAudioCategory.Ambient,
                "Reusing the same lease for another category rebinds the pooled source exactly at that boundary");
            typeof(FishingWorldAudio).GetMethod("TickLoops", Fields).Invoke(a, new object[] { .10f });
            Check(actionLoop.isPlaying && actionLoop.volume > .001f,
                "Effects zero does not suppress a reused Ambient source or multiply its channel volume twice");
            foreach (var step in Wait(.4)) yield return step;
            Check(a.LoopSource("validation-action") == null, "An omitted ambience lease still uses its original fade and releases");
            c.SetSetting("Effects", .7f);
            a.MaintainLoop("validation-action", "WaterPourLoop", position, .10f, category: FishingAudioCategory.Effects);
            typeof(FishingWorldAudio).GetMethod("TickLoops", Fields).Invoke(a, new object[] { .08f });
            foreach (var step in Wait(.2)) yield return step;
            Check(a.LoopSource("validation-action") == null,
                "A reused Effects water loop stops within a short release rather than an ambience-length tail");
            c.SetSetting("Ambient", .5f); c.ToggleQuiet();
            Check(!a.Play("MechanismClick", position) && !a.Play("UIConfirm", position), "Quiet mode rejects new world and interface cues");
            yield return null;
            foreach (var source in a.Sources) Check(!source.isPlaying && source.mute, "Quiet mode stops owned voices and preserves their mute route");
            c.ToggleQuiet(); yield return null;
            c.TogglePause(); yield return null;
            int ui = a.PlayCount("UIBack");
            Check(!a.Play("WaterEntry", position) && a.Play("UIBack", position, .3f) && a.PlayCount("UIBack") == ui + 1,
                "A paused world rejects physical movement while interface confirmation remains available");
            c.TogglePause(); yield return null;
            foreach (var step in Wait(.35)) yield return step;
        }

        private static IEnumerable ValidateStorageAndCrafting(FishingPrototype c)
        {
            ClosePanels(c); c.ReturnToHarbor(); yield return null;
            typeof(FishingPrototype).GetMethod("PrepareAcceptanceHome", Fields).Invoke(c, new object[] { FishingAcceptanceCase.HomeInterior });
            yield return null;
            Check(c.Shore.Relocate(c.Home.Origin + new Vector3(3.1f, .2f, -1.35f), Quaternion.Euler(0, 90, 0)),
                "A supported actual home-storage stance clears the nearby facilities");
            c.viewCamera.transform.LookAt(c.Home.InteractionPoint(6)); yield return null;
            Check(c.Career.Backpack.TryAdd(FieldItem.IslandFruit, 3), "The isolated actor owns a real transferable fruit stack");
            int fruitSlot = Slot(c.Career.Backpack, FieldItem.IslandFruit);
            Check(c.Home.InteractAt(6) && c.InventoryOpen && c.Paused && Time.timeScale == 0,
                "The production chest interaction opens the actual paused inventory panel");
            yield return null;
            int transfers = c.WorldAudio.PlayCount("StorageMove"), bagFruit = c.Career.Backpack.Count(FieldItem.IslandFruit);
            Check(c.Home.TransferStorage(true, fruitSlot) && c.Career.Backpack.Count(FieldItem.IslandFruit) == 0
                && c.Career.HomeStorage.Count(FieldItem.IslandFruit) == bagFruit && c.WorldAudio.PlayCount("StorageMove") == transfers + 1,
                "A successful actual chest deposit changes ownership and emits one sound while game time is zero");
            Check(!c.Home.TransferStorage(true, fruitSlot) && c.WorldAudio.PlayCount("StorageMove") == transfers + 1,
                "Repeating an empty deposit cannot duplicate the storage-success cue");
            foreach (var step in Wait(.14)) yield return step;
            Check(c.Home.TransferStorage(false, Slot(c.Career.HomeStorage, FieldItem.IslandFruit))
                && c.Career.Backpack.Count(FieldItem.IslandFruit) == bagFruit && c.WorldAudio.PlayCount("StorageMove") == transfers + 2,
                "The actual paused withdrawal restores the conserved stack and plays its independent success edge");
            AssertUiAudible(c, "StorageMove");
            c.ToggleInventory(); yield return null;

            foreach (CraftStation kind in new[] { CraftStation.Workbench, CraftStation.Forge })
            {
                // Wait for the preceding successful transaction voices to finish naturally. Pool overflow
                // was tested above; this check isolates a new transaction, not an artificial UI burst.
                foreach (var step in WaitForUiIdle(c)) yield return step;
                int stationId = -1;
                foreach (var station in c.Career.Workshop.Stations)
                    if (station.kind == kind && Vector3.Distance(new Vector3(station.x, station.y, station.z), c.Home.StationPosition(kind)) < .3f)
                    { stationId = station.id; break; }
                Check(stationId >= 0 && c.Shore.Relocate(c.Home.StationPosition(kind) + Vector3.back * 1.35f, Quaternion.identity),
                    "The actual cottage has a reachable " + kind + " with a safe approach");
                yield return null;
                string recipe = kind == CraftStation.Workbench ? "stone-pick" : "iron-pick-head";
                var data = FishingRecipes.Find(recipe);
                foreach (var cost in data.Costs) Check(c.Career.Backpack.TryAdd(cost.item, cost.count), "Supply exact real recipe cost: " + cost.item);
                Check(c.WorkshopWorld.OpenStation(stationId) && c.InventoryOpen && c.Paused && c.WorkshopWorld.Context == kind,
                    "The production station opens the actual crafting panel without unpausing gameplay: " + kind);
                yield return null;
                string cue = kind == CraftStation.Workbench ? "WorkbenchAssemble" : "ForgeStrike";
                int count = c.WorldAudio.PlayCount(cue), owned = c.Career.Backpack.Count(data.Output);
                Check(c.WorkshopWorld.Craft(recipe) && c.Career.Backpack.Count(data.Output) == owned + data.Count
                    && c.WorldAudio.PlayCount(cue) == count + 1,
                    "A successful real paused recipe consumes material, grants its output and sounds once: " + kind);
                AssertUiAudible(c, cue);
                Check(!c.WorkshopWorld.Craft("not-a-recipe") && c.WorldAudio.PlayCount(cue) == count + 1,
                    "A failed recipe cannot reuse the successful station cue: " + kind);
                c.ToggleInventory(); yield return null;
            }
        }

        private static IEnumerable ValidateEnvironment(FishingPrototype c)
        {
            Check(c.Home.Inside, "The real character is inside the cottage before shelter checks");
            foreach (var step in Wait(1.25)) yield return step;
            var wind = (AudioSource)Field(c.Weather, "_audio");
            Check(wind != null && wind.clip != null && c.WorldAudio.EnvironmentShelter > .95f
                && wind.volume <= c.GetAudioChannelVolume(wind) * .34f && wind.volume > 0,
                "Real indoor occupancy attenuates the authored coastal wind without changing its channel base");
            Check(c.GetComponent<AudioLowPassFilter>() == null && c.Weather.GetComponent<AudioLowPassFilter>() == null,
                "Indoor shelter never attaches a shared filter to the controller or weather root");
            double deadline = EditorApplication.timeSinceStartup + 16;
            while ((c.RegionalMusic.Transitioning || !MusicAudible(c)) && EditorApplication.timeSinceStartup < deadline) yield return null;
            Check(MusicAudible(c), "An actual regional BGM voice remains playing inside the cottage");
            foreach (var source in c.RegionalMusic.Voices)
                Check(source.GetComponent<AudioLowPassFilter>() == null && Near(source.volume, c.GetAudioChannelVolume(source)),
                    "Indoor acoustic attenuation leaves each BGM voice and its gain unfiltered");
            c.SetSetting("Ambient", 0); yield return null;
            Check(wind.volume == 0 && MusicAudible(c), "Ambient mute silences sheltered wind while BGM continues independently");
            c.SetSetting("Ambient", .5f); c.ToggleQuiet(); yield return null;
            Check(AudioListener.volume == 0 && wind.mute, "Quiet globally mutes the real sheltered weather source");
            foreach (var source in c.RegionalMusic.Voices) Check(source.mute, "Quiet also mutes each registered music voice");
            c.ToggleQuiet(); yield return null;
            Check(MusicAudible(c), "Unmuting restores BGM after the indoor/global mute probe");
        }

        private static IEnumerable ValidateFarmWater(FishingPrototype c)
        {
            ClosePanels(c); c.ReturnToHarbor(); yield return null;
            var fixture = new FishingCareer(); fixture.Farm.ClaimStarter(fixture.Backpack);
            Check(fixture.Farm.Till(0) && fixture.Farm.Plant(0, fixture.Backpack)
                && fixture.Farm.Till(1) && fixture.Farm.Plant(1, fixture.Backpack), "Prepare two unwatered crops in pure isolated career state");
            var snapshot = fixture.Snapshot(); snapshot.prologueSeen = true; snapshot.calendar.timeFixed = true;
            c.RestoreShopTestProfile(snapshot);
            typeof(FishingPrototype).GetMethod("PrepareAcceptanceHome", Fields).Invoke(c, new object[] { FishingAcceptanceCase.HomeInterior });
            if (!c.FirstPerson) c.ToggleView();
            yield return null;
            foreach (var step in ApproachFarm(c, 15)) yield return step;
            int handle = c.WorldAudio.PlayCount("CanHandle");
            Check(c.RequestWorldInteraction() && c.Farm.Busy, "Real reach and production targeting begin the watering-can pickup");
            foreach (var step in FinishFarm(c)) yield return step;
            Check(c.Farm.CarryingCan && c.WorldAudio.PlayCount("CanHandle") == handle + 1,
                "Actual hand contact acquires the same can and emits one handle cue");
            foreach (var step in ApproachFarm(c, 12)) yield return step;
            int water = c.Career.Farm.Water, rope = c.WorldAudio.LoopStarts("WellRopeLoop"), poured = c.WorldAudio.LoopStarts("WaterPourLoop");
            int finish = c.WorldAudio.PlayCount("WaterFinish"), bucket = c.WorldAudio.PlayCount("WellBucket");
            Check(c.RequestWorldInteraction() && c.Farm.Busy, "Production well interaction starts the actual refill animation");
            bool actualStream = false, actualRope = false;
            double deadline = EditorApplication.timeSinceStartup + 12;
            while (c.Farm.Busy && c.Farm.WorkProgress < .67f && EditorApplication.timeSinceStartup < deadline)
            {
                c.PresentCharacterFrame();
                var pourSource = c.WorldAudio.LoopSource("farm-water");
                var ropeSource = c.WorldAudio.LoopSource("well-rope");
                actualStream |= c.Farm.WaterStreamVisible && pourSource != null && pourSource.isPlaying && pourSource.clip == FishingWorldAudio.Clip("WaterPourLoop");
                actualRope |= ropeSource != null && ropeSource.isPlaying && ropeSource.clip == FishingWorldAudio.Clip("WellRopeLoop");
                yield return null;
            }
            Check(c.Farm.Busy && actualStream && actualRope && c.WorldAudio.LoopStarts("WaterPourLoop") > poured
                && c.WorldAudio.LoopStarts("WellRopeLoop") > rope && c.WorldAudio.PlayCount("WellBucket") == bucket + 1,
                "Actual lowering/hauling/pouring phases start the well contact and maintained recorded loops alongside the visible stream");
            c.CancelWorldAction(); yield return null;
            Check(!c.Farm.Busy && c.Career.Farm.Water == water && c.WorldAudio.PlayCount("WaterFinish") == finish && !c.Farm.WaterStreamVisible,
                "Cancelling the real refill grants no water or completion sound and hides its stream");
            foreach (var step in Wait(.8)) yield return step;
            Check(c.WorldAudio.LoopSource("farm-water") == null && c.WorldAudio.LoopSource("well-rope") == null,
                "Cancelled water and rope leases fade out and release their slots");
            Check(c.RequestWorldInteraction(), "The actual cancelled well refill can restart");
            foreach (var step in FinishFarm(c)) yield return step;
            Check(c.Career.Farm.Water == FishingFarm.WaterCapacity && c.WorldAudio.PlayCount("WaterFinish") == finish + 1,
                "Only a completed actual well refill fills the saved reservoir and plays one water-finish contact");
            foreach (var step in ApproachFarm(c, 0)) yield return step;
            c.SetSetting("Ambient", 0); c.SetSetting("Effects", .7f);
            water = c.Career.Farm.Water; finish = c.WorldAudio.PlayCount("WaterFinish");
            string soilCue = c.WorldAudio.HasCue("WaterSprinkleLoop") ? "WaterSprinkleLoop" : "WaterPourLoop";
            poured = c.WorldAudio.LoopStarts(soilCue); int legacyWater = c.ActionAudio.DetailCount("FarmWater");
            Check(c.RequestWorldInteraction() && !c.RequestWorldInteraction(), "Actual crop watering starts once and refuses overlapping input");
            actualStream = false; float peakWaterGain = 0; deadline = EditorApplication.timeSinceStartup + 12;
            while (c.Farm.Busy && EditorApplication.timeSinceStartup < deadline)
            {
                c.PresentCharacterFrame(); var source = c.WorldAudio.LoopSource("farm-water");
                actualStream |= c.Farm.WaterStreamVisible && source != null && source.isPlaying && source.clip == FishingWorldAudio.Clip(soilCue);
                if (c.Farm.WaterStreamVisible && source != null && !source.mute) peakWaterGain = Mathf.Max(peakWaterGain, source.volume);
                yield return null;
            }
            Check(!c.Farm.Busy && actualStream && c.WorldAudio.LoopStarts(soilCue) > poured
                && c.Career.Farm.Water == water - 1 && c.Career.Farm.Plot(0).wateredDay == c.Career.Farm.Day
                && c.WorldAudio.PlayCount("WaterFinish") == finish + 1 && c.ActionAudio.DetailCount("FarmWater") == legacyWater,
                "Real crop pouring sounds during its visible stream, consumes one charge, finishes once, and does not stack the old completion recording");
            Check(c.AmbientVolume == 0 && peakWaterGain >= .11f * c.EffectsVolume * .8f,
                "Real crop water remains clearly above zero during its short stream window when environmental wind/waves are muted");
            Check(!c.RequestWorldInteraction() && c.Career.Farm.Water == water - 1 && c.WorldAudio.PlayCount("WaterFinish") == finish + 1,
                "An already watered plot cannot consume another charge or repeat its finish sound");
            foreach (var step in Wait(.8)) yield return step;
            Check(c.WorldAudio.LoopSource("farm-water") == null, "Completed watering leaves no continuing pouring sound");
            foreach (var step in ApproachFarm(c, 1)) yield return step;
            c.SetSetting("Effects", 0); c.SetSetting("Ambient", .5f);
            water = c.Career.Farm.Water; finish = c.WorldAudio.PlayCount("WaterFinish");
            poured = c.WorldAudio.LoopStarts(soilCue); bool mutedStream = false, unexpectedLease = false;
            Check(c.RequestWorldInteraction(), "A second actual crop accepts watering while only Effects is muted");
            deadline = EditorApplication.timeSinceStartup + 12;
            while (c.Farm.Busy && EditorApplication.timeSinceStartup < deadline)
            {
                c.PresentCharacterFrame(); mutedStream |= c.Farm.WaterStreamVisible;
                unexpectedLease |= c.WorldAudio.LoopSource("farm-water") != null; yield return null;
            }
            Check(!c.Farm.Busy && mutedStream && !unexpectedLease && c.WorldAudio.LoopStarts(soilCue) == poured
                && c.WorldAudio.PlayCount("WaterFinish") == finish && c.Career.Farm.Water == water - 1
                && c.Career.Farm.Plot(1).wateredDay == c.Career.Farm.Day,
                "Effects mute suppresses actual water flow and finish recordings without suppressing the successful farming transaction");
            c.SetSetting("Effects", .7f);
        }

        private static IEnumerable ApproachFarm(FishingPrototype c, int id)
        {
            var p = c.Farm.Target(id).position + (id == 15 ? Vector3.left * .65f : Vector3.back * (id < 12 ? 1.1f : 1.3f));
            p.y = c.Home.Origin.y; if (id < 12) p.x -= .25f;
            Check(c.Shore.Relocate(p, Quaternion.identity), "Actual farm apron supports the actor capsule: " + id);
            c.View.ResetLook();
            if (id == 15)
            {
                float scale = .12f * c.MouseSensitivity;
                c.View.ApplyShoreLookDelta(new Vector2(90 / scale, -55 / scale * (c.InvertLook ? -1 : 1)));
            }
            for (int frame = 0; frame < 30; frame++)
            { c.Shore.Move(Vector2.zero, false, false, false, Time.deltaTime); yield return null; if (frame >= 3 && c.Shore.Grounded) break; }
            Check(c.Shore.Grounded, "Actor settles on the physical farm floor: " + id);
            FishingV036FarmValidation.Aim(c, c.Farm.Aim(id)); yield return null;
            Check(c.Farm.TryTarget(out int found, out _) && found == id, "Production farm targeting resolves the intended audio action: " + id);
        }
        private static IEnumerable FinishFarm(FishingPrototype c)
        {
            double deadline = EditorApplication.timeSinceStartup + 15;
            while (c.Farm.Busy && EditorApplication.timeSinceStartup < deadline) { c.PresentCharacterFrame(); yield return null; }
            Check(!c.Farm.Busy, "The real can animation reaches its contact and releases the action");
            yield return null; yield return null;
        }
        private static void AssertUiAudible(FishingPrototype c, string cue)
        {
            bool audible = false;
            foreach (var source in c.WorldAudio.Sources)
                if (source.clip == FishingWorldAudio.Clip(cue) && source.isPlaying && !source.mute && source.volume > 0 && source.spatialBlend == 0) audible = true;
            Check(audible && c.Paused && Time.timeScale == 0, "Recorded successful interface cue really plays through an unmuted pause-allowed source: " + cue);
        }
        private static bool MusicAudible(FishingPrototype c)
        { foreach (var source in c.RegionalMusic.Voices) if (source.isPlaying && !source.mute && source.volume > .0001f) return true; return false; }
        private static int Slot(FishingBackpack bag, FieldItem item)
        { for (int slot = 0; slot < FishingBackpack.SlotCount; slot++) if (bag.At(slot).item == item && bag.At(slot).count > 0) return slot; return -1; }
        private static IEnumerable Wait(double seconds)
        { double until = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < until) yield return null; }
        private static IEnumerable WaitForUiIdle(FishingPrototype c)
        {
            double deadline = EditorApplication.timeSinceStartup + 40;
            while (UiPlaying(c) && EditorApplication.timeSinceStartup < deadline) yield return null;
            Check(!UiPlaying(c), "Preceding interface recordings finish naturally before the next independent station transaction");
        }
        private static bool UiPlaying(FishingPrototype c)
        { foreach (var source in c.WorldAudio.Sources) if (source.spatialBlend == 0 && !source.loop && source.isPlaying) return true; return false; }
        private static void ClosePanels(FishingPrototype c)
        {
            c.Prologue?.Suspend(); c.HandGames?.Cancel(); c.Letter?.Close(); c.Residents?.Close(); c.TackleShop?.Close(); c.XRNotebook?.Close();
            c.Home?.CancelSleep(); c.CloseHomeChart(); c.CloseSaveMenu();
            if (c.StartMenuOpen) c.FinishTitleVoyage();
            if (c.InventoryOpen) c.ToggleInventory();
            if (c.JournalOpen) c.ToggleJournal();
            if (c.Paused) c.TogglePause();
            c.CancelWorldAction();
        }
        private sealed class Preference { public string Key; public bool Integer, Exists; public float Value; }
        private static Preference[] CapturePreferences()
        {
            var result = new List<Preference>();
            foreach (string name in new[] { "Master", "Music", "Ambient", "Effects" })
            { string key = "Fishing.V032." + name; result.Add(new Preference { Key = key, Exists = PlayerPrefs.HasKey(key), Value = PlayerPrefs.GetFloat(key, -913) }); }
            result.Add(new Preference { Key = "Fishing.V3.Quiet", Integer = true, Exists = PlayerPrefs.HasKey("Fishing.V3.Quiet"), Value = PlayerPrefs.GetInt("Fishing.V3.Quiet", -913) });
            return result.ToArray();
        }
        private static bool PreferencesUnchanged(Preference[] prefs)
        { foreach (var pref in prefs) if (PlayerPrefs.HasKey(pref.Key) != pref.Exists || !(pref.Integer ? PlayerPrefs.GetInt(pref.Key, -913) : PlayerPrefs.GetFloat(pref.Key, -913)).Equals(pref.Value)) return false; return true; }
        private static byte[] Read(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        private static bool Same(byte[] a, byte[] b)
        { if (a == null || b == null) return a == b; if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }
}
