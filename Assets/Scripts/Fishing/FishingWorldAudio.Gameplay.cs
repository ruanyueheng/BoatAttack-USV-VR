using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingWorldAudio
    {
        private FishingCareer _gameplayCareer;
        private CatchPhase _gameplayPhase;
        private bool _gameplayLeaping, _gameplaySampled;
        private float _gameplayFishStore = -1;
        private readonly Dictionary<int, int> _gameplayFurnaceReady = new Dictionary<int, int>();

        partial void BuildGameplay() => ResetGameplay();
        partial void ResetGameplay()
        {
            _gameplayCareer = _c == null ? null : _c.Career;
            _gameplayPhase = _c == null || _c.Session == null ? CatchPhase.Idle : _c.Session.Phase;
            _gameplayLeaping = _c != null && _c.Session != null && _c.Session.Phase == CatchPhase.Fighting && _c.Session.LeapProgress > 0;
            _gameplaySampled = false; _gameplayFishStore = -1; _gameplayFurnaceReady.Clear();
        }

        /// <summary>Only the actual successful fish-box transaction queues its final landing contact.</summary>
        public void NotifyFishStored() => _gameplayFishStore = .7f;

        /// <summary>Called at a real raycast impact. Animal hitboxes use their own successful kill hook.</summary>
        public bool NotifyProjectileImpact(RaycastHit hit)
        {
            var collider = hit.collider;
            if (collider == null || collider.isTrigger || FishingShore.NonSolidSurface(collider)) return false;
            var surface = FishingFootstepSurface.FromHit(hit);
            string names = collider.name;
            var ancestor = collider.transform.parent;
            for (int i = 0; i < 3 && ancestor != null; i++, ancestor = ancestor.parent) names += " " + ancestor.name;
            names = names.ToLowerInvariant();
            string cue = names.Contains("metal") || names.Contains("iron") || names.Contains("furnace") || names.Contains("forge") || names.Contains("铜") || names.Contains("铁")
                ? "ImpactMetal" : surface == FishingGroundAudio.Sand || surface == FishingGroundAudio.Grass
                ? "BodyLand" : surface == FishingGroundAudio.Wood || names.Contains("wood") || names.Contains("board") || names.Contains("chest") || names.Contains("tree") || names.Contains("palm")
                    || names.Contains("boat") || names.Contains("dock") || names.Contains("cottage") || names.Contains("木") || names.Contains("树") || names.Contains("屋")
                ? "ImpactWood" : collider is TerrainCollider ? "BodyLand" : "ImpactStone";
            return Play(cue, hit.point, .5f);
        }

        partial void TickGameplay(float dt)
        {
            if (_c == null || _c.Career == null) return;
            if (_gameplayCareer != _c.Career) ResetGameplay();
            var phase = _c.Session.Phase;
            bool leaping = phase == CatchPhase.Fighting && _c.Session.LeapProgress > 0;
            bool world = !_c.Paused && !_c.QuietMode && !_c.StartMenuOpen && (_c.Prologue == null || !_c.Prologue.Open)
                && (_c.Automated || Application.isFocused) && dt > 0 && dt < .25f;
            if (_gameplaySampled && world)
            {
                if (phase == CatchPhase.Escaped && phase != _gameplayPhase
                    && (_c.Session.Failure == CatchFailure.LineSnapped || _c.Session.Failure == CatchFailure.LeapSnapped))
                    Play("LineSnap", _c.ExplorerPosition + Vector3.up, .65f);
                // Line hum follows live tension: louder and higher as the fish pulls, silent outside the fight.
                if ((phase == CatchPhase.Fighting || phase == CatchPhase.Landing) && _c.Session.Tension > .05f)
                {
                    float tension = Mathf.Clamp01(_c.Session.Tension);
                    MaintainLoop("line-tension", "LineTensionLoop", _c.ExplorerPosition + Vector3.up * 1.2f,
                        Mathf.Lerp(.015f, .09f, tension * tension), true, 12, FishingAudioCategory.Effects);
                    var line = LoopSource("line-tension"); if (line != null) line.pitch = Mathf.Lerp(.85f, 1.45f, tension);
                }
                // One splash at takeoff and one at water re-entry. Cancellation/reposition never fabricates a splash.
                if (phase == CatchPhase.Fighting && _gameplayPhase == CatchPhase.Fighting && leaping != _gameplayLeaping && _c.ActiveFish != null)
                    Play("FishSplash", _c.ActiveFish.position, leaping ? .48f : .63f);
            }
            _gameplayPhase = phase; _gameplayLeaping = leaping; _gameplaySampled = true;
            if (!world) _gameplayFishStore = -1;
            if (_gameplayFishStore >= 0 && world)
            {
                _gameplayFishStore -= dt;
                if (_gameplayFishStore <= 0) { _gameplayFishStore = -1; if (_c.FishBox != null) Play("FishStore", _c.FishBox.position, .5f); }
            }
            if (_c.WorkshopWorld != null)
                foreach (var station in _c.Career.Workshop.Stations)
                {
                    var root = _c.WorkshopWorld.StationRoot(station.id);
                    if (root == null || !root.gameObject.activeInHierarchy) continue;
                    if (world && station.kind == CraftStation.Furnace && station.remaining > 0 && Vector3.Distance(_c.ExplorerPosition,root.position) < 16)
                        MaintainLoop("furnace:" + station.id, "FurnaceLoop", root.position + Vector3.up * .5f, .04f, true, 13);
                    if (world && _gameplayFurnaceReady.TryGetValue(station.id, out int ready) && station.ready > ready)
                        Play("CraftFinish", root.position + Vector3.up * .6f, .4f);
                    _gameplayFurnaceReady[station.id] = station.ready;
                }
            if (!world) return;
            var barbecue = _c.Career.Barbecue;
            if (_c.CoastWorld != null && _c.CoastWorld.Grill != null && barbecue.fish != null && barbecue.remaining > 0 && barbecue.remaining < 17
                && !_c.CoastWorld.Grill.TryGetComponent<FishingBarbecueEffects>(out _)
                && Vector3.Distance(_c.ExplorerPosition,_c.CoastWorld.CookingPoint) < 13)
                MaintainLoop("barbecue", "CookSizzleLoop", _c.CoastWorld.CookingPoint, .04f, true, 10);
            if (_c.Home != null && _c.Home.TelevisionOn && _c.Home.TelevisionScreen != null
                && Vector3.Distance(_c.ExplorerPosition,_c.Home.TelevisionScreen.bounds.center) < 11)
                MaintainLoop("television", "TVLoop", _c.Home.TelevisionScreen.bounds.center, .025f, true, 8);
        }
    }
}
