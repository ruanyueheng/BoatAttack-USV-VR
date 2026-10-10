using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Desktop composition prototype. An active XR device always owns its own tracked camera.</summary>
    public sealed class FishingViewRig : MonoBehaviour
    {
        public FishingPrototype Controller;
        public bool FirstPerson { get; private set; } = true;
        public bool OwnsCamera => !XRSettings.isDeviceActive;
        public bool IsLooking { get; private set; }
        public const float DesktopFieldOfView=68f;
        public const float BinocularFieldOfView=38f;
        public const float BinocularTransitionSeconds=.28f;
        public float BinocularBlend { get; private set; }
        public bool OpticalView=>OwnsCamera && FirstPerson && BinocularBlend>=.88f;
        public float OpticalOpacity=>OwnsCamera && FirstPerson?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,.9f,BinocularBlend)):0;
        private int _binocularFrame=-1;
        public Vector3 StableBoatOrigin=>new Vector3(Controller.Player.transform.position.x,_waterY,Controller.Player.transform.position.z);
        private bool _wasFishingSeat, _wasFirstPerson = true, _reset = true;
        private float _shoreYaw;
        private float _lookYaw, _lookPitch, _waterY, _waterVelocity;
        private Vector3 _velocity, _lastBoatPosition;
        private readonly FishingPointerGate _lookGate = new FishingPointerGate();
        private readonly RaycastHit[] _hits = new RaycastHit[48];

        public void Toggle()
        {
            if (!OwnsCamera) return;
            FirstPerson = !FirstPerson;
            ResetLook();
        }
        public void ResetLook() { if(Controller!=null && Controller.Ashore)_shoreYaw=Controller.Shore.Actor.eulerAngles.y; _lookYaw = _lookPitch = 0f; _reset = true; IsLooking = false; }

        public void ResetInput() { _lookGate.Reset(); IsLooking = false; ReleaseCursor(); }
        private bool _cursorOwned;
        private IFishingCursor _pointer=new FishingSystemCursor();
        public IFishingCursor Pointer
        {
            get => _pointer;
            set { ReleaseCursor(); _pointer=value??new FishingSystemCursor(); }
        }
        private void ReleaseCursor() { if(!_cursorOwned)return; _pointer.LockState=CursorLockMode.None;_pointer.Visible=true;_cursorOwned=false; }
        private void OnDisable() { ReleaseCursor();BinocularBlend=0; }
        public void ApplyShoreLookDelta(Vector2 delta)
        {
            if(Controller==null || !Controller.Ashore || Controller.Paused || Controller.ShopBlocksGameplay || !OwnsCamera || Controller.Expedition.Aligning)return;
            if(float.IsNaN(delta.x) || float.IsInfinity(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.y))return;
            _lookYaw=Mathf.Repeat(_lookYaw+delta.x*.12f+180,360)-180;
            _lookPitch=Mathf.Clamp(_lookPitch-delta.y*.12f,-65,65);
        }

        private void UpdateBinocularView()
        {
            var c=Controller;
            bool available=OwnsCamera && c!=null && c.Ashore && c.Career!=null
                && c.Career.Backpack.Equipped==FieldItem.Binoculars && !c.Paused && !c.InventoryOpen && !c.ShopBlocksGameplay;
            if(!available)
            {
                bool hadOptics=BinocularBlend>0;
                BinocularBlend=0;_binocularFrame=Time.frameCount;
                // 模态会提前返回相机更新；取消光学状态时也必须立即归还桌面投影。
                if(hadOptics && OwnsCamera && c!=null && c.viewCamera!=null)c.viewCamera.fieldOfView=FirstPerson?DesktopFieldOfView:58;
                return;
            }
            if(_binocularFrame==Time.frameCount)return;
            _binocularFrame=Time.frameCount;
            BinocularBlend=Mathf.MoveTowards(BinocularBlend,c.BinocularsRaised?1:0,Time.deltaTime/BinocularTransitionSeconds);
        }

        public void ApplyView() => ApplyView(Application.isFocused, Controller != null && Controller.Automated);

        /// <summary>Environment boundary used by isolated input tests. Mouse/keyboard still pass through Input System; Pointer isolates the OS lock boundary.</summary>
        public void ApplyView(bool focused, bool automated)
        {
            var c = Controller;
            UpdateBinocularView();
            if (c == null || c.Player == null || c.viewCamera == null || !OwnsCamera) { ReleaseCursor(); return; }
            if (c.Paused || c.InventoryOpen || c.ShopBlocksGameplay) { ResetInput(); return; }
            if(c.Ashore){ApplyShoreView(focused,automated);return;}
            ReleaseCursor();
            var boat = c.Player.transform;
            var yaw = Quaternion.Euler(0f, boat.eulerAngles.y, 0f);
            bool fishingSeat=c.UsesFishingSeat;
            bool changed = fishingSeat != _wasFishingSeat || FirstPerson != _wasFirstPerson;
            bool teleported = Vector3.Distance(boat.position, _lastBoatPosition) > 12f;
            if (changed) { _lookYaw = _lookPitch = 0f; _reset = true; }
            var mouse = Mouse.current;
            bool rightHeld = mouse != null && mouse.rightButton.isPressed;
            bool lookReady = _lookGate.Update(focused, c.Paused, false, rightHeld);
            IsLooking = !automated && lookReady && rightHeld;
            if (IsLooking)
            {
                // Pointer delta is already frame-relative; multiplying by deltaTime changes sensitivity with FPS.
                var delta = mouse.delta.ReadValue();
                _lookYaw = Mathf.Clamp(_lookYaw + delta.x * 0.12f, -100f, 100f);
                _lookPitch = Mathf.Clamp(_lookPitch - delta.y * 0.12f, -45f, 50f);
            }
            if (_reset || teleported) { _waterY = boat.position.y; _waterVelocity = 0f; _velocity = Vector3.zero; }
            // A pause/resume in one frame can leave deltaTime=0 even while Paused=false.
            // SmoothDamp may divide by zero on its overshoot path and poison the next frame.
            if (Time.deltaTime > 0f) _waterY = Mathf.SmoothDamp(_waterY, boat.position.y, ref _waterVelocity, c.ComfortMode ? 2.5f : 0.6f);
            var stableOrigin = new Vector3(boat.position.x, _waterY, boat.position.z);
            if (FirstPerson)
            {
                // 同源身体与眼位必须使用同一竖向参考系；不能只平滑镜头而把抬起的双手留在下方。
                // 船上朝向仍只取 yaw，第三人称继续使用平滑水线；XR 由其追踪参考系管理。
                c.viewCamera.transform.position = c.Shore.Character.EyePosition;
                c.viewCamera.nearClipPlane = .04f;
                float pitch = Mathf.Clamp((fishingSeat ? 25f : 0f) + _lookPitch, -45f, 70f);
                c.viewCamera.transform.rotation = yaw * Quaternion.Euler(pitch, (fishingSeat ? FishingPrototype.FishingSeatYaw : 0f) + _lookYaw, 0f);
                c.viewCamera.fieldOfView = DesktopFieldOfView;
            }
            else
            {
                Vector3 desired, look;
                if (c.Anchored) { desired = stableOrigin + yaw * new Vector3(-6.3f, 7f, -5f); look = stableOrigin + yaw * new Vector3(2.7f, 0.8f, 1f); }
                else if (c.HarborDistance < 25f) { desired = stableOrigin + yaw * new Vector3(-5,9,-13); look = Vector3.Lerp(stableOrigin,c.harbor,0.5f)+Vector3.up; }
                else { desired=stableOrigin+yaw*new Vector3(0,7.5f,-12.5f); look=stableOrigin+yaw*new Vector3(0,0.7f,5f); }
                var orbit = Quaternion.AngleAxis(_lookYaw, Vector3.up);
                desired = look + orbit * (desired - look) + Vector3.up * _lookPitch * 0.07f;
                desired=ConstrainToVisibleSegment(look,desired,boat);
                var smoothed=(_reset||teleported)?desired:Time.deltaTime<=0f?c.viewCamera.transform.position:Vector3.SmoothDamp(c.viewCamera.transform.position,desired,ref _velocity,0.22f);
                // Damping is allowed away from obstacles, never through them. A newly blocked view
                // must retract immediately rather than interpolate from behind a wall for several frames.
                var safe=ConstrainToVisibleSegment(look,smoothed,boat);
                if((safe-smoothed).sqrMagnitude>0.0001f)_velocity=Vector3.zero;
                c.viewCamera.transform.position=safe;
                c.viewCamera.transform.rotation=Quaternion.LookRotation(look-c.viewCamera.transform.position,Vector3.up);
                c.viewCamera.fieldOfView=58f;
            }
            _wasFishingSeat=fishingSeat;_wasFirstPerson=FirstPerson;_lastBoatPosition=boat.position;_reset=false;
        }
        private void ApplyShoreView(bool focused,bool automated)
        {
            var c=Controller;var mouse=Mouse.current;
            var keyboard=Keyboard.current;
            bool alt=keyboard!=null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            // The chart is a persistent navigation HUD, not a modal: landing opens it automatically.
            // Alt explicitly hands the pointer to the chart/buttons without disabling navigation look forever.
            bool blocked=c.InventoryOpen || c.JournalOpen || c.ShowDetails || c.Expedition.Aligning;
            bool capture=FishingHandInput.CaptureShorePointer(c.Ashore,OwnsCamera,focused,automated,c.Paused,blocked,alt);
            bool wasLocked=_cursorOwned && _pointer.LockState==CursorLockMode.Locked;
            if(capture)
            {
                // Do not request an OS cursor warp every frame. Reacquisition discards its first delta.
                if(_pointer.LockState!=CursorLockMode.Locked)_pointer.LockState=CursorLockMode.Locked;
                if(_pointer.Visible)_pointer.Visible=false;
                _cursorOwned=true;
            }
            else ReleaseCursor();
            IsLooking=capture && wasLocked && mouse!=null;
            if(IsLooking)ApplyShoreLookDelta(mouse.delta.ReadValue());
            var rotation=Quaternion.Euler(_lookPitch,_shoreYaw+_lookYaw,0);
            var eye=c.Shore.Character.EyePosition;
            c.viewCamera.transform.position=FirstPerson?eye:ConstrainToVisibleSegment(eye,eye+rotation*new Vector3(0,.65f,-4.2f),c.Player.transform);
            c.viewCamera.transform.rotation=FirstPerson?rotation:Quaternion.LookRotation(eye-Vector3.up*.45f-c.viewCamera.transform.position,Vector3.up);
            c.viewCamera.nearClipPlane=.04f;
            // Unity 的 fieldOfView 是垂直角；只放大第一人称世界，不放大第三人称或实体双手。
            float zoom=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,BinocularBlend));
            c.viewCamera.fieldOfView=FirstPerson?Mathf.Lerp(DesktopFieldOfView,BinocularFieldOfView,zoom):58;
            _reset=true; // Boarding must not interpolate the boat camera from an inland position.
        }
        private Vector3 ConstrainToVisibleSegment(Vector3 pivot,Vector3 position,Transform boat)
        {
            var delta=position-pivot;
            float distance=delta.magnitude;
            if(distance<0.001f)return position;
            int count=Physics.SphereCastNonAlloc(pivot,0.25f,delta/distance,_hits,distance,~(1<<4),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=_hits[i];
                if((Controller.Shore!=null && hit.collider.transform.IsChildOf(Controller.Shore.Actor)) || hit.collider.transform.IsChildOf(boat) || hit.collider.GetComponentInParent<WaterSystem.Water>()!=null)continue;
                distance=Mathf.Min(distance,Mathf.Max(0.05f,hit.distance-0.35f));
            }
            return pivot+delta.normalized*distance;
        }
        private void OnApplicationFocus(bool focus) { if(!focus)ResetInput(); }
    }
}
