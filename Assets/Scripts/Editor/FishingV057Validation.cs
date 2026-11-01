using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV057Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V057");
        public static IEnumerator Checks(FishingPrototype c)
        {
            Directory.CreateDirectory("Logs/V057");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.ReturnToHarbor();
            for(int i=0;i<25;i++)yield return null;
            var tools=FishingFeedbackToolsValidation.Checks(c);try{while(tools.MoveNext())yield return tools.Current;}finally{(tools as IDisposable)?.Dispose();}
            Check(c.GetComponent<FishingWreckRouteClearance>().Cleared,"Southwest racing mainland is cleared before route generation");
            foreach(float x in new[]{-440f,-350f,-260f,-150f})foreach(float z in new[]{-225f,-185f,-140f})
            {
                var p=new Vector3(x,0,z);Check(c.WaterRoute.ClearPoint(p),"Open water and hull clearance at former terrain "+p);
                foreach(var terrain in Terrain.activeTerrains)
                {
                    var local=p-terrain.transform.position;var size=terrain.terrainData.size;
                    if(local.x>=0 && local.z>=0 && local.x<=size.x && local.z<=size.z)
                        Check(terrain.SampleHeight(p)+terrain.transform.position.y < -3,"Terrain and collider are below the voyage at "+p);
                }
            }
            Check(c.WaterRoute.ClearSegment(new Vector3(-260,0,-270),new Vector3(-260,0,-140)),"Guidance crosses the former terrain cut edge");
            Check(!Physics.Linecast(new Vector3(-260,1,-270),new Vector3(-260,1,-240),~(1<<4),QueryTriggerInteraction.Ignore),"Manual boats meet no invisible cut-edge wall");
            Check(c.Shore.Sites.All(s=>s.Available),"All four original survey shores remain supported");
            var original=c.Career.Snapshot();
            try
            {
                var fixture=c.Career.Snapshot();fixture.workshop.stations.RemoveAll(s=>!s.permanent);fixture.workshop.nextId=2000;
                for(int i=0;i<FishingWorkshop.MaxPlayerStations;i++)fixture.workshop.stations.Add(new WorkshopStationData
                    {id=1000+i,kind=i==0?CraftStation.Furnace:CraftStation.Workbench,x=-260+i%4*2,y=22,z=-150+i/4*2,yaw=45,recipe=i==0?"iron":"",remaining=i==0?1:0,ready=i==0?1:0,progress=i==0?.42f:0});
                c.RestoreShopTestProfile(fixture);
                var migrate=typeof(FishingHome).GetMethod("RelocateClearedStations",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                migrate.Invoke(c.Home,null);var station=c.WorkshopWorld.State.Station(1000);var p=new Vector3(station.x,station.y,station.z);
                Check(FishingPrototype.FlatDistance(p,c.Home.Origin)<40 && station.yaw==45 && station.recipe=="iron" && station.remaining==1 && station.ready==1 && station.progress==.42f,"A facility saved on removed land moves onto supported home ground with its queued products intact");
                Check(c.WorkshopWorld.State.Stations.Where(s=>!s.permanent).All(s=>s.y<15 && s.yaw==45 && FishingPrototype.FlatDistance(new Vector3(s.x,s.y,s.z),c.Home.Origin)<40)
                    && c.WorkshopWorld.State.Stations.Count(s=>!s.permanent)==FishingWorkshop.MaxPlayerStations,"All 32 saved facilities survive the removed-land migration");
                migrate.Invoke(c.Home,null);station=c.WorkshopWorld.State.Station(1000);
                Check(p==new Vector3(station.x,station.y,station.z),"Saved facility relocation is stable on a second load");
            }
            finally{c.RestoreShopTestProfile(original);}
            var environment=GameObject.Find("Island Level/Environment Objects");
            Check(environment.GetComponentsInChildren<Renderer>().All(r=>!FishingWreckRouteClearance.Area.Contains(new Vector2(r.bounds.center.x,r.bounds.center.z))),"No legacy renderer remains over the cleared sea");
            c.ReturnToHarbor();yield return null;
            var rb=c.Player.engine.RB;rb.position=c.Home.Berth+Vector3.up*.7f;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
            c.SelectNavigation(c.IslandNavigationIndex(2));Check(c.WaterRoute.Plan(),"Home-to-wreck guidance plans a real navigable route: "+c.WaterRoute.Failure);
            foreach(var pair in c.WaterRoute.Points.Zip(c.WaterRoute.Points.Skip(1),(a,b)=>(a,b)))Check(c.WaterRoute.ClearSegment(pair.a,pair.b),"Every guidance segment has real terrain and hull clearance");
            var cam=c.viewCamera.transform;var position=cam.position;var rotation=cam.rotation;
            try
            {
                foreach(var p in new[]{new Vector3(-260,2,-140),new Vector3(-260,2,-240),new Vector3(-350,2,-280)})
                {cam.SetPositionAndRotation(p,Quaternion.LookRotation(FishingIslandWorld.Center(2)+Vector3.up*2-p));FishingV016Validation.Capture(c,"Logs/V057","route-"+(-Mathf.RoundToInt(p.z)));}
            }
            finally{cam.SetPositionAndRotation(position,rotation);}
            if(Environment.GetEnvironmentVariable("V057_BAKE_MAP")=="1")BoatAttack.Editor.FishingMapBaker.BakeInitializedScene();
            Debug.Log("[V057] CHECK: handcraft contact, arm posture, safe gestures and southwest water route validated");
        }
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("[V057] CHECK: "+message);}
    }
}
