using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV035FarmValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V035-FARM");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V035-FARM] CHECK: "+label);}
        private static IEnumerator Walk(FishingPrototype c,Vector3 p)=>FishingV035Levels.Walk(c,p);
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath("Logs/V035");Directory.CreateDirectory(evidence);var original=c.Career.Snapshot();
            try
            {
                c.ReturnToHarbor();yield return null;c.RestoreShopTestProfile(new FishingCareer().Snapshot());c.SelectFieldSlot(8);
                var ladder=UnityEngine.Object.FindObjectsOfType<FishingClimbRoute>().First(r=>FishingPrototype.FlatDistance(r.Bottom,FishingIslandWorld.Center(0))<35);
                var approach=ladder.Bottom+Vector3.ProjectOnPlane(ladder.ClimbEnd-ladder.Top,Vector3.up).normalized*(c.Shore.Body.radius+.4f);
                Check(c.Shore.Ground(approach,out var ladderFoot) && c.Shore.Clearance(ladderFoot),"Actual player capsule fits beside the rebuilt ladder at "+ladderFoot);
                var home=c.Home;var rb=c.Player.engine.RB;var p=home.Berth+Vector3.up*.7f;rb.position=p;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();
                Check(Vector3.Distance(c.Nightscape.LighthouseSweep.position,home.LighthouseLens)<.001f,"Night sweep uses the rebuilt lighthouse's authored lens marker");
                for(int i=0;i<20;i++)yield return null;
                Check(c.Shore.TryDisembark(),"Land at the real home berth after expanding the coastline");yield return null;
                Check(c.Shore.Position.y>.9f && !c.Shore.SafeCoastalFooting(home.Berth-Vector3.up),"Home berth prefers its named dry platform and rejects seabed landings");
                if(!c.FirstPerson)c.ToggleView();
                foreach(var point in new[]{home.BoardwalkStart,home.Entry,home.Origin+new Vector3(0,.2f,-3.8f),home.Entry,home.Origin+new Vector3(7.7f,0,-8.9f),home.Origin+new Vector3(9.2f,0,-10.5f)})
                {var walk=Walk(c,point);while(walk.MoveNext())yield return null;}
                Views(c,evidence);
                var interact=Use(c,13);while(interact.MoveNext())yield return null;
                Check(c.Career.Farm.StarterClaimed && c.Career.Backpack.Count(FieldItem.TurnipSeeds)==12,"Production E collects the first seed packet once");
                var w=Walk(c,home.Origin+new Vector3(12.8f,0,-7.5f));while(w.MoveNext())yield return null;
                for(int pass=0;pass<3;pass++)
                {
                    interact=Use(c,0);while(interact.MoveNext())yield return null;
                    Check(pass==0?c.Career.Farm.Plot(0).tilled:pass==1?c.Career.Farm.Plot(0).planted:c.Career.Farm.Plot(0).wateredDay==c.Career.Calendar.Day,"Production contact commits till / seed / water stage "+pass);
                    FishingV016Validation.Capture(c,evidence,"farm-stage-"+pass);
                }
                Check(c.Career.Backpack.Count(FieldItem.TurnipSeeds)==11 && c.Career.Farm.Water==11,"Real transactions consume one seed and one can charge");
                var data=c.Career.Snapshot();FishingSaveStore.Save(c.SavePath,c.Career);var loaded=FishingSaveStore.Load(c.SavePath,out string warning);
                Check(warning=="" && loaded.Farm.Plot(0).planted && loaded.Farm.Water==11 && loaded.Farm.StarterClaimed,"Real JSON disk save preserves planted crop, water and one-time packet");
                c.RestoreShopTestProfile(loaded.Snapshot());
                for(int day=0;day<3;day++)
                {
                    foreach(var point in new[]{home.Origin+new Vector3(12.8f,0,-7.5f),home.Origin+new Vector3(12.8f,0,-11),home.Origin+new Vector3(7.7f,0,-9),home.Entry,home.Origin+new Vector3(0,.2f,-.2f),home.Origin+new Vector3(-1.6f,.2f,2.5f)})
                    {w=Walk(c,point);while(w.MoveNext())yield return null;}
                    c.PresentCharacterFrame();c.viewCamera.transform.LookAt(home.Bed.position+Vector3.up*.7f);yield return null;
                    Check(c.RequestWorldInteraction() && home.Sleeping,"Production bed interaction advances one farming night "+day);
                    while(home.Sleeping)yield return null;yield return null;
                    Check(c.Career.Farm.Plot(0).growth==day+1,"The watered crop grows exactly one saved stage overnight "+day);
                    foreach(var point in new[]{home.Origin+new Vector3(0,.2f,-.2f),home.Entry,home.Origin+new Vector3(7.7f,0,-9),home.Origin+new Vector3(12.8f,0,-11),home.Origin+new Vector3(12.8f,0,-7.5f)})
                    {w=Walk(c,point);while(w.MoveNext())yield return null;}
                    if(day<2){interact=Use(c,0);while(interact.MoveNext())yield return null;}
                }
                Check(c.Career.Farm.Ready(0),"Mature turnips are visible after three watered nights");
                Views(c,evidence);interact=Use(c,0);while(interact.MoveNext())yield return null;
                Check(c.Career.Backpack.Count(FieldItem.Turnip)==1 && c.Career.Backpack.Count(FieldItem.TurnipSeeds)==12 && !c.Career.Farm.Plot(0).planted,"Production harvest adds a crop and its renewable seed exactly once");
                interact=Use(c,12);while(interact.MoveNext())yield return null;Check(c.Career.Farm.Water==12,"Water well refills the actual can");
                interact=Use(c,15);while(interact.MoveNext())yield return null;
                foreach(var point in new[]{new Vector3(10.8f,0,-10.7f),new Vector3(12.8f,0,-11),new Vector3(12.8f,0,-2.5f)}){w=Walk(c,home.Origin+point);while(w.MoveNext())yield return null;}
                interact=Use(c,14);while(interact.MoveNext())yield return null;Check(c.Career.Coins==18 && c.Career.Backpack.Count(FieldItem.Turnip)==0,"Harvest crate sells actual backpack produce");
                c.TogglePause();var before=JsonUtility.ToJson(c.Career.Farm.Snapshot());Check(!c.RequestWorldInteraction() && JsonUtility.ToJson(c.Career.Farm.Snapshot())==before,"Paused inputs cannot mutate the farm");c.TogglePause();yield return null;
                var ground=c.transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>();
                Check(ground.bounds.max.x>home.Origin.x+40 && ground.bounds.min.x<home.Origin.x-35,"Actual physical coastline includes the enlarged eastern garden lobe");
                var map=BoatAttack.Editor.FishingMapBaker.BakeInitializedScene();
                Check(map.IsUsable && map.sources.Any(s=>s.Contains("V035 Home HomeLand")) && map.sources.Any(s=>s.Contains("V035 home garden")),"Navigation map is rebaked from the real enlarged home and garden geometry");
                var homeChecks=FishingV026HomeValidation.Checks(c);try{while(homeChecks.MoveNext())yield return homeChecks.Current;}finally{(homeChecks as IDisposable)?.Dispose();}
            }
            finally
            {c.Home.CancelSleep();if(c.Paused)c.TogglePause();if(c.HomeChartOpen)c.CloseHomeChart();c.CancelWorldAction();c.ReturnToHarbor();c.RestoreShopTestProfile(original);}
        }
        private static IEnumerator Use(FishingPrototype c,int id)
        {
            if(id==12 && !c.Farm.CarryingCan){var pickup=Use(c,15);while(pickup.MoveNext())yield return null;}
            if(id<12 && c.Farm.Work(id)==FarmWork.Water && !c.Farm.CarryingCan)
            {
                var pickup=Use(c,15);while(pickup.MoveNext())yield return null;
                var refill=Use(c,12);while(refill.MoveNext())yield return null;
            }
            if(id<12 && c.Farm.Work(id)!=FarmWork.Water && c.Farm.CarryingCan)
            {var put=Use(c,15);while(put.MoveNext())yield return null;}
            var target=c.Farm.Target(id);var stance=target.position+Vector3.back*(id<12?1.1f:1.3f);
            if(id==15)stance=target.position+Vector3.back*.65f;
            stance.y=c.Home.Origin.y;
            if(id<12)stance.x-=.25f;
            if(id<12 && (c.Farm.CarryingCan || FishingPrototype.FlatDistance(c.Shore.Position,c.Farm.Target(15).position)<1.2f))foreach(var point in new[]{new Vector3(10.2f,0,-10.5f),new Vector3(12.8f,0,-11)}){var approach=Walk(c,c.Home.Origin+point);while(approach.MoveNext())yield return null;}
            if(id==13)foreach(var point in new[]{new Vector3(12.8f,0,-11),new Vector3(12.8f,0,-5.1f)}){var approach=Walk(c,c.Home.Origin+point);while(approach.MoveNext())yield return null;}
            if(id==12){var approach=Walk(c,c.Home.Origin+new Vector3(10.2f,0,-10.5f));while(approach.MoveNext())yield return null;}
            if(id==15)foreach(var point in new[]{new Vector3(12.8f,0,-11),new Vector3(11.8f,0,-10.7f),new Vector3(11.8f,0,-10.05f)}){var approach=Walk(c,c.Home.Origin+point);while(approach.MoveNext())yield return null;}
            if(id==14){var approach=Walk(c,c.Home.Origin+new Vector3(10.2f,0,-2.5f));while(approach.MoveNext())yield return null;}
            var walk=Walk(c,stance);while(walk.MoveNext())yield return null;
            if(id==15)
            {
                for(int frame=0;frame<12 && FishingPrototype.FlatDistance(c.Shore.Position,stance)>.025f;frame++){var delta=Vector3.ProjectOnPlane(stance-c.Shore.Position,Vector3.up);c.viewCamera.transform.rotation=Quaternion.LookRotation(delta);c.Shore.Move(Vector2.up,false,false,false,Mathf.Min(.02f,delta.magnitude/2.6f));yield return null;}
                c.Shore.Actor.rotation=Quaternion.identity;c.View.ResetLook();for(int frame=0;frame<4;frame++){c.Shore.Move(Vector2.zero,false,false,false,.05f);yield return null;}
            }
            FishingV036FarmValidation.Aim(c,c.Farm.Aim(id));yield return null;yield return null;
            Check(c.Farm.TryTarget(out int found,out _) && found==id,"Nearest visible farm target resolves "+id);
            if(id<12)Debug.Log("[V035-FARM] START GEOMETRY: "+c.InspectWorldInteractionStart());
            bool started=c.RequestWorldInteraction();Check(started,"Production E starts farming action "+id+" "+c.Farm.Work(id)+" message="+c.VisibleMessage);
            int frames=0;while(c.WorldActionBusy && frames++<300)yield return null;yield return null;yield return null;
            Check(frames<300,"Farm contact animation finishes and releases input");
        }
        private static void Views(FishingPrototype c,string directory)
        {
            var cam=c.viewCamera;var p=cam.transform.position;var q=cam.transform.rotation;float fov=cam.fieldOfView;c.Shore.Character.SetOpticalView(true);
            try
            {
                var positions=new[]{new Vector3(23,13,-27),new Vector3(20,2,-10),new Vector3(-1.2f,1.8f,-3.9f),new Vector3(26,31,-34)};
                var targets=new[]{new Vector3(7,1,0),new Vector3(19,.3f,3),new Vector3(0,2,2),new Vector3(5,0,0)};
                for(int i=0;i<positions.Length;i++)
                {cam.transform.position=c.Home.Origin+positions[i];cam.transform.LookAt(c.Home.Origin+targets[i]);cam.fieldOfView=i==2?78:60;FishingV016Validation.Capture(c,directory,"home-"+new[]{"approach","garden","interior","overview"}[i]);}
            }
            finally{cam.transform.SetPositionAndRotation(p,q);cam.fieldOfView=fov;c.Shore.Character.SetOpticalView(false);}
        }
    }
}
