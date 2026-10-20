using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private Vector2 _homeChartListScroll;
        private bool _homeChartDragging;
        private int _homeChartPaintFrame=-1;

        public static Vector2 HomeChartProject(Vector3 world, Vector2 centerXZ, float rangeMeters, Rect area)
        {
            float pixelsPerMeter = area.height / (2 * rangeMeters);
            return area.center + new Vector2(world.x - centerXZ.x, centerXZ.y - world.z) * pixelsPerMeter;
        }

        public void DrawHomeChart()
        {
            var c = Controller;
            if (c == null || !c.HomeChartOpen) return;
            if(Time.frameCount>_homeChartPaintFrame+1)_homeChartDragging=false;
            _homeChartPaintFrame=Time.frameCount;
            Fill(-_offset / _scale, 0, Screen.width / _scale, _height, new Color(.015f, .035f, .045f, .88f));
            var panel = new Rect(52, 38, 1496, _height - 76);
            Card(panel, Gold);
            Label(panel.x + 28, panel.y + 18, 600, 34, "航线书桌 · 回潮海航海图", 27, InkText, true);
            Label(panel.x + 28, panel.y + 58, 1000, 24, "独立阅读窗口  /  滚轮缩放  /  拖动底图  /  点击航点标记目的地", 14, Muted);
            if (Button(new Rect(panel.xMax - 178, panel.y + 20, 150, 42), "Esc 关闭海图", true, false, 16))
            { c.CloseHomeChart(); _homeChartDragging = false; return; }

            var map = new Rect(panel.x + 28, panel.y + 142, 1086, panel.height - 228);
            var list = new Rect(map.xMax + 20, map.y, 334, map.height);
            if (Button(new Rect(map.x, panel.y + 94, 92, 34), "＋ 放大", c.HomeChartRangeMeters > 40, false, 14)) c.ZoomHomeChart(1);
            if (Button(new Rect(map.x + 102, panel.y + 94, 92, 34), "－ 缩小", c.HomeChartRangeMeters < 1800, false, 14)) c.ZoomHomeChart(-1);
            if (Button(new Rect(map.x + 204, panel.y + 94, 114, 34), "定位当前位置", true, false, 14)) c.CenterHomeChartOnPlayer();
            if (Button(new Rect(map.x + 328, panel.y + 94, 90, 34), "全域", true, false, 14)) c.ResetHomeChartView();
            Label(map.x + 434, panel.y + 99, 640, 24, "视野半径 " + c.HomeChartRangeMeters.ToString("0") + " m  ·  已选：" + c.NavigationName, 15, Gold, true);
            Rounded(map, MapSurface, 4);
            DrawHomeUnknownGrid(map);
            if (HasHomeChartTerrain) DrawHomeTerrainMap(map); else DrawHomeChartFallback(map);
            DrawHomeChartGrid(map);
            DrawHomeChartWaypoints(map);
            DrawHomeChartScale(map);
            Outline(map, Gold, 4, 1.5f);
            Rounded(new Rect(map.x + 8, map.y + 6, 76, 25),MapSurface,3);
            Label(new Rect(map.x + 14, map.y + 7, 68, 22), "北 ↑ N", 15, InkText, true);
            Label(panel.x + 28, panel.yMax - 64, panel.width - 56, 22, HomeChartTerrainStatus, 13, Muted);
            Label(panel.x + 28, panel.yMax - 38, panel.width - 56, 20, "蓝色港口 · 橙色渔场 · 绿色岛屿 · 金圈为目标  /  未开放航点不可选  /  关闭后回到原镜头与游戏状态", 12, Muted);
            DrawHomeChartWaypointList(list);
            HandleHomeChartMapInput(map);
        }

        private void DrawHomeUnknownGrid(Rect area)
        {
            for (float x = area.x; x < area.xMax; x += 24)
                Segment(new Vector2(x, area.y), new Vector2(Mathf.Min(x + 24, area.xMax), Mathf.Min(area.y + 24, area.yMax)), Hairline * .3f);
            for (float y = area.y + 24; y < area.yMax; y += 24)
                Segment(new Vector2(area.x, y), new Vector2(Mathf.Min(area.x + 24, area.xMax), Mathf.Min(y + 24, area.yMax)), Hairline * .3f);
        }

        private void DrawHomeChartGrid(Rect area)
        {
            for (int i = 1; i < 8; i++)
            {
                float x = Mathf.Lerp(area.x, area.xMax, i / 8f);
                Segment(new Vector2(x, area.y), new Vector2(x, area.yMax), InkText * new Color(1, 1, 1, .12f));
            }
            for (int i = 1; i < 5; i++)
            {
                float y = Mathf.Lerp(area.y, area.yMax, i / 5f);
                Segment(new Vector2(area.x, y), new Vector2(area.xMax, y), InkText * new Color(1, 1, 1, .12f));
            }
        }

        private void DrawHomeChartFallback(Rect area)
        {
            // 仅示意现有虚构地点的相对方位，不伪造缺失的地形数据。
            var c = Controller;
            for (int i = 0; i < c.NavigationCount; i++)
            {
                if (!c.HomeChartWaypoint(i, out var world, out _, out _) || i <= c.fishingSpots.Length) continue;
                var p = HomeChartProject(world, c.HomeChartCenterXZ, c.HomeChartRangeMeters, area);
                float radius = Mathf.Clamp(24 / c.HomeChartRangeMeters * area.height, 4, 24);
                if (area.Contains(p + Vector2.one * radius) && area.Contains(p - Vector2.one * radius))
                {
                    Disc(p, radius, MapLow); Outline(new Rect(p.x - radius, p.y - radius, radius * 2, radius * 2), MapLand, radius, 1.5f);
                }
            }
            Label(new Rect(area.center.x - 220, area.y + 30, 440, 26), "虚构海域方位示意 · 地形资源未加载", 18, InkText, true, TextAnchor.MiddleCenter);
        }

        private void DrawHomeChartWaypoints(Rect area)
        {
            var c = Controller;
            var player = HomeChartProject(c.ExplorerPosition, c.HomeChartCenterXZ, c.HomeChartRangeMeters, area);
            var boat = HomeChartProject(c.Player.transform.position, c.HomeChartCenterXZ, c.HomeChartRangeMeters, area);
            if (c.HomeChartWaypoint(c.NavigationTarget, out var destination, out _, out _))
            {
                var target = HomeChartProject(destination, c.HomeChartCenterXZ, c.HomeChartRangeMeters, area);
                if (area.Contains(player) && area.Contains(target))
                    for (float d = 12; d < Vector2.Distance(player, target) - 14; d += 14)
                        Segment(player + (target - player).normalized * d, player + (target - player).normalized * (d + 6), Gold, 2);
            }
            for (int i = 0; i < c.NavigationCount; i++)
            {
                if (!c.HomeChartWaypoint(i, out var world, out var name, out var available)) continue;
                var p = HomeChartProject(world, c.HomeChartCenterXZ, c.HomeChartRangeMeters, area);
                if (p.x < area.x + 18 || p.x > area.xMax - 18 || p.y < area.y + 18 || p.y > area.yMax - 18) continue;
                var color = !available ? Faint : i == 0 ? MapPortColor : i <= c.fishingSpots.Length ? MapFishColor : MapShoreColor;
                if (i == c.NavigationTarget) Outline(new Rect(p.x - 17, p.y - 17, 34, 34), Gold, 17, 2);
                Disc(p, 12, MapSurface); Outline(new Rect(p.x - 12, p.y - 12, 24, 24), color, 12, 2);
                Label(new Rect(p.x - 12, p.y - 12, 24, 24), i == 0 ? "港" : i == c.HomeNavigationIndex ? "家" : i.ToString(), 12, InkText, true, TextAnchor.MiddleCenter);
                bool enabled = GUI.enabled; GUI.enabled = enabled && available;
                if (GUI.Button(new Rect(p.x - 16, p.y - 16, 32, 32), new GUIContent("", name + (available ? "" : " · 尚未开放")), GUIStyle.none))
                    c.SelectHomeChartNavigation(i);
                GUI.enabled = enabled;
            }
            if(c.Home!=null && c.HomeChartRangeMeters<180)
            {
                var garden=HomeChartProject(c.Home.Origin+new Vector3(20,0,0),c.HomeChartCenterXZ,c.HomeChartRangeMeters,area);
                if(area.Contains(garden)){Disc(garden,5,Mint);Label(garden.x+9,garden.y-10,100,22,"回潮菜园",12,InkText,true);}
            }
            if (area.Contains(boat) && c.Ashore) { Disc(boat, 7, MapPortColor); Label(boat.x + 9, boat.y - 10, 70, 20, "停泊船", 12, InkText, true); }
            if (player.x > area.x + 12 && player.x < area.xMax - 12 && player.y > area.y + 12 && player.y < area.yMax - 12)
            {
                Disc(player, 11, MapSurface); DrawArrow(player, c.Ashore ? c.viewCamera.transform.eulerAngles.y : c.Player.transform.eulerAngles.y, InkText, 9);
            }
            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
            {
                var tip = new Rect(area.x + 12, area.yMax - 38, 400, 27); Rounded(tip, MapSurface, 3);
                Label(tip.x + 8, tip.y + 3, tip.width - 16, 22, GUI.tooltip, 13, InkText, true);
            }
        }

        private void DrawHomeChartScale(Rect area)
        {
            float meters = (float)FishingMapMath.ScaleMeters(Controller.HomeChartRangeMeters);
            float pixels = meters / Controller.HomeChartRangeMeters * area.height * .5f;
            var a = new Vector2(area.x + 22, area.yMax - 17); var b = a + Vector2.right * pixels;
            Segment(a, b, InkText, 2); Segment(a - Vector2.up * 4, a + Vector2.up * 4, InkText, 2);
            Segment(b - Vector2.up * 4, b + Vector2.up * 4, InkText, 2);
            Label(a.x, a.y - 23, 120, 20, meters.ToString("0.##") + " m", 12, InkText, true);
        }

        private void DrawHomeChartWaypointList(Rect area)
        {
            var c = Controller;
            Label(area.x, area.y - 42, area.width, 28, "出航目的地", 19, Gold, true);
            _homeChartListScroll = GUI.BeginScrollView(area, _homeChartListScroll, new Rect(0, 0, area.width - 20, c.NavigationCount * 52));
            for (int i = 0; i < c.NavigationCount; i++)
            {
                if (!c.HomeChartWaypoint(i, out var world, out var name, out var available)) continue;
                string distance = FishingPrototype.FlatDistance(c.ExplorerPosition, world).ToString("0") + " m";
                string text = (i == c.NavigationTarget ? "● " : "") + name + "  ·  " + (available ? distance : "尚未开放");
                if (Button(new Rect(0, i * 52, area.width - 24, 44), text, available, i == c.NavigationTarget, 14))
                    c.SelectHomeChartNavigation(i);
            }
            GUI.EndScrollView();
        }

        private void HandleHomeChartMapInput(Rect area)
        {
            var e = Event.current; var c = Controller;
            if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition)) { c.ZoomHomeChart(-e.delta.y * .35f); e.Use(); }
            if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition)) { _homeChartDragging = true; e.Use(); }
            if (e.type == EventType.MouseDrag && e.button == 0 && _homeChartDragging)
            {
                float metersPerPixel = c.HomeChartRangeMeters * 2 / area.height;
                c.PanHomeChart(new Vector2(-e.delta.x, e.delta.y) * metersPerPixel); e.Use();
            }
            if (e.rawType == EventType.MouseUp) _homeChartDragging = false;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { c.CloseHomeChart(); _homeChartDragging = false; e.Use(); }
        }
    }
}
