using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// Bends only this rod's flexible blank. The imported mesh, handle and reel remain untouched.
    /// Authored gear coordinates run along +Z, with the reel entirely behind FlexibleStart.
    /// The controller supplies gameplay delta time so pause also freezes the spring's velocity.
    /// </summary>
    public sealed class FishingRodDynamics : MonoBehaviour
    {
        public const float FlexibleStart = .30f;

        private sealed class FlexibleMesh
        {
            public MeshFilter Filter;
            public Mesh Original, Instance;
            public Matrix4x4 ToRoot, ToMesh, NormalToRoot, NormalToMesh;
            public Vector3[] OriginalVertices, RootVertices, Vertices;
            public Vector3[] OriginalNormals, RootNormals, Normals;
            public Vector4[] OriginalTangents, Tangents;
            public Vector3[] RootTangents;
        }

        private readonly List<FlexibleMesh> _meshes = new List<FlexibleMesh>();
        private Transform _root;
        private Vector3 _restTip;
        private Quaternion _restTipRotation;
        private float _bendVelocity, _sideVelocity;
        private bool _spoolDirty;
        public float SpoolTurns { get; private set; }
        public float SpoolFill { get; private set; }=1;
        public int SpoolVertexCount { get; private set; }
        private static bool OnSpool(Vector3 p)
        {
            float radius=new Vector2(p.x,p.y+.14f).magnitude;
            return p.z>=.063f && p.z<=.153f && radius>=.043f && radius<=.065f;
        }
        public void AdvanceSpool(float woundMeters,float fill)
        {
            if(!Finite(woundMeters) || !Finite(fill))return;
            SpoolTurns+=woundMeters/(2*Mathf.PI*.049f);SpoolFill=Mathf.Clamp01(fill);_spoolDirty=true;
        }

        /// <summary>Main downward bend in radians, not an accumulated vertex displacement.</summary>
        public float CurrentBend { get; private set; }
        public float CurrentSideBend { get; private set; }
        public float FlexibleLength { get; private set; }
        public Transform Tip { get; private set; }
        public int MeshCount => _meshes.Count;

        public void Build(Transform root, Transform tip)
        {
            ReleaseMeshes();
            _root = root;
            Tip = tip;
            CurrentBend = CurrentSideBend = _bendVelocity = _sideVelocity = 0;
            SpoolTurns=0;SpoolFill=1;SpoolVertexCount=0;_spoolDirty=false;
            if (_root == null || Tip == null) return;
            _restTip = _root.InverseTransformPoint(Tip.position);
            _restTipRotation = Quaternion.Inverse(_root.rotation) * Tip.rotation;
            FlexibleLength = Mathf.Max(.01f, _restTip.z - FlexibleStart);

            // ReelCrank is a sibling, not part of the flexible blank. Do not clone or bend it.
            var body = _root.Find("RodBody");
            if (body == null) return;
            foreach (var filter in body.GetComponentsInChildren<MeshFilter>(true))
            {
                var original = filter.sharedMesh;
                if (original == null || !original.isReadable) continue;
                var toRoot = _root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var originalVertices = original.vertices;
                bool flexible = false;
                for (int i = 0; i < originalVertices.Length; i++)
                    if (toRoot.MultiplyPoint3x4(originalVertices[i]).z > FlexibleStart)
                    { flexible = true; break; }
                if (!flexible) continue;

                var item = new FlexibleMesh
                {
                    Filter = filter,
                    Original = original,
                    Instance = Instantiate(original),
                    ToRoot = toRoot,
                    ToMesh = toRoot.inverse,
                    NormalToRoot = toRoot.inverse.transpose,
                    NormalToMesh = toRoot.transpose,
                    OriginalVertices = originalVertices,
                    RootVertices = new Vector3[originalVertices.Length],
                    Vertices = new Vector3[originalVertices.Length],
                    OriginalNormals = original.normals,
                    RootNormals = new Vector3[originalVertices.Length],
                    Normals = new Vector3[originalVertices.Length],
                    OriginalTangents = original.tangents,
                    RootTangents = new Vector3[originalVertices.Length],
                    Tangents = new Vector4[originalVertices.Length]
                };
                item.Instance.name = original.name + " (instance flexible blank)";
                item.Instance.MarkDynamic();
                for (int i = 0; i < originalVertices.Length; i++)
                {
                    item.RootVertices[i] = toRoot.MultiplyPoint3x4(originalVertices[i]);
                    if(OnSpool(item.RootVertices[i]))SpoolVertexCount++;
                    if (item.OriginalNormals.Length == originalVertices.Length)
                        item.RootNormals[i] = item.NormalToRoot.MultiplyVector(item.OriginalNormals[i]).normalized;
                    if (item.OriginalTangents.Length == originalVertices.Length)
                        item.RootTangents[i] = toRoot.MultiplyVector(item.OriginalTangents[i]).normalized;
                }
                filter.sharedMesh = item.Instance;
                _meshes.Add(item);
            }
        }

        /// <param name="tension">Line load, clamped to 0..1.</param>
        /// <param name="side">Fish's sideways pull in rod coordinates, clamped to -1..1.</param>
        /// <param name="fighting">False gently unloads the blank without distorting the grip.</param>
        /// <param name="deltaTime">Gameplay time; zero completely freezes mesh and spring state.</param>
        public void SetLoad(float tension, float side, bool fighting, float deltaTime)
        {
            if (_root == null || Tip == null || !Finite(deltaTime) || deltaTime <= 0) return;
            tension = Finite(tension) ? Mathf.Clamp01(tension) : 0;
            side = Finite(side) ? Mathf.Clamp(side, -1, 1) : 0;
            if (!fighting && CurrentBend == 0 && CurrentSideBend == 0 && !_spoolDirty) return;
            float target = fighting ? .10f + .95f * Mathf.SmoothStep(0, 1, tension) : 0;
            float targetSide = fighting ? side * (.04f + .12f * tension) : 0;
            float step = Mathf.Min(deltaTime, .1f);
            float smooth = fighting ? .16f : .28f;
            CurrentBend = Mathf.SmoothDamp(CurrentBend, target, ref _bendVelocity, smooth, Mathf.Infinity, step);
            CurrentSideBend = Mathf.SmoothDamp(CurrentSideBend, targetSide, ref _sideVelocity, smooth, Mathf.Infinity, step);
            if (!fighting && CurrentBend < .00005f && Mathf.Abs(CurrentSideBend) < .00005f)
                CurrentBend = CurrentSideBend = _bendVelocity = _sideVelocity = 0;
            ApplyBend();
        }

        private void ApplyBend()
        {
            _spoolDirty=false;
            var spoolRotation=Quaternion.AngleAxis(Mathf.Repeat(SpoolTurns,1)*360,Vector3.forward);
            var direction = new Vector3(CurrentSideBend, -CurrentBend, 0);
            float bend = direction.magnitude;
            if (bend > .000001f) direction /= bend;
            foreach (var item in _meshes)
            {
                bool hasNormals = item.OriginalNormals.Length == item.Vertices.Length;
                bool hasTangents = item.OriginalTangents.Length == item.Vertices.Length;
                for (int i = 0; i < item.Vertices.Length; i++)
                {
                    if(OnSpool(item.RootVertices[i]) && (SpoolTurns!=0 || SpoolFill!=1))
                    {
                        var p=item.RootVertices[i]-new Vector3(0,-.14f,0);
                        // Only the wound line shrinks slightly; metal lips keep their original diameter.
                        if(new Vector2(p.x,p.y).magnitude<.054f){float fill=.85f+.15f*SpoolFill;p.x*=fill;p.y*=fill;}
                        item.Vertices[i]=item.ToMesh.MultiplyPoint3x4(new Vector3(0,-.14f,0)+spoolRotation*p);
                        if(hasNormals)item.Normals[i]=item.NormalToMesh.MultiplyVector(spoolRotation*item.RootNormals[i]).normalized;
                        if(hasTangents){var t=item.ToMesh.MultiplyVector(spoolRotation*item.RootTangents[i]).normalized;item.Tangents[i]=new Vector4(t.x,t.y,t.z,item.OriginalTangents[i].w);}
                        continue;
                    }
                    // Copy rigid vertices verbatim: no numerical drift in grip, cork, seat or reel.
                    if (bend <= .000001f || item.RootVertices[i].z <= FlexibleStart)
                    {
                        item.Vertices[i] = item.OriginalVertices[i];
                        if (hasNormals) item.Normals[i] = item.OriginalNormals[i];
                        if (hasTangents) item.Tangents[i] = item.OriginalTangents[i];
                        continue;
                    }
                    Quaternion rotation;
                    var point = BendPoint(item.RootVertices[i], direction, bend, out rotation);
                    item.Vertices[i] = item.ToMesh.MultiplyPoint3x4(point);
                    if (hasNormals)
                        item.Normals[i] = item.NormalToMesh.MultiplyVector(rotation * item.RootNormals[i]).normalized;
                    if (hasTangents)
                    {
                        var tangent = item.ToMesh.MultiplyVector(rotation * item.RootTangents[i]).normalized;
                        item.Tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, item.OriginalTangents[i].w);
                    }
                }
                item.Instance.vertices = item.Vertices;
                if (hasNormals) item.Instance.normals = item.Normals;
                if (hasTangents) item.Instance.tangents = item.Tangents;
                item.Instance.RecalculateBounds();
            }
            Quaternion tipRotation;
            Tip.position = _root.TransformPoint(BendPoint(_restTip, direction, bend, out tipRotation));
            Tip.rotation = _root.rotation * tipRotation * _restTipRotation;
        }

        private Vector3 BendPoint(Vector3 rest, Vector3 direction, float bend, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (rest.z <= FlexibleStart || bend <= .000001f) return rest;
            // A circular centerline preserves the blank's arc length; rotated cross sections keep
            // the guide rings round instead of flattening/stretched rubber and bending the reel.
            float angle = bend * (rest.z - FlexibleStart) / FlexibleLength;
            float radius = FlexibleLength / bend;
            var tangent = Vector3.forward * Mathf.Cos(angle) + direction * Mathf.Sin(angle);
            rotation = Quaternion.FromToRotation(Vector3.forward, tangent);
            var center = Vector3.forward * (FlexibleStart + radius * Mathf.Sin(angle))
                + direction * (radius * (1 - Mathf.Cos(angle)));
            return center + rotation * new Vector3(rest.x, rest.y, 0);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void ReleaseMeshes()
        {
            if (_root != null && Tip != null)
            {
                Tip.position = _root.TransformPoint(_restTip);
                Tip.rotation = _root.rotation * _restTipRotation;
            }
            foreach (var item in _meshes)
            {
                if (item.Filter != null && item.Filter.sharedMesh == item.Instance)
                    item.Filter.sharedMesh = item.Original;
                if (item.Instance == null) continue;
                if (Application.isPlaying) Destroy(item.Instance);
                else DestroyImmediate(item.Instance);
            }
            _meshes.Clear();
        }

        private void OnDestroy() => ReleaseMeshes();
    }
}
