using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        /// <summary>真实地形底图与航点共用米/像素比例；屏外徽标只代表方位。</summary>
        private void DrawChart()
        {
            var c = Controller;
            if(c.Realms!=null && c.Realms.Inside){DrawRealmChart();return;}
            if (!c.ChartExpanded)
            {
                var tab=Layout.ChartTab;
                if (SeaButton(new Rect(tab.x,tab.y,tab.width-52,tab.height), "M 全域航海图",false,true,14)) c.OpenPortableChart();
                if (SeaButton(new Rect(tab.xMax-48,tab.y,48,tab.height), "小图",false,true,13)) c.ToggleChart();
                return;
            }
            var r = Layout.Chart;
            if (SeaButton(new Rect(r.x, r.y, 156, 28), "M 全域航海图",false,true,14)) c.OpenPortableChart();
            if (SeaButton(new Rect(r.x+158,r.y,26,28), "－",false,true,13)) c.ToggleChart();
            if (SeaButton(new Rect(r.xMax - 60, r.y, 60, 28), c.ChartRangeMeters + "m",false,true,13)) c.CycleChartRange();
            var center = new Vector2(r.center.x, r.y + 130);
            const float radius = TerrainMapRadius;
            Disc(center, radius + 4, MapSurface);
            DrawTerrainMap(center, radius);
            DrawCompassFrame(center,radius);
            Outline(new Rect(center.x-radius, center.y-radius, radius*2, radius*2), Gold, radius, 1.5f);
            Outline(new Rect(center.x-radius*.5f, center.y-radius*.5f, radius, radius), Hairline*.45f, radius*.5f);
            for (int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12; var dir=new Vector2(Mathf.Sin(a),-Mathf.Cos(a));
                Segment(center+dir*(i%6==0?89:93),center+dir*96,i%6==0?Muted:Faint*new Color(1,1,1,.45f));
            }
            Label(new Rect(center.x-10,center.y-88,20,20),"N",13,Color.white,true,TextAnchor.MiddleCenter);
            var bearing=ChartPoint(c.NavigationPosition,center)-center;
            for(float d=16;d<bearing.magnitude-13;d+=10)
                Segment(center+bearing.normalized*d,center+bearing.normalized*Mathf.Min(d+4,bearing.magnitude-13),new Color(1,.77f,.42f,.65f),1.5f);
            DrawWorkshopMarkers(center);
            ChartMarker(c.harbor,center,"港",0,MapPortColor);
            for(int i=0;i<c.fishingSpots.Length;i++)
                ChartMarker(c.fishingSpots[i],center,(i+1).ToString(),i+1,FishingGrounds.AvailableForCareer(i,c.Career)?MapFishColor:Faint);
            if(c.Shore!=null)
                for(int i=0;i<c.Shore.Sites.Length;i++)if(c.Shore.Sites[i].Available)
                    ChartMarker(c.Ashore?c.Shore.Sites[i].Landing:c.Shore.Sites[i].Berth,center,
                        i==2 && c.Career.Expedition.Has(FishingExpedition.BeaconRepaired)?"灯":((char)('A'+i)).ToString(),
                        c.fishingSpots.Length+1+i,MapShoreColor);
            if(c.TackleShop!=null && c.TackleShop.Available)
                ChartMarker(c.Ashore?c.TackleShop.Position:c.TackleShop.mooringPoint.position,center,"店",c.ShopNavigationIndex,MapShoreColor);
            if(c.Home!=null)
                ChartMarker(c.Home.Origin,center,"家",c.HomeNavigationIndex,Gold);
            for(int i=0;i<FishingAdventure.Count;i++)
                ChartMarker(c.Ashore?FishingIslandWorld.Center(i):FishingIslandWorld.Berth(i),center,((char)('E'+i)).ToString(),c.IslandNavigationIndex(i),c.Career.Adventure.Restored(i)?Gold:MapShoreColor);
            for(int i=0;i<2;i++)ChartMarker(c.Ashore?FishingCoastalPresentation.Islets[i]:FishingCoastalPresentation.Berth(i),center,i==0?"拱":"萤",c.HomeNavigationIndex+1+i,MapShoreColor);
            if(c.Ashore)
            {
                var boat=ChartPoint(c.Player.transform.position,center);Disc(boat,7,MapSurface);Disc(boat,5,MapPortColor);
                if(c.Shore.HasSurveySite)
                {
                    var site=c.Shore.Sites[c.Shore.CurrentSite];
                    for(int t=0;t<2;t++)
                    {
                        var task=ChartPoint(site.Tasks[t],center);Disc(task,5,MapSurface);
                        Disc(task,3,FishingExploration.Done(c.Career.ShoreTaskMask,c.Shore.CurrentSite*2+t)?Mint:Gold);
                    }
                }
            }
            if(c.Expedition.PrecisionGuide && !c.Career.Expedition.Has(FishingExpedition.Delivered))
            {
                var hint=ChartPoint(c.Expedition.GuidePosition,center);var badge=hint+new Vector2(15,-15);
                Segment(hint,badge,Sky,1.5f);Disc(badge,9,MapSurface);
                Label(new Rect(badge.x-10,badge.y-11,20,22),"★",16,Color.white,true,TextAnchor.MiddleCenter);
            }
            Disc(center,12,MapSurface);
            DrawArrow(center,c.Ashore?c.viewCamera.transform.eulerAngles.y:c.Player.transform.eulerAngles.y,InkText,10);
            DrawTerrainMapGuide(center,radius,new Rect(r.x,center.y+radius+12,r.width,36));
            if(Event.current.type==EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
            {
                var tip=new Rect(r.x,r.y-48,r.width,42);Card(tip,MapShoreColor);
                Label(new Rect(tip.x+8,tip.y+4,tip.width-16,tip.height-8),GUI.tooltip,12,Color.white);
            }
        }
        private void DrawCompassFrame(Vector2 center,float radius)
        {
            var brass=new Color(.79f,.60f,.30f,.96f);var dark=new Color(.12f,.19f,.20f,.95f);
            Outline(new Rect(center.x-radius-7,center.y-radius-7,(radius+7)*2,(radius+7)*2),dark,radius+7,7);
            Outline(new Rect(center.x-radius-7,center.y-radius-7,(radius+7)*2,(radius+7)*2),brass,radius+7,2);
            Outline(new Rect(center.x-radius-2,center.y-radius-2,(radius+2)*2,(radius+2)*2),CreamInk,radius+2,1);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;var d=new Vector2(Mathf.Sin(a),-Mathf.Cos(a));var tangent=new Vector2(-d.y,d.x);
                var tip=center+d*(radius+(i%2==0?17:12));var foot=center+d*(radius+5);
                Segment(foot-tangent*5,tip,brass,2);Segment(tip,foot+tangent*5,brass,2);
                Disc(center+d*(radius+7),2.5f,CreamInk);
            }
        }
        private void DrawWorkshopMarkers(Vector2 center)
        {
            var c=Controller;if(c.WorkshopWorld==null)return;
            foreach(var station in c.WorkshopWorld.State.Stations)
            {
                var world=new Vector3(station.x,station.y,station.z);
                if(FishingPrototype.FlatDistance(c.ExplorerPosition,world)>c.ChartRangeMeters)continue;
                var p=ChartPoint(world,center);
                Rounded(new Rect(p.x-9,p.y-9,18,18),MapSurface,3);
                Outline(new Rect(p.x-9,p.y-9,18,18),MapShoreColor,3,1.5f);
                Label(new Rect(p.x-9,p.y-10,18,20),station.kind==CraftStation.Furnace?"炉":"工",12,Color.white,true,TextAnchor.MiddleCenter);
            }
            // 只在步行近景显示少量可采资源，不把 64 个节点挤满整张海图。
            if(!c.Ashore || c.ChartRangeMeters>40)return;
            int shown=0;
            foreach(var node in c.WorkshopWorld.Nodes)
            {
                if(shown==6)break;
                if(node.Root==null || !c.WorkshopWorld.State.ResourceReady(node.Id) || FishingPrototype.FlatDistance(c.ExplorerPosition,node.Feet)>c.ChartRangeMeters)continue;
                var p=ChartPoint(node.Feet,center);Disc(p,4,MapSurface);Disc(p,2,MapShoreColor);shown++;
            }
        }
        private Vector2 ChartPoint(Vector3 world,Vector2 center)
        {
            var p=Controller.ExplorerPosition;
            var point=FishingChartMath.Project(world.x,world.z,p.x,p.z,Controller.ChartRangeMeters,TerrainMapRadius);
            return center+new Vector2(point.X,point.Y);
        }
        private void ChartMarker(Vector3 world,Vector2 center,string text,int target,Color color)
        {
            var c=Controller;var p=ChartPoint(world,center);
            bool selected=c.NavigationTarget==target;
            float metres=FishingPrototype.FlatDistance(c.ExplorerPosition,world);
            bool offRange=metres>c.ChartRangeMeters;
            if(selected)Outline(new Rect(p.x-17,p.y-17,34,34),Gold,17,2);
            Disc(p,12,MapSurface);Outline(new Rect(p.x-12,p.y-12,24,24),color,12,1.5f);
            Label(new Rect(p.x-12,p.y-12,24,24),text,14,Color.white,true,TextAnchor.MiddleCenter);
            string name=target>c.HomeNavigationIndex?FishingCoastalPresentation.IsletName(target-c.HomeNavigationIndex-1):target==c.HomeNavigationIndex?"回潮渔屋":target==c.ShopNavigationIndex?c.ShopName:target==0?"潮汐渔港":target<=c.fishingSpots.Length
                ?FishingGrounds.Name(target-1):target>c.ShopNavigationIndex?FishingAdventure.Name(target-c.ShopNavigationIndex-1):FishingExploration.SiteName(target-c.fishingSpots.Length-1);
            string tip=name+" · "+metres.ToString("0")+" m"+(offRange?"（屏外方位）":"");
            if(GUI.Button(new Rect(p.x-16,p.y-16,32,32),new GUIContent("",tip),GUIStyle.none))c.SelectNavigation(target);
        }
        private void Segment(Vector2 from,Vector2 to,Color color,float width=1f)
        {
            var matrix=GUI.matrix;Rotate(Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg,from);
            Fill(from.x,from.y-width*.5f,Vector2.Distance(from,to),width,color);GUI.matrix=matrix;
        }
        private void DrawArrow(Vector2 center,float degrees,Color color,float size)
        {
            float a=degrees*Mathf.Deg2Rad;var forward=new Vector2(Mathf.Sin(a),-Mathf.Cos(a));var right=new Vector2(-forward.y,forward.x);
            var tip=center+forward*size;var rear=center-forward*size*.6f;
            Segment(rear+right*size*.55f,tip,color,2.5f);Segment(tip,rear-right*size*.55f,color,2.5f);Segment(rear,center,color,2.5f);
        }
    }
}
