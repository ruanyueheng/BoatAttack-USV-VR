using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        // The chart's marker projection must use this SAME radius and ChartRangeMeters.
        public const float TerrainMapRadius = 96f;
        private FishingMapDefinition _terrainMap;
        private Material _terrainMapMaterial;
        private bool _terrainMapLoaded;
        private static Color MapSurface => FishingUITheme.Paper;
        // 小面积标记采用经过色觉分离检查的三组色；文字/形状同时表达身份。
        private static readonly Color MapPortColor = new Color32(57,135,229,255);
        private static readonly Color MapFishColor = new Color32(217,89,38,255);
        private static readonly Color MapShoreColor = new Color32(25,158,112,255);
        private static Color MapWater => FishingUITheme.MapWater;
        private static Color MapLow => FishingUITheme.MapLow;
        private static Color MapLand => FishingUITheme.MapLand;
        private static Color MapHigh => FishingUITheme.MapHigh;

        private void EnsureTerrainMap()
        {
            if (_terrainMapLoaded) return;
            _terrainMapLoaded = true;
            _terrainMap = Resources.Load<FishingMapDefinition>("FishingMap");
            var shader = Resources.Load<Shader>("FishingTerrainMap");
            if (shader != null && shader.isSupported)
                _terrainMapMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public bool HasHomeChartTerrain
        {
            get { EnsureTerrainMap(); return _terrainMap != null && _terrainMap.IsUsable; }
        }

        private string HomeChartTerrainStatus => !HasHomeChartTerrain
            ? "地形资源缺失 · 当前为虚构海域示意图，不代表岛屿形状 / 水深 / 可通航"
            : _terrainMapMaterial == null ? "已加载海域底图 · 地图着色器不可用，显示原始烘焙图像"
            : "回潮海 · 游戏内虚构海域 · 高程 0–20m · 斜纹为未采集区域 · 不代表水深 / 可通航";

        private void DrawHomeTerrainMap(Rect area)
        {
            EnsureTerrainMap();
            if (Event.current.type != EventType.Repaint || !HasHomeChartTerrain) return;
            var center = Controller.HomeChartCenterXZ;
            float range = Controller.HomeChartRangeMeters;
            float halfX = range * area.width / area.height;
            var bounds = _terrainMap.worldXZBounds;
            var uv = new Rect((center.x - halfX - bounds.x) / (bounds.z - bounds.x),
                (center.y - range - bounds.y) / (bounds.w - bounds.y),
                2 * halfX / (bounds.z - bounds.x), 2 * range / (bounds.w - bounds.y));
            if (_terrainMapMaterial == null)
            {
                float u0 = Mathf.Clamp01(uv.xMin), u1 = Mathf.Clamp01(uv.xMax);
                float v0 = Mathf.Clamp01(uv.yMin), v1 = Mathf.Clamp01(uv.yMax);
                if (u1 <= u0 || v1 <= v0) return;
                var visible = Rect.MinMaxRect(area.x + (u0 - uv.xMin) / uv.width * area.width,
                    area.y + (uv.yMax - v1) / uv.height * area.height,
                    area.x + (u1 - uv.xMin) / uv.width * area.width,
                    area.y + (uv.yMax - v0) / uv.height * area.height);
                GUI.DrawTextureWithTexCoords(visible, _terrainMap.texture, Rect.MinMaxRect(u0, v0, u1, v1));
                return;
            }
            _terrainMapMaterial.SetFloat("_HasMap", 1);
            _terrainMapMaterial.SetFloat("_Circular", 0);
            _terrainMapMaterial.SetVector("_ViewUV", new Vector4(uv.x, uv.y, uv.width, uv.height));
            FishingUITheme.StyleMap(_terrainMapMaterial);
            var matrix = GUI.matrix; var color = GUI.color;
            try
            {
                var a = matrix.MultiplyPoint3x4(new Vector3(area.xMin, area.yMin, 0));
                var b = matrix.MultiplyPoint3x4(new Vector3(area.xMax, area.yMax, 0));
                GUI.matrix = Matrix4x4.identity; GUI.color = Color.white;
                Graphics.DrawTexture(Rect.MinMaxRect(a.x, a.y, b.x, b.y), _terrainMap.texture,
                    new Rect(0, 0, 1, 1), 0, 0, 0, 0, Faded(Color.white), _terrainMapMaterial, 0);
            }
            finally { GUI.matrix = matrix; GUI.color = color; }
        }

        /// <summary>仅底图绘制限制为 Repaint，交互按钮保持接收全部 GUI 事件。</summary>
        private void DrawTerrainMap(Vector2 center, float radius)
        {
            if (Event.current.type != EventType.Repaint || radius <= 0 || Controller == null) return;
            EnsureTerrainMap();
            if (_terrainMapMaterial == null)
            {
                Disc(center, radius, MapSurface);
                Label(new Rect(center.x - radius, center.y + 20, radius * 2, 20), "地形未烘焙", 12, Muted, false, TextAnchor.MiddleCenter);
                return;
            }
            var position = Controller.ExplorerPosition;
            FishingMapMath.ViewUV view = default;
            bool known = _terrainMap != null && _terrainMap.IsUsable
                && FishingMapMath.TryViewUV(_terrainMap.Bounds, position.x, position.z, Controller.ChartRangeMeters, out view)
                && Mathf.Abs((float)view.MinU) < float.MaxValue && Mathf.Abs((float)view.MinV) < float.MaxValue
                && (float)view.Width > 0 && (float)view.Width < float.MaxValue
                && (float)view.Height > 0 && (float)view.Height < float.MaxValue;
            _terrainMapMaterial.SetFloat("_HasMap", known ? 1 : 0);
            _terrainMapMaterial.SetFloat("_Circular", 1);
            FishingUITheme.StyleMap(_terrainMapMaterial);
            _terrainMapMaterial.SetVector("_ViewUV", known
                ? new Vector4((float)view.MinU, (float)view.MinV, (float)view.Width, (float)view.Height) : Vector4.zero);
            var matrix = GUI.matrix;
            var color = GUI.color;
            try
            {
                // Graphics.DrawTexture uses physical screen pixels. Apply the HUD transform exactly once.
                Vector3 a = matrix.MultiplyPoint3x4(new Vector3(center.x - radius, center.y - radius, 0));
                Vector3 b = matrix.MultiplyPoint3x4(new Vector3(center.x + radius, center.y + radius, 0));
                GUI.matrix = Matrix4x4.identity;
                GUI.color = Color.white;
                Graphics.DrawTexture(Rect.MinMaxRect(a.x, a.y, b.x, b.y), known ? _terrainMap.texture : Texture2D.whiteTexture,
                    new Rect(0, 0, 1, 1), 0, 0, 0, 0, Faded(Color.white), _terrainMapMaterial, 0);
            }
            finally { GUI.matrix = matrix; GUI.color = color; }
        }

        /// <summary>Replace the old chart footer; north label remains the chart's existing N.
        /// Reserve 36 logical pixels below the circle for this two-line noninteractive guide.</summary>
        private void DrawTerrainMapGuide(Vector2 center, float radius, Rect footer)
        {
            if (Event.current.type != EventType.Repaint) return;
            var matrix = GUI.matrix;
            var color = GUI.color;
            try
            {
                float metres = (float)FishingMapMath.ScaleMeters(Controller.ChartRangeMeters);
                float pixels = metres / Controller.ChartRangeMeters * radius;
                float x = footer.x + 4, y = footer.y + 11;
                if (pixels > 0)
                {
                    Segment(new Vector2(x, y), new Vector2(x + pixels, y), Muted, 2);
                    Segment(new Vector2(x, y - 3), new Vector2(x, y + 3), Muted, 1);
                    Segment(new Vector2(x + pixels, y - 3), new Vector2(x + pixels, y + 3), Muted, 1);
                    WorldLabel(x + pixels + 5, footer.y + 1, 46, 18, metres.ToString("0.##") + " m", 11, Muted);
                }
                float legendX = footer.x + 94;
                Fill(legendX, footer.y + 7, 6, 7, MapLow);
                Fill(legendX + 6, footer.y + 7, 6, 7, MapLand);
                Fill(legendX + 12, footer.y + 7, 6, 7, MapHigh);
                WorldLabel(legendX + 21, footer.y + 1, 78, 18, "高程0–20m", 10, Muted);
                Fill(legendX + 102, footer.y + 7, 7, 7, MapWater);
                WorldLabel(legendX + 112, footer.y + 1, 40, 18, "水面", 10, Muted);
                string note = _terrainMap != null && _terrainMap.IsUsable
                    ? "北↑ · 斜纹未知 · 不代表水深 / 可通航" : "北↑ · 地形未烘焙 · 仅显示方位标记";
                if(!Controller.Ashore && Controller.WaterRoute!=null && Controller.WaterRoute.Failure.Length>0)note=Controller.WaterRoute.Failure;
                WorldLabel(new Rect(footer.x, footer.y + 18, footer.width, 18), note, 11, Muted, false, TextAnchor.MiddleCenter);
            }
            finally { GUI.matrix = matrix; GUI.color = color; }
        }

        /// <summary>Call from HUD.OnDestroy. Also call after an editor rebake to reload the resource.</summary>
        public void ReleaseTerrainMap()
        {
            if (_terrainMapMaterial != null) Destroy(_terrainMapMaterial);
            _terrainMapMaterial = null;
            _terrainMap = null;
            _terrainMapLoaded = false;
        }
    }
}
