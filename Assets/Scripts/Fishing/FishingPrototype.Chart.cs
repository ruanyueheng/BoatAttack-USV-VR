using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public readonly struct ChartWaypoint
        {
            public readonly int Target;
            public readonly string Name, Symbol;
            public readonly Vector3 Position;
            public readonly bool Available;
            public ChartWaypoint(int target, string name, string symbol, Vector3 position, bool available=true)
            { Target=target;Name=name;Symbol=symbol;Position=position;Available=available; }
        }

        private readonly List<ChartWaypoint> _homeChartWaypoints=new List<ChartWaypoint>();
        private bool _homeChartPriorPause;
        private float _homeChartPriorTimeScale;
        private int _homeChartConsumedFrame=-1;
        private bool _portableChartLantern;
        private FishingCareer _portableChartCareer;
        public bool HomeChartOpen { get; private set; }
        public FishingPortableChart PortableChart { get; private set; }
        public bool HomeChartReady => HomeChartOpen && (PortableChart==null || !PortableChart.Active || PortableChart.Ready);
        public Vector3 ChartPlayerPosition => Realms!=null && Realms.Inside && Realms.Portal(Realms.Island)!=null
            ? Realms.Portal(Realms.Island).position : ExplorerPosition;
        public bool HomeChartBlocksGameplay => HomeChartOpen || _homeChartConsumedFrame==Time.frameCount;
        public Vector3 HomeChartCenter { get; private set; }
        public Vector2 HomeChartCenterXZ => new Vector2(HomeChartCenter.x,HomeChartCenter.z);
        public float HomeChartRangeMeters { get; private set; }=700;
        public IReadOnlyList<ChartWaypoint> ChartWaypoints => _homeChartWaypoints;

        public bool OpenPortableChart()
        {
            if(HomeChartOpen || Paused || InspectingItem || !DesktopDisplay || (Shore!=null && (Shore.Swimming || Shore.Climbing)))return false;
            bool lantern=LanternOn;
            if(!OpenHomeChart())return false;
            _portableChartLantern=lantern;_portableChartCareer=Career;
            if(PortableChart==null){PortableChart=gameObject.AddComponent<FishingPortableChart>();PortableChart.Build(this);}
            PortableChart.Open();
            Notice("随身航海图 · M / Esc 收起，展开后可缩放、拖动和标记航点。",5);
            return true;
        }

        public bool OpenHomeChart()
        {
            if(HomeChartOpen)return true;
            if(XRSettings.isDeviceActive || StartMenuOpen || InventoryOpen || JournalOpen || GuideOpening
                || StoryBlocksGameplay || ShopBlocksGameplay || WorldActionBlocksGameplay || HasPendingCatch
                || Home==null || Home.Sleeping || Player==null || Career==null)return false;
            _homeChartPriorPause=Paused;_homeChartPriorTimeScale=Time.timeScale;
            HomeChartOpen=true;_homeChartConsumedFrame=Time.frameCount;
            StoryModalChanged();Paused=true;Time.timeScale=0;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            BuildChartWaypoints();ResetHomeChartView();
            ApplyAudioSettings();
            Notice("航线书桌 · 滚轮缩放，拖动海图，点击航点；Esc 返回渔屋。",5);
            return true;
        }

        public void CloseHomeChart(bool immediate=false)
        {
            if(!HomeChartOpen)return;
            if(!immediate && PortableChart!=null && PortableChart.Active){PortableChart.Close();return;}
            PortableChart?.Cancel();
            HomeChartOpen=false;_homeChartConsumedFrame=Time.frameCount;
            Paused=_homeChartPriorPause;Time.timeScale=_homeChartPriorTimeScale;
            StoryModalChanged();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;ApplyAudioSettings();
            if(_portableChartCareer==Career)LanternOn=_portableChartLantern;
            _portableChartCareer=null;
        }

        public bool TickHomeChart(Keyboard keyboard)
        {
            if(!HomeChartOpen)
            {
                if(_homeChartConsumedFrame==Time.frameCount)return true;
                if((Application.isFocused || Automated) && keyboard!=null && keyboard.mKey.wasPressedThisFrame && OpenPortableChart())return true;
                return false;
            }
            if(_homeChartConsumedFrame==Time.frameCount)return true;
            if(XRSettings.isDeviceActive || (!Application.isFocused && !Automated)){CloseHomeChart(true);return true;}
            if(PortableChart!=null && PortableChart.Active)
            {
                PortableChart.Tick(Time.unscaledDeltaTime);
                if(!PortableChart.Active){CloseHomeChart(true);return true;}
            }
            if((Application.isFocused || Automated) && keyboard!=null
                && (keyboard.escapeKey.wasPressedThisFrame || keyboard.mKey.wasPressedThisFrame))CloseHomeChart();
            return true;
        }

        public void ResetHomeChartView()
        {
            var map=Resources.Load<FishingMapDefinition>("FishingMap");
            HomeChartCenter=map!=null && map.IsUsable
                ?new Vector3((map.worldXZBounds.x+map.worldXZBounds.z)*.5f,0,(map.worldXZBounds.y+map.worldXZBounds.w)*.5f)
                :Vector3.zero;
            HomeChartRangeMeters=map!=null && map.IsUsable
                ?Mathf.Clamp((map.worldXZBounds.w-map.worldXZBounds.y)*.56f,400,1600):700;
        }

        public void CenterHomeChartOnPlayer()
        {if(HomeChartOpen)HomeChartCenter=ChartPlayerPosition;}

        public bool HomeChartWaypoint(int target,out Vector3 world,out string name,out bool available)
        {
            for(int i=0;i<_homeChartWaypoints.Count;i++)if(_homeChartWaypoints[i].Target==target)
            {
                var waypoint=_homeChartWaypoints[i];world=waypoint.Position;name=waypoint.Name;available=waypoint.Available;return true;
            }
            world=Vector3.zero;name=string.Empty;available=false;return false;
        }

        public void ZoomHomeChart(float steps)
        {
            if(!HomeChartOpen || !FishingMapMath.Finite(steps))return;
            HomeChartRangeMeters=Mathf.Clamp(HomeChartRangeMeters*Mathf.Pow(.8f,Mathf.Clamp(steps,-5,5)),40,1800);
        }

        public void PanHomeChart(Vector2 worldDelta)
        {
            if(!HomeChartOpen || !FishingMapMath.Finite(worldDelta.x) || !FishingMapMath.Finite(worldDelta.y))return;
            var center=HomeChartCenter+new Vector3(worldDelta.x,0,worldDelta.y);
            center.x=Mathf.Clamp(center.x,-1800,1800);center.z=Mathf.Clamp(center.z,-1800,1800);HomeChartCenter=center;
        }

        public bool SelectHomeChartNavigation(int target)
        {
            if(!HomeChartOpen)return false;
            for(int i=0;i<_homeChartWaypoints.Count;i++)if(_homeChartWaypoints[i].Target==target && _homeChartWaypoints[i].Available)
            {NavigationTarget=target;Notice("已标记航点："+_homeChartWaypoints[i].Name+" · 不会自动移动船只。",5);return true;}
            return false;
        }

        private void BuildChartWaypoints()
        {
            _homeChartWaypoints.Clear();
            _homeChartWaypoints.Add(new ChartWaypoint(0,"潮汐渔港","港",harbor));
            for(int i=0;i<fishingSpots.Length;i++)
                _homeChartWaypoints.Add(new ChartWaypoint(i+1,FishingGrounds.Name(i),(i+1).ToString(),fishingSpots[i],FishingGrounds.AvailableForCareer(i,Career)));
            if(Shore!=null)for(int i=0;i<Shore.Sites.Length;i++)if(Shore.Sites[i].Available)
                _homeChartWaypoints.Add(new ChartWaypoint(fishingSpots.Length+1+i,FishingExploration.SiteName(i),((char)('A'+i)).ToString(),Shore.Sites[i].Landing));
            if(TackleShop!=null && TackleShop.Available)
                _homeChartWaypoints.Add(new ChartWaypoint(ShopNavigationIndex,ShopName,"店",TackleShop.Position));
            _homeChartWaypoints.Add(new ChartWaypoint(HomeNavigationIndex,"回潮渔屋","家",Home.Origin));
            for(int i=0;i<FishingAdventure.Count;i++)
                _homeChartWaypoints.Add(new ChartWaypoint(IslandNavigationIndex(i),FishingAdventure.Name(i),((char)('E'+i)).ToString(),FishingIslandWorld.Center(i)));
            for(int i=0;i<FishingCoastalPresentation.Islets.Length;i++)
                _homeChartWaypoints.Add(new ChartWaypoint(HomeNavigationIndex+1+i,FishingCoastalPresentation.IsletName(i),i==0?"拱":"萤",FishingCoastalPresentation.Islets[i]));
        }
    }
}
