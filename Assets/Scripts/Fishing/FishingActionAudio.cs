using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingActionAudio ActionAudio { get; private set; }
    }

    /// <summary>Action feedback only. Never changes inventory, motion, rewards or music.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class FishingActionAudio : MonoBehaviour
    {
        private FishingPrototype _c;
        private AudioSource _feet, _items, _fish;
        private readonly FishingFootstepClock _steps = new FishingFootstepClock();
        private readonly FishingFishFlapClock _flaps = new FishingFishFlapClock();
        private readonly System.Random _random = new System.Random(211067);
        private readonly AudioClip[][] _footClips = new AudioClip[4][];
        private readonly int[] _lastFoot = { -1, -1, -1, -1 };
        private AudioClip[] _stonePickup, _woodPickup, _softPickup, _stoneHit, _fishFlop;
        private readonly Dictionary<string, AudioClip[]> _detailClips = new Dictionary<string, AudioClip[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _detailCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _lastDetail = new Dictionary<string, int>(StringComparer.Ordinal);
        private Vector3 _lastFeet;
        private bool _sampled;
        private string _rightFishId;
        private float _rightFishTime;

        public int FootstepsPlayed { get; private set; }
        public int PickupSoundsPlayed { get; private set; }
        public int HarvestSoundsPlayed { get; private set; }
        public int FishFlapsPlayed { get; private set; }
        public FishingGroundAudio LastFootstepSurface { get; private set; }
        public AudioSource FootstepSource => _feet;
        public AudioSource ItemSource => _items;
        public AudioSource FishSource => _fish;

        public static AudioClip Clip(string name) => Resources.Load<AudioClip>("FishingAudio/SFX/" + name);
        private static AudioClip[] Clips(string prefix, int count)
        {
            var result = new AudioClip[count];
            for (int i = 0; i < count; i++) result[i] = Clip(prefix + (i + 1));
            return result;
        }
        public void Build(FishingPrototype controller)
        {
            _c = controller;
            _feet = Source("Surface footsteps", .17f, .65f);
            _items = Source("Successful resource interaction", .24f, .45f);
            _fish = Source("Held live fish movement", .15f, .45f);
            _footClips[0] = Clips("StepSand", 4); _footClips[1] = Clips("StepGrass", 4);
            _footClips[2] = Clips("StepWood", 4); _footClips[3] = Clips("StepStone", 4);
            _stonePickup = Clips("StonePickup", 3); _woodPickup = Clips("WoodPickup", 3);
            _softPickup = Clips("SoftPickup", 3); _stoneHit = Clips("StoneHit", 3);
            _fishFlop = Clips("FishFlop", 3);
        }
        private AudioSource Source(string label, float volume, float spatial)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = spatial;
            source.dopplerLevel = 0; source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2; source.maxDistance = 18;
            _c.RegisterAudioSource(source, FishingAudioCategory.Effects, volume);
            return source;
        }
        private bool Audible => _c != null && _c.Career != null && !_c.Paused && !_c.QuietMode
            && !_c.StartMenuOpen && (_c.Prologue == null || !_c.Prologue.Open)
            && (_c.Home == null || !_c.Home.Sleeping) && (_c.Automated || Application.isFocused);
        private AudioClip Pick(AudioClip[] clips) => clips == null || clips.Length == 0 ? null : clips[_random.Next(clips.Length)];
        private bool Play(AudioSource source, AudioClip clip, Vector3 at, float gain = 1)
        {
            if (!Audible || source == null || clip == null) return false;
            source.transform.position = at;
            source.pitch = .95f + (float)_random.NextDouble() * .10f;
            source.PlayOneShot(clip, gain); return true;
        }
        private static bool Mineral(FieldItem item) => item == FieldItem.Stone || item == FieldItem.IronOre
            || item == FieldItem.Coal || item == FieldItem.Clay || item == FieldItem.Shell || item == FieldItem.TurtleShell;
        private static bool Timber(FieldItem item) => item == FieldItem.Driftwood || item == FieldItem.Stick || item == FieldItem.Plank;
        public void Pickup(FieldItem item, Vector3 at)
        {
            if (Play(_items, Pick(Mineral(item) ? _stonePickup : Timber(item) ? _woodPickup : _softPickup), at)) PickupSoundsPlayed++;
        }
        public void Harvest(FieldItem item, Vector3 at)
        {
            if (Play(_items, Pick(Mineral(item) ? _stoneHit : Timber(item) ? _woodPickup : _softPickup), at)) HarvestSoundsPlayed++;
        }
        public int DetailCount(string cue)
            => cue != null && _detailCounts.TryGetValue(cue, out int count) ? count : 0;
        /// <summary>One successful action cue. Missing imported variants stay silent; no synthesized substitute or game transaction.</summary>
        public bool Detail(string cue, Vector3 at)
        {
            if (!Audible || !DetailCue(cue) || !Finite(at.x) || !Finite(at.y) || !Finite(at.z)) return false;
            if (!_detailClips.TryGetValue(cue, out var clips) || clips.Length < 2)
            {
                var first = Clip(cue + "1"); var second = Clip(cue + "2");
                if (first == null && second == null) return false;
                clips = first != null && second != null ? new[] { first, second }
                    : new[] { first != null ? first : second };
                _detailClips[cue] = clips;
            }
            int choice = _lastDetail.TryGetValue(cue, out int last) && clips.Length > 1
                ? (last + 1) % clips.Length : _random.Next(clips.Length);
            if (!Play(_items, clips[choice], at, .7f)) return false;
            _lastDetail[cue] = choice;
            _detailCounts[cue] = DetailCount(cue) + 1;
            return true;
        }
        private static bool DetailCue(string cue) => cue == "RodCast" || cue == "FloatSplash" || cue == "NetFishPlace"
            || cue == "FarmTill" || cue == "FarmPlant" || cue == "FarmWater" || cue == "FarmHarvest" || cue == "ShopPurchase";
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void LateUpdate()
        {
            if (_c == null || _c.Career == null || _c.Shore == null) return;
            float dt = Time.deltaTime;
            Vector3 feet = _c.Shore.Position;
            var delta = _sampled ? feet - _lastFeet : Vector3.zero;
            _sampled = true; _lastFeet = feet;
            bool walking = Audible && _c.Ashore && _c.Shore.Body.enabled && !_c.Shore.Swimming && !_c.Shore.Climbing
                && !_c.StoryBlocksGameplay && !_c.ShopBlocksGameplay && !_c.WorldActionBlocksGameplay
                && !_c.InventoryOpen && !_c.JournalOpen && !_c.ShowDetails
                && (_c.Expedition == null || !_c.Expedition.Aligning)
                && (_c.XRNotebook == null || !_c.XRNotebook.BlocksGameplay);
            // Room-scale XR moves may have a horizontal-only Move call; sample the nearby support too.
            bool grounded = walking && (_c.Shore.Body.isGrounded || (_c.Shore.Ground(feet, out var ground)
                && feet.y - ground.y >= -.03f && feet.y - ground.y < .10f));
            if (_steps.Tick(new Vector2(delta.x, delta.z).magnitude, delta.y, dt, walking, grounded, _c.Shore.Crouched))
            {
                var surface = FishingFootstepSurface.Probe(_c, feet);
                int index = surface == FishingGroundAudio.Grass ? 1 : surface == FishingGroundAudio.Wood ? 2
                    : surface == FishingGroundAudio.Stone ? 3 : 0;
                var clips = _footClips[index];
                int choice = _lastFoot[index] < 0 ? _random.Next(clips.Length)
                    : (_lastFoot[index] + 1 + _random.Next(clips.Length - 1)) % clips.Length;
                _lastFoot[index] = choice;
                if (Play(_feet, clips[choice], feet + Vector3.up * .12f, _c.Shore.Crouched ? .65f : 1))
                { FootstepsPlayed++; LastFootstepSurface = surface; }
            }
            if (!walking && _feet.isPlaying) _feet.Stop();
            TickFish(dt);
            if (!Audible) { _feet.Stop(); _items.Stop(); _fish.Stop(); }
        }
        private void TickFish(float dt)
        {
            var rig = _c.CatchRig;
            if (!_c.HasPendingCatch || rig == null || _c.Career.PendingCatch?.fish == null)
            { _flaps.Reset(); _rightFishId = null; _rightFishTime = 0; _fish.Stop(); return; }
            string id = _c.Career.PendingCatch.fish.id;
            bool left = rig.Location == FishingCatchRig.Place.LeftHand;
            bool held = left || rig.Location == FishingCatchRig.Place.Hand;
            bool playing = Audible && held && !rig.PlacingInNet && !_c.StoryBlocksGameplay
                && !_c.ShopBlocksGameplay && !_c.InventoryOpen && !_c.JournalOpen
                && (_c.XRNotebook == null || !_c.XRNotebook.BlocksGameplay);
            if (_rightFishId != id || !held) { _rightFishId = id; _rightFishTime = 0; }
            if (!left && playing && dt > 0 && !float.IsNaN(dt) && !float.IsInfinity(dt)) _rightFishTime += Mathf.Min(dt, .05f);
            float time = left ? rig.InspectionMotionTime : _rightFishTime;
            if (_flaps.Tick(id, time, playing && (!left || rig.LeftInspectionSettled))
                && Play(_fish, Pick(_fishFlop), _c.ActiveFish.position)) FishFlapsPlayed++;
            if (!playing && _fish.isPlaying) _fish.Stop();
        }
        private void OnDisable()
        {
            _sampled = false; _steps.Reset(); _flaps.Reset();
            if (_feet != null) _feet.Stop(); if (_items != null) _items.Stop(); if (_fish != null) _fish.Stop();
        }
    }
}
