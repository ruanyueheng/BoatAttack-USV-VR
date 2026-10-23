using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingActionAudioValidation
    {
        public static void Run() => FishingV011Validation.Begin("ACTION-AUDIO");
        private static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            Debug.Log("[ACTION-AUDIO] CHECK: " + label);
        }
        private static T Private<T>(object owner, string name)
            => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),
                "All action audio checks use the actual scene with a temporary isolated save");
            var original = c.Career.Snapshot();
            float master = c.MasterVolume, effects = c.EffectsVolume;
            bool quiet = c.QuietMode;
            string productionPath = Path.Combine(Application.persistentDataPath, "boatattack-fishing-v13.json");
            byte[] production = Read(productionPath), backup = Read(productionPath + ".bak");
            try
            {
                if (c.QuietMode) c.ToggleQuiet();
                c.SetSetting("Master", 1); c.SetSetting("Effects", 1);
                c.ReturnToHarbor(); c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                yield return null;
                Check(c.ActionAudio != null, "The production action-audio component is initialized");
                ValidateResources(c);
                ValidateSettings(c);
                ValidateSurfaces(c);

                PlaceBoat(c, c.Home.Berth);
                for (int i = 0; i < 20; i++) yield return null;
                Check(c.Shore.TryDisembark(), "The real home berth supports disembarking for footstep checks");
                Check(c.Shore.Relocate(c.Home.BoardwalkStart + Vector3.forward * .55f, Quaternion.identity),
                    "The real boardwalk entrance has capsule clearance");
                c.View.ResetLook(); c.PresentCharacterFrame();
                for (int i = 0; i < 5; i++)
                { c.Shore.Move(Vector2.zero, false, false, false, Time.deltaTime); yield return null; }
                int steps = c.ActionAudio.FootstepsPlayed;
                Vector3 start = c.Shore.Position;
                for (int frame = 0; frame < 240 && FishingPrototype.FlatDistance(start, c.Shore.Position) < 2.1f; frame++)
                {
                    // Use the observed frame time: fixed .05 steps at 60 Hz look like a teleport to the cadence gate.
                    c.Shore.Move(Vector2.up, false, false, false, Time.deltaTime);
                    yield return null;
                }
                float moved = FishingPrototype.FlatDistance(start, c.Shore.Position);
                Check(moved > 1.2f && c.ActionAudio.FootstepsPlayed > steps
                    && c.ActionAudio.FootstepsPlayed - steps <= 6
                    && c.ActionAudio.LastFootstepSurface == FishingGroundAudio.Wood,
                    "Actual CharacterController travel on the home boardwalk emits paced wood steps, distance=" + moved
                    + ", steps=" + (c.ActionAudio.FootstepsPlayed - steps));
                steps = c.ActionAudio.FootstepsPlayed;
                for (int i = 0; i < 24; i++)
                { c.Shore.Move(Vector2.zero, false, false, false, Time.deltaTime); yield return null; }
                Check(c.ActionAudio.FootstepsPlayed == steps, "Standing still does not spray footstep sounds");
                c.TogglePause();
                for (int i = 0; i < 5; i++)
                { c.Shore.Move(Vector2.up, false, false, false, Time.unscaledDeltaTime); yield return null; }
                Check(c.ActionAudio.FootstepsPlayed == steps && !c.ActionAudio.FootstepSource.isPlaying,
                    "Pause blocks both movement-driven steps and their source playback");
                c.TogglePause(); yield return null;

                var farmDetails = ValidateFarmDetails(c);
                while (farmDetails.MoveNext()) yield return farmDetails.Current;
                ValidatePickup(c);
                ValidateHarvest(c);
                var shopDetails = ValidateShopDetails(c);
                while (shopDetails.MoveNext()) yield return shopDetails.Current;
                var inspections = ValidateToolInspections(c);
                while (inspections.MoveNext()) yield return inspections.Current;

                c.ReturnToHarbor(); yield return null;
                c.MoveToSpotForAutomation(0); c.Interact();
                int casts = c.ActionAudio.DetailCount("RodCast"), splashes = c.ActionAudio.DetailCount("FloatSplash");
                c.CastMotion.Prepare(); c.CastMotion.Tick(.1f); c.CastMotion.CancelPreparation();
                Check(c.ActionAudio.DetailCount("RodCast") == casts && c.ActionAudio.DetailCount("FloatSplash") == splashes,
                    "Cancelling cast preparation emits neither a released-rod cue nor a water impact");
                Check(c.Anchored && c.Cast(.8f), "The real production cast starts reel-audio checks");
                Check(c.ActionAudio.DetailCount("RodCast") == casts + 1 && !c.Cast(.8f)
                    && c.ActionAudio.DetailCount("RodCast") == casts + 1,
                    "A successful released cast plays exactly one rod cue; overlapping rejected casts stay silent");
                FishingCatchValidation.KeepOrdinaryReceipt(c);
                var reel = Private<AudioSource>(c.Angling, "_reelAudio");
                var reelIn = FishingActionAudio.Clip("ReelInLoop");
                var reelOut = FishingActionAudio.Clip("ReelOutLoop");
                for (int i = 0; i < 10; i++)
                { c.CastMotion.Tick(.05f); c.Angling.Tick(.05f, false, true); }
                Check(c.Angling.LineTravel < 0 && reel.clip == reelOut,
                    "Real cast-flight line travel selects the imported payout loop");
                Check(c.ActionAudio.DetailCount("FloatSplash") == splashes,
                    "A float still in flight has not played its landing sound early");
                c.Angling.Tick(.05f, false, true);
                Check(c.Angling.LineTravel == 0 && !c.Angling.ReelSoundPlaying,
                    "No actual line movement stops the loop even with the fishing tool available");
                for (int i = 0; i < 20; i++)
                { c.CastMotion.Tick(.1f); c.Angling.Tick(.1f, false, true); }
                for (int frame = 0; frame < 4 && c.ActionAudio.DetailCount("FloatSplash") == splashes; frame++) yield return null;
                Check(c.ActionAudio.DetailCount("FloatSplash") == splashes + 1,
                    "The actual first water-contact presentation frame emits one generated float splash");
                yield return null;
                Check(c.ActionAudio.DetailCount("FloatSplash") == splashes + 1,
                    "Later waiting frames cannot repeat the float's one-shot landing sound");
                for (int i = 0; i < 500 && c.Session.Phase == CatchPhase.Waiting; i++) c.Session.Tick(.02f, false);
                Check(c.Hook(), "The actual bite hooks into a fight for line-in/line-out checks");
                c.Session.Tick(.05f, true); c.Angling.Tick(.05f, true, true);
                Check(c.Angling.LineTravel > 0 && reel.clip == reelIn,
                    "Retrieving real line switches the same source to the imported winding loop");
                c.Session.Tick(.05f, false); c.Angling.Tick(.05f, false, true);
                Check(c.Angling.LineTravel < 0 && reel.clip == reelOut,
                    "A fish taking real line switches the same source back to the distinct payout loop");
                c.Angling.Tick(0, true, true);
                Check(!c.Angling.ReelSoundPlaying, "A zero-time frame cannot keep a held-input reel loop alive");
                c.Angling.Tick(.05f, true, false);
                Check(!c.Angling.ReelSoundPlaying, "Loss of tool availability stops reel playback");
                for (int i = 0; i < 6500 && c.Session.Phase == CatchPhase.Fighting; i++)
                    c.Session.Tick(.02f, c.Session.Tension < .55f && c.Session.Beat != FishBeat.Leap, -Math.Sign(c.Session.FishDirection));
                Check(c.Session.Phase == CatchPhase.Landing, "The short audio fixture reaches the existing landing phase");
                yield return null;
                Check(c.CatchRig.RequestLiftForInspection() && c.HasPendingCatch && c.CatchRig.LeftHandInspecting,
                    "The real hand-first catch creates a durable live-fish inspection");
                int netPlacements = c.ActionAudio.DetailCount("NetFishPlace");
                int flaps = c.ActionAudio.FishFlapsPlayed;
                float lastMotion = c.CatchRig.InspectionMotionTime;
                int firstFrame = Time.frameCount, flapFrames = 0;
                for (int frame = 0; frame < 240 && c.ActionAudio.FishFlapsPlayed - flaps < 2; frame++)
                {
                    yield return null;
                    if (c.CatchRig.InspectionMotionTime > lastMotion) flapFrames++;
                    lastMotion = c.CatchRig.InspectionMotionTime;
                }
                Check(c.CatchRig.LeftInspectionSettled && c.ActionAudio.FishFlapsPlayed - flaps == 2
                    && flapFrames > 15 && Time.frameCount - firstFrame > 15,
                    "A genuinely inspected live fish emits two separated animation-peak flaps, not one per render frame");
                flaps = c.ActionAudio.FishFlapsPlayed;
                c.TogglePause();
                for (int i = 0; i < 8; i++) yield return null;
                Check(c.ActionAudio.FishFlapsPlayed == flaps && !c.ActionAudio.FishSource.isPlaying,
                    "Pausing a held fish stops flap playback without replaying missed beats");
                Check(c.ActionAudio.DetailCount("NetFishPlace") == netPlacements,
                    "Safety-parking a fish on pause does not pretend a placement animation completed");
                c.TogglePause(); yield return null;
                c.CatchRig.PrepareHandAction();
                Check(c.CatchRig.Location == FishingCatchRig.Place.Hand, "The same durable fish can enter the existing right-hand inspection pose");
                int rightFlaps = c.ActionAudio.FishFlapsPlayed;
                for (int frame = 0; frame < 240 && c.ActionAudio.FishFlapsPlayed - rightFlaps < 2; frame++) yield return null;
                Check(c.ActionAudio.FishFlapsPlayed == rightFlaps + 2,
                    "Right-hand inspection uses the same generated fish bank on separated actual motion peaks");
                c.CatchRig.ToggleInspect();
                for (int frame = 0; frame < 5; frame++) yield return null;
                Check(c.CatchRig.Location == FishingCatchRig.Place.Tray && c.ActionAudio.FishFlapsPlayed == rightFlaps + 2
                    && !c.ActionAudio.FishSource.isPlaying, "Putting down the right-held fish stops struggle audio without granting a reward");
                // Pause safely parks the fish in its net. Explicitly pick up the same receipt again.
                Check(c.CatchRig.ToggleLeftInspection(), "The same pending live fish can be inspected after pause recovery");
                for (int i = 0; i < 100 && !c.CatchRig.LeftInspectionSettled; i++) yield return null;
                Check(c.CatchRig.PlaceLeftFishInNet(), "The existing hand-to-net placement starts normally");
                Check(!c.CatchRig.PlaceLeftFishInNet() && c.ActionAudio.DetailCount("NetFishPlace") == netPlacements,
                    "Repeated placement requests do not play the contact cue at animation start");
                yield return null;
                c.TogglePause();
                for (int i = 0; i < 3; i++) yield return null;
                Check(c.ActionAudio.DetailCount("NetFishPlace") == netPlacements,
                    "Cancelling an unfinished placement through pause emits no false net-contact sound");
                c.TogglePause(); yield return null;
                Check(c.CatchRig.ToggleLeftInspection(), "The cancelled placement preserves the same inspectable receipt");
                for (int i = 0; i < 100 && !c.CatchRig.LeftInspectionSettled; i++) yield return null;
                Check(c.CatchRig.PlaceLeftFishInNet(), "A deliberate retry can complete the real net placement");
                flaps = c.ActionAudio.FishFlapsPlayed;
                for (int i = 0; i < 100 && c.CatchRig.PlacingInNet; i++) yield return null;
                for (int i = 0; i < 10; i++) yield return null;
                Check(c.CatchRig.Location == FishingCatchRig.Place.Net && c.ActionAudio.FishFlapsPlayed == flaps
                    && !c.ActionAudio.FishSource.isPlaying && c.HasPendingCatch,
                    "Placement and the netted state silence held-fish flaps without settling the receipt");
                Check(c.ActionAudio.DetailCount("NetFishPlace") == netPlacements + 1
                    && !c.CatchRig.PlaceLeftFishInNet() && c.ActionAudio.DetailCount("NetFishPlace") == netPlacements + 1,
                    "Only the completed placement emits one wet net-contact cue, never its failed retries or later frames");
                Check(c.ResolveCatch(true) && !c.ResolveCatch(true), "The audio fixture still commits its real fish only once");
            }
            finally
            {
                c.EndItemInspection();
                if (c.InventoryOpen) c.ToggleInventory();
                if (c.Paused) c.TogglePause();
                c.ReturnToHarbor(); c.RestoreShopTestProfile(original);
                c.SetSetting("Master", master); c.SetSetting("Effects", effects);
                if (c.QuietMode != quiet) c.ToggleQuiet();
            }
            Check(Same(production, Read(productionPath)) && Same(backup, Read(productionPath + ".bak")),
                "The player's original persistent save and backup are unchanged");
        }

        private static void ValidateResources(FishingPrototype c)
        {
            int count = 0;
            foreach (string prefix in new[] { "StepSand", "StepGrass", "StepWood", "StepStone" })
            {
                var variants = new HashSet<AudioClip>();
                for (int i = 1; i <= 4; i++) { var clip = ValidateClip(prefix + i); variants.Add(clip); count++; }
                Check(variants.Count == 4, "Four distinct imported footstep variants exist for " + prefix);
            }
            foreach (string prefix in new[] { "StonePickup", "StoneHit", "WoodPickup", "SoftPickup", "FishFlop" })
                for (int i = 1; i <= 3; i++) { ValidateClip(prefix + i); count++; }
            foreach (string clip in new[] { "ReelInLoop", "ReelOutLoop", "RifleShot" }) { ValidateClip(clip); count++; }
            Check(count == 34 && FishingActionAudio.Clip("ReelInLoop") != FishingActionAudio.Clip("ReelOutLoop"),
                "All 34 action clips exist and reel-in/reel-out are distinct resources");
            var rifleShots = Private<AudioClip[]>(c.RifleHunting, "_shots");
            Check(rifleShots.Length == 2 && rifleShots[0] == FishingActionAudio.Clip("RifleShot1")
                && rifleShots[1] == FishingActionAudio.Clip("RifleShot2")
                && Private<AudioClip>(c.RifleHunting, "_shot") == rifleShots[0],
                "The actual rifle prefers both generated shot variants instead of its legacy fallback");
            var fishBank = Private<AudioClip[]>(c.ActionAudio, "_fishFlop");
            Check(fishBank.Length == 2 && fishBank[0] == FishingActionAudio.Clip("FishInspect1")
                && fishBank[1] == FishingActionAudio.Clip("FishInspect2"),
                "Both live-fish inspection hands share the generated struggle bank with no synthetic clips mixed in");
            Check(fishBank[0].length / .95f < FishingFishFlapClock.Period
                && fishBank[1].length / .95f < FishingFishFlapClock.Period,
                "Even at the slowest playback pitch each fish burst ends before the next animation peak");
            int generated = 0;
            foreach (string cue in new[] { "RodCast", "FloatSplash", "NetFishPlace", "FarmTill", "FarmPlant", "FarmWater", "FarmHarvest", "ShopPurchase" })
            {
                var first = ValidateClip(cue + "1"); var second = ValidateClip(cue + "2"); generated += 2;
                Check(first != second && UnityEditor.AssetDatabase.GetAssetPath(first).EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                    && UnityEditor.AssetDatabase.GetAssetPath(second).EndsWith(".wav", StringComparison.OrdinalIgnoreCase),
                    "Two distinct generated WAV variants are imported for " + cue);
            }
            Check(generated == 16, "All sixteen generated detail clips coexist with the original thirty-four action clips");
            int inspectionClips = 0;
            foreach (string cue in new[] { "PickaxeInspect", "RifleInspect", "FishInspect", "RifleShot" })
            {
                var first = ValidateClip(cue + "1"); var second = ValidateClip(cue + "2"); inspectionClips += 2;
                Check(first != second, "The inspection/fire bank contains two distinct imported variants: " + cue);
            }
            Check(inspectionClips == 8, "All eight generated pickaxe/rifle/fish inspection and gunshot clips are present");
            Check(!c.ActionAudio.Detail("not-a-cue", c.ExplorerPosition) && c.ActionAudio.DetailCount("not-a-cue") == 0
                && !c.ActionAudio.Detail("RodCast", new Vector3(float.NaN, 0, 0)),
                "Unknown cue names and invalid spatial coordinates cannot fabricate playback or counts");
        }

        private static AudioClip ValidateClip(string name)
        {
            var clip = FishingActionAudio.Clip(name);
            Check(clip != null && clip.samples > 32 && clip.channels == 1 && clip.frequency >= 16000 && clip.frequency <= 48000,
                "Imported mono action clip with a supported sample rate: " + name);
            var samples = new float[clip.samples * clip.channels];
            Check(clip.GetData(samples, 0), "Imported action clip can be decoded for validation: " + name);
            float peak = 0; bool finite = true;
            foreach (float sample in samples)
            { finite &= !float.IsNaN(sample) && !float.IsInfinity(sample); peak = Mathf.Max(peak, Mathf.Abs(sample)); }
            Check(finite && peak > .001f && peak <= 1.001f, "Decoded action clip contains finite, non-silent audio: " + name);
            return clip;
        }

        private static void ValidateSurfaces(FishingPrototype c)
        {
            var land = c.transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>();
            var mesh = land.sharedMesh;
            Check(mesh != null && mesh.isReadable, "The authored home-island support mesh is available for real surface probes");
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            bool sand = false, grass = false;
            for (int i = 0; i < triangles.Length && (!sand || !grass); i += 3)
            {
                var a = land.transform.TransformPoint(vertices[triangles[i]]);
                var b = land.transform.TransformPoint(vertices[triangles[i + 1]]);
                var d = land.transform.TransformPoint(vertices[triangles[i + 2]]);
                if (Vector3.Cross(b - a, d - a).normalized.y < .69f) continue;
                var point = (a + b + d) / 3;
                if (point.y < .8f) continue;
                var type = FishingFootstepSurface.Probe(c, point + Vector3.up * .025f);
                sand |= type == FishingGroundAudio.Sand; grass |= type == FishingGroundAudio.Grass;
            }
            Check(sand && grass, "Actual V035 home-island atlas triangles resolve to different sand and grass footstep banks");

            var material = new Material(c.PaletteShader) { name = "Audio support validation" };
            var colors = new[] { new Color(.80f, .70f, .40f), new Color(.24f, .52f, .18f), new Color(.5f, .5f, .5f) };
            var types = new[] { FishingGroundAudio.Sand, FishingGroundAudio.Grass, FishingGroundAudio.Stone };
            try
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    var fixture = new GameObject("Audio support fixture", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                    var test = new Mesh();
                    try
                    {
                        fixture.transform.position = new Vector3(3000 + i * 4, 2000, 3000);
                        test.vertices = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1) };
                        test.colors = new[] { colors[i], colors[i], colors[i] }; test.triangles = new[] { 0, 1, 2 };
                        test.RecalculateNormals(); test.RecalculateBounds();
                        fixture.GetComponent<MeshFilter>().sharedMesh = test;
                        fixture.GetComponent<MeshRenderer>().sharedMaterial = material;
                        fixture.GetComponent<MeshCollider>().sharedMesh = test; Physics.SyncTransforms();
                        Check(FishingFootstepSurface.Probe(c, fixture.transform.position + new Vector3(-.25f, .02f, -.25f)) == types[i],
                            "A real vertex-colour collision triangle resolves its interpolated surface: " + types[i]);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(fixture); UnityEngine.Object.DestroyImmediate(test); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }

            var data = new TerrainData { heightmapResolution = 33, alphamapResolution = 16, size = new Vector3(4, 1, 4) };
            var sandLayer = new TerrainLayer { name = "BeachSand_wet", diffuseTexture = Texture2D.whiteTexture };
            var grassLayer = new TerrainLayer { name = "Bush", diffuseTexture = Texture2D.whiteTexture };
            GameObject terrain = null;
            try
            {
                data.terrainLayers = new[] { sandLayer, grassLayer };
                var alpha = new float[16, 16, 2];
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) alpha[y, x, x < 8 ? 0 : 1] = 1;
                data.SetAlphamaps(0, 0, alpha);
                terrain = Terrain.CreateTerrainGameObject(data); terrain.transform.position = new Vector3(3020, 2000, 3000);
                terrain.GetComponent<Terrain>().drawHeightmap = false; Physics.SyncTransforms();
                Check(FishingFootstepSurface.Probe(c, terrain.transform.position + new Vector3(1, .02f, 2)) == FishingGroundAudio.Sand
                    && FishingFootstepSurface.Probe(c, terrain.transform.position + new Vector3(3, .02f, 2)) == FishingGroundAudio.Grass,
                    "A real TerrainCollider uses its painted dominant sand/grass layer, not the terrain object's name");
            }
            finally
            {
                if (terrain != null) UnityEngine.Object.DestroyImmediate(terrain);
                UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(sandLayer); UnityEngine.Object.DestroyImmediate(grassLayer);
            }
        }

        private static void ValidateSettings(FishingPrototype c)
        {
            var a = c.ActionAudio;
            var sources = new[] { a.FootstepSource, a.ItemSource, a.FishSource, a.InspectionSource };
            c.SetSetting("Effects", 0);
            foreach (var source in sources) Check(source != null && source.volume == 0 && !source.ignoreListenerVolume,
                "Effects mute reaches the registered action source: " + (source == null ? "missing" : source.name));
            c.SetSetting("Effects", .5f);
            Check(Mathf.Abs(a.FootstepSource.volume - .085f) < .00001f
                && Mathf.Abs(a.ItemSource.volume - .12f) < .00001f
                && Mathf.Abs(a.FishSource.volume - .075f) < .00001f && Mathf.Abs(a.InspectionSource.volume - .08f) < .00001f,
                "Effects gain scales each action source's original base volume");
            c.SetSetting("Master", .4f);
            Check(Mathf.Abs(AudioListener.volume - .4f) < .00001f, "Master gain reaches the common listener");
            c.ToggleQuiet(); int pickups = a.PickupSoundsPlayed, details = a.DetailCount("RodCast");
            a.Pickup(FieldItem.Stone, c.ExplorerPosition);
            Check(c.QuietMode && AudioListener.volume == 0 && a.FootstepSource.mute && a.ItemSource.mute
                && a.FishSource.mute && a.InspectionSource.mute && a.PickupSoundsPlayed == pickups,
                "Quiet mode mutes every action category and suppresses new action playback");
            Check(!a.Detail("RodCast", c.ExplorerPosition) && a.DetailCount("RodCast") == details,
                "Quiet mode also suppresses generated detail cues without accumulating replay counts");
            c.ToggleQuiet(); c.SetSetting("Master", 1); c.SetSetting("Effects", 1);
        }

        private static void ValidatePickup(FishingPrototype c)
        {
            int before = c.ActionAudio.PickupSoundsPlayed;
            bool collected = false;
            foreach (var pickup in c.FieldWorld.Pickups)
            {
                if (pickup.Root == null || !pickup.Root.gameObject.activeInHierarchy) continue;
                for (int angle = 0; angle < 360 && !collected; angle += 45)
                {
                    if (!c.FieldWorld.TryLocation(pickup.Feet, .85f, angle, out var feet)
                        || !c.Shore.Relocate(feet, Quaternion.identity)) continue;
                    Physics.SyncTransforms();
                    int owned = c.Career.Backpack.Count(pickup.Item);
                    collected = c.FieldWorld.PickupById(pickup.Id, pickup.Item, pickup.Count);
                    if (!collected) continue;
                    Check(c.ActionAudio.PickupSoundsPlayed == before + 1
                        && c.Career.Backpack.Count(pickup.Item) == owned + pickup.Count,
                        "One successful actual world pickup adds its item and exactly one pickup sound");
                    Check(!c.FieldWorld.PickupById(pickup.Id, pickup.Item, pickup.Count)
                        && c.ActionAudio.PickupSoundsPlayed == before + 1,
                        "A duplicate world pickup fails without playing another sound");
                }
                if (collected) break;
            }
            Check(collected, "The initialized scene supplies a reachable original pickup fixture");
            Check(!c.CollectFieldItem(-1, FieldItem.Stone, 1) && c.ActionAudio.PickupSoundsPlayed == before + 1,
                "An invalid atomic pickup receipt also remains silent");
        }

        private static void ValidateHarvest(FishingPrototype c)
        {
            int before = c.ActionAudio.HarvestSoundsPlayed;
            bool harvested = false;
            foreach (var node in c.WorkshopWorld.Nodes)
            {
                if (node.Item == FieldItem.Coal || node.Item == FieldItem.IronOre || node.Root == null) continue;
                for (int angle = 0; angle < 360 && !harvested; angle += 45)
                {
                    if (!c.FieldWorld.TryLocation(node.Feet, .9f, angle, out var feet)
                        || !c.Shore.Relocate(feet, Quaternion.identity)) continue;
                    Physics.SyncTransforms(); var tool = c.Career.Backpack.Equipped;
                    harvested = c.WorkshopWorld.HarvestNode(node.Id, node.Item, tool);
                    if (!harvested) continue;
                    Check(c.ActionAudio.HarvestSoundsPlayed == before + 1,
                        "One successful production resource harvest emits one contact sound");
                    Check(!c.WorkshopWorld.HarvestNode(node.Id, node.Item, tool)
                        && c.ActionAudio.HarvestSoundsPlayed == before + 1,
                        "A recovering resource cannot repeat its harvest sound or transaction");
                }
                if (harvested) break;
            }
            Check(harvested, "The initialized coast supplies a reachable non-mineral resource fixture");
        }

        private static bool FarmCommit(FishingPrototype c, int id, FarmWork work)
            => (bool)typeof(FishingFarmWorld).GetMethod("CommitAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(c.Farm, new object[] { id, work });

        private static IEnumerator ValidateFarmDetails(FishingPrototype c)
        {
            const int id = 0;
            var plot = c.Farm.Target(id);
            Check(plot != null && c.Ashore, "The action-audio fixture can reach the existing home garden");
            var stance = plot.position + Vector3.back * 1.1f + Vector3.left * .25f;
            Check(c.Shore.Relocate(stance, Quaternion.identity)
                && FishingPrototype.FlatDistance(c.Shore.Position, plot.position) < 2.25f,
                "The real player capsule is close enough to the first garden plot for production commits");
            c.View.ResetLook(); c.PresentCharacterFrame(); yield return null;
            var a = c.ActionAudio; var farm = c.Career.Farm; var bag = c.Career.Backpack;
            int till = a.DetailCount("FarmTill"), plant = a.DetailCount("FarmPlant"), water = a.DetailCount("FarmWater"), harvest = a.DetailCount("FarmHarvest");
            bool continuousWater=c.WorldAudio!=null && c.WorldAudio.HasCue("WaterPourLoop");
            int waterAfter=water+(continuousWater?0:1),pourStarts=c.WorldAudio==null?0:c.WorldAudio.LoopStarts("WaterPourLoop");
            Check(FarmCommit(c, id, FarmWork.Till) && farm.Plot(id).tilled && a.DetailCount("FarmTill") == till + 1,
                "A real successful till commit emits exactly one generated soil-contact cue");
            Check(!FarmCommit(c, id, FarmWork.Till) && a.DetailCount("FarmTill") == till + 1,
                "A stale duplicate till commit is silent");
            Check(!FarmCommit(c, id, FarmWork.Plant) && a.DetailCount("FarmPlant") == plant,
                "Planting without a seed fails without playing a seed sound");
            Check(bag.TryAdd(FieldItem.TurnipSeeds, 2), "The isolated garden fixture owns two real renewable seeds");
            int seeds = bag.Count(FieldItem.TurnipSeeds);
            Check(FarmCommit(c, id, FarmWork.Plant) && farm.Plot(id).planted && bag.Count(FieldItem.TurnipSeeds) == seeds - 1
                && a.DetailCount("FarmPlant") == plant + 1,
                "One committed planting consumes one seed and emits one generated planting cue");
            Check(!FarmCommit(c, id, FarmWork.Plant) && a.DetailCount("FarmPlant") == plant + 1,
                "A duplicate planting cannot play another cue or consume another seed");
            int charges = farm.Water;
            Check(!FarmCommit(c, id, FarmWork.Water) && !c.Farm.InteractAt(id) && farm.Water == charges
                && a.DetailCount("FarmWater") == water,
                "The current farm rejects watering without its real carried can and keeps the water cue silent");
            Check(bag.Count(FieldItem.StonePickaxe)>0 || bag.TryAdd(FieldItem.StonePickaxe,1),
                "The isolated can fixture owns a real pickaxe for the shared Z inspection route");
            int pickSlot=-1;
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(bag.At(i).item==FieldItem.StonePickaxe){pickSlot=i;break;}
            Check(pickSlot>=0 && pickSlot<FishingBackpack.HotbarCount,"The can fixture's real pickaxe is in a reachable hotbar slot");
            c.SelectFieldSlot(pickSlot);yield return null;
            var canApproach=ApproachAudioFarm(c,15);while(canApproach.MoveNext())yield return canApproach.Current;
            Check(c.RequestWorldInteraction() && c.Farm.Busy && !c.Farm.CarryingCan,
                "The real E route begins reaching for the persistent watering can before taking ownership");
            var canFinish=FinishAudioFarm(c);while(canFinish.MoveNext())yield return canFinish.Current;
            Check(c.Farm.CarryingCan && farm.Water==charges && a.DetailCount("FarmWater")==water,
                "Completed handle contact picks up the same can without consuming water or playing a watering cue");
            int pickInspection=a.DetailCount("PickaxeInspect");
            Check(!c.InspectFieldItem(pickSlot) && !c.InspectingItem && c.Farm.CarryingCan
                && a.DetailCount("PickaxeInspect")==pickInspection,
                "The shared Z inspection entry rejects a tool while the real can owns the hand, without queueing sound");
            canApproach=ApproachAudioFarm(c,id);while(canApproach.MoveNext())yield return canApproach.Current;
            Check(!FarmCommit(c,id,FarmWork.Water) && farm.Water==charges && a.DetailCount("FarmWater")==water,
                "Even a held can cannot bypass the real final-contact commit permission");
            Check(c.RequestWorldInteraction() && c.Farm.Busy && !c.RequestWorldInteraction()
                && farm.Water==charges && a.DetailCount("FarmWater")==water,
                "Starting the actual watering animation, or repeating its input, plays no premature watering cue");
            int canFrames=0;
            while(c.Farm.Busy && c.Farm.WorkProgress<.45f && canFrames++<600)yield return null;
            Check(c.Farm.Busy && c.Farm.WaterStreamVisible && farm.Water==charges && a.DetailCount("FarmWater")==water,
                "The actual tilted can pours visible water before the crop transaction and success cue");
            if(continuousWater)
            {
                // Audio leases are observed in LateUpdate, after the actual stream presentation.
                yield return null;
                Check(c.WorldAudio.LoopStarts("WaterPourLoop")==pourStarts+1 && c.WorldAudio.LoopSource("farm-water")!=null,
                    "A real visible water stream starts one continuous pour voice, rather than waiting for the final crop transaction");
            }
            canFinish=FinishAudioFarm(c);while(canFinish.MoveNext())yield return canFinish.Current;
            Check(farm.Water == charges - 1 && farm.Plot(id).wateredDay==farm.Day && a.DetailCount("FarmWater") == waterAfter,
                "Only completed real can contact consumes water; an available continuous stream replaces the old completion-only water recording");
            Check(!FarmCommit(c, id, FarmWork.Water) && a.DetailCount("FarmWater") == waterAfter,
                "An already watered plot rejects the same-day repeat without another water cue");
            canApproach=ApproachAudioFarm(c,15);while(canApproach.MoveNext())yield return canApproach.Current;
            Check(c.RequestWorldInteraction() && c.Farm.Busy && c.Farm.CarryingCan,
                "The can returns through its real stand-contact animation");
            canFinish=FinishAudioFarm(c);while(canFinish.MoveNext())yield return canFinish.Current;
            Check(!c.Farm.CarryingCan && farm.Water==charges-1 && a.DetailCount("FarmWater")==waterAfter,
                "Returning the can preserves saved water and cannot repeat its watering success sound");
            for(int frame=0;frame<8;frame++){c.PresentCharacterFrame();yield return null;}
            Check(c.InspectFieldItem(pickSlot),"After returning the actual can, the same Z inspection entry accepts the pickaxe");
            for(int frame=0;frame<20 && a.DetailCount("PickaxeInspect")==pickInspection;frame++)
            {c.PresentCharacterFrame();yield return null;}
            Check(c.FieldWorld.PresentedItem==FieldItem.StonePickaxe && a.DetailCount("PickaxeInspect")==pickInspection+1,
                "A real visible pickaxe inspection plays once after the watering can releases the hand");
            c.EndItemInspection();yield return null;
            canApproach=ApproachAudioFarm(c,id);while(canApproach.MoveNext())yield return canApproach.Current;
            Check(!FarmCommit(c, id, FarmWork.Harvest) && a.DetailCount("FarmHarvest") == harvest,
                "An immature crop cannot emit a successful harvest cue");
            int day = c.Career.Calendar.Day;
            // Advance only pure calendar/growth state, not the full farm suite, sleeping animation or navigation-map baker.
            for (int i = 0; i < FishingFarm.GrowingDays; i++)
            {
                c.Career.Calendar.RestUntilMorning();
                Check(farm.AdvanceDay(c.Career.Calendar.Day), "Pure calendar advances one watered farming night " + i);
                if (i + 1 < FishingFarm.GrowingDays) Check(farm.WaterPlot(id), "Pure setup supplies the next growth-day watering " + i);
            }
            c.Farm.Refresh();
            Check(c.Career.Calendar.Day == day + FishingFarm.GrowingDays && farm.Ready(id)
                && a.DetailCount("FarmWater") == waterAfter, "The short mature-crop fixture does not fabricate extra world-action water sounds");
            int turnips = bag.Count(FieldItem.Turnip); seeds = bag.Count(FieldItem.TurnipSeeds);
            Check(FarmCommit(c, id, FarmWork.Harvest) && bag.Count(FieldItem.Turnip) == turnips + 1
                && bag.Count(FieldItem.TurnipSeeds) == seeds + 1 && a.DetailCount("FarmHarvest") == harvest + 1,
                "The actual mature harvest transaction grants produce and one seed with exactly one harvest cue");
            Check(!FarmCommit(c, id, FarmWork.Harvest) && a.DetailCount("FarmHarvest") == harvest + 1,
                "Repeating an already harvested crop remains silent");
            yield return null;
        }

        private static IEnumerator ApproachAudioFarm(FishingPrototype c,int id)
        {
            var position=c.Farm.Target(id).position+(id==15?Vector3.left*.65f:Vector3.back*1.1f);
            position.y=c.Home.Origin.y;if(id<12)position.x-=.25f;
            Check(c.Shore.Relocate(position,Quaternion.identity),"The actor reaches actual farm audio target "+id);
            c.View.ResetLook();
            if(id==15)
            {
                float scale=.12f*c.MouseSensitivity;
                c.View.ApplyShoreLookDelta(new Vector2(90/scale,-55/scale*(c.InvertLook?-1:1)));
            }
            for(int frame=0;frame<30;frame++)
            {
                c.Shore.Move(Vector2.zero,false,false,false,Time.deltaTime);yield return null;
                if(frame>=3 && c.Shore.Grounded)break;
            }
            Check(c.Shore.Grounded,"The actor settles on the real farm floor at target "+id);
            FishingV036FarmValidation.Aim(c,c.Farm.Aim(id));yield return null;
            Check(c.Farm.TryTarget(out int actual,out _) && actual==id,"Production targeting resolves farm audio target "+id);
        }

        private static IEnumerator FinishAudioFarm(FishingPrototype c)
        {
            int frames=0;while(c.Farm.Busy && frames++<600)yield return null;
            Check(frames<600,"The real watering-can action reaches its final contact and releases movement");
            yield return null;yield return null;
        }

        private static IEnumerator ValidateShopDetails(FishingPrototype c)
        {
            const int product = 5; // Stackable rope avoids special equipment ownership or rifle-price routes.
            c.ReturnToHarbor(); yield return null;
            var funded = c.Career.Snapshot(); funded.coins = FishingSupplies.Price(product);
            c.RestoreShopTestProfile(funded); yield return null;
            var shop = c.TackleShop; var rb = c.Player.engine.RB;
            rb.position = shop.mooringPoint.position; rb.rotation = shop.transform.rotation;
            rb.velocity = rb.angularVelocity = Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position, rb.rotation); Physics.SyncTransforms();
            Check(c.Shore.TryDisembark() && c.Shore.Relocate(shop.purchasePoint.position, shop.transform.rotation),
                "The real shop berth and counter are reachable for an actual supply purchase");
            for (int i = 0; i < 5; i++) yield return null;
            Check(c.ToggleShop() && shop.CanPurchase, "The original store purchase context opens at the real counter");
            yield return null;
            int bought = c.ActionAudio.DetailCount("ShopPurchase"), owned = c.Career.Backpack.Count(FishingSupplies.Items[product]);
            Check(shop.BuySupply(product) && c.Career.Coins == 0
                && c.Career.Backpack.Count(FishingSupplies.Items[product]) == owned + FishingSupplies.Quantity(product)
                && c.ActionAudio.DetailCount("ShopPurchase") == bought + 1,
                "One real affordable purchase charges the exact price, grants the real supply and plays one transaction cue");
            Check(!shop.BuySupply(product) && c.ActionAudio.DetailCount("ShopPurchase") == bought + 1
                && c.Career.Backpack.Count(FishingSupplies.Items[product]) == owned + FishingSupplies.Quantity(product),
                "A second unaffordable purchase fails without duplicating the supply or purchase sound");
            int level = c.Career.Level(UpgradeTrack.Line), upgradeCost = c.Career.NextUpgradeCost(UpgradeTrack.Line);
            Check(upgradeCost > 0, "The real counter fixture has a purchasable line upgrade");
            funded = c.Career.Snapshot(); funded.coins = upgradeCost;
            c.RestoreShopTestProfile(funded); yield return null;
            int upgraded = c.ActionAudio.DetailCount("ShopPurchase");
            Check(shop.Buy(UpgradeTrack.Line) && c.Career.Coins == 0 && c.Career.Level(UpgradeTrack.Line) == level + 1
                && c.ActionAudio.DetailCount("ShopPurchase") == upgraded + 1,
                "One actual counter upgrade charges its exact quote and emits one purchase cue");
            Check(!shop.Buy(UpgradeTrack.Line) && c.Career.Coins == 0 && c.Career.Level(UpgradeTrack.Line) == level + 1
                && c.ActionAudio.DetailCount("ShopPurchase") == upgraded + 1,
                "An unaffordable follow-up upgrade does not level up or play another transaction cue");
            Check(c.Career.Backpack.Count(FieldItem.HuntingRifle) == 0, "The real counter fixture does not already own a rifle");
            funded = c.Career.Snapshot(); funded.coins = FishingCareer.HuntingRiflePrice;
            c.RestoreShopTestProfile(funded); yield return null;
            int rifles = c.ActionAudio.DetailCount("ShopPurchase");
            Check(shop.BuyRifle() && c.Career.Coins == 0 && c.Career.Backpack.Count(FieldItem.HuntingRifle) == 1
                && c.Career.Backpack.Equipped == FieldItem.HuntingRifle && c.ActionAudio.DetailCount("ShopPurchase") == rifles + 1,
                "One actual rifle purchase charges its exact price, equips the real gun and emits one transaction cue");
            funded = c.Career.Snapshot(); funded.coins = FishingCareer.HuntingRiflePrice;
            c.RestoreShopTestProfile(funded); yield return null;
            Check(!shop.BuyRifle() && c.Career.Coins == FishingCareer.HuntingRiflePrice
                && c.Career.Backpack.Count(FieldItem.HuntingRifle) == 1 && c.ActionAudio.DetailCount("ShopPurchase") == rifles + 1,
                "Even with enough money, an already-owned rifle fails without another gun, charge or purchase cue");
            shop.Close(); yield return null;
            Check(!shop.Buy(UpgradeTrack.Line) && !shop.BuyRifle()
                && c.Career.Coins == FishingCareer.HuntingRiflePrice && c.ActionAudio.DetailCount("ShopPurchase") == rifles + 1,
                "Closed-counter upgrade and rifle requests stay silent and cannot spend the player's money");
        }

        private static IEnumerator ValidateToolInspections(FishingPrototype c)
        {
            c.EndItemInspection(); c.Hands.ResetInteraction(); c.FieldWorld.ClearToolPresentation();
            if (!c.FirstPerson) c.ToggleView();
            c.View.ResetLook(); c.PresentCharacterFrame(); yield return null;
            Check((c.Career.Backpack.Count(FieldItem.StonePickaxe)>0 || c.Career.Backpack.TryAdd(FieldItem.StonePickaxe, 1))
                && (c.Career.Backpack.Count(FieldItem.IronPickaxe)>0 || c.Career.Backpack.TryAdd(FieldItem.IronPickaxe, 1)),
                "The isolated inspection fixture owns real stone and iron picks beside its purchased rifle");
            foreach (var item in new[] { FieldItem.StonePickaxe, FieldItem.IronPickaxe, FieldItem.HuntingRifle })
            {
                int slot = -1;
                for (int i = 0; i < FishingBackpack.SlotCount; i++) if (c.Career.Backpack.At(i).item == item) { slot = i; break; }
                Check(slot >= 0, "The real inspected tool exists in the backpack: " + item);
                string cue = item == FieldItem.HuntingRifle ? "RifleInspect" : "PickaxeInspect";
                int count = c.ActionAudio.DetailCount(cue);
                Check(c.InspectFieldItem(slot) && c.ActionAudio.DetailCount(cue) == count,
                    "Successful inspection queues, but does not play ahead of the visible hand pose: " + item);
                for (int frame = 0; frame < 12 && c.ActionAudio.DetailCount(cue) == count; frame++)
                { c.PresentCharacterFrame(); yield return null; }
                Check(c.InspectingItem && c.FieldWorld.PresentedItem == item && c.ActionAudio.DetailCount(cue) == count + 1,
                    "The actual visible inspected tool triggers exactly one generated handling sound: " + item);
                Check(c.InspectFieldItem(slot), "An active inspection remains safe to request again: " + item);
                for (int frame = 0; frame < 12; frame++)
                {
                    typeof(FishingPrototype).GetProperty("InspectionAngles").SetValue(c, new Vector2(frame, frame * 3));
                    c.PresentCharacterFrame(); yield return null;
                }
                Check(c.ActionAudio.DetailCount(cue) == count + 1,
                    "Turning the actual model and repeated same-tool requests never replay a per-frame inspection sound: " + item);
                c.EndItemInspection();
                Check(!c.ActionAudio.InspectionSource.isPlaying, "Cancelling inspection immediately stops its dedicated source: " + item);
                yield return null;
                Check(c.InspectFieldItem(slot), "A new inspection may begin after cancellation: " + item);
                c.EndItemInspection();
                for (int frame = 0; frame < 3; frame++) yield return null;
                Check(c.ActionAudio.DetailCount(cue) == count + 1,
                    "Cancellation before the model reaches its hand pose stays silent: " + item);
                c.TogglePause();
                Check(!c.InspectFieldItem(slot) && c.ActionAudio.DetailCount(cue) == count + 1
                    && !c.ActionAudio.InspectionSource.isPlaying,
                    "Paused inspection requests cannot trigger or queue handling sound: " + item);
                c.TogglePause(); yield return null;
            }
            int picks = c.ActionAudio.DetailCount("PickaxeInspect"), rifles = c.ActionAudio.DetailCount("RifleInspect");
            Check(!c.InspectFieldItem(-1) && !c.InspectFieldItem(FishingBackpack.SlotCount - 1)
                && c.ActionAudio.DetailCount("PickaxeInspect") == picks && c.ActionAudio.DetailCount("RifleInspect") == rifles,
                "Invalid or empty tool slots emit neither pickaxe nor rifle inspection sound");
        }

        private static void PlaceBoat(FishingPrototype c, Vector3 point)
        {
            var rb = c.Player.engine.RB; rb.position = point + Vector3.up * .7f; rb.rotation = Quaternion.identity;
            rb.velocity = rb.angularVelocity = Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position, rb.rotation); Physics.SyncTransforms(); c.View.ResetLook();
        }
        private static byte[] Read(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        private static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
