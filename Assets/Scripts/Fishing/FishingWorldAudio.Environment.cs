using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingWorldAudio
    {
        private enum EnvironmentSourceKind { Coast, Wind, Thunder, Rain }
        private sealed class EnvironmentBorrow
        {
            public AudioSource Source;
            public AudioClip Clip;
            public EnvironmentSourceKind Kind;
            public AudioLowPassFilter Filter;
            public bool OwnFilter, Enabled;
            public float Cutoff, Resonance;
        }
        private sealed class EnvironmentAnimalSample
        {
            public Vector3 Position;
            public float Distance;
        }

        private readonly List<EnvironmentBorrow> _environmentBorrowed = new List<EnvironmentBorrow>();
        private readonly List<Transform> _environmentTrees = new List<Transform>();
        private readonly Dictionary<Transform, EnvironmentAnimalSample> _environmentAnimals = new Dictionary<Transform, EnvironmentAnimalSample>();
        private readonly System.Random _environmentRandom = new System.Random(372031);
        private float _environmentShelter, _environmentRealmShelter, _environmentNextBind;
        private float _environmentBirdWait, _environmentLeafWait, _environmentAnimalWait, _environmentAnimalSampleAge;
        private int _environmentBirdCursor;
        private bool _environmentTreesScanned;
        public float EnvironmentShelter => _environmentShelter;

        partial void BuildEnvironment()
        {
            _environmentBirdWait = 8;
            _environmentLeafWait = 7;
            // Weather.Start and the final coastal dressing run after this component is built.
            _environmentNextBind = Time.unscaledTime + .75f;
        }

        private bool EnvironmentInside => _c != null && ((_c.Home != null && _c.Home.Inside)
            || (_c.TackleShop != null && _c.TackleShop.Inside));
        private bool EnvironmentInRealm => _c != null && _c.Realms != null && _c.Realms.Inside;

        partial void TickEnvironment(float dt)
        {
            if (_c == null || _c.Player == null || _c.viewCamera == null || !EnvironmentFinite(dt) || dt <= 0 || dt > .5f) return;
            if (Time.unscaledTime >= _environmentNextBind)
            {
                BindEnvironmentSources();
                if (!_environmentTreesScanned) FindEnvironmentTrees();
                _environmentNextBind = Time.unscaledTime + 5;
            }
            _environmentShelter = Mathf.MoveTowards(_environmentShelter, EnvironmentInside ? 1 : 0, dt * 1.4f);
            _environmentRealmShelter = Mathf.MoveTowards(_environmentRealmShelter, EnvironmentInRealm ? 1 : 0, dt * 1.4f);
            var listener = _c.viewCamera.transform.position;
            if (_c.Realms != null && _c.Realms.Transitioning) return;

            if (EnvironmentInRealm)
            {
                int island = _c.Realms.Island;
                string cue = island == 0 ? "WindCorridorLoop" : island == 1 ? "ForestLoop" : island == 2 ? "WreckLoop" : "StarsLoop";
                // Each room has a quiet sound bed, never a second musical layer.
                MaintainLoop("environment-realm-" + island, cue, listener, island == 3 ? .020f : .030f, false);
                return;
            }

            float rain = _c.Weather != null ? Mathf.Clamp01(_c.Weather.StormAmount) : 0;
            if (rain > .015f)
            {
                // The rain voice owns its own GameObject: a low-pass here cannot colour gunshots or UI.
                MaintainLoop("environment-rain", "RainLoop", listener, rain * .045f, false);
                BindRainVoice();
            }
            if (EnvironmentInside) return;
            TickEnvironmentBirds(dt, listener);
            TickEnvironmentAnimals(dt, listener);
            TickEnvironmentLeaves(dt, listener, rain);
        }

        private void BindEnvironmentSources()
        {
            foreach (var source in FindObjectsOfType<AudioSource>(true))
            {
                if (source == null || source.gameObject.scene != gameObject.scene || source.clip == null) continue;
                string name = source.clip.name;
                bool weather = _c.Weather != null && source.gameObject == _c.Weather.gameObject;
                if (weather && name == "Original soft coastal wind") BorrowEnvironment(source, EnvironmentSourceKind.Wind, false);
                else if (weather && name == "Original distant sea thunder") BorrowEnvironment(source, EnvironmentSourceKind.Thunder, false);
                else if (source.loop && (name == "ShoreWaves" || name == "ShoreRocks" || name == "Jetty" || name == "FlowingWater"))
                    BorrowEnvironment(source, EnvironmentSourceKind.Coast, SafeEnvironmentFilterHost(source));
            }
        }

        private static bool SafeEnvironmentFilterHost(AudioSource source)
        {
            // A Unity audio filter processes every source on its GameObject. Never attach one to
            // the prototype root (weather, rifle, feedback and prologue deliberately share it).
            return source != null && source.GetComponent<FishingPrototype>() == null
                && source.GetComponent<FishingWeather>() == null && source.GetComponents<AudioSource>().Length == 1;
        }

        private void BindRainVoice()
        {
            var source = LoopSource("environment-rain");
            if (source != null && SafeEnvironmentFilterHost(source)) BorrowEnvironment(source, EnvironmentSourceKind.Rain, true);
        }

        private void BorrowEnvironment(AudioSource source, EnvironmentSourceKind kind, bool filter)
        {
            foreach (var entry in _environmentBorrowed) if (entry.Source == source) return;
            var borrowed = new EnvironmentBorrow { Source = source, Clip = source.clip, Kind = kind };
            if (filter)
            {
                borrowed.Filter = source.GetComponent<AudioLowPassFilter>();
                if (borrowed.Filter == null)
                {
                    borrowed.Filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                    borrowed.OwnFilter = true;
                    borrowed.Filter.enabled = false;
                }
                borrowed.Enabled = borrowed.Filter.enabled;
                borrowed.Cutoff = borrowed.Filter.cutoffFrequency;
                borrowed.Resonance = borrowed.Filter.lowpassResonanceQ;
            }
            _environmentBorrowed.Add(borrowed);
        }

        partial void ApplyEnvironmentShelter()
        {
            if (_c == null || !_c.isActiveAndEnabled) return;
            for (int i = _environmentBorrowed.Count - 1; i >= 0; i--)
            {
                var entry = _environmentBorrowed[i];
                if (entry.Source == null || entry.Source.clip != entry.Clip)
                {
                    RestoreEnvironmentBorrow(entry);
                    _environmentBorrowed.RemoveAt(i);
                    continue;
                }
                float indoor = entry.Kind == EnvironmentSourceKind.Rain ? .48f
                    : entry.Kind == EnvironmentSourceKind.Thunder ? .32f : .30f;
                float attenuation = Mathf.Lerp(1, indoor, _environmentShelter) * Mathf.Lerp(1, .02f, _environmentRealmShelter);
                // This is an output gain only. Do not overwrite the controller's authored base, and
                // do not multiply last frame's volume: sliders and weather continue to work normally.
                entry.Source.volume = _c.GetAudioChannelVolume(entry.Source) * attenuation;
                if (entry.Filter == null) continue;
                float cover = Mathf.Max(_environmentShelter, _environmentRealmShelter);
                if (cover <= .001f)
                {
                    entry.Filter.cutoffFrequency = entry.Cutoff;
                    entry.Filter.lowpassResonanceQ = entry.Resonance;
                    entry.Filter.enabled = entry.Enabled;
                }
                else
                {
                    float original = entry.Enabled ? entry.Cutoff : 22000;
                    entry.Filter.enabled = true;
                    entry.Filter.cutoffFrequency = Mathf.Lerp(original, Mathf.Min(original, entry.Kind == EnvironmentSourceKind.Rain ? 3000 : 2100), cover);
                    entry.Filter.lowpassResonanceQ = entry.Resonance;
                }
            }
        }

        partial void ResetEnvironment()
        {
            foreach (var entry in _environmentBorrowed) RestoreEnvironmentBorrow(entry);
            _environmentBorrowed.Clear();
            _environmentAnimals.Clear();
            _environmentShelter = _environmentRealmShelter = _environmentAnimalSampleAge = 0;
            _environmentBirdWait = Mathf.Max(_environmentBirdWait, 5);
            _environmentLeafWait = Mathf.Max(_environmentLeafWait, 4);
            _environmentAnimalWait = .5f;
            _environmentNextBind = Time.unscaledTime + .3f;
        }

        private void RestoreEnvironmentBorrow(EnvironmentBorrow entry)
        {
            // The controller restores the full original scene mix when it is disabled. Do not
            // replace that restoration with a fishing-scene mix during GameObject teardown.
            if (entry.Source != null && entry.Source.clip == entry.Clip && _c != null && _c.isActiveAndEnabled)
                entry.Source.volume = _c.GetAudioChannelVolume(entry.Source);
            if (entry.Filter == null) return;
            entry.Filter.cutoffFrequency = entry.Cutoff;
            entry.Filter.lowpassResonanceQ = entry.Resonance;
            entry.Filter.enabled = entry.Enabled;
            if (entry.OwnFilter) Destroy(entry.Filter);
        }

        private void TickEnvironmentBirds(float dt, Vector3 listener)
        {
            _environmentBirdWait -= dt;
            if (_environmentBirdWait > 0 || _c.RifleHunting == null || !HasCue("SeagullCall")) return;
            var targets = _c.RifleHunting.Targets;
            for (int step = 0; step < targets.Count; step++)
            {
                int index = (_environmentBirdCursor + step) % targets.Count;
                var animal = targets[index];
                if (animal.Kind != LandAnimalKind.Gull || animal.Dead || animal.Root == null || !animal.Root.gameObject.activeInHierarchy
                    || (animal.Root.position - listener).sqrMagnitude > 45 * 45) continue;
                Play("SeagullCall", animal.Root.position, .30f, FishingAudioCategory.Ambient);
                _environmentBirdCursor = index + 1;
                _environmentBirdWait = 15 + (float)_environmentRandom.NextDouble() * 17;
                return;
            }
            _environmentBirdWait = 3;
        }

        private void TickEnvironmentAnimals(float dt, Vector3 listener)
        {
            _environmentAnimalWait = Mathf.Max(0, _environmentAnimalWait - dt);
            _environmentAnimalSampleAge += dt;
            if (_environmentAnimalSampleAge < .18f || _c.FieldWorld == null) return;
            _environmentAnimalSampleAge = 0;
            foreach (var animal in _c.FieldWorld.Animals)
            {
                if (animal.Root == null) continue;
                var root = animal.Root;
                if (!_environmentAnimals.TryGetValue(root, out var sample))
                {
                    _environmentAnimals[root] = new EnvironmentAnimalSample { Position = root.position };
                    continue;
                }
                float distance = Vector3.ProjectOnPlane(root.position - sample.Position, Vector3.up).magnitude;
                sample.Position = root.position;
                bool landMammal = animal.Kind == LandAnimalKind.Rabbit || animal.Kind == LandAnimalKind.Fox;
                if (!landMammal || animal.Dead || !root.gameObject.activeInHierarchy || distance > 1.5f
                    || (root.position - listener).sqrMagnitude > 12 * 12 || distance < .015f)
                { sample.Distance = 0; continue; }
                sample.Distance += distance;
                if (_environmentAnimalWait > 0 || sample.Distance < (animal.Fleeing ? .65f : .9f)) continue;
                // A rabbit crossing a stone jetty should not rustle foliage under its feet.
                if (FishingFootstepSurface.Probe(_c, animal.Feet) != FishingGroundAudio.Grass) { sample.Distance = 0; continue; }
                Play("AnimalRustle", root.position + Vector3.up * .12f, animal.Fleeing ? .30f : .19f, FishingAudioCategory.Ambient);
                sample.Distance = 0;
                _environmentAnimalWait = .65f + (float)_environmentRandom.NextDouble() * .45f;
            }
        }

        private void FindEnvironmentTrees()
        {
            _environmentTrees.Clear();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var tree in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!EnvironmentTreeName(tree.name)) continue;
                    // Imported mesh children may repeat the prefab's name. Keep one sound point.
                    if (tree.parent != null && EnvironmentTreeName(tree.parent.name)) continue;
                    _environmentTrees.Add(tree);
                    if (_environmentTrees.Count >= 512) { _environmentTreesScanned = true; return; }
                }
            _environmentTreesScanned = true;
        }

        private static bool EnvironmentTreeName(string name) => name == "OrchardTree"
            || name.StartsWith("tree_palm", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("tree_oak", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("tree_pine", StringComparison.OrdinalIgnoreCase)
            || name == "DunePalm" || name == "DunePalm(Clone)"
            || name == "MangroveTree" || name == "MangroveTree(Clone)"
            || name == "AncientTree" || name == "AncientTree(Clone)";

        private void TickEnvironmentLeaves(float dt, Vector3 listener, float rain)
        {
            _environmentLeafWait -= dt;
            if (_environmentLeafWait > 0 || !_c.Ashore || !HasCue("FruitShake")) return;
            Transform nearest = null;
            float best = 13 * 13;
            foreach (var tree in _environmentTrees)
            {
                if (tree == null || !tree.gameObject.activeInHierarchy) continue;
                var point = tree.position + Vector3.up * 2.5f;
                float distance = (point - listener).sqrMagnitude;
                if (distance < best) { best = distance; nearest = tree; }
            }
            if (nearest != null)
                Play("FruitShake", nearest.position + Vector3.up * 2.5f, Mathf.Lerp(.075f, .12f, rain), FishingAudioCategory.Ambient);
            _environmentLeafWait = 9 + (float)_environmentRandom.NextDouble() * 10;
        }

        private static bool EnvironmentFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
