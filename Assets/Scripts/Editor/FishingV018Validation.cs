using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV018Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V018");
        private static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);Debug.Log("[V018] CHECK: "+name);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath("Logs/v018-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            Check(c.Islands.WorldRoot.childCount==4 && c.NavigationCount==14,"Four islands integrated into navigation");
            c.ReturnToHarbor();c.Weather.Climate.Select(BayWeather.Clear,true);
            for(int i=0;i<4;i++)
            {
                var landing=FishingIslandWorld.Landing(i);
                Check(c.Shore.Ground(landing,out var feet) && Mathf.Abs(feet.y-landing.y)<.3f && c.Shore.Clearance(feet) && c.Shore.SafeCoastalFooting(feet),"Real supported island landing "+i);
                Place(c,FishingIslandWorld.Berth(i));float settle=Time.time+.4f;while(Time.time<settle)yield return null;
                Check(c.Shore.TryDisembark(),"Disembark on authored island jetty "+i);
                Check(c.Shore.LocationName==FishingAdventure.Name(i),"Island location label "+i);
                var root=c.Islands.WorldRoot.GetChild(i);var letter=root.Find("Voyage letter");
                var walk=Walk(c,letter.position+new Vector3(0,0,.42f));while(walk.MoveNext())yield return null;
                Check(c.Islands.TryTarget(out int id,out _) && id==i*3,"Letter reachable by continuous walk "+i);
                var action=Contact(c,letter);while(action.MoveNext())yield return null;
                Check(c.Career.Adventure.Visited(i),"Island letter discovery through real hand action "+i);
                var treasure=root.Find("Hidden treasure");walk=Walk(c,treasure.position+new Vector3(0,0,-.38f));while(walk.MoveNext())yield return null;
                Check(c.Islands.TryTarget(out id,out _) && id==i*3+1,"Treasure target reachable "+i);
                action=Contact(c,treasure);while(action.MoveNext())yield return null;
                Check(c.Career.Adventure.TreasureFound(i),"Treasure paid through real hand action "+i);
                int coins=c.Career.Coins;Check(!c.Islands.InteractAt(id) && c.Career.Coins==coins,"Treasure cannot pay twice "+i);
                var beacon=root.Find("Compass beacon");walk=Walk(c,beacon.position+new Vector3(0,0,.4f));while(walk.MoveNext())yield return null;
                int target=i==0?3:i==1?0:i==2?1:2;
                for(int d=0;d<target;d++){action=Contact(c,beacon);while(action.MoveNext())yield return null;}
                Check(c.Islands.ConfirmNearby() && c.Career.Adventure.Restored(i),"Correct direction lights actual signal "+i);
                c.viewCamera.transform.SetPositionAndRotation(FishingIslandWorld.Center(i)+new Vector3(35,23,40),Quaternion.LookRotation(new Vector3(-35,-21,-40)));
                FishingV016Validation.Capture(c,evidence,"island-"+i);
                walk=Walk(c,FishingIslandWorld.Landing(i));while(walk.MoveNext())yield return null;
                Check(c.Shore.TryBoard() && !c.Ashore,"Continuous return walk and real boarding from new island "+i);
            }
            c.ReturnToHarbor();Check(c.Islands.CompleteAtHarbor()==150 && c.Islands.CompleteAtHarbor()==0,"Actual harbor finale pays once");
            foreach(var track in new[]{UpgradeTrack.Engine,UpgradeTrack.Rudder,UpgradeTrack.Cabin})Check(c.BuyUpgrade(track),"Actual harbor fleet purchase "+track);
            Check(c.Player.engine.horsePower==FishingFleet.Power(1) && c.Player.engine.rudderResponse>1 && c.Fleet.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"Purchase updates real engine and visible ship mesh");
            c.Islands.Refresh();var saved=FishingSaveStore.Load(c.SavePath,out string warning);
            Check(warning=="" && saved.Adventure.Finale && saved.EngineLevel==1 && saved.Coins==c.Career.Coins,"Unity JSON fleet/story reload");
            var display=c.TackleShop.transform.Find("V018 stocked chandlery");
            Check(display!=null && display.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3<15000 && display.GetComponents<BoxCollider>().Length==2,"Stocked store remains bounded and collidable");
            BeachTackleShopBuilder.ValidateRoute(c.TackleShop);
            var shop=c.TackleShop;var camera=c.viewCamera;
            camera.transform.SetPositionAndRotation(shop.transform.TransformPoint(new Vector3(0,1.75f,-4.3f)),Quaternion.LookRotation(shop.transform.forward));
            FishingV016Validation.Capture(c,evidence,"shop-interior");
            camera.transform.SetPositionAndRotation(shop.transform.TransformPoint(new Vector3(16,9,-21)),Quaternion.LookRotation(shop.Position+Vector3.up*2-shop.transform.TransformPoint(new Vector3(16,9,-21))));
            FishingV016Validation.Capture(c,evidence,"shop-exterior");
            Check(c.BuyUpgrade(UpgradeTrack.Cabin),"Second luxury tier buys visible canopy");
            camera.transform.SetPositionAndRotation(c.Player.transform.position+new Vector3(-5,3,-6),Quaternion.LookRotation(new Vector3(5,-2,6)));
            FishingV016Validation.Capture(c,evidence,"luxury-boat");
            var map=BoatAttack.Editor.FishingMapBaker.BakeInitializedScene(new[]{c.TackleShop.transform,c.Islands.WorldRoot});
            Check(map.IsUsable,"Initialized map captures new islands and store");
            var sea=new Vector3(-5,0,130);Place(c,sea);
            var driver=c.Player.gameObject.AddComponent<FishingV017Driver>();driver.Engine=c.Player.engine;c.enabled=false;
            try
            {
                c.Player.engine.horsePower=FishingFleet.Power(0);c.Player.engine.forwardSpeedLimit=FishingFleet.SpeedLimit(0);
                float until=Time.time+2;while(Time.time<until)yield return null;driver.Throttle=1;until=Time.time+12;while(Time.time<until)yield return null;float baseline=c.Speed;
                Check(baseline>5f && baseline<FishingFleet.SpeedLimit(0)+.5f,"Base cruise measurably faster than v17 4.48m/s; actual="+baseline);
                driver.Throttle=0;Place(c,sea);until=Time.time+1;while(Time.time<until)yield return null;c.Player.engine.horsePower=FishingFleet.Power(2);c.Player.engine.forwardSpeedLimit=FishingFleet.SpeedLimit(2);
                driver.Throttle=1;until=Time.time+12;while(Time.time<until)yield return null;Check(c.Speed>baseline*1.2f && c.Speed<FishingFleet.SpeedLimit(2)+.5f,"Paid propulsion improves real water speed; actual="+c.Speed);
                driver.Steering=1;float maxTilt=0;until=Time.time+8;
                while(Time.time<until){maxTilt=Mathf.Max(maxTilt,Vector3.Angle(c.Player.transform.up,Vector3.up));yield return null;}
                Check(maxTilt<2,"High-speed upgraded steering keeps hull upright");
            }
            finally{Object.Destroy(driver);c.enabled=true;c.Fleet.Refresh();c.ReturnToHarbor();}
            Debug.Log("[V018] EVIDENCE: "+evidence);
        }
        private static void Place(FishingPrototype c,Vector3 point)
        {var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=point;rb.rotation=Quaternion.identity;c.Player.transform.SetPositionAndRotation(point,Quaternion.identity);Physics.SyncTransforms();}
        private static IEnumerator Walk(FishingPrototype c,Vector3 target)
        {
            for(int i=0;i<800 && FishingPrototype.FlatDistance(c.Shore.Position,target)>.08f;i++)
            {
                var d=Vector3.ProjectOnPlane(target-c.Shore.Position,Vector3.up).normalized;
                c.Shore.Body.Move(d*.1f+Vector3.down*.03f);yield return null;
            }
            Check(FishingPrototype.FlatDistance(c.Shore.Position,target)<.4f,"Island interior walk has continuous collision support");
        }
        private static IEnumerator Contact(FishingPrototype c,Transform target)
        {
            c.Shore.Actor.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(target.position-c.Shore.Position,Vector3.up));
            c.PresentCharacterFrame();for(int f=0;f<16;f++)yield return null;
            Check(c.RequestWorldInteraction(),"Real contact action starts: "+target.name+" / "+c.InspectWorldInteractionStart());
            float until=Time.time+5;while(c.WorldActionBusy && Time.time<until)yield return null;
            Check(c.LastActionSucceeded,"Real contact action commits once: "+target.name);yield return null;
        }
    }
}
