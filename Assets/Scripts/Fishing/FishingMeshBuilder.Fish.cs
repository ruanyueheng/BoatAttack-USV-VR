using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Single-mesh, vertex-coloured fish and navigation buoys: one renderer each, no primitives.</summary>
    public sealed partial class FishingMeshBuilder
    {
        public const string FishMeshPrefix = "Fishing v0.14 fish ";
        private static readonly Color EyeWhite = new Color(.95f, .93f, .86f);
        private static readonly Color Pupil = new Color(.03f, .05f, .07f);
        private static readonly Color Pearl = new Color(1f, .88f, .62f);

        /// <summary>Body outline shared by the loft and by everything attached to its surface.</summary>
        private struct BodyShape
        {
            public float Tail, Snout, Width, Height, Peak, Peduncle, Belly;

            public float Z(float t) => Mathf.Lerp(Tail, Snout, t);

            // Widest a little behind the head, blunt at the snout, never thinner than the caudal peduncle.
            // A small floor keeps the snout ring open so the front cap has real area and valid normals.
            public Vector2 Size(float t)
            {
                float s = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(t), Peak))), .75f);
                s = Mathf.Max(s, Mathf.Max(Peduncle * (1f - t), .07f));
                return new Vector2(Width * s, Height * s);
            }

            /// <summary>Point on the skin at body position t and angle a (π/2 is the back, -π/2 the belly).</summary>
            public Vector3 Surface(float t, float a)
            {
                var size = Size(t);
                float s = Mathf.Sin(a);
                return new Vector3(Mathf.Cos(a) * size.x, s * size.y * (s < 0f ? Belly : 1f), Z(t));
            }
        }

        public static string FishMeshName(FishKind kind, bool rare) => FishMeshPrefix + kind + (rare ? " pearl" : "");

        public static Mesh Fish(FishKind kind, bool rare)
        {
            var b = new FishingMeshBuilder();
            switch (kind)
            {
                case FishKind.Dart: b.Dart(); break;
                case FishKind.Puffer: b.Puffer(); break;
                case FishKind.Sailfish: b.Sailfish(); break;
                case FishKind.Coral: b.Coral(); break;
                default: b.CoastalSpecies(kind); break;
            }
            // Pearl variants keep their pattern but shift towards a nacreous gold; dark eyes stay dark.
            if (rare) b.Tint(Pearl, .45f, .12f);
            var mesh = b.Finish(FishMeshName(kind, rare));
            var bounds = mesh.bounds; var vertices = mesh.vertices;
            float scale = FishingSpecies.LengthMeters(kind) / bounds.size.z;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = (vertices[i] - bounds.center) * scale;
            mesh.vertices = vertices; mesh.RecalculateBounds(); return mesh;
        }

        public static Mesh Buoy()
        {
            var b = new FishingMeshBuilder();
            var red = new Color(.91f, .33f, .12f);
            var white = new Color(.96f, .94f, .87f);
            var dark = new Color(.08f, .12f, .15f);
            b.Tube(new Vector3(0, -.4f, 0), new Vector3(0, .1f, 0), .46f, .46f, red, 18);
            b.Tube(new Vector3(0, .1f, 0), new Vector3(0, .25f, 0), .47f, .46f, white, 18);
            b.Tube(new Vector3(0, .25f, 0), new Vector3(0, .36f, 0), .46f, .38f, red, 18);
            b.Tube(new Vector3(0, .36f, 0), new Vector3(0, 1.02f, 0), .35f, .07f, red, 18);
            b.Tube(new Vector3(0, .98f, 0), new Vector3(0, 1.58f, 0), .03f, .026f, dark, 8);
            b.Tube(new Vector3(-.17f, 1.34f, 0), new Vector3(.17f, 1.34f, 0), .02f, .02f, dark, 6);
            b.Ellipsoid(new Vector3(0, 1.66f, 0), new Vector3(.09f, .12f, .09f), new Color(1f, .87f, .46f), 10, 6);
            return b.Finish("Fishing v0.4 navigation buoy");
        }

        /// <summary>
        /// Elliptical sections along +z (tail to snout); paint(position, t, angle) colours each vertex,
        /// with t 0 at the tail and 1 at the snout. The belly half is flattened by the body's Belly factor.
        /// </summary>
        private void Loft(List<float> zs, BodyShape body, Func<Vector3, float, float, Color> paint, int sides = 20)
        {
            int start = _vertices.Count;
            float z0 = zs[0], z1 = zs[zs.Count - 1];
            for (int r = 0; r < zs.Count; r++)
            {
                float t = (zs[r] - z0) / (z1 - z0);
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    var p = body.Surface(t, a);
                    p.z = zs[r];
                    Add(p, paint(p, t, a));
                }
            }
            for (int r = 0; r < zs.Count - 1; r++)
                for (int j = 0; j < sides; j++)
                {
                    int a = start + r * sides + j, b = start + r * sides + (j + 1) % sides, c = a + sides, d = b + sides;
                    Face(a, b, c);
                    Face(b, d, c);
                }
            int last = start + (zs.Count - 1) * sides;
            int back = _vertices.Count;
            Add(new Vector3(0, 0, z0), paint(new Vector3(0, 0, z0), 0f, 0f));
            int front = _vertices.Count;
            Add(new Vector3(0, 0, z1 + .02f), paint(new Vector3(0, 0, z1), 1f, 0f));
            for (int j = 0; j < sides; j++)
            {
                Face(back, start + (j + 1) % sides, start + j);
                Face(front, last + j, last + (j + 1) % sides);
            }
        }

        /// <summary>Evenly spaced rings plus a close pair at every colour edge, so painted bands stay crisp.</summary>
        private static List<float> Stations(BodyShape body, int count, params float[] edges)
        {
            var zs = new List<float>();
            for (int i = 0; i < count; i++) zs.Add(body.Z(i / (count - 1f)));
            foreach (float e in edges)
            {
                zs.Add(body.Z(e) - .004f);
                zs.Add(body.Z(e) + .004f);
            }
            zs.Sort();
            for (int i = zs.Count - 1; i > 0; i--) if (zs[i] - zs[i - 1] < .0015f) zs.RemoveAt(i);
            return zs;
        }

        /// <summary>Double-sided triangle with per-corner colours for gradient fins.</summary>
        public void Fin(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            int start = _vertices.Count;
            Add(a, ca); Add(b, cb); Add(c, cc);
            Face(start, start + 1, start + 2);
            start = _vertices.Count;
            Add(a, ca); Add(c, cc); Add(b, cb);
            Face(start, start + 1, start + 2);
        }

        private void Tint(Color target, float amount, float keepBelowLuminance)
        {
            for (int i = 0; i < _colors.Count; i++)
            {
                var c = _colors[i];
                if (c.r * .3f + c.g * .59f + c.b * .11f < keepBelowLuminance) continue;
                _colors[i] = Color.Lerp(c, target, amount);
            }
        }

        private void Pectorals(BodyShape body, float t, float length, Color root, Color tip)
        {
            var size = body.Size(t);
            foreach (float side in new[] { -1f, 1f })
            {
                var a = new Vector3(side * size.x * .92f, -size.y * .15f, body.Z(t));
                var b = new Vector3(side * size.x * .92f, -size.y * .35f, body.Z(t) - .06f);
                Fin(a, a + new Vector3(side * length * .6f, -length * .35f, -length * .75f), b, root, tip, root);
            }
        }

        private void Eyes(BodyShape body, float t, float lift, float scale = 1f)
        {
            var size = body.Size(t);
            float along = Mathf.Clamp01(lift / Mathf.Max(.001f, size.y));
            float x = size.x * Mathf.Sqrt(1f - along * along) * .96f;
            foreach (float side in new[] { -1f, 1f })
            {
                var centre = new Vector3(side * x, lift, body.Z(t));
                Ellipsoid(centre, new Vector3(.03f, .068f, .068f) * scale, EyeWhite, 10, 6);
                Ellipsoid(centre + new Vector3(side * .022f * scale, 0, .004f), new Vector3(.018f, .044f, .044f) * scale, Pupil, 10, 6);
                Ellipsoid(centre + new Vector3(side * .036f * scale, .018f * scale, .016f * scale), Vector3.one * .011f * scale, Color.white, 6, 4);
            }
        }

        private static float Frac(float x) => x - Mathf.Floor(x);

        private void Coral()
        {
            var body = new BodyShape { Tail = -.46f, Snout = .56f, Width = .2f, Height = .36f, Peak = 1.35f, Peduncle = .2f, Belly = .86f };
            var red = new Color(.92f, .24f, .13f);
            var light = new Color(1f, .6f, .4f);
            var band = new Color(.98f, .95f, .88f);
            Loft(Stations(body, 22, .40f, .49f, .64f, .71f), body, (p, t, a) =>
            {
                float up = Mathf.Sin(a);
                if ((t > .40f && t < .49f) || (t > .64f && t < .71f)) return Color.Lerp(band, Color.white, (up + 1f) * .5f);
                var c = Color.Lerp(light, red, Mathf.Clamp01(up * .6f + .55f));
                return t > .88f ? Color.Lerp(c, new Color(1f, .55f, .22f), (t - .88f) / .12f) : c;
            });
            var finRoot = new Color(.9f, .28f, .15f);
            var finTip = new Color(1f, .74f, .42f);
            var tail = new Vector3(0, 0, body.Tail + .02f);
            for (int k = 0; k < 6; k++)
            {
                float a0 = Mathf.Lerp(-62f, 62f, k / 6f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-62f, 62f, (k + 1) / 6f) * Mathf.Deg2Rad;
                Fin(tail, new Vector3(0, Mathf.Sin(a0) * .36f, body.Tail - .3f - Mathf.Cos(a0) * .08f),
                    new Vector3(0, Mathf.Sin(a1) * .36f, body.Tail - .3f - Mathf.Cos(a1) * .08f), finRoot, finTip, finTip);
            }
            // Spiny dorsal ridge: alternating tall and short rays.
            for (int k = 0; k < 7; k++)
            {
                float t0 = Mathf.Lerp(.34f, .84f, k / 7f), t1 = Mathf.Lerp(.34f, .84f, (k + 1) / 7f);
                var b0 = body.Surface(t0, Mathf.PI * .5f) - Vector3.up * .02f;
                var b1 = body.Surface(t1, Mathf.PI * .5f) - Vector3.up * .02f;
                Fin(b0, (b0 + b1) * .5f + new Vector3(0, .13f + (k % 2) * .05f, -.02f), b1, finRoot, finTip, finRoot);
            }
            Fin(body.Surface(.26f, -Mathf.PI * .5f), body.Surface(.42f, -Mathf.PI * .5f), body.Surface(.3f, -Mathf.PI * .5f) + new Vector3(0, -.15f, -.04f), finRoot, finRoot, finTip);
            Pectorals(body, .72f, .2f, finRoot, finTip);
            Eyes(body, .84f, .07f);
            Ellipsoid(new Vector3(0, -.03f, body.Snout), new Vector3(.05f, .03f, .03f), new Color(.45f, .1f, .08f), 8, 4);
        }

        private void Dart()
        {
            var body = new BodyShape { Tail = -.62f, Snout = .86f, Width = .15f, Height = .2f, Peak = 1.2f, Peduncle = .25f, Belly = .86f };
            var back = new Color(.07f, .27f, .58f);
            var flank = new Color(.52f, .72f, .86f);
            var belly = new Color(.94f, .96f, .98f);
            var stripe = new Color(.98f, .84f, .3f);
            Loft(Stations(body, 26), body, (p, t, a) =>
            {
                float up = Mathf.Sin(a);
                if (Mathf.Abs(up) < .14f && t > .12f && t < .9f) return stripe;
                return up > 0f ? Color.Lerp(flank, back, Mathf.SmoothStep(0f, 1f, up * 1.4f)) : Color.Lerp(flank, belly, Mathf.SmoothStep(0f, 1f, -up * 1.6f));
            }, 24);
            var finRoot = new Color(.1f, .32f, .6f);
            var finTip = new Color(.62f, .84f, .96f);
            var root = new Vector3(0, 0, body.Tail + .02f);
            // Deeply forked caudal lobes for a fast swimmer.
            foreach (float side in new[] { -1f, 1f })
                Fin(root, new Vector3(0, side * .34f, body.Tail - .44f), new Vector3(0, side * .05f, body.Tail - .17f), finRoot, finTip, finRoot);
            Fin(body.Surface(.46f, Mathf.PI * .5f), body.Surface(.62f, Mathf.PI * .5f), body.Surface(.5f, Mathf.PI * .5f) + new Vector3(0, .17f, -.06f), finRoot, finRoot, finTip);
            Fin(body.Surface(.2f, Mathf.PI * .5f), body.Surface(.28f, Mathf.PI * .5f), body.Surface(.22f, Mathf.PI * .5f) + new Vector3(0, .08f, -.05f), finRoot, finRoot, finTip);
            Fin(body.Surface(.2f, -Mathf.PI * .5f), body.Surface(.3f, -Mathf.PI * .5f), body.Surface(.22f, -Mathf.PI * .5f) + new Vector3(0, -.08f, -.05f), finRoot, finRoot, finTip);
            Pectorals(body, .74f, .18f, finRoot, finTip);
            Eyes(body, .87f, .035f, .9f);
            Ellipsoid(new Vector3(0, -.01f, body.Snout), new Vector3(.03f, .02f, .03f), new Color(.1f, .15f, .22f), 8, 4);
        }

        private void Puffer()
        {
            var body = new BodyShape { Tail = -.45f, Snout = .55f, Width = .42f, Height = .42f, Peak = 1.05f, Peduncle = .18f, Belly = .95f };
            var top = new Color(.95f, .76f, .3f);
            var belly = new Color(.99f, .94f, .8f);
            Loft(Stations(body, 20), body, (p, t, a) => Color.Lerp(belly, top, Mathf.SmoothStep(0f, 1f, Mathf.Sin(a) * .8f + .45f)), 24);
            var spot = new Color(.42f, .27f, .1f);
            var spine = new Color(.98f, .92f, .74f);
            // Golden-ratio scatter keeps the spots and spines evenly spread without randomness.
            for (int i = 0; i < 12; i++)
                Ellipsoid(body.Surface(.3f + .45f * Frac(i * .618f), Mathf.Lerp(.35f, 2.8f, Frac(i * .382f + .1f))), Vector3.one * (.04f + .015f * (i % 3)), spot, 8, 5);
            float centreZ = body.Z(.55f);
            for (int i = 0; i < 18; i++)
            {
                var p = body.Surface(.22f + .6f * Frac(i * .7548f), i * 2.39996f);
                var n = new Vector3(p.x, p.y, (p.z - centreZ) * .35f).normalized;
                Tube(p - n * .01f, p + n * .1f, .02f, .002f, spine, 5);
            }
            var finRoot = new Color(.92f, .68f, .26f);
            var finTip = new Color(1f, .9f, .6f);
            var root = new Vector3(0, 0, body.Tail + .03f);
            for (int k = 0; k < 4; k++)
            {
                float a0 = Mathf.Lerp(-50f, 50f, k / 4f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-50f, 50f, (k + 1) / 4f) * Mathf.Deg2Rad;
                Fin(root, new Vector3(0, Mathf.Sin(a0) * .22f, body.Tail - .2f), new Vector3(0, Mathf.Sin(a1) * .22f, body.Tail - .2f), finRoot, finTip, finTip);
            }
            Fin(body.Surface(.22f, Mathf.PI * .5f), body.Surface(.32f, Mathf.PI * .5f), body.Surface(.24f, Mathf.PI * .5f) + new Vector3(0, .12f, -.06f), finRoot, finRoot, finTip);
            Pectorals(body, .66f, .2f, finRoot, finTip);
            Eyes(body, .78f, .14f, 1.25f);
            Ellipsoid(new Vector3(0, -.04f, body.Snout), new Vector3(.07f, .045f, .04f), new Color(.5f, .32f, .12f), 8, 4);
        }

        private static float SailHeight(float u) => (.3f + .75f * Mathf.Pow(u, .7f)) * (u > .88f ? Mathf.Lerp(1f, .35f, (u - .88f) / .12f) : 1f);

        private void Sailfish()
        {
            var body = new BodyShape { Tail = -1.25f, Snout = 1.15f, Width = .2f, Height = .34f, Peak = 1.25f, Peduncle = .12f, Belly = .9f };
            var back = new Color(.05f, .13f, .3f);
            var flank = new Color(.18f, .45f, .8f);
            var belly = new Color(.9f, .93f, .96f);
            var bar = new Color(.62f, .86f, 1f);
            var edges = new List<float>();
            for (int k = 3; k <= 10; k++) { edges.Add(k / 14f); edges.Add((k + .28f) / 14f); }
            Loft(Stations(body, 30, edges.ToArray()), body, (p, t, a) =>
            {
                float up = Mathf.Sin(a);
                var c = up > .15f ? Color.Lerp(flank, back, Mathf.SmoothStep(0f, 1f, (up - .15f) * 1.6f)) : Color.Lerp(flank, belly, Mathf.SmoothStep(0f, 1f, (.15f - up) * 1.3f));
                // Vertical bars of pale spots along the flanks, as on real sailfish.
                bool barred = t > 3f / 14f && t < 10.28f / 14f && Mathf.Repeat(t * 14f, 1f) < .28f && up > -.35f && up < .72f;
                return barred ? Color.Lerp(c, bar, .72f) : c;
            }, 24);
            var navy = new Color(.07f, .14f, .3f);
            Tube(new Vector3(0, 0f, body.Snout - .04f), new Vector3(0, .05f, body.Snout + .82f), .05f, .006f, navy, 8);
            var sailBase = new Color(.14f, .34f, .72f);
            var sailTip = new Color(1f, .78f, .36f);
            const int segments = 12;
            for (int k = 0; k < segments; k++)
            {
                float u0 = k / (float)segments, u1 = (k + 1f) / segments;
                var b0 = body.Surface(Mathf.Lerp(.28f, .86f, u0), Mathf.PI * .5f) - Vector3.up * .03f;
                var b1 = body.Surface(Mathf.Lerp(.28f, .86f, u1), Mathf.PI * .5f) - Vector3.up * .03f;
                var h0 = b0 + new Vector3(0, SailHeight(u0), -.1f);
                var h1 = b1 + new Vector3(0, SailHeight(u1), -.1f);
                // Alternate panel tint so the sail's rays read at a distance.
                var tint = k % 2 == 0 ? sailTip : Color.Lerp(sailTip, sailBase, .25f);
                Fin(b0, h0, b1, sailBase, tint, sailBase);
                Fin(b1, h0, h1, sailBase, tint, tint);
            }
            var root = new Vector3(0, 0, body.Tail + .03f);
            foreach (float side in new[] { -1f, 1f })
            {
                var mid = new Vector3(0, side * .3f, body.Tail - .26f);
                var inner = new Vector3(0, side * .07f, body.Tail - .22f);
                Fin(root, mid, inner, navy, flank, navy);
                Fin(mid, new Vector3(0, side * .64f, body.Tail - .6f), inner, flank, bar, navy);
                Tube(body.Surface(.72f, -Mathf.PI * .5f) + new Vector3(side * .03f, .02f, 0), body.Surface(.62f, -Mathf.PI * .5f) + new Vector3(side * .07f, -.42f, -.25f), .014f, .004f, navy, 5);
            }
            Pectorals(body, .78f, .45f, navy, flank);
            Fin(body.Surface(.24f, -Mathf.PI * .5f), body.Surface(.36f, -Mathf.PI * .5f), body.Surface(.26f, -Mathf.PI * .5f) + new Vector3(0, -.18f, -.08f), navy, navy, flank);
            Fin(body.Surface(.12f, Mathf.PI * .5f), body.Surface(.18f, Mathf.PI * .5f), body.Surface(.13f, Mathf.PI * .5f) + new Vector3(0, .09f, -.06f), navy, navy, flank);
            Eyes(body, .91f, .05f, 1.1f);
        }
    }
}
