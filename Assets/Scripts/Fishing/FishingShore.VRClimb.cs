using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingShore
    {
        private readonly FishingPressEdge[] _climbGrip = { new FishingPressEdge(), new FishingPressEdge() };
        private readonly bool[] _climbHeld = new bool[2];
        private readonly Vector3[] _climbPalm = new Vector3[2];
        private float _handClimbDelta;
        private int _handClimbSamples;
        private void ResetClimbHands()
        {
            for (int i = 0; i < 2; i++) { _climbHeld[i] = false; _climbGrip[i].Reset(); }
            _handClimbDelta = 0; _handClimbSamples = 0;
        }
        private void ReadClimbHands()
        {
            for (int i = 0; i < 2; i++)
            {
                var node = i == 0 ? XRNode.LeftHand : XRNode.RightHand;
                bool tracked = Controller.Hands.TryControllerPalm(i == 0, out var palm, out _);
                var device = InputDevices.GetDeviceAtXRNode(node);
                bool held = FishingXRInput.Grip(device, .65f);
                StepClimbHand(i == 0, tracked && Application.isFocused, held, palm);
            }
        }
        /// <summary>Pull a gripped rung down to climb up; each hand can release and take the next rung.</summary>
        public void StepClimbHand(bool left, bool tracked, bool held, Vector3 palm)
        {
            int hand = left ? 0 : 1;
            bool available = tracked && Ashore && !Swimming && !Controller.Paused && !Controller.StoryBlocksGameplay
                && !Controller.ShopBlocksGameplay && !Controller.WorldActionBlocksGameplay && !Controller.InventoryOpen && !Controller.JournalOpen
                && (Controller.XRNotebook == null || !Controller.XRNotebook.BlocksGameplay)
                && !float.IsNaN(palm.sqrMagnitude) && !float.IsInfinity(palm.sqrMagnitude);
            bool press = _climbGrip[hand].Step(available, held);
            if (!available || !held) { _climbHeld[hand] = false; return; }
            var route = Climbing ? _climb : NearbyClimb();
            if (route == null) { _climbHeld[hand] = false; return; }
            var axis = route.RungEnd - route.RungBottom;
            var rung = route.RungBottom + axis * Mathf.Clamp01(Vector3.Dot(palm - route.RungBottom, axis) / Mathf.Max(.01f, axis.sqrMagnitude));
            if (press && Vector3.Distance(palm, rung) < .35f && ClearSight(palm, rung, route.transform))
            {
                if (!Climbing && !TryClimb()) return;
                _climbHeld[hand] = true; _climbPalm[hand] = palm - Actor.position;
                return;
            }
            if (!_climbHeld[hand]) return;
            var local = palm - Actor.position;
            var delta = local - _climbPalm[hand]; _climbPalm[hand] = local;
            if (Vector3.Distance(palm, rung) > .8f || delta.magnitude > .18f)
            { _climbHeld[hand] = false; _climbGrip[hand].Reset(); return; }
            // Body translation is removed from each sample, and two held hands never double the climb speed.
            _handClimbDelta -= delta.y; _handClimbSamples++;
        }
    }
}
