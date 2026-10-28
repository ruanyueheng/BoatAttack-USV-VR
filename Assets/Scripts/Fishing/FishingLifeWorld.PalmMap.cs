using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingLifeWorld
    {
        public const float ChartWidth = .16f;
        public bool PalmMapShown { get; private set; }
        public Transform PalmMap => _palm;
        public Transform PalmPlayerMarker => _palmPlayer;
        private Transform _palm, _chart, _palmPlayer, _palmTarget, _tideBar;
        private Text _palmText;
        private FishingMapDefinition _map;
        private Material _chartMaterial;
        private float _chartDepth, _palmUpTime, _palmDownTime, _palmScale;

        private void BuildPalmMap()
        {
            _palm = new GameObject("VR palm chart").transform; _palm.SetParent(_root, false);
            _map = Resources.Load<FishingMapDefinition>("FishingMap");
            float aspect = _map != null && _map.IsUsable ? _map.texture.height / (float)_map.texture.width : .7f;
            _chartDepth = ChartWidth * aspect;
            var b = new FishingLifeMesh(); Color wood = new Color(.42f, .27f, .15f), brass = new Color(.8f, .62f, .33f);
            b.Box(new Vector3(0, -.007f, 0), new Vector3(ChartWidth + .016f, .012f, _chartDepth + .016f), wood);
            b.Box(new Vector3(0, -.0005f, _chartDepth * .5f + .006f), new Vector3(ChartWidth + .016f, .002f, .003f), brass);
            b.Box(new Vector3(0, -.0005f, -_chartDepth * .5f - .006f), new Vector3(ChartWidth + .016f, .002f, .003f), brass);
            b.Box(new Vector3(ChartWidth * .5f + .02f, .002f, 0), new Vector3(.012f, .03f, _chartDepth * .8f), wood); // tide gauge post
            MeshObject("Palm chart base", b.Finish("Palm chart base"), _palette, _palm);

            var quad = new Mesh { name = "Palm chart sheet" };
            float hw = ChartWidth * .5f, hd = _chartDepth * .5f;
            quad.vertices = new[] { new Vector3(-hw, 0, -hd), new Vector3(-hw, 0, hd), new Vector3(hw, 0, hd), new Vector3(hw, 0, -hd) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 }; quad.RecalculateNormals(); quad.RecalculateBounds();
            var shader = Resources.Load<Shader>("FishingTerrainMap");
            _chartMaterial = Own(new Material(shader) { name = "Palm chart", renderQueue = 3000 });
            _chartMaterial.SetFloat("_Circular", 0); _chartMaterial.SetFloat("_ZTest", 4); _chartMaterial.SetVector("_ViewUV", new Vector4(0, 0, 1, 1));
            _chartMaterial.SetFloat("_HasMap", _map != null && _map.IsUsable ? 1 : 0);
            if (_map != null && _map.IsUsable) _chartMaterial.mainTexture = _map.texture;
            _chart = MeshObject("Palm chart sheet", quad, _chartMaterial, _palm);

            var arrow = new FishingLifeMesh(); arrow.Cylinder(Vector3.zero, new Vector3(0, .012f, 0), .004f, .004f, Color.white, 6);
            arrow.Cylinder(new Vector3(0, .012f, 0), new Vector3(0, .012f, .011f), .005f, 0, new Color(.98f, .96f, .9f), 3);
            _palmPlayer = MeshObject("Palm chart player arrow", arrow.Finish("Palm player arrow"), _palette, _palm);
            var pin = new FishingLifeMesh(); pin.Cylinder(Vector3.zero, new Vector3(0, .02f, 0), .0012f, .0012f, brass, 5);
            pin.Box(new Vector3(0, .022f, 0), new Vector3(.007f, .007f, .007f), new Color(1, .77f, .42f), Quaternion.Euler(45, 0, 45));
            _palmTarget = MeshObject("Palm chart target pin", pin.Finish("Palm target pin"), _palette, _palm);
            var tide = new FishingLifeMesh(); tide.Box(new Vector3(0, .5f, 0), new Vector3(1, 1, 1), new Color(.35f, .62f, .72f));
            _tideBar = MeshObject("Palm chart tide level", tide.Finish("Palm tide bar"), _palette, _palm);

            var canvasGo = new GameObject("Palm chart label", typeof(RectTransform), typeof(Canvas)); canvasGo.layer = 5; canvasGo.transform.SetParent(_palm, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = _c.viewCamera;
            var rt = (RectTransform)canvasGo.transform; rt.sizeDelta = new Vector2(560, 64); rt.localScale = Vector3.one * .0003f;
            rt.localPosition = new Vector3(0, .004f, -hd - .02f); rt.localRotation = Quaternion.Euler(90, 0, 0);
            var plate = canvasGo.AddComponent<Image>(); plate.color = new Color(.075f, .125f, .23f, 1); plate.raycastTarget = false;
            var label = new GameObject("Text", typeof(RectTransform), typeof(Text)); label.layer = 5; label.transform.SetParent(canvasGo.transform, false);
            var lr = (RectTransform)label.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(12, 4); lr.offsetMax = new Vector2(-12, -4);
            _palmText = label.GetComponent<Text>(); _palmText.font = FishingUIFont.Font; _palmText.fontSize = 30; _palmText.alignment = TextAnchor.MiddleCenter;
            _palmText.color = new Color(.97f, .92f, .82f); _palmText.raycastTarget = false; _palmText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _palm.gameObject.SetActive(false);
        }

        private bool LeftHand(out Vector3 p, out Quaternion q)
        {
            if (DebugLeftHand.HasValue) { p = DebugLeftHand.Value.position; q = DebugLeftHand.Value.rotation; return true; }
            return FishingXRTracking.TryPose(_c.viewCamera, XRNode.LeftHand, out p, out q);
        }
        /// <summary>Left palm (controller +X) faces up within 50°, hand in front of the chest, 20–70 cm from the eye and below it.</summary>
        public static bool PalmUp(Vector3 eye, Vector3 hand, Quaternion handRotation)
        {
            var d = hand - eye; float dist = d.magnitude;
            return Vector3.Angle(handRotation * Vector3.right, Vector3.up) < 50 && dist > .2f && dist < .7f && d.y < -.08f;
        }
        private void UpdatePalmMap()
        {
            var eye = _c.viewCamera.transform;
            bool want = Vr && !_c.Paused && !_c.StoryBlocksGameplay && !_c.XRNotebook.IsOpen && !VrRaised
                && LeftHand(out var p, out var q) && PalmUp(eye.position, p, q);
            // Hysteresis: 0.15 s to appear, 0.2 s to go, so a wobbling wrist never flickers the chart.
            if (want) { _palmUpTime += Time.unscaledDeltaTime; _palmDownTime = 0; } else { _palmDownTime += Time.unscaledDeltaTime; _palmUpTime = 0; }
            if (want && _palmUpTime >= .15f) PalmMapShown = true; else if (!want && _palmDownTime >= .2f) PalmMapShown = false;
            _palmScale = Mathf.MoveTowards(_palmScale, PalmMapShown ? 1 : 0, Time.unscaledDeltaTime / .15f);
            _palm.gameObject.SetActive(_palmScale > 0);
            if (_palmScale <= 0 || !LeftHand(out p, out q)) return;
            FishingUITheme.StyleMap(_chartMaterial);
            var away = Vector3.ProjectOnPlane(p - eye.position, Vector3.up); if (away.sqrMagnitude < 1e-4f) away = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
            _palm.SetPositionAndRotation(p + Vector3.up * .07f, Quaternion.LookRotation(away.normalized, Vector3.up) * Quaternion.Euler(-30, 0, 0));
            _palm.localScale = Vector3.one * Mathf.SmoothStep(0, 1, _palmScale);
            PlaceOnChart(_palmPlayer, _c.ExplorerPosition);
            var look = Vector3.ProjectOnPlane(eye.forward, Vector3.up); if (look.sqrMagnitude > 1e-4f) _palmPlayer.localRotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            PlaceOnChart(_palmTarget, _c.NavigationPosition);
            var cal = _c.Career.Calendar; float phase = (float)(cal.Minute % 720 / 720), level = .5f - .5f * Mathf.Cos(phase * Mathf.PI * 2);
            float bar = _chartDepth * .75f * Mathf.Max(.05f, level);
            _tideBar.localPosition = new Vector3(ChartWidth * .5f + .02f, .0175f, -_chartDepth * .375f); _tideBar.localScale = new Vector3(.006f, .003f, bar);
            float meters = FishingPrototype.FlatDistance(_c.ExplorerPosition, _c.NavigationPosition);
            _palmText.text = cal.TideName + "  ·  " + _c.NavigationName + "  " + Mathf.RoundToInt(meters) + " m";
        }
        /// <summary>World XZ → chart sheet local position (north = away from the viewer).</summary>
        private void PlaceOnChart(Transform marker, Vector3 world)
        {
            bool ok = _map != null && _map.IsUsable && FishingMapMath.TryWorldToUV(_map.Bounds, world.x, world.z, out var u, out var v) && FishingMapMath.KnownUV(u, v);
            marker.gameObject.SetActive(ok);
            if (!ok) return;
            FishingMapMath.TryWorldToUV(_map.Bounds, world.x, world.z, out u, out v);
            marker.localPosition = new Vector3(((float)u - .5f) * ChartWidth, .001f, ((float)v - .5f) * _chartDepth);
        }
    }
}
