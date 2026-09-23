using UnityEngine;
using BoatAttack.VR;

namespace BoatAttack
{
    /// <summary>One controller for desktop and XR input; Boat.Setup still creates this component.</summary>
    public class HumanController : BaseController
    {
        private USVInputRouter _input;
        private bool _paused;
        private float _resumeTimeScale = 1f;

        private void Awake() { _input = USVInputRouter.EnsureInstance(); }
        public override void OnEnable()
        {
            base.OnEnable();
            if (_input == null) _input = USVInputRouter.EnsureInstance();
        }

        private void Update()
        {
            if (_input == null) return;
            // Commands run in Update: a paused game has no FixedUpdate to unpause it.
            if (_input.Current.pause)
            {
                if (!_paused) { _resumeTimeScale = Time.timeScale; Time.timeScale = 0f; }
                else Time.timeScale = _resumeTimeScale;
                _paused = !_paused;
            }
            if (_input.Current.reset && controller != null) controller.ResetPosition();
        }

        private void FixedUpdate()
        {
            if (_input == null || engine == null) return;
            var state = _input.Current;
            // Original Engine supports forward thrust only. Reverse is not implemented here.
            engine.Accelerate(Mathf.Max(0f, state.throttle));
            engine.Turn(state.steering);
            if (engine.RB != null && state.brake > 0f)
            {
                // Slow horizontal motion only, preserving vertical buoyancy response.
                var velocity = engine.RB.velocity;
                var horizontal = new Vector3(velocity.x, 0f, velocity.z);
                horizontal = Vector3.MoveTowards(horizontal, Vector3.zero,
                    _input.Settings.brakeDeceleration * state.brake * Time.fixedDeltaTime);
                engine.RB.velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
            }
        }

        private void OnDisable()
        {
            if (_paused) Time.timeScale = _resumeTimeScale;
            _paused = false;
        }
    }
}
