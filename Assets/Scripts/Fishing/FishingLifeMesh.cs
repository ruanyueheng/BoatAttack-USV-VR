using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Tiny vertex-colour builder for the life props: boxes, faceted cylinders/cones and double-sided triangles.</summary>
    internal sealed class FishingLifeMesh
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Color> _c = new List<Color>();
        private readonly List<int> _t = new List<int>();
        public int Triangles => _t.Count / 3;

        public void Box(Vector3 center, Vector3 size, Color color, Quaternion? rotation = null)
        {
            var q = rotation ?? Quaternion.identity; var h = size * .5f;
            Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var f in n)
            {
                Vector3 u = Mathf.Abs(f.y) > .5f ? Vector3.right : Vector3.up, w = Vector3.Cross(f, u);
                int s = _v.Count;
                for (int i = 0; i < 4; i++)
                {
                    float a = i == 0 || i == 3 ? -1 : 1, b = i < 2 ? -1 : 1;
                    _v.Add(center + q * Vector3.Scale(f + u * a + w * b, h)); _c.Add(color);
                }
                _t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); // u×w == f, so this winding faces outward
            }
        }
        public void Cylinder(Vector3 from, Vector3 to, float r0, float r1, Color color, int sides = 8)
        {
            var axis = to - from; if (axis.sqrMagnitude < 1e-8f) return;
            var q = Quaternion.FromToRotation(Vector3.up, axis.normalized); int s = _v.Count;
            for (int ring = 0; ring < 2; ring++) for (int j = 0; j < sides; j++)
            {
                float a = (j + .5f) * Mathf.PI * 2 / sides;
                _v.Add((ring == 0 ? from : to) + q * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (ring == 0 ? r0 : r1)); _c.Add(color);
            }
            int bottom = _v.Count; _v.Add(from); _c.Add(color); int top = _v.Count; _v.Add(to); _c.Add(color);
            for (int j = 0; j < sides; j++)
            {
                int a = s + j, b = s + (j + 1) % sides, c = a + sides, d = b + sides;
                _t.AddRange(new[] { a, c, b, b, c, d, bottom, a, b, top, d, c });
            }
        }
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int s = _v.Count; _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(a); _v.Add(c); _v.Add(b);
            for (int i = 0; i < 6; i++) _c.Add(color);
            _t.AddRange(new[] { s, s + 1, s + 2, s + 3, s + 4, s + 5 });
        }
        public Mesh Finish(string name)
        {
            var mesh = new Mesh { name = name };
            if (_v.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_v); mesh.SetColors(_c); mesh.SetTriangles(_t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
