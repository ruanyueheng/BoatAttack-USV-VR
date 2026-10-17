using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV033PresentationValidation
    {
        private static void Check(bool success,string label)
        {
            if(!success)throw new InvalidOperationException("[V033 Presentation] "+label);
            Debug.Log("[V033 Presentation] PASS: "+label);
        }

        [MenuItem("Boat Attack/Fishing/验证 V033 海图与标题资源")]
        public static void ValidateAssets()
        {
            var map=Resources.Load<FishingMapDefinition>("FishingMap");
            Check(map!=null && map.IsUsable,"虚构海域底图定义及真实图像资源存在");
            Check(map.texture.width>=512 && map.texture.height>=512,"海图烘焙纹理具有可阅读分辨率");
            Check(Resources.Load<Shader>("FishingTerrainMap")!=null,"海图着色器资源存在");
            var area=new Rect(0,0,1000,500);var center=new Vector2(12,-25);
            var projected=FishingHUD.HomeChartProject(new Vector3(112,0,75),center,500,area);
            Check((projected-(area.center+new Vector2(50,-50))).sqrMagnitude<.001f,"宽幅海图横纵米像素比例一致且北向正确");
            string home=File.ReadAllText("Assets/Scripts/Fishing/FishingHome.cs");
            Check(home.Contains("return _c.OpenHomeChart();") && home.Contains("_c.XRNotebook.Open(12)"),"书桌桌面独立海图与 XR 手册入口均保留");
            string prototype=File.ReadAllText("Assets/Scripts/Fishing/FishingPrototype.cs");
            string hud=File.ReadAllText("Assets/Scripts/Fishing/FishingHUD.cs");
            Check(prototype.Contains("TickHomeChart(k)"),"主 Update 已消费海图模态帧");
            Check(hud.Contains("c.HomeChartOpen") && hud.Contains("DrawHomeChart()"),"主 HUD 已接入独立海图绘制分支");
            Debug.Log("[V033 Presentation] 资源与静态接线验证完成；动画、冻结和指针需运行 Checks。");
        }

        public static IEnumerator Checks(FishingPrototype c)
        {
            ValidateAssets();
            Check(c!=null && c.Player!=null && c.Home!=null && c.DesktopDisplay,"桌面场景已初始化");
            if(c.StartMenuOpen){c.FinishTitleVoyage();yield return null;}
            yield return null;
            var career=c.Career;var position=c.Player.transform.position;var rotation=c.Player.transform.rotation;
            var shorePosition=c.Ashore?c.Shore.Position:Vector3.zero;
            var cameraPosition=c.viewCamera.transform.position;var cameraRotation=c.viewCamera.transform.rotation;
            float cameraFov=c.viewCamera.fieldOfView;
            var sweep=c.Nightscape.LighthouseSweep;var sweepRotation=sweep.rotation;
            var sourceTransforms=new Dictionary<Transform,Matrix4x4>();
            foreach(var collider in UnityEngine.Object.FindObjectsOfType<Collider>())
                if(collider!=null && !collider.isTrigger)sourceTransforms[collider.transform]=collider.transform.localToWorldMatrix;
            c.TitleVoyage.Open();
            Check(c.TitleVoyage.Boat.GetComponentsInChildren<Collider>(true).Length==0,"标题船与人物只含视觉部件，无真实碰撞体");
            c.TitleVoyage.Present(.25f);var titlePosition=c.TitleVoyage.Boat.position;
            c.TitleVoyage.Present(1.25f);
            Check((c.TitleVoyage.Boat.position-titlePosition).sqrMagnitude>.00001f,"标题装饰船轻微浮动实际改变视觉");
            Check(Quaternion.Angle(sweep.rotation,sweepRotation)>1,"现有灯塔视觉扫光在菜单暂停时可运动");
            Check(c.TitleVoyage.DecorativeCount<=48,"装饰对象数量有界，无每帧场景扫描");
            bool collisionTransformsUnchanged=true;
            foreach(var item in sourceTransforms)
                if(item.Key!=null && item.Key.localToWorldMatrix!=item.Value)collisionTransformsUnchanged=false;
            Check(collisionTransformsUnchanged,"标题动态不移动任何真实碰撞 Transform");
            c.TitleVoyage.Close(true);
            Check((c.viewCamera.transform.position-cameraPosition).sqrMagnitude<.00001f
                && Quaternion.Angle(c.viewCamera.transform.rotation,cameraRotation)<.001f
                && Mathf.Abs(c.viewCamera.fieldOfView-cameraFov)<.001f,"关闭标题恢复继续游戏的相机位置、朝向和视野");
            Check(Quaternion.Angle(sweep.rotation,sweepRotation)<.001f,"退出标题还原灯塔扫光角度");
            yield return null;
            int compactRange=c.ChartRangeMeters;bool compactExpanded=c.ChartExpanded;
            bool previousPause=c.Paused;float previousTimeScale=Time.timeScale;
            position=c.Player.transform.position;rotation=c.Player.transform.rotation;
            shorePosition=c.Ashore?c.Shore.Position:Vector3.zero;
            Check(c.OpenHomeChart(),"直接打开独立桌面海图");
            Check(c.HomeChartOpen && c.Paused && Time.timeScale==0 && c.HomeChartBlocksGameplay,"海图冻结游戏并拥有模态状态");
            Check(Cursor.lockState==CursorLockMode.None && Cursor.visible,"海图打开时鼠标可见且解锁");
            Check(c.HomeChartRangeMeters>=400 && c.GetComponent<FishingHUD>().HasHomeChartTerrain,"岸上近景范围不会限制全域底图");
            float range=c.HomeChartRangeMeters;c.ZoomHomeChart(1);
            Check(c.HomeChartRangeMeters<range,"缩放改变独立地图范围");
            range=c.HomeChartRangeMeters;c.ZoomHomeChart(float.NaN);
            Check(c.HomeChartRangeMeters==range,"非有限缩放输入被拒绝");
            var centerBefore=c.HomeChartCenterXZ;c.PanHomeChart(new Vector2(10,-20));
            Check((c.HomeChartCenterXZ-centerBefore-new Vector2(10,-20)).sqrMagnitude<.001f,"拖动只改变海图中心");
            c.SelectHomeChartNavigation(0);Check(c.NavigationTarget==0,"暂停状态仍可选择航点");
            c.SelectHomeChartNavigation(c.NavigationCount+50);Check(c.NavigationTarget==0,"不存在的海图航点不可选");
            for(int i=0;i<3;i++){Check(c.TickHomeChart(null),"海图完整消费主 Update 帧");c.View.ApplyView(true,true);yield return null;}
            Check((c.Player.transform.position-position).sqrMagnitude<.0001f && Quaternion.Angle(c.Player.transform.rotation,rotation)<.01f,"阅读海图期间真实船只不移动");
            if(c.Ashore)Check((c.Shore.Position-shorePosition).sqrMagnitude<.0001f,"阅读海图期间人物不移动");
            c.CloseHomeChart();
            Check(!c.HomeChartOpen && c.Paused==previousPause && Time.timeScale==previousTimeScale,"关闭还原此前暂停状态与时间尺度");
            Check(c.HomeChartBlocksGameplay && c.TickHomeChart(null),"关闭海图的同帧不能泄漏输入");
            Check(c.ChartRangeMeters==compactRange && c.ChartExpanded==compactExpanded,"独立海图不覆盖行进小地图状态");
            Check(c.Career==career,"标题与海图不替换存档进度对象");
            yield return null;
            Check(!c.HomeChartBlocksGameplay,"关闭后的下一帧释放模态");
            Debug.Log("[V033 Presentation] 海图/标题运行验证完成；实际截图与 Esc、拖动按钮仍由统一验收驱动采集。");
        }
    }
}
