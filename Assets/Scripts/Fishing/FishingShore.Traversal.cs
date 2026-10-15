using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using WaterSystem;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingShore
    {
        public bool Swimming { get; private set; }
        public bool Climbing { get; private set; }
        public float Stamina => _motion.Stamina;
        public string MovementName => Swimming ? "游泳" : Climbing ? "攀登" : Crouched ? "蹲行" : "步行";
        private FishingClimbRoute _climb;
        private float _climbProgress;
        private NativeArray<float3> _waterPoint;
        private float3[] _waterHeights = new float3[1], _waterNormals = new float3[1];
        private FishingClimbRoute[] _routes;
        public void RefreshClimbRoutes() => _routes = FindObjectsOfType<FishingClimbRoute>();
        private float WaterHeight()
        {
            if (!GerstnerWavesJobs.Initialized) return 0;
            if (!_waterPoint.IsCreated) _waterPoint = new NativeArray<float3>(1, Allocator.Persistent);
            _waterPoint[0] = Actor.position;
            GerstnerWavesJobs.UpdateSamplePoints(ref _waterPoint, GetInstanceID());
            GerstnerWavesJobs.GetData(GetInstanceID(), ref _waterHeights, ref _waterNormals);
            return _waterHeights[0].y;
        }
        public bool Relocate(Vector3 feet, Quaternion rotation)
        {
            if (!Ashore || !Ground(feet, out var supported) || Mathf.Abs(supported.y - feet.y) > .3f || !Clearance(supported)) return false;
            Body.enabled = false; Actor.SetPositionAndRotation(supported + Vector3.up * .08f, rotation); Body.enabled = true;
            _lastSafe = Actor.position; _motion.Reset(); Swimming = Climbing = false; SetCrouch(false, true); return true;
        }
        public string ClimbHint => Climbing ? "W / S 攀登 · 空格离开绳梯" : NearbyClimb() != null ? "E 攀登绳梯 · 登高观察岛屿" : null;
        private FishingClimbRoute NearbyClimb()
        {
            if (!Ashore || Swimming || Controller.Realms == null || Controller.Realms.Inside || _routes == null) return null;
            foreach (var route in _routes)
                if (route != null && route.gameObject.activeInHierarchy && Mathf.Min(Vector3.Distance(Position, route.Bottom), Vector3.Distance(Position, route.Top)) < 1.7f) return route;
            return null;
        }
        public bool TryClimb()
        {
            if (Controller.Paused || Controller.StoryBlocksGameplay || Controller.ShopBlocksGameplay || Controller.WorldActionBusy || Controller.InventoryOpen || Controller.JournalOpen || Climbing) return false;
            var route = NearbyClimb(); if (route == null) return false;
            _climb = route; _climbProgress = Vector3.Distance(Position, route.Bottom) < Vector3.Distance(Position, route.Top) ? 0 : 1;
            Climbing = true; Controller.LowerFieldTools(); Controller.CancelWorldAction(); _motion.Reset(); SetCrouch(false, true);
            Controller.Notice("已握住绳梯 · W / S 上下攀登，空格离开；体力不足时速度降低。", 5); return true;
        }
        private void Climb(Vector2 input, bool jump, float dt)
        {
            if (_climb == null) { Climbing = false; return; }
            if (jump) { Climbing = false; _motion.Reset(); return; }
            float distance = Vector3.Distance(_climb.Bottom, _climb.Top);
            bool effort = input.y > .1f;
            _motion.Exert(effort, Mathf.Abs(input.y) < .1f, dt);
            float speed = Stamina > .08f || input.y < 0 ? 1.8f : .45f;
            _climbProgress = Mathf.Clamp01(_climbProgress + input.y * speed * dt / Mathf.Max(.1f, distance));
            var target = Vector3.Lerp(_climb.Bottom, _climb.Top, _climbProgress) + Vector3.up * .08f;
            Body.Move(target - Actor.position); WalkSpeed = Mathf.Abs(input.y) * speed;
            var forward = Vector3.ProjectOnPlane(_climb.Top - _climb.Bottom, Vector3.up);
            if (forward.sqrMagnitude > .01f) Actor.rotation = Quaternion.LookRotation(forward);
            if ((input.y > .1f && _climbProgress >= .999f) || (input.y < -.1f && _climbProgress <= .001f))
            {
                var end = input.y > 0 ? _climb.Top : _climb.Bottom;
                if (Relocate(end, Actor.rotation)) Controller.Notice("已离开绳梯 · 停步可恢复体力。", 3);
            }
        }
        private void Swim(Vector2 axes, Quaternion heading, bool sprint, bool jump, float dt)
        {
            Swimming = true; SetCrouch(false, true); Controller.LowerFieldTools();
            _motion.Exert(sprint && axes.sqrMagnitude > .01f, axes.sqrMagnitude < .01f, dt);
            float speed = UnityEngine.XR.XRSettings.isDeviceActive ? 1.2f : sprint && Stamina > .08f ? 2.7f : 1.45f;
            var velocity = heading * new Vector3(axes.x, 0, axes.y) * speed;
            float water = WaterHeight();
            var grab=Position+Vector3.ProjectOnPlane(velocity,Vector3.up).normalized*.9f;
            if ((jump || UnityEngine.XR.XRSettings.isDeviceActive) && axes.sqrMagnitude>.01f && GroundBelow(grab, water + 1.7f, 2.6f, out var shore) && shore.y <= water + 1.35f && SafeCoastalFooting(shore) && Clearance(shore)
                && ClearSight(Position+Vector3.up*1.55f,shore+Vector3.up*1.55f))
            {
                if (Relocate(shore, Actor.rotation)) { Controller.View.ResetLook(); Controller.Notice("已攀上低岸 · 继续探索。", 3); return; }
            }
            float eyeHeight = UnityEngine.XR.XRSettings.isDeviceActive ? FishingXRSeat.EyeHeight : _character.EyePosition.y - Actor.position.y;
            float vertical = Mathf.Clamp((water + .24f - eyeHeight - Actor.position.y) * 4, -2.4f, 1.8f);
            var previous = Position; Body.Move((velocity + Vector3.up * vertical) * dt);
            WalkSpeed = Vector3.ProjectOnPlane(Position - previous, Vector3.up).magnitude / dt;
            if (velocity.sqrMagnitude > .01f) Actor.rotation = Quaternion.LookRotation(velocity);
        }
        public void ApplyTraversalPose()
        {
            if (!Ashore || (!Swimming && !Climbing)) return;
            _character.ApplyTraversalPose(Swimming, Climbing, WalkSpeed, UnityEngine.XR.XRSettings.isDeviceActive, Controller.Paused);
        }
        public bool EatRation()
        {
            var item=Controller.Career.Backpack.Equipped;
            if(!Ashore || Controller.Paused || Controller.StoryBlocksGameplay || Controller.ShopBlocksGameplay || Controller.WorldActionBusy || Stamina>=.99f
                || (item!=FieldItem.CookedMeat && item!=FieldItem.GrilledFish)
                || !Controller.Career.Backpack.Exchange(new[]{FishingRecipes.Stack(item,1)},FieldItem.Empty,0))return false;
            _motion.RestoreStamina();Controller.Hands.ReachGesture(Character.HandReferencePosition+Actor.forward*.22f+Vector3.up*.22f);
            Controller.Save();Controller.Notice(item==FieldItem.GrilledFish?"吃下香烤海鱼 · 体力恢复50%":"食用远航口粮 · 体力恢复50%",4);return true;
        }
        private void ResetTraversal() { Swimming = Climbing = false; _climb = null; }
        private void OnDestroy() { if (_waterPoint.IsCreated) _waterPoint.Dispose(); }
    }
}
