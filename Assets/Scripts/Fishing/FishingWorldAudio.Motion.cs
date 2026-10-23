using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using WaterSystem;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingWorldAudio
    {
        private bool _motionSampled, _motionAshore, _motionSwimming, _motionClimbing, _motionGrounded;
        private Vector3 _motionFeet, _motionBoatPosition;
        private float _motionAirTime, _motionFallDistance, _motionDownSpeed, _motionStrokeDistance, _motionClimbDistance;
        private float _motionNextWater, _motionNextLanding, _motionNextBoatSlap, _motionNextImpact, _motionContactGrace;
        private bool _motionBoatSampled;
        private float _motionBoatWater;
        private Rigidbody _motionBoatBody;
        private FishingBoatContactAudio _motionBoatContact;
        private NativeArray<float3> _motionWaterPoints;
        private float3[] _motionWaterHeights = new float3[2];
        private float3[] _motionWaterNormals = new float3[2];
        private int _motionWaterWarm;

        private sealed class BoatAudioContact
        {
            public Vector3 Point;
            public float Tangent, At;
        }
        private readonly Dictionary<int, BoatAudioContact> _motionContacts = new Dictionary<int, BoatAudioContact>();

        partial void BuildMotion()
        {
            BindMotionBoat();
            ResetMotion();
        }

        private void BindMotionBoat()
        {
            var body = _c == null || _c.Player == null || _c.Player.engine == null ? null : _c.Player.engine.RB;
            if (body == _motionBoatBody && _motionBoatContact != null) return;
            if (_motionBoatContact != null) _motionBoatContact.Unbind(this);
            _motionBoatBody = body; _motionBoatContact = null; _motionBoatSampled = false;
            if (body == null) return;
            _motionBoatContact = body.GetComponent<FishingBoatContactAudio>();
            if (_motionBoatContact == null) _motionBoatContact = body.gameObject.AddComponent<FishingBoatContactAudio>();
            _motionBoatContact.Bind(this);
        }

        partial void ResetMotion()
        {
            _motionSampled = _motionBoatSampled = false;
            _motionAirTime = _motionFallDistance = _motionDownSpeed = _motionStrokeDistance = _motionClimbDistance = 0;
            _motionWaterWarm = 0; _motionContacts.Clear();
            _motionNextWater = _motionNextLanding = Time.time + .25f;
            _motionNextBoatSlap = Time.time + 2.5f;
            _motionContactGrace = Time.time + .3f;
        }

        partial void DisposeMotion()
        {
            GerstnerWavesJobs.RemoveSamplePoints(GetInstanceID());
            if (_motionWaterPoints.IsCreated) _motionWaterPoints.Dispose();
            if (_motionBoatContact != null)
            {
                _motionBoatContact.Unbind(this);
                Destroy(_motionBoatContact);
            }
            _motionContacts.Clear();
        }

        private bool MotionContext => isActiveAndEnabled && _c != null && _c.isActiveAndEnabled && _c.Shore != null
            && !_c.Paused && !_c.QuietMode && !_c.StartMenuOpen && (_c.Prologue == null || !_c.Prologue.Open)
            && (_c.Automated || Application.isFocused);
        private bool MotionTransferBlocked => _c.StoryBlocksGameplay || _c.ShopBlocksGameplay || _c.WorldActionBlocksGameplay
            || _c.InventoryOpen || _c.JournalOpen || _c.ShowDetails || (_c.Expedition != null && _c.Expedition.Aligning)
            || (_c.XRNotebook != null && _c.XRNotebook.BlocksGameplay);

        /// <summary>Called only by successful TryBoard/TryDisembark, never by rescue/load/portal relocation.</summary>
        public void NotifyBoardingContact()
        {
            if (!MotionContext) return;
            var feet = _c.Shore.Position;
            string cue = !_c.Ashore ? "ImpactWood" : LandingCue(FishingFootstepSurface.Probe(_c, feet));
            Play(cue, feet + Vector3.up * .1f, .36f);
            _motionSampled = false; _motionWaterWarm = 0; _motionContacts.Clear();
            _motionContactGrace = Time.time + .35f;
        }

        partial void TickMotion(float dt)
        {
            BindMotionBoat();
            if (!MotionContext || !MotionFinite(dt) || dt <= 0 || dt > .25f) { ResetMotion(); return; }
            var shore = _c.Shore; var feet = shore.Position;
            if (!MotionFinite(feet.x) || !MotionFinite(feet.y) || !MotionFinite(feet.z)) { ResetMotion(); return; }
            SampleMotionWater(feet);
            TickMotionBoat(dt);
            bool grounded = shore.Ashore && shore.Body != null && shore.Body.enabled && shore.Grounded;
            if (!_motionSampled)
            {
                StoreMotionPose(feet, grounded); _motionSampled = true; return;
            }
            var delta = feet - _motionFeet;
            bool transfer = shore.Ashore != _motionAshore;
            bool discontinuity = delta.magnitude > Mathf.Max(1.5f, dt * 9f);
            if (transfer || discontinuity || !shore.Ashore || MotionTransferBlocked || shore.Body == null || !shore.Body.enabled)
            {
                ClearMotionDistances(); StoreMotionPose(feet, grounded);
                if (transfer || discontinuity) { _motionWaterWarm = 0; _motionContacts.Clear(); _motionContactGrace = Time.time + .35f; }
                return;
            }

            if (shore.Swimming != _motionSwimming && !_motionClimbing && !shore.Climbing && Time.time >= _motionNextWater)
            {
                Play("WaterEntry", feet + Vector3.up * .45f, shore.Swimming ? .7f : .4f);
                _motionNextWater = Time.time + .45f;
                ClearMotionDistances();
            }

            if (shore.Climbing)
            {
                _motionClimbDistance += delta.magnitude;
                if (_motionClimbDistance >= .55f && delta.magnitude > .001f)
                {
                    Play("ClimbGrip", feet + Vector3.up * 1.1f, .5f);
                    _motionClimbDistance = Mathf.Min(.2f, _motionClimbDistance - .55f);
                }
                _motionAirTime = _motionFallDistance = _motionDownSpeed = _motionStrokeDistance = 0;
            }
            else if (shore.Swimming)
            {
                _motionStrokeDistance += Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
                if (_motionStrokeDistance >= .85f && Time.time >= _motionNextWater && shore.WalkSpeed > .12f)
                {
                    Play("WaterStroke", feet + Vector3.up * 1.1f, .5f);
                    _motionStrokeDistance = Mathf.Min(.2f, _motionStrokeDistance - .85f);
                    _motionNextWater = Time.time + .55f;
                }
                _motionAirTime = _motionFallDistance = _motionDownSpeed = _motionClimbDistance = 0;
            }
            else
            {
                if (!grounded)
                {
                    _motionAirTime += dt;
                    _motionFallDistance += Mathf.Max(0, -delta.y);
                    _motionDownSpeed = Mathf.Max(_motionDownSpeed, -delta.y / dt);
                }
                else
                {
                    if (!_motionGrounded && !_motionSwimming && !_motionClimbing && _motionAirTime >= .13f
                        && _motionFallDistance > .14f && _motionDownSpeed > .8f && Time.time >= _motionNextLanding)
                    {
                        var surface = FishingFootstepSurface.Probe(_c, feet);
                        Play(LandingCue(surface), feet + Vector3.up * .08f, Mathf.Lerp(.28f, .75f, Mathf.InverseLerp(.8f, 7f, _motionDownSpeed)));
                        _motionNextLanding = Time.time + .35f;
                    }
                    _motionAirTime = _motionFallDistance = _motionDownSpeed = 0;
                }
                _motionClimbDistance = 0;
                bool shallow = grounded && _motionWaterWarm >= 3 && (_c.Realms == null || !_c.Realms.Inside)
                    && feet.y < _motionWaterHeights[0].y + .04f && feet.y > _motionWaterHeights[0].y - .65f;
                if (shallow)
                {
                    _motionStrokeDistance += Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
                    if (_motionStrokeDistance >= .65f && Time.time >= _motionNextWater && shore.WalkSpeed > .12f)
                    {
                        Play("WaterStroke", feet + Vector3.up * .05f, .35f);
                        _motionStrokeDistance = Mathf.Min(.2f, _motionStrokeDistance - .65f);
                        _motionNextWater = Time.time + .38f;
                    }
                }
                else _motionStrokeDistance = 0;
            }
            StoreMotionPose(feet, grounded);
        }

        private void StoreMotionPose(Vector3 feet, bool grounded)
        {
            _motionFeet = feet; _motionAshore = _c.Shore.Ashore; _motionSwimming = _c.Shore.Swimming;
            _motionClimbing = _c.Shore.Climbing; _motionGrounded = grounded;
        }
        private void ClearMotionDistances()
        { _motionAirTime = _motionFallDistance = _motionDownSpeed = _motionStrokeDistance = _motionClimbDistance = 0; }
        private static string LandingCue(FishingGroundAudio surface) => surface == FishingGroundAudio.Wood ? "ImpactWood"
            : surface == FishingGroundAudio.Stone ? "ImpactStone" : "BodyLand";
        private static bool MotionFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void SampleMotionWater(Vector3 feet)
        {
            if (!GerstnerWavesJobs.Initialized || (_c.Realms != null && _c.Realms.Inside)) { _motionWaterWarm = 0; return; }
            if (!_motionWaterPoints.IsCreated) _motionWaterPoints = new NativeArray<float3>(2, Allocator.Persistent);
            _motionWaterPoints[0] = feet;
            _motionWaterPoints[1] = _motionBoatBody != null ? _motionBoatBody.position : feet;
            GerstnerWavesJobs.UpdateSamplePoints(ref _motionWaterPoints, GetInstanceID());
            GerstnerWavesJobs.GetData(GetInstanceID(), ref _motionWaterHeights, ref _motionWaterNormals);
            _motionWaterWarm = Mathf.Min(3, _motionWaterWarm + 1);
        }

        private void TickMotionBoat(float dt)
        {
            if (_motionBoatBody == null) return;
            Vector3 position = _motionBoatBody.position;
            if (!_motionBoatSampled || Vector3.Distance(position, _motionBoatPosition) > Mathf.Max(2f, dt * 12f))
            {
                _motionBoatPosition = position; _motionBoatSampled = true;
                _motionBoatWater = _motionWaterHeights[1].y; _motionContacts.Clear();
                _motionContactGrace = Time.time + .35f; _motionNextBoatSlap = Time.time + 2.5f; return;
            }
            float waveRise = (_motionWaterHeights[1].y - _motionBoatWater) / dt;
            _motionBoatWater = _motionWaterHeights[1].y; _motionBoatPosition = position;
            if (!_c.Ashore && _c.Player.engine.InWater && _motionWaterWarm >= 3 && Time.time >= _motionNextBoatSlap
                && waveRise > .25f && (Mathf.Abs(_motionBoatBody.velocity.y) > .16f || _c.Speed > .6f))
            {
                Play("BoatSlap", position + _motionBoatBody.transform.forward * 1.1f,
                    Mathf.Lerp(.18f, .45f, Mathf.InverseLerp(.25f, 1.8f, waveRise)), FishingAudioCategory.Ambient);
                _motionNextBoatSlap = Time.time + 2.5f;
            }
            if (!CanHearBoatContact) { _motionContacts.Clear(); return; }
            BoatAudioContact strongest = null;
            foreach (var contact in _motionContacts.Values)
                if (Time.time - contact.At <= .14f && contact.Tangent > .35f && (strongest == null || contact.Tangent > strongest.Tangent)) strongest = contact;
            if (strongest != null)
                MaintainLoop("boat-scrape", "BoatScrapeLoop", strongest.Point,
                    Mathf.Lerp(.012f, .045f, Mathf.InverseLerp(.35f, 4f, strongest.Tangent)), true, 20);
        }

        internal bool CanHearBoatContact => MotionContext && _motionBoatSampled && _motionBoatBody != null
            && !_c.Ashore && !_c.Anchored && !_c.HasPendingCatch && !MotionTransferBlocked
            && Time.time >= _motionContactGrace && Vector3.Distance(_motionBoatBody.position, _motionBoatPosition) < 2f;

        internal void ObserveBoatContact(Collision collision, bool entering)
        {
            if (!CanHearBoatContact || collision == null || collision.collider == null || collision.contactCount == 0) return;
            var other = collision.collider;
            if (other.isTrigger || other.transform.IsChildOf(_c.Player.transform) || other.transform.IsChildOf(_c.Shore.Actor)
                || FishingShore.NonSolidSurface(other, false)) return;
            var contact = collision.GetContact(0); var relative = collision.relativeVelocity;
            float normal = Mathf.Abs(Vector3.Dot(relative, contact.normal));
            float tangent = Vector3.ProjectOnPlane(relative, contact.normal).magnitude;
            if (!MotionFinite(normal) || !MotionFinite(tangent)) return;
            int id = other.GetInstanceID();
            if (!_motionContacts.TryGetValue(id, out var sample))
            {
                if (_motionContacts.Count >= 24) _motionContacts.Clear();
                _motionContacts.Add(id, sample = new BoatAudioContact());
            }
            sample.Point = contact.point; sample.Tangent = tangent; sample.At = Time.time;
            if (entering && normal >= .9f && Time.time >= _motionNextImpact)
            {
                Play(BoatContactCue(other), contact.point, Mathf.Lerp(.25f, .9f, Mathf.InverseLerp(.9f, 6.5f, normal)));
                _motionNextImpact = Time.time + .4f;
            }
        }
        internal void EndBoatContact(Collider other) { if (other != null) _motionContacts.Remove(other.GetInstanceID()); }

        private static string BoatContactCue(Collider other)
        {
            for (var t = other.transform; t != null; t = t.parent)
            {
                string name = t.name.ToLowerInvariant();
                if (name.Contains("metal") || name.Contains("iron") || name.Contains("steel")) return "ImpactMetal";
                if (name.Contains("wood") || name.Contains("jetty") || name.Contains("deck") || name.Contains("plank")
                    || name.Contains("timber") || name.Contains("boardwalk") || name.Contains("码头") || name.Contains("栈桥")) return "ImpactWood";
            }
            return "ImpactStone";
        }
    }

    /// <summary>Audio-only collision forwarding attached to the existing vessel rigid body.</summary>
    public sealed class FishingBoatContactAudio : MonoBehaviour
    {
        private FishingWorldAudio _owner;
        public void Bind(FishingWorldAudio owner) { _owner = owner; }
        public void Unbind(FishingWorldAudio owner) { if (_owner == owner) _owner = null; }
        private void OnCollisionEnter(Collision collision) { if (_owner != null) _owner.ObserveBoatContact(collision, true); }
        private void OnCollisionStay(Collision collision) { if (_owner != null) _owner.ObserveBoatContact(collision, false); }
        private void OnCollisionExit(Collision collision) { if (_owner != null) _owner.EndBoatContact(collision.collider); }
    }
}
