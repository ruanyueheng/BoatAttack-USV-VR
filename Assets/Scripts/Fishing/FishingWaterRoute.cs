using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>A water-only route with hull clearance; the chart image is never used as a depth map.</summary>
    public sealed class FishingWaterRoute : MonoBehaviour
    {
        private const float Cell = 4f, Clearance = 1.8f;
        private FishingPrototype _c;
        private Transform _root;
        private LineRenderer _halo, _line, _destination;
        private readonly LineRenderer[] _arrows = new LineRenderer[12];
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Vector3> _route = new List<Vector3>(), _draw = new List<Vector3>();
        private readonly Collider[] _overlaps = new Collider[32];
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private Vector3 _goal, _origin;
        private int _target = -1, _width, _depth;
        private byte[] _water;
        private float _nextPlan;
        public IReadOnlyList<Vector3> Points => _route;
        public bool Visible => _root != null && _root.gameObject.activeSelf;
        public string Failure { get; private set; } = "";
        public bool Arrived => _c != null && FishingPrototype.FlatDistance(_c.Player.transform.position, _goal) < 7f;

        public void Build(FishingPrototype c)
        {
            _c = c;
            var map = Resources.Load<FishingMapDefinition>("FishingMap");
            var bounds = map != null && map.IsUsable ? map.worldXZBounds : new Vector4(-600, -500, 600, 400);
            _origin = new Vector3(bounds.x - 80, 0, bounds.y - 80);
            _width = Mathf.CeilToInt((bounds.z - bounds.x + 160) / Cell) + 1;
            _depth = Mathf.CeilToInt((bounds.w - bounds.y + 160) / Cell) + 1;
            _water = new byte[_width * _depth];
            _root = new GameObject("V022 water guidance").transform; _root.SetParent(transform, false);
            var shader = Resources.Load<Shader>("FishingWaterGuidance");
            if (shader == null) throw new InvalidOperationException("Missing water guidance shader");
            var halo = Material(shader, new Color(.015f, .11f, .14f, .55f), false);
            var pearl = Material(shader, new Color(.98f, 1f, .83f, .96f), true);
            var accent = Material(shader, new Color(.83f, 1f, .91f, .9f), false);
            pearl.renderQueue=3101;accent.renderQueue=3102;
            _halo = Line("Route shadow", halo, .9f); _line = Line("Pearl current", pearl, .42f);
            _destination = Line("Arrival ring", accent, .10f); _destination.loop = true;
            for (int i = 0; i < _arrows.Length; i++) _arrows[i] = Line("Flow arrow " + i, accent, .15f);
            _root.gameObject.SetActive(false);
        }
        private Material Material(Shader shader, Color tint, bool flow)
        {
            var material = new Material(shader) { name = "V022 water guide", renderQueue = 3100 };
            material.SetColor("_Tint", tint); material.SetFloat("_Flow", flow ? 1 : 0);
            _materials.Add(material); return material;
        }
        private LineRenderer Line(string name, Material material, float width)
        {
            var line = FishingArt.Line(name, _root, material, width);
            line.transform.rotation = Quaternion.Euler(90, 0, 0); line.alignment = LineAlignment.TransformZ;
            line.numCornerVertices = 5; line.numCapVertices = 5; line.generateLightingData = false;
            line.textureMode = LineTextureMode.Tile; line.receiveShadows = false;
            line.shadowCastingMode = ShadowCastingMode.Off; return line;
        }
        private bool Ignored(Collider col) => col.transform.IsChildOf(_c.Player.transform)
            || col == _c.Shore.Body || col.gameObject.layer == 5 || FishingShore.NonSolidSurface(col);
        public bool ClearPoint(Vector3 p)
        {
            p.y = 1.2f;
            int count = Physics.OverlapCapsuleNonAlloc(p, p + Vector3.up, Clearance, _overlaps, ~(1 << 4), QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!Ignored(_overlaps[i])) return false;
            return true;
        }
        public bool ClearSegment(Vector3 from, Vector3 to)
        {
            from.y = to.y = 1.2f;
            if (!ClearPoint(from) || !ClearPoint(to)) return false;
            var delta = to - from; if (delta.sqrMagnitude < .001f) return true;
            int count = Physics.CapsuleCastNonAlloc(from, from + Vector3.up, Clearance, delta.normalized,
                _hits, delta.magnitude, ~(1 << 4), QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false;
            for (int i = 0; i < count; i++) if (!Ignored(_hits[i].collider)) return false;
            return true;
        }
        private Vector3 Point(int id) => _origin + new Vector3(id % _width * Cell, 0, id / _width * Cell);
        private bool Water(int id)
        {
            if (_water[id] == 0) _water[id] = (byte)(ClearPoint(Point(id)) ? 1 : 2);
            return _water[id] == 1;
        }
        private int Connect(Vector3 p)
        {
            int x = Mathf.RoundToInt((p.x - _origin.x) / Cell), z = Mathf.RoundToInt((p.z - _origin.z) / Cell);
            int best = -1; float distance = float.MaxValue;
            for (int dz = -2; dz <= 2; dz++) for (int dx = -2; dx <= 2; dx++)
            {
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= _width || nz >= _depth) continue;
                int id = nz * _width + nx; float d = (Point(id) - p).sqrMagnitude;
                if (d >= distance || !Water(id) || !ClearSegment(p, Point(id))) continue;
                best = id; distance = d;
            }
            return best;
        }
        public bool Plan()
        {
            _target = _c.NavigationTarget; _goal = _c.NavigationPosition; _goal.y = 0; _route.Clear(); Failure = "";
            var start = _c.Player.transform.position; start.y = 0;
            if (ClearSegment(start, _goal)) { _route.Add(start); _route.Add(_goal); return true; }
            // Moorings have poles beside them. Stop in clear water within boarding reach, never on the jetty.
            var approaches=new Dictionary<int,Vector3>();
            int direct=ClearPoint(_goal)?Connect(_goal):-1;
            if(direct>=0)approaches[direct]=_goal;
            for (int radius = 2; radius <= 8; radius += 2) for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8; var p = _goal + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                if (!ClearPoint(p)) continue; int candidate=Connect(p);if(candidate<0 || approaches.ContainsKey(candidate))continue;
                approaches[candidate]=p;
            }
            if(approaches.Count==0){Failure="航点附近没有可通行的水面";return false;}
            int first = Connect(start);
            if (first < 0) { Failure="船体过于靠近岸边，请退回开阔水面"; return false; }
            int n = _water.Length; var cost = new float[n]; var previous = new int[n]; var closed = new bool[n];
            for (int i = 0; i < n; i++) { cost[i] = float.MaxValue; previous[i] = -1; }
            var open = new SortedSet<(float score, int id)>(); cost[first] = 0;
            open.Add((Vector3.Distance(Point(first), _goal), first));int last=-1;
            while (open.Count > 0)
            {
                var head = open.Min; open.Remove(head); int id = head.id;
                if (closed[id]) continue; closed[id] = true;
                if (approaches.TryGetValue(id,out var approach)) {last=id;_goal=approach;break;}
                int x = id % _width, z = id / _width;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= _width || nz >= _depth) continue;
                    int next = nz * _width + nx;
                    float step = dx != 0 && dz != 0 ? Cell * 1.414214f : Cell;
                    if (closed[next] || cost[id] + step >= cost[next] || !Water(next)) continue;
                    if (dx != 0 && dz != 0 && (!Water(z * _width + nx) || !Water(nz * _width + x))) continue;
                    if (!ClearSegment(Point(id), Point(next))) continue;
                    cost[next] = cost[id] + step; previous[next] = id;
                    open.Add((cost[next] + Vector3.Distance(Point(next), _goal), next));
                }
            }
            if (last<0) { Failure="当前水道不连通，请退回开阔水面"; return false; }
            var reverse = new List<Vector3> { _goal };
            for (int id = last; id >= 0; id = previous[id]) reverse.Add(Point(id));
            reverse.Add(start); reverse.Reverse();
            _route.Add(start);
            for (int i = 0; i < reverse.Count - 1;)
            {
                int next = reverse.Count - 1;
                while (next > i + 1 && !ClearSegment(reverse[i], reverse[next])) next--;
                _route.Add(reverse[next]); i = next;
            }
            return true;
        }
        private void LateUpdate()
        {
            if (_c == null || _c.Player == null) return;
            bool show = !_c.Ashore && !_c.Anchored && !_c.StoryBlocksGameplay;
            _root.gameObject.SetActive(show); if (!show) return;
            var boat = _c.Player.transform.position; boat.y = 0;
            int segment = 0; float nearest = float.MaxValue; Vector3 projection = boat;
            for (int i = 0; i < _route.Count - 1; i++)
            {
                var d = _route[i + 1] - _route[i];
                var p = _route[i] + d * Mathf.Clamp01(Vector3.Dot(boat - _route[i], d) / Mathf.Max(.001f, d.sqrMagnitude));
                float distance = (p - boat).sqrMagnitude;
                if (distance >= nearest) continue; nearest = distance; segment = i; projection = p;
            }
            if (!_c.Paused && (_target != _c.NavigationTarget || (nearest > 100 && Time.time >= _nextPlan)))
            {
                bool planned = Plan(); _nextPlan = Time.time + 2;
                segment = 0; projection = boat;
                if (!planned) { _root.gameObject.SetActive(false); return; }
            }
            if (_route.Count < 2) { _root.gameObject.SetActive(false); return; }
            _draw.Clear();
            if (!Arrived)
            {
                // Start ahead of the hull and discard the wake behind the player.
                var from = projection;
                for (int i = segment + 1; i < _route.Count; i++)
                {
                    var to = _route[i]; int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / 2));
                    for (int j = 0; j < steps; j++)
                    {
                        var p = Vector3.Lerp(from, to, j / (float)steps);
                        if (FishingPrototype.FlatDistance(boat, p) > 3.5f) _draw.Add(p + Vector3.up * .18f);
                    }
                    from = to;
                }
                _draw.Add(_goal + Vector3.up * .18f);
            }
            _line.positionCount = _halo.positionCount = _draw.Count;
            for (int i = 0; i < _draw.Count; i++) { _line.SetPosition(i, _draw[i]); _halo.SetPosition(i, _draw[i]); }
            float length = 0; int arrow = 0;
            for (int i = 1; i < _draw.Count && arrow < _arrows.Length; i++)
            {
                var d = _draw[i] - _draw[i - 1]; float step = d.magnitude;
                if (length + step >= 6 + arrow * 14)
                {
                    var p = _draw[i - 1] + d.normalized * (6 + arrow * 14 - length);
                    var side = Vector3.Cross(Vector3.up, d.normalized) * .65f;
                    var line = _arrows[arrow++]; line.enabled = true; line.positionCount = 3;
                    line.SetPosition(0, p - d.normalized * 1.3f + side); line.SetPosition(1, p);
                    line.SetPosition(2, p - d.normalized * 1.3f - side);
                }
                length += step;
            }
            for (; arrow < _arrows.Length; arrow++) _arrows[arrow].enabled = false;
            _destination.positionCount = 64;
            for (int i = 0; i < 64; i++) { float a = i * Mathf.PI / 32; _destination.SetPosition(i, _goal + new Vector3(Mathf.Cos(a) * 2.8f, .20f, Mathf.Sin(a) * 2.8f)); }
        }
        private void OnDestroy() { foreach (var material in _materials) if (material != null) Destroy(material); }
    }
}
