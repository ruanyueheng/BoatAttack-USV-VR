using UnityEngine;
using UnityEngine.InputSystem;

namespace BoatAttack.Fishing
{
    /// <summary>Desktop disembarking onto nearby solid ground, with a moored boat and dry-foot walking.</summary>
    public sealed class FishingShoreWalker : MonoBehaviour
    {
        public const float StopSpeed = 0.5f;
        public const float BoardingRadius = 2f;
        private const float Radius = .3f, Height = 1.8f, EyeHeight = 1.65f;
        private const int GroundMask = ~(1 << 4);
        public FishingPrototype Controller;
        public bool IsAshore { get; private set; }
        public Vector3 Position => _body == null ? transform.position : _body.transform.position;
        public Vector3 EyePosition => Position + Vector3.up * EyeHeight;
        public Vector3 BoardingPoint { get; private set; }
        public bool CanBoard => IsAshore && Vector3.Distance(Position, BoardingPoint) <= BoardingRadius;
        private CharacterController _body;
        private bool _wasKinematic, _wasFirstPerson;
        private readonly RaycastHit[] _hits = new RaycastHit[64];
        private readonly Collider[] _overlaps = new Collider[64];

        public bool Toggle()
        {
            if (Controller.Paused || !Controller.View.OwnsCamera) return false;
            if (IsAshore)
            {
                if (!CanBoard) { Controller.Notice("走回下船位置附近，再按 G 上船。"); return false; }
                Board(); Controller.Notice("已回到船上 · W 前进，A/D 转向。"); return true;
            }
            if (Controller.Anchored) { Controller.Notice("先按 Q 收起渔具，再靠岸下船。"); return false; }
            if (Controller.Speed > StopSpeed) { Controller.Notice("请按住 S / Shift 停稳船只，再按 G 下船。"); return false; }
            if (!TryFindLanding(out var feet)) { Controller.Notice("附近没有安全落脚点，请靠近岸边或码头再按 G。"); return false; }
            if (_body == null)
            {
                var go = new GameObject("Fishing shore walker"); go.transform.SetParent(transform, false);
                _body = go.AddComponent<CharacterController>();
                _body.height = Height; _body.radius = Radius; _body.center = Vector3.up * (Height * .5f);
                _body.skinWidth = .025f; _body.stepOffset = .3f; _body.slopeLimit = 45f; _body.minMoveDistance = 0f;
            }
            _body.enabled = false; _body.transform.position = feet; _body.enabled = true;
            BoardingPoint = feet;
            var rb = Controller.Player.engine.RB;
            _wasKinematic = rb.isKinematic; rb.velocity = rb.angularVelocity = Vector3.zero; rb.isKinematic = true;
            _wasFirstPerson = Controller.FirstPerson;
            if (!_wasFirstPerson) Controller.View.Toggle();
            IsAshore = true; Controller.View.ResetLook();
            Controller.Notice("已下船 · WASD 步行，右键环顾；回到此处按 G 上船，R 救援回港。", 8f);
            return true;
        }

        public void Board()
        {
            if (!IsAshore) return;
            IsAshore = false;
            if (_body != null) _body.enabled = false;
            if (Controller.Player != null)
            {
                var rb = Controller.Player.engine.RB; rb.isKinematic = _wasKinematic;
                if (!rb.isKinematic) rb.velocity = rb.angularVelocity = Vector3.zero;
            }
            if (Controller.View != null)
            {
                if (Controller.FirstPerson != _wasFirstPerson) Controller.View.Toggle();
                Controller.View.ResetLook();
            }
        }

        public void Tick()
        {
            if (!IsAshore) return;
            // Returning XR ownership must also release the parked rigidbody.
            if (!Controller.View.OwnsCamera) { Board(); return; }
            if (Controller.Automated || Controller.Paused || Controller.ShopOpen || !Application.isFocused) return;
            var k = Keyboard.current;
            if (k == null) return;
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
            float z = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
            Walk(new Vector2(x, z), Time.deltaTime);
        }

