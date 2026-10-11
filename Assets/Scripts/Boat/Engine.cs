using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using WaterSystem;

namespace BoatAttack
{
    public class Engine : MonoBehaviour
    {
        [NonSerialized] public Rigidbody RB; // The rigid body attatched to the boat
        [NonSerialized] public float VelocityMag; // Boats velocity

        public AudioSource engineSound; // Engine sound clip
        public AudioSource waterSound; // Water sound clip

        //engine stats
        public float steeringTorque = 5f;
        public float horsePower = 18f;
        public bool stableHull;
        private NativeArray<float3> _point; // engine submerged check
        private float3[] _heights = new float3[1]; // engine submerged check
        private float3[] _normals = new float3[1]; // engine submerged check
        private int _guid;
        private float _yHeight = float.NegativeInfinity;

        public Vector3 enginePosition;
        private Vector3 _engineDir;
        private float _turnVel;
        private float _currentAngle;
        private bool _wet;
        private float _dryTime;
        private BuoyantObject _buoyancy;
        public bool InWater => stableHull ? _wet : _yHeight > -0.1f;

        private void Awake()
        {
            RandomizeAudioStart(engineSound);
            RandomizeAudioStart(waterSound);
            _guid = GetInstanceID();
            if (RB == null) RB = GetComponentInParent<Rigidbody>();
            if (RB != null) _buoyancy = RB.GetComponent<BuoyantObject>();
        }

        private static void RandomizeAudioStart(AudioSource source)
        {
            if (source != null && source.clip != null && source.clip.length > 0f)
                source.time = UnityEngine.Random.Range(0f, source.clip.length);
        }

        private void OnEnable()
        {
            // OnDisable releases this buffer; pooled/re-enabled boats need a fresh one.
            if (!_point.IsCreated) _point = new NativeArray<float3>(1, Allocator.Persistent);
            _yHeight = float.NegativeInfinity;
            _wet = false;
            _dryTime = 0;
        }

        private void Start()
        {
            if (!stableHull || RB == null) return;
            // Rotation constraints use inertia axes; the asymmetric hull otherwise couples yaw into roll.
            RB.inertiaTensorRotation = Quaternion.identity;
            RB.maxAngularVelocity = 1f;
        }

        private void FixedUpdate()
        {
            if (RB == null) return;
            VelocityMag = RB.velocity.sqrMagnitude;
            if (engineSound != null) engineSound.pitch = Mathf.Max(VelocityMag * 0.01f, 0.3f);
            if (!_point.IsCreated || !GerstnerWavesJobs.Initialized)
            {
                _yHeight = float.NegativeInfinity;
                _wet = false;
                return;
            }

            // Get the water level from the engines position and store it
            _point[0] = transform.TransformPoint(enginePosition);
            GerstnerWavesJobs.UpdateSamplePoints(ref _point, _guid);
            GerstnerWavesJobs.GetData(_guid, ref _heights, ref _normals);
            _yHeight = _heights[0].y - _point[0].y;
            bool immersed = _yHeight > (_wet ? -0.3f : -0.15f);
            _dryTime = immersed ? 0 : _dryTime + Time.fixedDeltaTime;
            // A short wave trough must not kill thrust while the hull is still afloat.
            _wet = immersed || (stableHull && _wet && _dryTime < .6f && _buoyancy != null && _buoyancy.PercentSubmerged > .01f);
            if (stableHull && _wet)
            {
                var sideways = Vector3.ProjectOnPlane(RB.transform.right, Vector3.up).normalized;
                RB.AddForce(-sideways * Vector3.Dot(RB.velocity, sideways) * 1.8f, ForceMode.Acceleration);
            }
        }

        private void OnDisable()
        {
            if (_point.IsCreated) _point.Dispose();
            _yHeight = float.NegativeInfinity;
            _wet = false;
        }
        
        /// <summary>
        /// Controls the acceleration of the boat
        /// </summary>
        /// <param name="modifier">Signed throttle: forward 0..1, reverse -1..0</param>
        public void Accelerate(float modifier)
        {
            if (RB != null && InWater)
            {
                modifier = BoatAttack.VR.USVPropulsion.Thrust(modifier, Vector3.Dot(RB.velocity, Vector3.ProjectOnPlane(RB.transform.forward, Vector3.up).normalized));
                var forward = RB.transform.forward;
                forward.y = 0f;
                forward.Normalize();
                if (stableHull && modifier > 0f)
                    modifier *= Mathf.Clamp01(1f - Vector3.Dot(RB.velocity, forward) / 7f);
                RB.AddForce(horsePower * modifier * forward, ForceMode.Acceleration); // add force forward based on input and horsepower
                if (!stableHull) RB.AddRelativeTorque(-Vector3.right * modifier, ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Controls the turning of the boat
        /// </summary>
        /// <param name="modifier">Steering modifier, positive for right, negative for negative</param>
        public void Turn(float modifier)
        {
            if (RB != null && InWater)
            {
                modifier = Mathf.Clamp(modifier, -1f, 1f); // clamp for reasonable values
                if (stableHull)
                {
                    float rate = Mathf.Lerp(45f, 26f, Mathf.Clamp01(Mathf.Sqrt(VelocityMag) / 7f)) * Mathf.Deg2Rad;
                    RB.AddTorque(Vector3.up * (modifier * rate - RB.angularVelocity.y) * 4f, ForceMode.Acceleration);
                }
                else RB.AddRelativeTorque(new Vector3(0f, steeringTorque, -steeringTorque * 0.5f) * modifier, ForceMode.Acceleration);
            }

            _currentAngle = Mathf.SmoothDampAngle(_currentAngle, 
                60f * -modifier, 
                ref _turnVel, 
                0.5f, 
                10f,
                Time.fixedDeltaTime);
            transform.localEulerAngles = new Vector3(0f, _currentAngle, 0f);
        }

        // Draw some helper gizmos
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(enginePosition, new Vector3(0.1f, 0.2f, 0.3f)); // Draw teh engine position with sphere
        }
	}
}
