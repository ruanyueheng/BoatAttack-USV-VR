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
        private bool _wasAnchored, _wasFirstPerson = true, _reset = true;
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
        private void OnDisable() { ReleaseCursor(); }
        public void ApplyShoreLookDelta(Vector2 delta)
        {
            if(Controller==null || !Controller.Ashore || Controller.Paused || Controller.ShopOpen || !OwnsCamera || Controller.Expedition.Aligning)return;
            if(float.IsNaN(delta.x) || float.IsInfinity(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.y))return;
            _lookYaw=Mathf.Repeat(_lookYaw+delta.x*.12f+180,360)-180;
            _lookPitch=Mathf.Clamp(_lookPitch-delta.y*.12f,-65,65);
        }

        public void ApplyView() => ApplyView(Application.isFocused, Controller != null && Controller.Automated);

        /// <summary>Environment boundary used by isolated input tests. Mouse/keyboard still pass through Input System; Pointer isolates the OS lock boundary.</summary>
        public void ApplyView(bool focused, bool automated)
        {
            var c = Controller;
            if (c == null || c.Player == null || c.viewCamera == null || !OwnsCamera) { ReleaseCursor(); return; }
            if (c.Paused || c.InventoryOpen || c.ShopOpen) { ResetInput(); return; }
            if(c.Ashore){ApplyShoreView(focused,automated);return;}
            ReleaseCursor();
            var boat = c.Player.transform;
            var yaw = Quaternion.Euler(0f, boat.eulerAngles.y, 0f);
            bool changed = c.Anchored != _wasAnchored || FirstPerson != _wasFirstPerson;
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
                // Fixed cockpit/station; no locomotion, head bob, roll or speed-linked FOV.
                var eye = c.Anchored ? new Vector3(0.05f, 2.0f, -0.55f) : new Vector3(0f, 1.8f, -0.25f);
                c.viewCamera.transform.position = stableOrigin + yaw * eye;
                float pitch = Mathf.Clamp((c.Anchored ? 18f : 8f) + _lookPitch, -45f, 70f);
                c.viewCamera.transform.rotation = yaw * Quaternion.Euler(pitch, (c.Anchored ? 66f : 0f) + _lookYaw, 0f);
                c.viewCamera.fieldOfView = 72f;
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
            _wasAnchored=c.Anchored;_wasFirstPerson=FirstPerson;_lastBoatPosition=boat.position;_reset=false;
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
            var eye=c.Shore.Actor.position+Vector3.up*c.Shore.EyeHeight;
            c.viewCamera.transform.position=FirstPerson?eye:ConstrainToVisibleSegment(eye,eye+rotation*new Vector3(0,.65f,-4.2f),c.Player.transform);
            c.viewCamera.transform.rotation=FirstPerson?rotation:Quaternion.LookRotation(eye-Vector3.up*.45f-c.viewCamera.transform.position,Vector3.up);
            c.viewCamera.fieldOfView=c.BinocularsRaised && FirstPerson?38:FirstPerson?72:58;
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
