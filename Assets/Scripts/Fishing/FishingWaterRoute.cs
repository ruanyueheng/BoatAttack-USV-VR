using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>A water-only route with hull clearance; the chart image is never used as a depth map.</summary>
    public sealed partial class FishingWaterRoute : MonoBehaviour
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
        private readonly Dictionary<Collider,bool> _nonSolid=new Dictionary<Collider,bool>();
        private Vector3 _goal, _origin;
        private int _target = -1, _width, _depth;
        private byte[] _water;
        private float _nextPlan,_nextDraw;
        private Vector3 _drawnGoal;
        private bool _destinationDrawn;
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
            BuildPlanningBuffers();
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
        private bool Ignored(Collider col)
        {
            if(col.transform.IsChildOf(_c.Player.transform) || col==_c.Shore.Body || col.gameObject.layer==5)return true;
            // shortcut: water/NavMesh collision roles are fixed after construction; clear this cache if existing colliders are renamed or reparented at runtime.
            if(!_nonSolid.TryGetValue(col,out bool ignored)){ignored=FishingShore.NonSolidSurface(col,false);_nonSolid.Add(col,ignored);}
            return ignored;
        }
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
        private void LateUpdate()
        {
            if (_c == null || _c.Player == null) return;
            bool show = !_c.Ashore && !_c.Anchored && !_c.StoryBlocksGameplay;
            _root.gameObject.SetActive(show); if (!show) { CancelPlanning();_nextDraw=0;return; }
            if (Planning && _planningTarget != _c.NavigationTarget) CancelPlanning();
            var boat = _c.Player.transform.position; boat.y = 0;
            int segment = 0; float nearest = float.MaxValue; Vector3 projection = boat;
            for (int i = 0; i < _route.Count - 1; i++)
            {
                var d = _route[i + 1] - _route[i];
                var p = _route[i] + d * Mathf.Clamp01(Vector3.Dot(boat - _route[i], d) / Mathf.Max(.001f, d.sqrMagnitude));
                float distance = (p - boat).sqrMagnitude;
                if (distance >= nearest) continue; nearest = distance; segment = i; projection = p;
            }
            if (!_c.Paused && !Planning && (_target != _c.NavigationTarget || ((nearest > 100 || _routeTarget != _c.NavigationTarget) && Time.time >= _nextPlan)))RequestPlan();
            if (!_c.Paused && Planning)
            {
                bool committed=AdvancePlanning();
                if(committed){segment=0;projection=boat;_nextDraw=0;}
            }
            if (_route.Count < 2 || _routeTarget != _c.NavigationTarget) { _root.gameObject.SetActive(false);_nextDraw=0;return; }
            if(Time.time<_nextDraw)return;
            _nextDraw=Time.time+1f/15; // Flow remains smooth in the shader; the route's geometry changes more slowly.
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
            if(!_destinationDrawn || _drawnGoal!=_goal)
            {
                _destinationDrawn=true;_drawnGoal=_goal;_destination.positionCount=64;
                for(int i=0;i<64;i++){float a=i*Mathf.PI/32;_destination.SetPosition(i,_goal+new Vector3(Mathf.Cos(a)*2.8f,.20f,Mathf.Sin(a)*2.8f));}
            }
        }
        private void OnDisable()=>CancelPlanning();
        private void OnDestroy() { CancelPlanning();foreach (var material in _materials) if (material != null) Destroy(material); }
    }
}