        // Shared by live input and the opt-in Play Mode regression driver.
        internal void Walk(Vector2 input, float deltaTime)
        {
            if (!IsAshore || Controller.Paused || Controller.ShopOpen || deltaTime <= 0f) return;
            var yaw = Quaternion.Euler(0, Controller.viewCamera.transform.eulerAngles.y, 0);
            var direction = yaw * new Vector3(input.x, 0, input.y);
            var movement = Vector3.ClampMagnitude(direction, 1f) * (3f * Mathf.Min(deltaTime, .1f));
            var start = Position;
            // Ground footprint checks prevent walking off piers or onto submerged seabed.
            if (!TryGround(start + movement, start.y, .35f, .55f, out var ground) || !Supported(ground)) return;
            // Lift onto the validated small step before moving horizontally. A single diagonal
            // Move can wedge the capsule against the vertical lip where a ramp meets a porch.
            float rise = Mathf.Max(0f, ground.y - start.y);
            if (rise > .001f) _body.Move(Vector3.up * rise);
            _body.Move(new Vector3(movement.x, Mathf.Min(0f, ground.y - start.y) - .02f, movement.z));
            // Collision sliding can change the final horizontal position; validate that too.
            if (!TryGround(Position, start.y, .35f, .55f, out var actual) || !Supported(actual))
            { _body.enabled = false; _body.transform.position = start; _body.enabled = true; }
        }

        public bool TryFindLanding(out Vector3 feet)
        {
            feet = default;
            if (Controller.Player == null) return false;
            var boat = Controller.Player.transform;
            // Search all sides: beaches need not be near the market and boats can arrive at any heading.
            for (float distance = 2.5f; distance <= 5.5f; distance += .75f)
                for (int i = 0; i < 24; i++)
                {
                    var offset = Quaternion.Euler(0, boat.eulerAngles.y + i * 15f, 0) * Vector3.forward * distance;
                    if (!TryGround(boat.position + offset, boat.position.y, 2f, 2f, out var candidate)) continue;
                    if (!Supported(candidate) || !ClearBody(candidate)) continue;
                    if (!ClearTransfer(boat.position + Vector3.up * EyeHeight, candidate + Vector3.up * EyeHeight)) continue;
                    feet = candidate; return true;
                }
            return false;
        }

        private bool IsGround(Collider collider)
        {
            if (collider == _body || collider.transform.IsChildOf(Controller.Player.transform)) return false;
            if (collider.GetComponentInParent<WaterSystem.Water>() != null) return false;
            return collider.attachedRigidbody == null || collider.attachedRigidbody.isKinematic;
        }
        private bool TryGround(Vector3 sample, float referenceY, float up, float down, out Vector3 feet)
        {
            feet = default;
            int count = Physics.RaycastNonAlloc(new Vector3(sample.x, referenceY + up, sample.z), Vector3.down, _hits, up + down, GroundMask, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
                if (IsGround(_hits[i].collider) && _hits[i].distance < nearest)
                { nearest = _hits[i].distance; feet = _hits[i].point; }
            if (nearest == float.MaxValue || feet.y < .25f) return false; // The fishing sea is at y=0.
            for (int i = 0; i < count; i++)
                if (_hits[i].distance == nearest && IsGround(_hits[i].collider))
                    return _hits[i].normal.y >= .7072f;
            return false;
        }
        private bool Supported(Vector3 feet)
        {
            for (int i = 0; i < 4; i++)
            {
                var offset = Quaternion.Euler(0, i * 90, 0) * Vector3.forward * (Radius + .05f);
                if (!TryGround(feet + offset, feet.y, .35f, .4f, out _)) return false;
            }
            return true;
        }
        private bool ClearBody(Vector3 feet)
        {
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (Radius + .05f), feet + Vector3.up * (Height - Radius), Radius, _overlaps, GroundMask, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (_overlaps[i] != _body) return false;
            return true;
        }
        private bool ClearTransfer(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            int count = Physics.SphereCastNonAlloc(from, Radius, delta.normalized, _hits, delta.magnitude, GroundMask, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!_hits[i].transform.IsChildOf(Controller.Player.transform) && _hits[i].collider.GetComponentInParent<WaterSystem.Water>() == null) return false;
            return true;
        }
        private void OnDisable() { Board(); }
    }
}
