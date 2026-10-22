using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingHomeChartTitleValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool passed, string message)
        {
            if (!passed) throw new InvalidOperationException("[V033-海图标题] " + message);
            Debug.Log("[V033-海图标题] CHECK: " + message);
        }

        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c != null && c.Automated && c.DesktopDisplay, "使用独立自动化桌面场景，不操作用户存档");
            if (c.StartMenuOpen) c.FinishTitleVoyage();
            c.CloseHomeChart();
            if (c.InventoryOpen) c.ToggleInventory();
            if (c.JournalOpen) c.ToggleJournal();
            if (c.Paused) c.TogglePause();
            yield return null;
            var career = c.Career;
            int navigation = c.NavigationTarget;
            var hud = c.GetComponent<FishingHUD>();
            var map = Resources.Load<FishingMapDefinition>("FishingMap");
            Check(map != null && map.IsUsable && hud.HasHomeChartTerrain, "虚构海域烘焙图像资源可用");
            try
            {
                if(c.Ashore)c.Shore.BoardImmediately();c.SelectNavigation(c.HomeNavigationIndex);
                var rb = c.Player.engine.RB;
                var berth = c.Home.Berth + Vector3.up * .7f;
                rb.position = berth; rb.rotation = Quaternion.identity; rb.velocity = rb.angularVelocity = Vector3.zero;
                c.Player.transform.SetPositionAndRotation(berth, Quaternion.identity); Physics.SyncTransforms();
                for (int warm = 0; warm < 20; warm++) yield return null;
                Check(c.Shore.TryDisembark(), "由渔屋真实泊位下船");
                Check(c.Shore.Relocate(c.Home.Origin + new Vector3(3.65f, .20f, -2.4f), Quaternion.identity), "可站在航线书桌交互范围");
                c.View.ResetLook(); yield return null;
                FishingV036FarmValidation.Aim(c,c.Home.InteractionPoint(2));yield return null;
                Check(c.Home.ReachInteraction(2), "书桌航海图由实际家具命中");
                bool expanded = c.ChartExpanded;
                int smallRange = c.ChartRangeMeters;
                float priorTimeScale = Time.timeScale;
                var position = c.Shore.Position;
                var boatPosition = c.Player.transform.position;
                var cameraPosition = c.viewCamera.transform.position;
                var cameraRotation = c.viewCamera.transform.rotation;
                Check(c.RequestWorldInteraction() && c.HomeChartOpen, "桌边 E 打开独立海图而非仅切换小海图");
                Check(c.Paused && Time.timeScale == 0 && c.HomeChartBlocksGameplay, "海图模态暂停真实物理及工具输入");
                Check(c.HomeChartRangeMeters > 320 && c.ChartExpanded == expanded && c.ChartRangeMeters == smallRange, "海图默认全海域视野，保留岸上小图设置");
                Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "桌面指针立即解锁且可交互");
                for (int i = 0; i < 5; i++) yield return null;
                Check(Vector3.Distance(position, c.Shore.Position) < .001f && Vector3.Distance(boatPosition, c.Player.transform.position) < .001f, "阅读期间人物与真实船不移动");
                Check(Vector3.Distance(cameraPosition, c.viewCamera.transform.position) < .001f && Quaternion.Angle(cameraRotation, c.viewCamera.transform.rotation) < .01f, "海图不更改玩家镜头");
                float range = c.HomeChartRangeMeters;
                c.ZoomHomeChart(2); Check(c.HomeChartRangeMeters < range, "缩放按钮/滚轮使用独立视野范围");
                c.ZoomHomeChart(float.NaN); Check(!float.IsNaN(c.HomeChartRangeMeters), "非法缩放值不会污染视野");
                c.ZoomHomeChart(100); c.ZoomHomeChart(-100); Check(c.HomeChartRangeMeters >= 40 && c.HomeChartRangeMeters <= 1800, "缩放始终处于可读范围");
                var area = new Rect(0, 0, 1000, 600);
                var center = c.HomeChartCenterXZ;
                var middle = FishingHUD.HomeChartProject(new Vector3(center.x, 0, center.y), center, range, area);
                var north = FishingHUD.HomeChartProject(new Vector3(center.x, 0, center.y + range), center, range, area);
                var east = FishingHUD.HomeChartProject(new Vector3(center.x + range, 0, center.y), center, range, area);
                Check((middle - area.center).sqrMagnitude < .001f && Mathf.Abs(north.y - area.yMin) < .001f
                    && Mathf.Abs(east.x - middle.x - area.height * .5f) < .001f, "宽幅图横纵比例相同且北方向正确");
                c.PanHomeChart(new Vector2(25, -40)); Check(c.HomeChartCenterXZ == center + new Vector2(25, -40), "拖动平移不移动真实人物");
                Check(c.SelectHomeChartNavigation(0) && c.NavigationTarget == 0, "暂停阅读中仍可选择真实港口航点");
                Check(!c.SelectHomeChartNavigation(-1) && !c.SelectHomeChartNavigation(c.NavigationCount), "非法航点被拒绝");
                typeof(FishingHUD).GetField("_terrainMap", Private).SetValue(hud, null);
                Check(!hud.HasHomeChartTerrain, "地图资源不可用时进入明确虚构示意 fallback");
                hud.ReleaseTerrainMap(); Check(hud.HasHomeChartTerrain, "释放后可重新加载烘焙底图");
                c.CloseHomeChart();
                Check(!c.HomeChartOpen && !c.Paused && Time.timeScale == priorTimeScale && c.HomeChartBlocksGameplay, "关闭恢复原暂停状态并消费当前输入帧");
                yield return null;
                Check(!c.HomeChartBlocksGameplay, "关闭后下一帧正常恢复游戏输入");
                c.TogglePause(); yield return null;
                Check(c.OpenHomeChart(), "已暂停场景也可打开阅读窗口");
                c.CloseHomeChart(); Check(c.Paused && Time.timeScale == 0, "原本暂停的场景关闭海图仍保持暂停");
                c.TogglePause(); yield return null;

                var sources = new Dictionary<Transform, Matrix4x4>();
                foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>())
                    sources[collider.transform] = collider.transform.localToWorldMatrix;
                c.OpenStartMenu();
                Check(c.StartMenuOpen && c.TitleVoyage.Boat != null, "标题保留现有装饰船");
                Check(c.TitleVoyage.Boat.GetComponentsInChildren<Collider>().Length == 0, "标题副本不产生真实碰撞");
                c.TitleVoyage.Present(0); var titlePosition = c.TitleVoyage.Boat.position;
                c.TitleVoyage.Present(2);
                Check(Vector3.Distance(titlePosition, c.TitleVoyage.Boat.position) > .01f && Time.timeScale == 0, "unscaled 标题波浪动画在暂停中实际改变画面");
                Check(c.TitleVoyage.DecorativeCount > 0, "标题已有花草/旗帜元素获得轻量动态");
                foreach (var pair in sources)
                    if (pair.Key != null) Check(pair.Key.localToWorldMatrix == pair.Value, "装饰动画不移动真实Collider：" + pair.Key.name);
                var yawField = typeof(FishingViewRig).GetField("_lookYaw", Private);
                float yaw = (float)yawField.GetValue(c.View);
                c.TitleVoyage.Close(true);
                Check(Mathf.Abs((float)yawField.GetValue(c.View) - yaw) < .001f, "继续游戏关闭标题保留已有视角yaw");
                c.FinishTitleVoyage();
                Check(c.Career == career, "地图及标题不替换角色存档对象");
            }
            finally
            {
                c.CloseHomeChart();
                if (c.StartMenuOpen) c.FinishTitleVoyage();
                if (c.Paused) c.TogglePause();
                c.ReturnToHarbor(); c.SelectNavigation(navigation); hud.ReleaseTerrainMap();
            }
        }
    }
}
